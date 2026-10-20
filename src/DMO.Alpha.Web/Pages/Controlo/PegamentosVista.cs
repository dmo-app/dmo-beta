using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Os formatos e rótulos da folha de Pegamentos, partilhados pelos partials
/// das zonas (a página e a resposta só com as zonas de uma gravação, E11).
/// </summary>
public static class PegamentosVista
{
    public static string Fmt(decimal value) =>
        value.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture);

    public static string FmtOptional(decimal? value) =>
        value is null ? "—" : Fmt(value.Value);

    public static string FmtSigned(decimal? value) =>
        value is null ? "—" : DMO.Alpha.Core.Runtime.DmoNumberFormat.Signed(value.Value);

    /// <summary>
    /// O valor exato, sem arredondar, para a pré-visualização no browser
    /// (os limites do corredor e a tolerância): com duas casas, 70.205
    /// passava a 70.21 e o browser marcava outra coisa que o servidor.
    /// </summary>
    public static string Exato(decimal value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A ovalização a vermelho: a mesma regra do alerta da linha
    /// (<see cref="PegamentosCalculation.IsOvalizacaoAlert"/>; Diogo,
    /// 19/10/2026, F8: igual ou acima da tolerância, para mais ou para
    /// menos).
    /// </summary>
    public static bool OvalAlert(PegamentosRowEvaluation evaluation, decimal toleranceMm) =>
        PegamentosCalculation.IsOvalizacaoAlert(evaluation.OvalizacaoMm, toleranceMm);

    /// <summary>
    /// CM, BQ e MF, como em toda a app (Diogo, 19/10/2026: «ou usam CM BQ MF
    /// ou nome completo»).
    /// </summary>
    public static string ComponentLabel(PegamentosComponent component) => component.ToString().ToUpperInvariant();

    public static bool AxisAlert(PegamentosRowEvaluation evaluation, PegamentosEvaluatedAxis axis) =>
        evaluation.Values.FirstOrDefault(v => v.Axis == axis) is { } value && value.Alert;

    public static string StateLabel(PegamentosRowEvaluation evaluation) => evaluation switch
    {
        { IsNotEvaluable: true } => "não avaliável",
        { HasAlert: true } => "fora",
        _ => "dentro"
    };

    /// <summary>O token da zona de um componente (pegCartaoCM, pegCartaoBQ, pegCartaoMF).</summary>
    public static string Token(PegamentosComponent component) =>
        component == PegamentosComponent.Cm ? "CM" : component == PegamentosComponent.Bq ? "BQ" : "MF";
}

/// <summary>O cartão de um componente na sua zona (_PegamentosCartao).</summary>
public sealed record PegamentosCartaoZona(
    PegamentosModel Pagina,
    PegamentosComponentContextReadModel Componente);
