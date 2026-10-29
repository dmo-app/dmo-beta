namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado tipado do registo de um movimento de Boquilhas.
/// </summary>
public enum RegisterBqMovementResult
{
    Success,

    /// <summary>O bq_repair_trace_id não existe.</summary>
    TraceNotFound,

    /// <summary>O tool_id do trace não resolve a Tool canónica no registo Ferramentas.</summary>
    TraceToolNotRegistered,

    /// <summary>O tipo de movimento não é um dos três autorizados (saida, entrada, entrada_sem_reparacao).</summary>
    UnknownMovementType,

    /// <summary>
    /// A quantidade observada é ilegível (abc), nula ou não-positiva (0, -5) —
    /// nunca o 0 silencioso que reescrevia o fato observado (o achado do
    /// stresser das Boquilhas 2026-10-12).
    /// </summary>
    InvalidQuantity,

    /// <summary>
    /// Já não é devolvido: a hora que cai na mudança de hora passa para a hora
    /// seguinte e a página avisa. Mantém-se para os links antigos.
    /// </summary>
    InvalidOccurrenceTime,

    /// <summary>A máquina indicada não existe.</summary>
    MachineNotFound,

    /// <summary>A quantidade de AN escrita é ilegível ou negativa (vazia ou 0 = sem AN).</summary>
    InvalidAnQuantity,

    /// <summary>A linha não pertence à ferramenta nem à produção histórica escolhida.</summary>
    MachineNotCompatible
}
