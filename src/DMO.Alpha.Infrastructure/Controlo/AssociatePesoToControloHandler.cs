using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A associação posterior explícita do Peso pré-produção ao controlo_id
/// de uma produção (spec PESO.md L62-101). Regra do Owner (2026-10-16):
/// o Peso é candidato quando a CM do Job On (CM_ID) usa o MESMO tool_id
/// que o Peso pesou. O MESMO peso_id passa a pertencer à produção — nada
/// é recriado, os valores congelados do Peso ficam intactos — e a
/// cardinalidade é material: um Peso por produção (índice único
/// <c>IX_Pesos_ControloId</c>) e uma produção por Peso.
/// </summary>
public sealed class AssociatePesoToControloHandler : IAssociatePesoToControloHandler
{
    private readonly DmoDbContext _db;

    public AssociatePesoToControloHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<AssociatePesoToControloOutcome> HandleAsync(
        AssociatePesoToControlo command,
        CancellationToken cancellationToken = default)
    {
        var production = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == command.ControloId)
            .Select(c => new
            {
                CmToolId = _db.CmContexts
                    .Where(ctx => ctx.JobOnId == c.JobOnId)
                    .Select(ctx => (string?)ctx.ToolId)
                    .FirstOrDefault()
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

        if (peso.ControloId is not null)
        {
            return Result(AssociatePesoToControloResult.PesoAlreadyAssociated);
        }

        if (production.CmToolId is null)
        {
            return Result(AssociatePesoToControloResult.ContextMissingCm);
        }

        if (!string.Equals(peso.CmToolId, production.CmToolId, StringComparison.Ordinal))
        {
            return Result(AssociatePesoToControloResult.CmMismatch);
        }

        if (await _db.Pesos.AnyAsync(p => p.ControloId == command.ControloId, cancellationToken))
        {
            return Result(AssociatePesoToControloResult.ControloAlreadyHasPeso);
        }

        peso.ControloId = command.ControloId;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Outro Peso foi associado/criado para este controlo entre a
            // verificação e a escrita: o índice único é a última verdade.
            _db.Entry(peso).State = EntityState.Detached;
            return Result(AssociatePesoToControloResult.ControloAlreadyHasPeso);
        }

        return Result(AssociatePesoToControloResult.Success);
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
        var cmToolId = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => _db.CmContexts
                .Where(ctx => ctx.JobOnId == c.JobOnId)
                .Select(ctx => (string?)ctx.ToolId)
                .FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
        if (cmToolId is null
            || await _db.Pesos.AsNoTracking().AnyAsync(p => p.ControloId == controloId, cancellationToken))
        {
            return Array.Empty<UnassociatedPesoCandidate>();
        }

        return await FindUnassociatedByCmToolAsync(cmToolId, cancellationToken);
    }

    public async Task<IReadOnlyList<UnassociatedPesoCandidate>> FindUnassociatedByCmToolAsync(
        string cmToolId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Pesos
            .AsNoTracking()
            .Where(p => p.ControloId == null && p.CmToolId == cmToolId)
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
            .Select(p => new { p.CmToolId, p.ControloId })
            .FirstOrDefaultAsync(cancellationToken);
        if (peso is null || peso.ControloId is not null)
        {
            return Array.Empty<PesoProductionCandidate>();
        }

        // Produções sem controlo_id ainda (pré-corte) também contam: o
        // controlo é assegurado no POST, antes de associar.
        return await (
                from cm in _db.CmContexts.AsNoTracking()
                where cm.ToolId == peso.CmToolId
                join j in _db.JobOns on cm.JobOnId equals j.Id
                join m in _db.Machines on j.MachineId equals m.Id
                where !_db.Controlos.Any(c => c.JobOnId == j.Id
                    && _db.Pesos.Any(p => p.ControloId == c.Id))
                orderby j.Id descending
                select new PesoProductionCandidate(j.Id, j.ProductionNumber, j.Reference, m.Code))
            .ToListAsync(cancellationToken);
    }
}
