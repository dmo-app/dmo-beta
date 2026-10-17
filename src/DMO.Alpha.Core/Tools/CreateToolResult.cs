namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da criação de uma Tool no registo Ferramentas.
/// Vocabulário FECHADO: recusas de negócio voltam por enum, nunca texto
/// livre nem exceção.
/// </summary>
public enum CreateToolResult
{
    Success,

    /// <summary>O token de tipo não é um dos canónicos (CM, MF, BQ).</summary>
    UnknownTypeToken,

    /// <summary>
    /// A referência está em branco — é a identidade HUMANA do registo
    /// (aprovado: MissingReference é recusa, nunca texto inventado).
    /// </summary>
    MissingReference,

    /// <summary>
    /// O lote está em branco. Desde a beta.63 já não é devolvido (o lote
    /// vazio grava «s/lote» com aviso); fica no enum para que um link antigo
    /// com ?create_refusal=MissingLot continue a mostrar a mensagem.
    /// </summary>
    MissingLot,

    /// <summary>Um machine_id que não resolve a máquina persistida.</summary>
    MachineNotFound,

    InvalidLotQuantity
}
