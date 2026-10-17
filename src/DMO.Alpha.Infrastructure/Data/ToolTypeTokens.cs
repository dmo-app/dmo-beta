using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Conversão entre o <see cref="ToolType"/> e o token persistido no registo
/// canónico de Tools (exatamente CM, MF e BQ). Os conversores do EF Core são
/// árvores de expressão, pelo que o switch/throw vive aqui dentro de chamadas
/// de método simples.
/// </summary>
internal static class ToolTypeTokens
{
    public static string ToStorage(ToolType type) => type switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        ToolType.Bq => "BQ",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de Tool desconhecido.")
    };

    public static ToolType FromStorage(string token) => token switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => throw new FormatException($"Tipo de Tool desconhecido: {token}.")
    };
}
