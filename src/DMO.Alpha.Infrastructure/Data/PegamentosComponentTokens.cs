using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Conversão entre o <see cref="PegamentosComponent"/> e o token persistido
/// nas medições de Pegamentos (exatamente CM, BQ e MF). Os conversores do
/// EF Core são árvores de expressão, pelo que o switch/throw vive aqui
/// dentro de chamadas de método simples.
/// </summary>
internal static class PegamentosComponentTokens
{
    public static string ToStorage(PegamentosComponent component) => component switch
    {
        PegamentosComponent.Cm => "CM",
        PegamentosComponent.Bq => "BQ",
        PegamentosComponent.Mf => "MF",
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Componente de Pegamentos desconhecido.")
    };

    public static PegamentosComponent FromStorage(string token) => token switch
    {
        "CM" => PegamentosComponent.Cm,
        "BQ" => PegamentosComponent.Bq,
        "MF" => PegamentosComponent.Mf,
        _ => throw new FormatException($"Componente de Pegamentos desconhecido: {token}.")
    };
}
