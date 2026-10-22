using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Implementação purpose-specific da <see cref="IPegamentosQuery"/>: a
/// projeção DA FOLHA de Pegamentos por controlo_id (a LEI da app: «com
/// controlo_id ganhas o contexto todo para pegamentos»), composta no
/// momento da leitura a partir das relações persistidas — o contexto
/// canónico da produção pela query existente
/// <see cref="IProductionContextQuery"/> (controlo → jobon → contextos,
/// nunca re-selecionado pelo utilizador), o facto técnico corrente de
/// gargalo de cada tool da combinação, a tolerância efetiva corrente
/// (configuração ?? default do Owner) e o registo durável quando já
/// existe: as leituras com os derivados e a avaliação do corredor são
/// calculados a partir dos factos CONGELADOS (nominal da linha, tolerância
/// do registo) — nunca do estado corrente das tools/configuração. Só de
/// leitura, AsNoTracking em tudo.
/// </summary>
public sealed class PegamentosQuery : IPegamentosQuery
{
    private readonly DmoDbContext _db;
    private readonly IProductionContextQuery _productionContext;
    private readonly IPegamentosToleranceConfiguration _toleranceConfiguration;

    public PegamentosQuery(
        DmoDbContext db,
        IProductionContextQuery productionContext,
        IPegamentosToleranceConfiguration toleranceConfiguration)
    {
        _db = db;
        _productionContext = productionContext;
        _toleranceConfiguration = toleranceConfiguration;
    }

    public async Task<PegamentosSheetReadModel?> FindSheetByControloAsync(
        int controloId,
        int? pegamentosId = null,
        CancellationToken cancellationToken = default)
    {
        // A âncora é o controlo_id persistido: um controlo inexistente
        // devolve null — nenhum contexto é inventado.
        var jobOnId = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => (int?)c.JobOnId)
            .FirstOrDefaultAsync(cancellationToken);
        if (jobOnId is not { } canonicalJobOnId)
        {
            return null;
        }

        // O contexto canónico da produção pela query EXISTENTE — a mesma
        // superfície que o Resumo e os módulos consomem (a LEI: jobon →
        // controlo → contexto todo).
        var production = await _productionContext.GetAsync(canonicalJobOnId, cancellationToken);
        if (production is null)
        {
            return null;
        }

        // As folhas da produção, cada uma com as suas ferramentas. A
        // mostrada é a pedida (desta produção) ou a das ferramentas que a
        // produção tem agora; depois de uma troca ainda não há nenhuma: a
        // folha mostra-se pronta a medir com as ferramentas novas (
        // 18/10/2026). Nunca «a última»: voltar a uma ferramenta já usada
        // retoma a folha dela.
        var current = new PegamentosCombination(production.CmToolId, production.BqToolId, production.MfToolId);
        var sheets = await FindRecordsAsync(
            _db.Pegamentos.AsNoTracking().Where(p => p.ControloId == controloId), current, cancellationToken);
        var registroModel = sheets.FirstOrDefault(s => s.PegamentosId == pegamentosId)
            ?? sheets.FirstOrDefault(s => s.IsCurrent);

        // Os componentes aplicáveis com o facto técnico corrente de cada
        // tool (gargalo herdado; null = em falta, completável no fluxo): os
        // da folha mostrada ou, sem folha, os do contexto. O contexto pode
        // não ter um componente (o MF do J10 real) — só os presentes entram.
        var components = registroModel?.Components
            ?? await ComponentsAsync(production.CmToolId, production.BqToolId, production.MfToolId, cancellationToken);
        var currentComponents = registroModel is { IsCurrent: false }
            ? await ComponentsAsync(production.CmToolId, production.BqToolId, production.MfToolId, cancellationToken)
            : components;

        // A tolerância efetiva corrente (a que a criação do registo
        // congelará): configurada quando existir, default do Owner na sua
        // falta — nunca locked no cálculo.
        var currentTolerance = await _toleranceConfiguration.FindConfiguredAsync(cancellationToken)
            ?? PegamentosCalculation.DefaultToleranceMm;

        return new PegamentosSheetReadModel(
            controloId,
            production.JobOnId,
            production.Reference,
            production.ProductionNumber,
            production.Machine,
            production.CmId,
            registroModel is null ? production.CmToolId : registroModel.CmToolId,
            production.MfId,
            registroModel is null ? production.MfToolId : registroModel.MfToolId,
            production.BqId,
            registroModel is null ? production.BqToolId : registroModel.BqToolId,
            components,
            currentTolerance,
            registroModel)
        {
            Sheets = sheets,
            CurrentComponents = currentComponents
        };
    }

    public async Task<PegamentosSheetReadModel?> FindSheetByPegamentosAsync(
        int pegamentosId,
        CancellationToken cancellationToken = default)
    {
        var registro = (await FindRecordsAsync(
            _db.Pegamentos.AsNoTracking().Where(p => p.Id == pegamentosId), null, cancellationToken))
            .FirstOrDefault();
        if (registro is null)
        {
            return null;
        }

        // Já associado: a folha da produção (o contexto todo pelo
        // controlo_id), esta folha com as ferramentas dela.
        if (registro.ControloId is int controloId)
        {
            return await FindSheetByControloAsync(controloId, pegamentosId, cancellationToken);
        }

        // Sem Job On: as ferramentas são as guardadas no registo.
        var currentTolerance = await _toleranceConfiguration.FindConfiguredAsync(cancellationToken)
            ?? PegamentosCalculation.DefaultToleranceMm;

        return new PegamentosSheetReadModel(
            null,
            null,
            registro.CmToolReference ?? "—",
            PegamentosSheetReadModel.SemJobOnProductionNumber,
            "—",
            null,
            registro.CmToolId,
            null,
            registro.MfToolId,
            null,
            registro.BqToolId,
            registro.Components,
            currentTolerance,
            registro);
    }

    public async Task<IReadOnlyList<PegamentosReadModel>> ListSheetsByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var current = await PegamentosSheets.CurrentCombinationAsync(_db, controloId, cancellationToken);
        if (current is null)
        {
            return Array.Empty<PegamentosReadModel>();
        }

        return await FindRecordsAsync(
            _db.Pegamentos.AsNoTracking().Where(p => p.ControloId == controloId), current, cancellationToken);
    }

    /// <summary>
    /// Os componentes aplicáveis da combinação com o facto técnico corrente
    /// de cada tool (gargalo; null = em falta, completável no fluxo). Só os
    /// componentes com ferramenta entram na folha.
    /// </summary>
    private async Task<List<PegamentosComponentContextReadModel>> ComponentsAsync(
        string? cmToolId,
        string? bqToolId,
        string? mfToolId,
        CancellationToken cancellationToken)
    {
        var components = new List<PegamentosComponentContextReadModel>();
        foreach (var (component, toolId) in new[]
                 {
                     (PegamentosComponent.Cm, cmToolId),
                     (PegamentosComponent.Bq, bqToolId),
                     (PegamentosComponent.Mf, mfToolId)
                 })
        {
            if (toolId is null)
            {
                continue;
            }

            var projected = await (
                from tool in _db.Tools.AsNoTracking()
                join tech in _db.ToolTechnicalContexts on tool.ToolId equals tech.ToolId into techGroup
                from tech in techGroup.DefaultIfEmpty()
                where tool.ToolId == toolId
                select new
                {
                    tool.Reference,
                    tool.Lot,
                    Gargalo = tech != null ? (decimal?)tech.DiametroGargaloMm : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            components.Add(new PegamentosComponentContextReadModel(
                component,
                toolId,
                projected?.Reference,
                projected?.Lot,
                projected?.Gargalo));
        }

        return components;
    }

    /// <summary>
    /// Os registos pedidos, pela ordem de gravação, numa consulta: as
    /// leituras, a referência/lote atuais das Tools (só servem de fallback
    /// para dados legados sem os factos congelados) e o gargalo atual de cada
    /// ferramenta (o nominal de um componente ainda sem leituras).
    /// <paramref name="current"/> é a combinação que a produção tem agora
    /// (marca a folha atual); null nos Pegamentos sem Job On.
    /// </summary>
    private async Task<IReadOnlyList<PegamentosReadModel>> FindRecordsAsync(
        IQueryable<DMO.Alpha.Core.Controlo.Pegamentos> source,
        PegamentosCombination? current,
        CancellationToken cancellationToken)
    {
        var registros = await (
            from p in source
            join cm in _db.Tools on p.CmToolId equals cm.ToolId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join bq in _db.Tools on p.BqToolId equals bq.ToolId into bqGroup
            from bq in bqGroup.DefaultIfEmpty()
            join mf in _db.Tools on p.MfToolId equals mf.ToolId into mfGroup
            from mf in mfGroup.DefaultIfEmpty()
            orderby p.Id
            select new
            {
                p.Id,
                p.ControloId,
                p.ToleranceMm,
                p.CmToolId,
                p.BqToolId,
                p.MfToolId,
                p.CmToolReferenceFrozen,
                p.CmToolLotFrozen,
                p.BqToolReferenceFrozen,
                p.BqToolLotFrozen,
                p.MfToolReferenceFrozen,
                p.MfToolLotFrozen,
                p.RegistadoPor,
                p.RegistadoEmUtc,
                CmToolReference = cm != null ? cm.Reference : null,
                CmToolLot = cm != null ? cm.Lot : null,
                BqToolReference = bq != null ? bq.Reference : null,
                BqToolLot = bq != null ? bq.Lot : null,
                MfToolReference = mf != null ? mf.Reference : null,
                MfToolLot = mf != null ? mf.Lot : null,
                CmGargalo = _db.ToolTechnicalContexts.Where(t => t.ToolId == p.CmToolId).Select(t => t.DiametroGargaloMm).FirstOrDefault(),
                BqGargalo = _db.ToolTechnicalContexts.Where(t => t.ToolId == p.BqToolId).Select(t => t.DiametroGargaloMm).FirstOrDefault(),
                MfGargalo = _db.ToolTechnicalContexts.Where(t => t.ToolId == p.MfToolId).Select(t => t.DiametroGargaloMm).FirstOrDefault(),
                Measurements = p.Measurements
                    .OrderBy(m => m.RowPosition)
                    .Select(m => new
                    {
                        m.Id,
                        m.RowPosition,
                        m.Component,
                        m.CosturaMm,
                        m.ContraCosturaMm,
                        m.NominalGargaloMm,
                        m.NumeroFerramenta
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return registros.Select(registro =>
        {
            // As leituras persistidas com a avaliação backend: os derivados
            // são calculados a partir dos factos CONGELADOS (nominal da
            // linha + tolerância congelada no registo) — uma edição
            // posterior da tool/configuração não reescreve a avaliação
            // histórica da leitura.
            var measurementModels = registro.Measurements
                .Select(m => new PegamentosMeasurementReadModel(
                    m.Id,
                    m.RowPosition,
                    m.Component,
                    m.CosturaMm,
                    m.ContraCosturaMm,
                    m.NominalGargaloMm,
                    PegamentosCalculation.EvaluateRow(
                        m.NominalGargaloMm,
                        registro.ToleranceMm,
                        m.CosturaMm,
                        m.ContraCosturaMm),
                    m.NumeroFerramenta))
                .ToList();

            // Médias por componente (informativas; nenhuma média cancela
            // um alerta de leitura individual). Agregar os resultados de
            // cada linha evita emparelhar costuras de eixo único com contra
            // costuras que pertencem a outras leituras.
            var componentAverages = measurementModels
                .GroupBy(m => m.Component)
                .Select(group =>
                {
                    var rows = group.ToList();
                    var averageCostura = rows.Average(m => m.CosturaMm);
                    var averageContraCostura = rows.Average(m => m.ContraCosturaMm);
                    var averageOvalizacao = rows.Average(m => m.Evaluation.OvalizacaoMm);
                    var averageMedia = rows.Average(m => m.Evaluation.MediaMm);

                    return new PegamentosComponentAveragesReadModel(
                        group.Key,
                        rows.Count,
                        rows.Count(m => m.Evaluation.HasAlert),
                        rows.Count(m => m.Evaluation.IsNotEvaluable),
                        averageCostura,
                        averageContraCostura,
                        averageOvalizacao,
                        averageMedia);
                })
                .OrderBy(a => a.Component)
                .ToList();

            // Os componentes da folha: as ferramentas guardadas nela, com os
            // factos atuais de cada uma (o gargalo serve o componente ainda
            // sem leituras).
            var components = new[]
                {
                    (PegamentosComponent.Cm, registro.CmToolId, registro.CmToolReference, registro.CmToolLot, registro.CmGargalo),
                    (PegamentosComponent.Bq, registro.BqToolId, registro.BqToolReference, registro.BqToolLot, registro.BqGargalo),
                    (PegamentosComponent.Mf, registro.MfToolId, registro.MfToolReference, registro.MfToolLot, registro.MfGargalo)
                }
                .Where(c => c.Item2 is not null)
                .Select(c => new PegamentosComponentContextReadModel(c.Item1, c.Item2!, c.Item3, c.Item4, c.Item5))
                .ToList();

            return new PegamentosReadModel(
                registro.Id,
                registro.ControloId,
                registro.ToleranceMm,
                registro.CmToolId,
                registro.CmToolReferenceFrozen ?? registro.CmToolReference,
                registro.CmToolLotFrozen ?? registro.CmToolLot,
                registro.BqToolId,
                registro.BqToolReferenceFrozen ?? registro.BqToolReference,
                registro.BqToolLotFrozen ?? registro.BqToolLot,
                registro.MfToolId,
                registro.MfToolReferenceFrozen ?? registro.MfToolReference,
                registro.MfToolLotFrozen ?? registro.MfToolLot,
                measurementModels,
                componentAverages,
                registro.RegistadoPor)
            {
                RegistadoEmUtc = registro.RegistadoEmUtc,
                IsCurrent = registro.ControloId is null
                    || current?.Matches(registro.CmToolId, registro.BqToolId, registro.MfToolId) == true,
                Components = components
            };
        }).ToList();
    }
}
