namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado tipado da edição dos valores técnicos de uma Tool. Recusa
/// partilhada: ToolNotFound; o token de processo (quando fornecido) é
/// validado tipadamente (NNPB/PS) como no Peso.
/// </summary>
public enum EditToolTechnicalValuesResult
{
    Success,

    /// <summary>
    /// A tool_id não resolve a Tool canónica no registo (inclui a linha
    /// DUMMY REMOVED, que nunca é endereçável).
    /// </summary>
    ToolNotFound,

    /// <summary>O token do processo fornecido não é um dos autorizados (NNPB, PS).</summary>
    UnknownProcessToken,

    /// <summary>A supplied numeric field is malformed; an empty field remains valid.</summary>
    InvalidNumericValue,

    /// <summary>
    /// Outra pessoa gravou a informação técnica depois de a ficha ser aberta (a versão
    /// enviada já não é a gravada). Nada foi gravado: a ficha volta com o
    /// que ficou gravado, para a pessoa refazer a alteração por cima.
    /// </summary>
    ChangedByAnotherUser,

    /// <summary>
    /// Um valor é fisicamente impossível: 0 ou menos (a calote do tampão só
    /// abaixo de 0). Nada foi gravado (Diogo, 18/10/2026).
    /// </summary>
    ImpossibleValue
}
