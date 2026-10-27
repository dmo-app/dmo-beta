using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// Os números da Revisão e do Resumo do controlo: duas casas, «—» sem valor
/// (deep sweep de 18/10/2026, regra 66). Antes estavam repetidos em cada página.
/// </summary>
public static class ControloNumeros
{
    public static string Gramas(decimal? value) => value is { } v ? v.ToString("N2", DmoNumberFormat.Culture) + " g" : "—";

    public static string Cm3(decimal? value) => value is { } v ? v.ToString("N2", DmoNumberFormat.Culture) + " cm³" : "—";

    /// <summary>Com «+» à frente quando é positivo; a unidade vai colada (« g», « cm³»).</summary>
    public static string ComSinal(decimal? value, string unit) => value is { } v ? (v > 0 ? "+" : "") + v.ToString("N2", DmoNumberFormat.Culture) + unit : "—";
}
