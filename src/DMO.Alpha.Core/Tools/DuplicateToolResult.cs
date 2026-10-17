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
    /// Já existe uma ferramenta com o mesmo tipo, referência e lote. Desde a
    /// beta.63 já não é devolvido: a duplicação abre a ferramenta existente
    /// (Success com <see cref="CreatedTool.AlreadyExisted"/>); fica para os
    /// links antigos com ?duplicate_refusal=LotAlreadyExists.
    /// </summary>
    LotAlreadyExists
}
