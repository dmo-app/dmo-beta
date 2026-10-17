namespace DMO.Alpha.Core.Production;

/// <summary>
/// Máquina de produção. Entidade mínima para a proof de contexto;
/// não pretende ser um modelo de domínio completo.
/// </summary>
public sealed class Machine
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;
}
