namespace DMO.Alpha.Core.Production;

/// <summary>
/// Contexto CM (conjunto de moldagem) associado a um Job On.
/// Entidade mínima e purpose-specific; não pretende ser genérica.
/// </summary>
public sealed class CmContext
{
    public int Id { get; set; }

    public int JobOnId { get; set; }

    public JobOn JobOn { get; set; } = null!;

    public string ToolId { get; set; } = null!;
}
