namespace DMO.Alpha.Core.Production;

/// <summary>
/// Contexto BQ (boquilha) associado a um Job On.
/// Entidade mínima e purpose-specific; não pretende ser genérica.
/// </summary>
public sealed class BqContext
{
    public int Id { get; set; }

    public int JobOnId { get; set; }

    public JobOn JobOn { get; set; } = null!;

    public string ToolId { get; set; } = null!;
}
