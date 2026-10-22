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
    /// O lote de uma BQ está em branco (Diogo, 22/10/2026: o lote é preciso
    /// para identificar a boquilha). Nas CM e MF o lote vazio grava «s/lote»
    /// com aviso (beta.63).
    /// </summary>
    MissingLot,

    /// <summary>Um machine_id que não resolve a máquina persistida.</summary>
    MachineNotFound,

    InvalidLotQuantity,

    /// <summary>
    /// Já existe uma ferramenta com o mesmo tipo, referência e lote (o
    /// proprietário, 18/10/2026: bloquear, não abrir a existente). Nada foi
    /// registado; a existente ficou com o aviso de ferramenta repetida.
    /// </summary>
    ToolAlreadyExists
}
