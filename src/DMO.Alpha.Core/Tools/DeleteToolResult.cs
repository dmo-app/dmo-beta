namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da eliminação de uma Tool.
/// </summary>
public enum DeleteToolResult
{
    Success,

    /// <summary>
    /// A tool_id não resolve a Tool canónica no registo (inclui a linha
    /// DUMMY REMOVED, que nunca é eliminável — é a tombstone que recebe as
    /// reatribuições).
    /// </summary>
    ToolNotFound
}
