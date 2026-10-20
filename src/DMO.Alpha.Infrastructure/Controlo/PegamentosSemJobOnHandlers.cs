using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A criação do registo durável de Pegamentos, comum à produção e ao sem
/// Job On: congela as ferramentas da combinação (referência e lote) e a
/// tolerância efetiva do corredor. Não grava: o registo é acrescentado ao
/// contexto e a escrita fica para o chamador.
/// </summary>
internal static class PegamentosRecordFactory
{
    internal static async Task<DMO.Alpha.Core.Controlo.Pegamentos> AddAsync(
        DmoDbContext db,
        IPegamentosToleranceConfiguration toleranceConfiguration,
        int? controloId,
        string? cmToolId,
        string? bqToolId,
        string? mfToolId,
        CancellationToken cancellationToken)
    {
        var toleranceMm = await toleranceConfiguration.FindConfiguredAsync(cancellationToken)
            ?? PegamentosCalculation.DefaultToleranceMm;

        var frozenFacts = await db.Tools
            .AsNoTracking()
            .Where(t => (t.ToolId == cmToolId || t.ToolId == bqToolId || t.ToolId == mfToolId)
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
        Tool? FactsOf(string? toolId) =>
            toolId is not null && frozenFacts.TryGetValue(toolId, out var tool) ? tool : null;
        var (cmFacts, bqFacts, mfFacts) = (FactsOf(cmToolId), FactsOf(bqToolId), FactsOf(mfToolId));

        var registro = new DMO.Alpha.Core.Controlo.Pegamentos
        {
            ControloId = controloId,
            CmToolId = cmToolId,
            BqToolId = bqToolId,
            MfToolId = mfToolId,
            ToleranceMm = toleranceMm,
            CmToolReferenceFrozen = cmFacts?.Reference,
            CmToolLotFrozen = cmFacts?.Lot,
            BqToolReferenceFrozen = bqFacts?.Reference,
            BqToolLotFrozen = bqFacts?.Lot,
            MfToolReferenceFrozen = mfFacts?.Reference,
            MfToolLotFrozen = mfFacts?.Lot
        };
        db.Pegamentos.Add(registro);
        return registro;
    }
}

/// <summary>
/// Cria uns Pegamentos sem Job On (DMO-P01): as ferramentas são escolhidas
/// à mão e têm de existir no Ferramentas com o tipo do componente; o
/// registo nasce sem controlo_id e recebe as medições pelo pegamentos_id.
/// </summary>
public sealed class CreateToolScopedPegamentosHandler : ICreateToolScopedPegamentosHandler
{
    private readonly DmoDbContext _db;
    private readonly IPegamentosToleranceConfiguration _toleranceConfiguration;

    public CreateToolScopedPegamentosHandler(
        DmoDbContext db,
        IPegamentosToleranceConfiguration toleranceConfiguration)
    {
        _db = db;
        _toleranceConfiguration = toleranceConfiguration;
    }

    public async Task<CreateToolScopedPegamentosOutcome> HandleAsync(
        CreateToolScopedPegamentos command,
        CancellationToken cancellationToken = default)
    {
        var cm = Clean(command.CmToolId);
        var bq = Clean(command.BqToolId);
        var mf = Clean(command.MfToolId);
        if (cm is null || bq is null || mf is null)
        {
            return new(CreateToolScopedPegamentosResult.NoTools);
        }

        if (cm is not null && !await ExistsAsync(cm, ToolType.Cm, cancellationToken))
        {
            return new(CreateToolScopedPegamentosResult.CmToolNotFound);
        }

        if (bq is not null && !await ExistsAsync(bq, ToolType.Bq, cancellationToken))
        {
            return new(CreateToolScopedPegamentosResult.BqToolNotFound);
        }

        if (mf is not null && !await ExistsAsync(mf, ToolType.Mf, cancellationToken))
        {
            return new(CreateToolScopedPegamentosResult.MfToolNotFound);
        }

        // Dois envios iguais esperam um pelo outro; uma folha sem produção
        // com as mesmas ferramentas e ainda sem medições é aberta em vez de
        // criar outra (varredura de 18/10/2026, B5).
        return await DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, "pegamentos:criar", cancellationToken);
            var toleranceMm = await _toleranceConfiguration.FindConfiguredAsync(cancellationToken)
                ?? PegamentosCalculation.DefaultToleranceMm;
            var existingId = await _db.Pegamentos
                .AsNoTracking()
                .Where(p => p.ControloId == null
                    && p.CmToolId == cm && p.BqToolId == bq && p.MfToolId == mf
                    && p.ToleranceMm == toleranceMm
                    && !p.Measurements.Any())
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingId is int id)
            {
                return new CreateToolScopedPegamentosOutcome(CreateToolScopedPegamentosResult.Success, id);
            }

            var registro = await PegamentosRecordFactory.AddAsync(
                _db, _toleranceConfiguration, null, cm, bq, mf, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return new CreateToolScopedPegamentosOutcome(CreateToolScopedPegamentosResult.Success, registro.Id);
        }, cancellationToken);
    }

    private Task<bool> ExistsAsync(string toolId, ToolType type, CancellationToken cancellationToken) =>
        _db.Tools.AsNoTracking().AnyAsync(
            t => t.ToolId == toolId && t.Type == type && t.ToolId != ToolRegistryConstants.RemovedToolId,
            cancellationToken);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Associa uns Pegamentos sem Job On ao controlo_id de uma produção. Cada
/// ferramenta guardada no registo tem de ser a do Job On para o mesmo
/// componente: medições de outra ferramenta não passam a ser da produção.
/// Uma folha por combinação de ferramentas da produção (a que o registo de
/// medições usa): a produção que já tem a folha das ferramentas atuais
/// recusa outra; uma folha de antes de uma troca da CM, da BQ ou do MF não
/// conta (18/10/2026). Repetir a mesma associação é idempotente.
/// </summary>
public sealed class AssociatePegamentosToControloHandler : IAssociatePegamentosToControloHandler
{
    private readonly DmoDbContext _db;

    public AssociatePegamentosToControloHandler(DmoDbContext db)
    {
        _db = db;
    }

    // ASSOCIAR AO MESMO TEMPO (deep sweep, 18/10/2026, I3-M-2): associar uma
    // folha enquanto outra era associada à mesma produção, ou enquanto o
    // operador gravava a 1.ª leitura, deixava duas folhas no mesmo controlo e
    // a página mostrava só a primeira. O trinco do controlo, o mesmo da 1.ª
    // leitura: o segundo espera e já vê a folha do primeiro. A mesma folha
    // associada ao mesmo tempo a duas produções (I3-M-8) gravava as duas
    // vezes: o trinco da folha a seguir (a ordem da 1.ª leitura), e o segundo
    // já a vê associada.
    public Task<AssociatePegamentosToControloOutcome> HandleAsync(
        AssociatePegamentosToControlo command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"pegamentos:controlo:{command.ControloId}", cancellationToken);
            await RecordPegamentosMeasurementHandler.LockSheetAsync(_db, command.PegamentosId, cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<AssociatePegamentosToControloOutcome> HandleLockedAsync(
        AssociatePegamentosToControlo command,
        CancellationToken cancellationToken)
    {
        var production = await PegamentosSheets.CurrentCombinationAsync(_db, command.ControloId, cancellationToken);
        if (production is null)
        {
            return new(AssociatePegamentosToControloResult.ControloNotFound);
        }

        var registro = await _db.Pegamentos.FirstOrDefaultAsync(p => p.Id == command.PegamentosId, cancellationToken);
        if (registro is null)
        {
            return new(AssociatePegamentosToControloResult.PegamentosNotFound);
        }

        if (registro.ControloId == command.ControloId)
        {
            return new(AssociatePegamentosToControloResult.Success);
        }

        if (registro.ControloId is not null)
        {
            return new(AssociatePegamentosToControloResult.PegamentosAlreadyAssociated);
        }

        var mismatched = new List<PegamentosComponent>();
        if (registro.CmToolId is not null && registro.CmToolId != production.CmToolId)
        {
            mismatched.Add(PegamentosComponent.Cm);
        }
        if (registro.BqToolId is not null && registro.BqToolId != production.BqToolId)
        {
            mismatched.Add(PegamentosComponent.Bq);
        }
        if (registro.MfToolId is not null && registro.MfToolId != production.MfToolId)
        {
            mismatched.Add(PegamentosComponent.Mf);
        }
        if (mismatched.Count > 0)
        {
            return new(AssociatePegamentosToControloResult.ToolMismatch, mismatched);
        }

        if (await _db.Pegamentos.OfCombination(command.ControloId, production).AnyAsync(cancellationToken))
        {
            return new(AssociatePegamentosToControloResult.ControloAlreadyHasPegamentos);
        }

        registro.ControloId = command.ControloId;
        await _db.SaveChangesAsync(cancellationToken);
        return new(AssociatePegamentosToControloResult.Success);
    }
}
