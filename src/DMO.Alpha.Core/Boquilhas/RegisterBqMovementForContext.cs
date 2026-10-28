namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: registar um movimento de Boquilhas no registo de uma produção
/// (bq_id), criando o registo (bq_repair_trace_id) quando a produção ainda
/// não o tem. Decisão do proprietário de 16/10/2026: abrir a máquina não
/// cria o registo; o registo nasce com o primeiro movimento. A ancoragem é
/// a mesma do fluxo antigo: o registo pré-produção não resolvido da mesma
/// ferramenta é reutilizado e associado ao bq_id; sem ele, nasce um registo
/// já associado ao bq_id com a referência e o lote congelados. O registo e o
/// movimento gravam juntos.
/// </summary>
public sealed record RegisterBqMovementForContext(
    int BqContextId,
    string MovementType,
    int Quantity,
    string? RegisteredByOperatorId = null,
    DateTime? OccurredAtUtc = null,
    int? MachineId = null,
    // Os AN que vão com as BQ (Diogo, 28/10/2026); null = sem AN.
    int? AnQuantity = null);
