namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Uma entrada do RESUMO DIÁRIO · 6 MÁQUINAS (o historyTab do dmo-html —
/// «Resumo diário — B1, B2, B3, C1, C2, C3: dia na vertical; máquinas na
/// horizontal»): os movimentos de UM REGISTO (um bq_repair_trace — o link
/// do html) num dia numa máquina, agrupados pelo backend — cada
/// referência/BQ/lote aparece separada quando há mudança na mesma máquina
/// (mudança de registo). Os contadores E/S/SR são CONTAGENS de movimentos
/// do registo nesse dia (E = Entradas de reparação, S = Saídas, SR =
/// Entradas sem reparação; zeros omitidos na folha). O read model é o que
/// o gerador PDF da A5 consome — o gerador SÓ FORMATA, nunca calcula (a
/// regra do Owner 14/10). O bq_repair_trace_id vive no URL (o duplo clique
/// abre o registo) — nunca no texto da folha.
/// </summary>
public sealed record BqDailySummaryEntry(
    DateOnly Day,
    string MachineCode,
    int BqRepairTraceId,
    string? ToolReference,
    string? ToolLot,
    string? ProductionNumber,
    string? ProductionReference,
    int Entradas,
    int Saidas,
    int EntradasSemReparacao,
    /// <summary>As SOMAS DE QUANTIDADE do registo no dia — o que o PDF da
    /// grelha mostra (o Owner 14/10: «Saiu 21 entrou 21» são as PEÇAS
    /// movidas, não a contagem de movimentos). Agregados pelo query; o
    /// gerador da A5 só formata.</summary>
    int EntradasQuantidade = 0,
    int SaidasQuantidade = 0,
    int EntradasSemReparacaoQuantidade = 0);
