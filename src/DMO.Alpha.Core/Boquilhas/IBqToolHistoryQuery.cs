namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// O HISTÓRICO DE PRODUÇÕES de uma boquilha (a traçabilidade reversa — a
/// decisão do Owner 2026-10-12: «temos de ver se da para ver o historico
/// de uma tool_id atraves do bq_id por exemplo, os jobons que foi usado
/// etc»): a ferramenta física resolve-se pelos CONTEXT IDS (a cascata:
/// os módulos ancoram nos contextos, nunca no tool_id) — os BqContexts
/// cujo ToolId é o pedido → OS JOBONS EM QUE FOI USADA. Do mais recente
/// para o mais antigo; os FATOS HUMANOS (o nº, a referência, a máquina).
/// </summary>
public interface IBqToolHistoryQuery
{
    /// <summary>Uma página (25 por defeito) dos Job Ons em que a boquilha foi usada.</summary>
    Task<DMO.Alpha.Core.Paging.PagedList<BqToolJobHistoryItem>> ListJobHistoryAsync(
        string toolId,
        int? pageNumber,
        int pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Os dias do mês que têm movimentos (os pontos do calendário).</summary>
    Task<IReadOnlyList<BqHistoryDayCount>> ListMonthCountsAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// OS MOVIMENTOS DETALHADOS do Histórico (o vista «Movimentos
    /// detalhados» do dmo-html): o intervalo sobre a DATA REAL do
    /// movimento (desde/até inclusivos em hora local do servidor; null =
    /// sem limite) com os filtros do html — a máquina, o tipo (o token
    /// canónico: saida/entrada/entrada_sem_reparacao; null = todos), o
    /// reparador (o nome congelado ou o nº do operador — parciais contam)
    /// e a pesquisa de texto (a boquilha, o lote, a produção, a máquina, o
    /// reparador — contém, case-insensitive). Do mais recente para o mais
    /// antigo; os FATOS HUMANOS de cada movimento.
    /// </summary>
    Task<IReadOnlyList<BqHistoryMovementItem>> ListMovementsAsync(
        DateOnly? desde,
        DateOnly? ate,
        string? machine,
        string? type,
        string? operatorId,
        string? text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O RESUMO DIÁRIO · 6 MÁQUINAS (o vista «Resumo diário» do dmo-html):
    /// uma entrada por (dia, máquina, REGISTO) — os movimentos do mesmo
    /// bq_repair_trace num dia numa máquina agrupam-se numa entrada com os
    /// contadores E/S/SR; cada referência/BQ/lote aparece separada quando
    /// há mudança de registo na mesma máquina. Os mesmos filtros do
    /// ListMovementsAsync sobre a DATA REAL. O read model que o gerador PDF
    /// da A5 consome — o gerador SÓ FORMATA, nunca calcula.
    /// </summary>
    Task<IReadOnlyList<BqDailySummaryEntry>> ListDailySummaryAsync(
        DateOnly desde,
        DateOnly ate,
        string? machine,
        string? type,
        string? operatorId,
        string? text,
        CancellationToken cancellationToken = default);
}

/// <summary>Um movimento do Histórico (os fatos humanos de toda a cadeia).</summary>
public sealed record BqHistoryMovementItem(
    int MovementId,
    /// <summary>O bq_repair_trace_id do movimento (o registo que o duplo
    /// clique abre — o id vive no URL, nunca no texto da folha).</summary>
    int BqRepairTraceId,
    string MovementType,
    int Quantity,
    int? Discrepancy,
    /// <summary>A DATA/HORA REAL do movimento (UTC — o occurredAt; o
    /// backfill dos ~2 meses torna o registo antigo verdadeiro no seu
    /// dia).</summary>
    DateTime OccurredAtUtc,
    string? RegisteredByOperatorId,
    string? ToolReference,
    string? ToolLot,
    string? MachineCode,
    string? ProductionNumber,
    string? Reference,
    string? BqRepairerNameFrozen = null);

/// <summary>Um dia do calendário com movimentos (o ponto do Histórico).</summary>
public sealed record BqHistoryDayCount(
    DateOnly Day,
    int MovementCount);

/// <summary>Uma produção em que a boquilha esteve (o card do histórico).</summary>
public sealed record BqToolJobHistoryItem(
    int JobOnId,
    string ProductionNumber,
    string Reference,
    string MachineCode);
