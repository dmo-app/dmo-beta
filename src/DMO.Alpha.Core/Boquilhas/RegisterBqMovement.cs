namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Comando: registar um movimento real de Boquilhas num bq_repair_trace_id
/// existente. Contém apenas os factos suportados pelo blueprint atual: o
/// trace de destino, o tipo de movimento (token canónico) e a quantidade
/// observada. A discrepância é produzida pelo backend conforme as regras de
/// matched/unmatched e nunca é fornecida pelo cliente. A DATA/HORA REAL do
/// movimento (o Owner 2026-10-14: o backfill dos ~2 meses de registos
/// passados é a verdade do registo antigo — a app não estava feita quando
/// aconteceram) chega em UTC pelo <c>OccurredAtUtc</c>; SEM data fornecida
/// o agora do backend manda — o uso diário fica intacto e o momento de
/// gravação técnica vive sempre no CreatedAtUtc do movimento persistido.
/// Uma data real no futuro (para lá da folga de
/// <see cref="BqMovementOccurrence.FutureToleranceMinutes"/> minutos) grava e
/// a página avisa.
/// </summary>
public sealed record RegisterBqMovement(
    int BqRepairTraceId,
    string MovementType,
    int Quantity,
    string? RegisteredByOperatorId = null,
    DateTime? OccurredAtUtc = null,
    int? MachineId = null,
    // Os AN que vão com as BQ (Diogo, 28/10/2026); null = sem AN.
    int? AnQuantity = null);
