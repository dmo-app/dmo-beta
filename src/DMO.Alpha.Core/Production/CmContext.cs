namespace DMO.Alpha.Core.Production;

/// <summary>
/// Contexto CM (conjunto de moldagem) associado a um Job On: o cm_id é o
/// retrato de um tool_id naquele momento, ligado à produção (jobon_id). O
/// ToolId de um cm_id nunca muda (Diogo, 18/10/2026: «se um tool_id usar o
/// mesmo cm_id não faz sentido; pior, o CM que estava primeiro desaparece do
/// registo da produção»; Decisão 001). Trocar a CM no Editar do Job On cria
/// um cm_id novo; o anterior fica no registo da produção, em
/// <see cref="ReplacedJobOnId"/>.
/// </summary>
public sealed class CmContext
{
    public int Id { get; set; }

    /// <summary>
    /// A produção de que este é o cm_id em uso. Null depois de a CM ter sido
    /// trocada: a produção fica então em <see cref="ReplacedJobOnId"/>.
    /// </summary>
    public int? JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>
    /// A produção a que este cm_id pertencia antes de a CM ser trocada no
    /// Editar do Job On; null enquanto é o cm_id em uso.
    /// </summary>
    public int? ReplacedJobOnId { get; set; }

    public JobOn? ReplacedJobOn { get; set; }

    /// <summary>Quando a CM foi trocada (UTC); null enquanto é o cm_id em uso.</summary>
    public DateTime? ReplacedAtUtc { get; set; }

    public string ToolId { get; set; } = null!;
}
