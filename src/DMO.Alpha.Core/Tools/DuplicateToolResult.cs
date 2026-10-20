namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da duplicação de uma Tool.
/// </summary>
public enum DuplicateToolResult
{
    Success,

    /// <summary>
    /// A tool_id de origem não resolve a Tool canónica no registo (inclui
    /// a linha DUMMY REMOVED, que nunca é duplicável).
    /// </summary>
    ToolNotFound,

    /// <summary>
    /// O lote novo está em branco — a duplicação exige o lote da nova
    /// Tool real (nunca muta a fonte nem o seu lote).
    /// </summary>
    MissingNewLot,

    /// <summary>
    /// Já existe uma ferramenta com o mesmo tipo, referência e lote. Entre a
    /// beta.63 e a ferramenta repetida (18/10/2026) a duplicação abria a
    /// existente; agora é recusada e a existente fica com o aviso de
    /// ferramenta repetida.
    /// </summary>
    LotAlreadyExists
}
