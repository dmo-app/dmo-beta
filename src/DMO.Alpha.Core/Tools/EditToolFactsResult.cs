namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da edição dos factos de uma Tool. Recusa partilhada
/// com a edição de valores técnicos: ToolNotFound (a tool_id não resolve
/// no registo — inclui a DUMMY, que nunca é editável) + as de máquina.
/// </summary>
public enum EditToolFactsResult
{
    Success,

    /// <summary>
    /// A tool_id não resolve a Tool canónica no registo (inclui a linha
    /// DUMMY REMOVED, que nunca é endereçável).
    /// </summary>
    ToolNotFound,

    /// <summary>O token de tipo não é um dos canónicos (CM, MF, BQ).</summary>
    UnknownTypeToken,

    /// <summary>A referência está em branco (identidade humana do registo).</summary>
    MissingReference,

    /// <summary>
    /// O lote está em branco. Desde a beta.63 já não é devolvido (o lote
    /// vazio grava «s/lote» com aviso); fica para os links antigos.
    /// </summary>
    MissingLot,

    /// <summary>Um machine_id que não resolve a máquina persistida.</summary>
    MachineNotFound,

    /// <summary>Outra ferramenta já tem o mesmo tipo, referência e lote.</summary>
    LotAlreadyExists,

    /// <summary>A quantidade do lote não é um inteiro igual ou superior a 0.</summary>
    InvalidLotQuantity
}
