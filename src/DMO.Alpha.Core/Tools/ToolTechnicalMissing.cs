namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Os dados técnicos que faltam a uma ferramenta, com os rótulos da
/// oficina: os que o Job On mostra no cartão de cada tipo (CM → processo,
/// punção e gargalo; BQ → marisa e gargalo; MF → gargalo nominal). A mesma
/// regra serve a ficha da ferramenta (abre a informação técnica com o aviso
/// amarelo) e a ficha da produção (o aviso junto à ferramenta).
/// </summary>
public static class ToolTechnicalMissing
{
    public static IReadOnlyList<string> For(
        ToolType type,
        bool hasProcess,
        decimal? volumePuncaoCm3,
        decimal? volumeMarisaCm3,
        decimal? diametroGargaloMm)
    {
        var missing = new List<string>();
        if (type == ToolType.Cm)
        {
            if (!hasProcess) missing.Add("processo");
            if (volumePuncaoCm3 is null) missing.Add("punção");
        }

        if (type == ToolType.Bq && volumeMarisaCm3 is null) missing.Add("marisa");
        if (diametroGargaloMm is null) missing.Add(type == ToolType.Mf ? "gargalo nominal" : "gargalo");
        return missing;
    }

    /// <summary>«Faltam dados técnicos: processo e punção.»</summary>
    public static string Message(IReadOnlyList<string> missing) => missing.Count switch
    {
        0 => string.Empty,
        1 => $"Falta o dado técnico: {missing[0]}.",
        _ => $"Faltam dados técnicos: {string.Join(", ", missing.Take(missing.Count - 1))} e {missing[^1]}."
    };

    /// <summary>O token CM/MF/BQ do Job On para o tipo; null quando desconhecido.</summary>
    public static ToolType? TypeOf(string? token) => token switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => null
    };
}
