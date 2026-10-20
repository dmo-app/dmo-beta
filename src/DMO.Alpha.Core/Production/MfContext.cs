namespace DMO.Alpha.Core.Production;

/// <summary>
/// Contexto MF (molde / ferramenta) associado a um Job On: o mf_id é o
/// retrato do tool_id usado para o jobon_id, e o que associa a produção é o
/// jobon_id (Diogo, 19/10/2026: «mf_id snapshot do tool_id usado para
/// jobon_id, então alterar tool_id deve alterar mf_id»). O ToolId de um mf_id
/// nunca muda: trocar o MF no Editar do Job On cria um mf_id novo; o anterior
/// fica no registo da produção, em <see cref="ReplacedJobOnId"/>.
/// </summary>
public sealed class MfContext
{
    public int Id { get; set; }

    /// <summary>
    /// A produção de que este é o mf_id em uso. Null depois de o MF ter sido
    /// trocado: a produção fica então em <see cref="ReplacedJobOnId"/>.
    /// </summary>
    public int? JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>
    /// A produção a que este mf_id pertencia antes de o MF ser trocado no
    /// Editar do Job On; null enquanto é o mf_id em uso.
    /// </summary>
    public int? ReplacedJobOnId { get; set; }

    public JobOn? ReplacedJobOn { get; set; }

    /// <summary>Quando o MF foi trocado (UTC); null enquanto é o mf_id em uso.</summary>
    public DateTime? ReplacedAtUtc { get; set; }

    public string ToolId { get; set; } = null!;
}
