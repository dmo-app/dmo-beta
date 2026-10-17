namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Movimento de Boquilhas: um evento real de quantidade que pertence a um
/// registo de reparação (bq_repair_trace_id). A identidade canónica é o
/// movement_id, emitido pelo backend, distinta de bq_repair_trace_id,
/// bq_id, tool_id e de qualquer identidade de registo/audit.
/// A quantidade observada é registada na totalidade — nunca truncada,
/// compensada ou reescrita. A discrepância produzida pelo movimento é um
/// facto histórico (por exemplo -2 para duas BQ não explicadas numa
/// Entrada); null significa movimento normal, sem discrepância (em
/// branco, nunca zero exibido como facto). Nenhum estado de trace é criado
/// ou alterado por movimentos, e nada é escrito de volta no Job On.
/// Entidade mínima de identidade e factos explicitamente definidos: as
/// DUAS DATAS (a real do movimento e a técnica do registo), o reparador
/// da saída resolvido pela rota com o nome congelado e o operador que
/// registou são factos persistidos; a razão/motivo não está definida no
/// blueprint atual.
/// </summary>
public sealed class BqMovement
{
    public int Id { get; set; }

    public int BqRepairTraceId { get; set; }

    public BqRepairTrace Trace { get; set; } = null!;

    public BqMovementType Type { get; set; }

    /// <summary>Quantidade observada, registada na totalidade.</summary>
    public int Quantity { get; set; }

    /// <summary>Máquina efetiva no movimento; preservada se o contexto ou compatibilidades mudarem.</summary>
    public string? MachineCodeFrozen { get; set; }

    /// <summary>
    /// Discrepância produzida por este movimento (negativa: quantidade
    /// observada não explicada). Null = movimento normal, sem discrepância.
    /// </summary>
    public int? Discrepancy { get; set; }

    /// <summary>
    /// O MOMENTO REAL do movimento (UTC — o corte F fatia 4, o Owner
    /// 2026-10-13: o calendário do Histórico carrega num dia e vê os
    /// movimentos desse dia). O HISTÓRICO e os CONTADORES agrupam e
    /// ordenam POR ESTA data. Vem do form «Data e hora reais» quando o
    /// operador o indica — o backfill dos ~2 meses de registos passados é
    /// a verdade do registo antigo (a decisão do Owner 2026-10-14: a
    /// regra antiga do «agora do backend» era contra a falsificação no
    /// uso diário, e o backfill é o caso em que o registo antigo só
    /// acontece agora); sem data fornecida é o agora do backend.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>
    /// O AGORA TÉCNICO do registo (a auditoria: quando o backend gravou a
    /// linha) — sempre UtcNow, nunca reescrito pela data real do
    /// movimento. O momento real do movimento vive em
    /// <see cref="OccurredAtUtc"/>.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// O nº de operador QUE registou o movimento (o rasto do registo — o
    /// filtro por reparador do Histórico; o human-readable, o fato).
    /// </summary>
    public string? RegisteredByOperatorId { get; set; }

    /// <summary>
    /// A ENTIDADE REPARADORA usada pela SAÍDA (o Owner 2026-10-12:
    /// «reparadores sao as entidades para onde enviamos as boquilhas»):
    /// resolvida PELA ROTA no registo — a máquina do contexto de produção
    /// do trace (ou, antes de produção, a máquina associada da ficha da
    /// Tool) → a config máquina→reparador (ModuleSettings). Nunca um
    /// input do form (blueprint boquilhas/OVERVIEW.md L357: «A Saída
    /// does not ask the operator to choose the repairer manually»). Null
    /// quando a máquina não tem config ou a entidade já não existe — o
    /// movimento grava na mesma SEM reparador (truthful, a app nunca
    /// bloqueia). Os movimentos entrada/entrada_sem_reparacao não
    /// transportam reparador: só a saída o define.
    /// </summary>
    public int? BqRepairerId { get; set; }

    /// <summary>
    /// O NOME DO REPARADOR CONGELADO no momento da saída (o PDF-alvo
    /// mostra «Reparado» no cabeçalho de histórico): a verdade histórica
    /// do movimento — sobrevive à edição/eliminação da entidade e à
    /// mudança posterior da config (blueprint boquilhas/OVERVIEW.md
    /// L372-374: «The repairer that applied when the real movement
    /// occurred remains part of that movement's historical truth»).
    /// Null quando a saída não teve reparador.
    /// </summary>
    public string? BqRepairerNameFrozen { get; set; }

    /// <summary>
    /// QUEM fez a última correção (o Owner 2026-10-16): o nome do utilizador
    /// congelado no momento da correção. Null quando o movimento nunca foi
    /// corrigido ou quando a correção chegou sem identidade (grava na mesma).
    /// </summary>
    public string? CorrectedByNameFrozen { get; set; }

    /// <summary>QUANDO foi feita a última correção (UTC). Null = nunca corrigido.</summary>
    public DateTime? CorrectedAtUtc { get; set; }
}
