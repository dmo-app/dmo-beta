namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Read model da FOLHA de Pegamentos de um controlo: a âncora (controlo_id)
/// com o contexto de produção herdado do Job On (a mesma projeção canónica
/// de <see cref="DMO.Alpha.Core.Production.IProductionContextQuery"/>,
/// composta no momento da leitura — nunca re-selecionada pelo utilizador),
/// os componentes aplicáveis da combinação (CM/BQ/MF presentes no
/// contexto) com o facto técnico corrente de cada tool (o gargalo herdado,
/// null = em falta, completável no fluxo), a tolerância efetiva corrente
/// (a que a criação do registo congelará) e o registo durável quando já
/// existe. O registo ausente é um estado válido (a folha mostra-se pronta a
/// medir; o registo nasce da primeira medição).
/// </summary>
/// <remarks>
/// Nos Pegamentos sem Job On, <see cref="ControloId"/> e <see cref="JobOnId"/>
/// são null, a referência é a da CM (ou «—»), o nº de produção é
/// <see cref="SemJobOnProductionNumber"/> e os componentes são as
/// ferramentas guardadas no registo.
/// </remarks>
public sealed record PegamentosSheetReadModel(
    int? ControloId,
    int? JobOnId,
    string Reference,
    string ProductionNumber,
    string Machine,
    int? CmId,
    string? CmToolId,
    int? MfId,
    string? MfToolId,
    int? BqId,
    string? BqToolId,
    IReadOnlyList<PegamentosComponentContextReadModel> Components,
    decimal CurrentToleranceMm,
    PegamentosReadModel? Pegamentos)
{
    /// <summary>O texto do nº de produção nos Pegamentos sem Job On.</summary>
    public const string SemJobOnProductionNumber = "Sem Job On";

    /// <summary>True nos Pegamentos sem Job On (ainda por associar).</summary>
    public bool SemJobOn => ControloId is null;
}

/// <summary>
/// Um componente aplicável da combinação da folha: a tool herdada do
/// contexto Job On (factos descritivos atuais — apenas leitura, a
/// composição não transfere ownership) e o seu valor técnico corrente de
/// gargalo (mm; null quando a tool ainda não o tem — completion in-flow na
/// folha, gravando no tool_id owner).
/// </summary>
public sealed record PegamentosComponentContextReadModel(
    PegamentosComponent Component,
    string ToolId,
    string? ToolReference,
    string? ToolLot,
    decimal? DiametroGargaloMm);

/// <summary>
/// Read model do registo durável de Pegamentos (pegamentos_id): as âncoras
/// congeladas (tools da combinação herdadas na criação, tolerância do
/// corredor congelada), os factos descritivos preservados das tools âncora
/// (apenas leitura), as leituras persistidas com os derivados e a
/// avaliação backend (calculados a partir dos factos congelados — nunca do
/// estado corrente) e as médias por componente.
/// </summary>
public sealed record PegamentosReadModel(
    int PegamentosId,
    int? ControloId,
    decimal ToleranceMm,
    string? CmToolId,
    string? CmToolReference,
    string? CmToolLot,
    string? BqToolId,
    string? BqToolReference,
    string? BqToolLot,
    string? MfToolId,
    string? MfToolReference,
    string? MfToolLot,
    IReadOnlyList<PegamentosMeasurementReadModel> Measurements,
    IReadOnlyList<PegamentosComponentAveragesReadModel> ComponentAverages,
    string? RegistadoPor = null);

/// <summary>
/// Uma leitura persistida da folha tal como a superfície a apresenta: os
/// factos observados (posição física, componente, gesto em cruz), o
/// nominal gargalo congelado na linha no momento da medição (null =
/// NotEvaluable) e a avaliação backend contra o corredor (ovalização com
/// sinal, média, limites congelados, valores verificados). A média nunca
/// substitui as leituras individuais (manual §7 L316).
/// </summary>
public sealed record PegamentosMeasurementReadModel(
    int MeasurementId,
    int RowPosition,
    PegamentosComponent Component,
    decimal CosturaMm,
    decimal? ContraCosturaMm,
    decimal? NominalGargaloMm,
    PegamentosRowEvaluation Evaluation);

/// <summary>
/// Médias e contagens por componente, derivadas apenas das leituras
/// persistidas: média dos resultados por linha; contra costura e ovalização
/// incluem apenas os valores existentes, sem inventar um segundo eixo nas
/// leituras de eixo único. Informativas: nenhuma média cancela
/// um alerta de leitura individual (spec L109-110).
/// </summary>
public sealed record PegamentosComponentAveragesReadModel(
    PegamentosComponent Component,
    int SampleCount,
    int AlertCount,
    int NotEvaluableCount,
    decimal? AverageCosturaMm,
    decimal? AverageContraCosturaMm,
    decimal? AverageOvalizacaoMm,
    decimal? AverageMediaMm);
