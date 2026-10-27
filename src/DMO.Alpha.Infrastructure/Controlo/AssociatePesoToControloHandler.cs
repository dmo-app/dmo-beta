using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A associação posterior explícita do Peso pré-produção ao controlo_id
/// de uma produção (spec PESO.md L62-101). Regra do Owner (2026-10-16):
/// o Peso é candidato quando a CM do Job On (CM_ID) usa o MESMO tool_id
/// que o Peso pesou. O MESMO peso_id passa a pertencer à produção — nada
/// é recriado, os valores congelados do Peso ficam intactos — e a
/// cardinalidade é material: uma produção por Peso e, numa produção, um
/// Peso para cada CM+BQ (índice único
/// <c>IX_Pesos_ControloId_CmToolId_BqToolId</c>). A CM e a BQ do Peso têm de
/// ser cada uma a da produção agora ou uma que ela teve antes de uma troca;
/// o Peso passa a ser o dessa combinação (resposta R16-6 do especialista,
/// 18/10/2026). A BQ conta pela referência, qualquer lote (o Diogo,
/// 27/10/2026), e o Peso fica com o lote da produção.
/// </summary>
public sealed class AssociatePesoToControloHandler : IAssociatePesoToControloHandler
{
    private readonly DmoDbContext _db;

    public AssociatePesoToControloHandler(DmoDbContext db)
    {
        _db = db;
    }

    // ASSOCIAR AO MESMO TEMPO (deep sweep, 18/10/2026, I3-M-8): dois pedidos
    // a associar o mesmo Peso a produções diferentes liam-no ainda por
    // associar e gravavam os dois, ficando o último. O trinco do Peso antes
    // de o ler: o segundo espera e já o vê associado. O da produção antes
    // dele (sempre por esta ordem): dois Pesos da mesma CM+BQ associados à
    // mesma produção ao mesmo tempo, o segundo já vê o primeiro.
    public Task<AssociatePesoToControloOutcome> HandleAsync(
        AssociatePesoToControlo command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"peso:controlo:{command.ControloId}", cancellationToken);
            await DbWrite.LockAsync(_db, $"peso:registo:{command.PesoId}", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<AssociatePesoToControloOutcome> HandleLockedAsync(
        AssociatePesoToControlo command,
        CancellationToken cancellationToken)
    {
        var production = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == command.ControloId)
            .Select(c => new
            {
                CmToolId = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault(),
                // A CM e a BQ que a produção tem agora e as que teve antes de
                // uma troca (o contexto anterior fica ligado por
                // ReplacedJobOnId).
                CmToolIds = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId || ctx.ReplacedJobOnId == c.JobOnId)
                    .Select(ctx => ctx.ToolId)
                    .ToList(),
                BqToolId = _db.BqContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault(),
                BqToolIds = _db.BqContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId || ctx.ReplacedJobOnId == c.JobOnId)
                    .Select(ctx => ctx.ToolId)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (production is null)
        {
            return Result(AssociatePesoToControloResult.ControloNotFound);
        }

        var peso = await _db.Pesos.FirstOrDefaultAsync(p => p.Id == command.PesoId, cancellationToken);
        if (peso is null)
        {
            return Result(AssociatePesoToControloResult.PesoNotFound);
        }

        // Repetir a associação já feita é idempotente (duplo clique, PRG).
        if (peso.ControloId == command.ControloId)
        {
            return Result(AssociatePesoToControloResult.Success);
        }

        // Um Peso só para comparação não se associa (o Diogo, 22/10/2026):
        // é recusado como um Peso que já tem dono.
        if (peso.ControloId is not null || peso.ComparacaoDoPesoId is not null)
        {
            return Result(AssociatePesoToControloResult.PesoAlreadyAssociated);
        }

        if (production.CmToolId is null)
        {
            return Result(AssociatePesoToControloResult.ContextMissingCm);
        }

        if (!production.CmToolIds.Contains(peso.CmToolId, StringComparer.Ordinal))
        {
            return Result(AssociatePesoToControloResult.CmMismatch);
        }

        // A BQ entra no cálculo (o volume marisa): um Peso de outra BQ não é
        // desta produção, como a CM (R16-6).
        if (production.BqToolIds.Count == 0)
        {
            return Result(AssociatePesoToControloResult.ContextMissingBq);
        }

        // A BQ conta pela referência, não pelo lote (o Diogo, 27/10/2026: «o
        // vol da BQ é sempre igual, independentemente se é lote 6 ou 59»): o
        // Peso fica com o lote da produção, a atual primeiro.
        var bqDaProducao = await BqDaProducaoAsync(peso, production.BqToolId, production.BqToolIds, cancellationToken);
        if (bqDaProducao is null)
        {
            return Result(AssociatePesoToControloResult.BqMismatch);
        }

        if (await _db.Pesos.AnyAsync(p => p.ControloId == command.ControloId
                && p.CmToolId == peso.CmToolId
                && p.BqToolId == bqDaProducao.ToolId, cancellationToken))
        {
            return Result(AssociatePesoToControloResult.ControloAlreadyHasPeso);
        }

        if (peso.BqToolId != bqDaProducao.ToolId)
        {
            peso.BqToolId = bqDaProducao.ToolId;
            peso.BqToolLotFrozen = bqDaProducao.Lot;
        }

        peso.ControloId = command.ControloId;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Outro Peso desta CM+BQ foi associado/criado para este controlo
            // entre a verificação e a escrita: o índice único é a última
            // verdade.
            _db.Entry(peso).State = EntityState.Detached;
            return Result(AssociatePesoToControloResult.ControloAlreadyHasPeso);
        }

        return Result(AssociatePesoToControloResult.Success);
    }

    private async Task<Tool?> BqDaProducaoAsync(
        Peso peso,
        string? bqAtual,
        IReadOnlyCollection<string> bqToolIds,
        CancellationToken cancellationToken)
    {
        var bqs = await _db.Tools
            .AsNoTracking()
            .Where(t => bqToolIds.Contains(t.ToolId))
            .ToListAsync(cancellationToken);
        if (bqs.FirstOrDefault(t => t.ToolId == peso.BqToolId) is { } mesma)
        {
            return mesma;
        }

        var referencia = peso.BqToolReferenceFrozen
            ?? await _db.Tools.Where(t => t.ToolId == peso.BqToolId).Select(t => t.Reference).FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(referencia)
            ? null
            : bqs.Where(t => string.Equals(t.Reference, referencia, StringComparison.Ordinal))
                .OrderByDescending(t => t.ToolId == bqAtual)
                .FirstOrDefault();
    }

    private static AssociatePesoToControloOutcome Result(AssociatePesoToControloResult result) => new(result);
}

/// <summary>Leituras purpose-specific da associação posterior do Peso (AsNoTracking).</summary>
public sealed class PesoAssociationQuery : IPesoAssociationQuery
{
    private readonly DmoDbContext _db;

    public PesoAssociationQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UnassociatedPesoCandidate>> FindCandidatesForControloAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var production = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => new
            {
                CmToolId = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault(),
                CmToolIds = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId || ctx.ReplacedJobOnId == c.JobOnId)
                    .Select(ctx => ctx.ToolId)
                    .ToList(),
                BqToolIds = _db.BqContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId || ctx.ReplacedJobOnId == c.JobOnId)
                    .Select(ctx => ctx.ToolId)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Com o Peso da CM e da BQ atuais não há nada a propor. Depois de uma
        // troca, só os Pesos que a associação aceita (R16-6): CM e BQ desta
        // produção, agora ou antes da troca, numa combinação que ainda não tem
        // Peso.
        if (production?.CmToolId is null
            || production.BqToolIds.Count == 0
            || await PesoCombinations.CurrentPesoIdAsync(_db, controloId, cancellationToken) is not null)
        {
            return Array.Empty<UnassociatedPesoCandidate>();
        }

        // A BQ conta pela referência, não pelo lote (o Diogo, 27/10/2026).
        var (cmToolIds, bqToolIds) = (production.CmToolIds, production.BqToolIds);
        // As referências entram na mesma consulta (sem mais uma ida à base).
        var bqReferencias = _db.Tools
            .Where(t => bqToolIds.Contains(t.ToolId))
            .Select(t => t.Reference);
        var candidates = await _db.Pesos
            .AsNoTracking()
            .Where(p => p.ControloId == null && p.ComparacaoDoPesoId == null && cmToolIds.Contains(p.CmToolId)
                && (bqToolIds.Contains(p.BqToolId) || bqReferencias.Contains(p.BqToolReferenceFrozen ?? p.BqTool.Reference)))
            .Where(p => !_db.Pesos.Any(o => o.ControloId == controloId && o.CmToolId == p.CmToolId
                && (o.BqToolId == p.BqToolId || (o.BqToolReferenceFrozen ?? o.BqTool.Reference) == (p.BqToolReferenceFrozen ?? p.BqTool.Reference))))
            .OrderByDescending(p => p.Id)
            .Select(p => new UnassociatedPesoCandidate(
                p.Id,
                p.CmToolReferenceFrozen ?? p.CmTool.Reference,
                p.CmToolLotFrozen ?? p.CmTool.Lot,
                p.BqToolReferenceFrozen ?? p.BqTool.Reference,
                p.BqToolLotFrozen ?? p.BqTool.Lot,
                p.Measurements.Count))
            .ToListAsync(cancellationToken);
        return candidates;
    }

    public async Task<IReadOnlyList<UnassociatedPesoCandidate>> FindUnassociatedByCmToolAsync(
        string cmToolId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Pesos
            .AsNoTracking()
            .Where(p => p.ControloId == null && p.ComparacaoDoPesoId == null && p.CmToolId == cmToolId)
            .OrderByDescending(p => p.Id)
            .Select(p => new UnassociatedPesoCandidate(
                p.Id,
                p.CmToolReferenceFrozen ?? p.CmTool.Reference,
                p.CmToolLotFrozen ?? p.CmTool.Lot,
                p.BqToolReferenceFrozen ?? p.BqTool.Reference,
                p.BqToolLotFrozen ?? p.BqTool.Lot,
                p.Measurements.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PesoProductionCandidate>> FindProductionsForPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default)
    {
        var peso = await _db.Pesos
            .AsNoTracking()
            .Where(p => p.Id == pesoId)
            .Select(p => new { p.CmToolId, p.BqToolId, BqReferencia = p.BqToolReferenceFrozen ?? p.BqTool.Reference, p.ControloId, p.ComparacaoDoPesoId })
            .FirstOrDefaultAsync(cancellationToken);
        // Um Peso só para comparação nunca pede um Job On (o Diogo, 22/10/2026).
        if (peso is null || peso.ControloId is not null || peso.ComparacaoDoPesoId is not null)
        {
            return Array.Empty<PesoProductionCandidate>();
        }

        // Produções sem controlo_id ainda (pré-corte) também contam: o
        // controlo é assegurado no POST, antes de associar. A CM e a BQ do
        // Peso são cada uma a do Job On agora ou uma que ele teve antes de uma
        // troca, e a produção ainda não tem Peso desta CM+BQ (R16-6).
        // A BQ conta pela referência, não pelo lote (o Diogo, 27/10/2026).
        var (cmToolId, bqToolId, bqReferencia) = (peso.CmToolId, peso.BqToolId, peso.BqReferencia);
        return await (
                from j in _db.JobOns.AsNoTracking()
                where _db.CmContexts.Any(cm => cm.ToolId == cmToolId && (cm.JobOnId == j.Id || cm.ReplacedJobOnId == j.Id))
                    && _db.BqContexts.Any(bq => (bq.JobOnId == j.Id || bq.ReplacedJobOnId == j.Id)
                        && (bq.ToolId == bqToolId || _db.Tools.Any(t => t.ToolId == bq.ToolId && t.Reference == bqReferencia)))
                join m in _db.Machines on j.MachineId equals m.Id
                where !_db.Controlos.Any(c => c.JobOnId == j.Id
                    && _db.Pesos.Any(p => p.ControloId == c.Id && p.CmToolId == cmToolId
                        && (p.BqToolId == bqToolId || (p.BqToolReferenceFrozen ?? p.BqTool.Reference) == bqReferencia)))
                orderby j.Id descending
                select new PesoProductionCandidate(j.Id, j.ProductionNumber, j.Reference, m.Code))
            .ToListAsync(cancellationToken);
    }
}
