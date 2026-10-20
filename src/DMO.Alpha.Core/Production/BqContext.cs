namespace DMO.Alpha.Core.Production;

/// <summary>
/// Contexto BQ (boquilha) associado a um Job On: o bq_id liga uma produção
/// (jobon_id) a UMA boquilha física (tool_id). O ToolId de um bq_id nunca
/// muda (Diogo, 18/10/2026: «um tool_id nunca pode ter o mesmo bq_id, senão
/// o histórico não faz sentido»). Trocar a BQ no Editar do Job On cria um
/// bq_id novo; o anterior fica com os seus movimentos, na BQ antiga, e passa
/// para <see cref="ReplacedJobOnId"/>.
/// </summary>
public sealed class BqContext
{
    public int Id { get; set; }

    /// <summary>
    /// A produção de que este é o bq_id em uso. Null depois de a BQ ter sido
    /// trocada: a produção fica então em <see cref="ReplacedJobOnId"/>.
    /// </summary>
    public int? JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>
    /// A produção a que este bq_id pertencia antes de a BQ ser trocada no
    /// Editar do Job On; null enquanto é o bq_id em uso.
    /// </summary>
    public int? ReplacedJobOnId { get; set; }

    public JobOn? ReplacedJobOn { get; set; }

    /// <summary>Quando a BQ foi trocada (UTC); null enquanto é o bq_id em uso.</summary>
    public DateTime? ReplacedAtUtc { get; set; }

    public string ToolId { get; set; } = null!;
}
