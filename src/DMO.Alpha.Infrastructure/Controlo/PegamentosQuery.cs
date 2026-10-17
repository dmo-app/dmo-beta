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

        // Os componentes aplicáveis da combinação com o facto técnico
        // corrente de cada tool (gargalo herdado; null = em falta,
        // completável no fluxo). O contexto pode não ter um componente
        // (o MF do J10 real) — só os presentes entram na folha.
        var components = await ComponentsAsync(
            production.CmToolId, production.BqToolId, production.MfToolId, cancellationToken);

        // A tolerância efetiva corrente (a que a criação do registo
        // congelará): configurada quando existir, default do Owner na sua
        // falta — nunca locked no cálculo.
        var currentTolerance = await _toleranceConfiguration.FindConfiguredAsync(cancellationToken)
            ?? PegamentosCalculation.DefaultToleranceMm;

        var registroModel = await FindRecordByControloAsync(controloId, cancellationToken);

        return new PegamentosSheetReadModel(
            controloId,
            production.JobOnId,
            production.Reference,
            production.ProductionNumber,
            production.Machine,
            production.CmId,
            production.CmToolId,
            production.MfId,
            production.MfToolId,
            production.BqId,
            production.BqToolId,
            components,
            currentTolerance,
            registroModel);
    }

    public async Task<PegamentosSheetReadModel?> FindSheetByPegamentosAsync(
        int pegamentosId,
        CancellationToken cancellationToken = default)
    {
        var registro = await FindRecordAsync(
            _db.Pegamentos.AsNoTracking().Where(p => p.Id == pegamentosId), cancellationToken);
        if (registro is null)
        {
            return null;
        }

        // Já associado: a folha da produção (o contexto todo pelo controlo_id).
        if (registro.ControloId is int controloId)
        {
            return await FindSheetByControloAsync(controloId, cancellationToken);
        }

        // Sem Job On: as ferramentas são as guardadas no registo.
        var components = await ComponentsAsync(
            registro.CmToolId, registro.BqToolId, registro.MfToolId, cancellationToken);
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
            components,
            currentTolerance,
            registro);
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

    public Task<PegamentosReadModel?> FindRecordByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        FindRecordAsync(_db.Pegamentos.AsNoTracking().Where(p => p.ControloId == controloId), cancellationToken);

    private async Task<PegamentosReadModel?> FindRecordAsync(
        IQueryable<DMO.Alpha.Core.Controlo.Pegamentos> source,
        CancellationToken cancellationToken)
    {
        // O registo durável do controlo, quando já existe (a ausência é o
        // estado válido antes da primeira medição): o caso normal é um por
        // produção; o determinismo da leitura é o primeiro por Id. Na mesma
        // consulta vêm as leituras e a referência/lote atuais das Tools (só
        // servem de fallback para dados legados sem os factos congelados).
        var registro = await (
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
                CmToolReference = cm != null ? cm.Reference : null,
                CmToolLot = cm != null ? cm.Lot : null,
                BqToolReference = bq != null ? bq.Reference : null,
                BqToolLot = bq != null ? bq.Lot : null,
                MfToolReference = mf != null ? mf.Reference : null,
                MfToolLot = mf != null ? mf.Lot : null,
                Measurements = p.Measurements
                    .OrderBy(m => m.RowPosition)
                    .Select(m => new
                    {
                        m.Id,
                        m.RowPosition,
                        m.Component,
                        m.CosturaMm,
                        m.ContraCosturaMm,
                        m.NominalGargaloMm
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registro is null)
        {
            return null;
        }

        // As leituras persistidas com a avaliação backend: os derivados
        // são calculados a partir dos factos CONGELADOS (nominal da
        // linha + tolerância congelada no registo) — uma edição
        // posterior da tool/configuração não reescreve a avaliação
        // histórica da leitura.
        var measurements = registro.Measurements;

        var measurementModels = measurements
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
                    m.ContraCosturaMm)))
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
            registro.RegistadoPor);
    }
}
