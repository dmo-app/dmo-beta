namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O resultado da calote esférica para um par (D, h) — a matemática do apoio
/// TP/calote do dmo-html (controlo-v04, função <c>cap(D, h)</c>): o raio da
/// esfera R em mm e o volume da calote em cm³. R fica <c>null</c> quando
/// h = 0 (calote plana, sem esfera definida — o html: «height === 0 ? null»);
/// o volume é 0 nesse caso (nunca um facto inventado).
/// <code>
/// a = D/2; R = (a² + h²) / (2h) mm; V = π·h·(3a² + h²) / 6000 cm³
/// </code>
/// (o /6000 é o /6 do volume da calote esférica com o /1000 mm³→cm³).
/// </summary>
public sealed record PesoCapResult(decimal DiametroMm, decimal AlturaMm, decimal? RaioMm, decimal VolumeCm3);

/// <summary>
/// O apoio TP/calote — a comparação da calote efetiva com a referência da
/// Tool e com o desenho (a função <c>support(geometry, effective)</c> do
/// dmo-html): a referência é cap(D, h_ref) da Tool; a efetiva é cap(D,
/// calote efetiva) — a calote do Job On quando existe, senão a própria h_ref
/// da Tool. As diferenças PRESERVAM O SINAL (decisão do Owner: «+0,3 e −0,3
/// são fatos distintos»):
/// <code>
/// ΔR = actual.R − reference.R;  ΔV = actual.V − reference.V;
/// drawing_delta = reference.R − R_draw
/// </code>
/// O apoio é SEPARADO do cálculo principal do Peso (PesoCalculation.cs:6-7:
/// «o cálculo de suporte TP é separado e não está neste corte») e NUNCA o
/// bloqueia — é cálculo de apoio, nunca um gate.
/// </summary>
public sealed record PesoCapSupport(
    PesoCapResult Referencia,
    PesoCapResult Efetivo,
    decimal RaioDesenhoMm,
    /// <summary>drawing_delta = reference.R − R_draw; null quando a referência não tem R (h_ref = 0).</summary>
    decimal? DiferencaDesenhoMm,
    /// <summary>ΔR = actual.R − reference.R; null quando um dos lados não tem R (calote plana).</summary>
    decimal? DeltaRaioMm,
    /// <summary>ΔV = actual.V − reference.V (os volumes existem sempre; 0 cm³ na calote plana).</summary>
    decimal DeltaVolumeCm3);

/// <summary>
/// O cálculo de suporte TP/calote detido pelo backend/Core — a FONTE ÚNICA
/// da matemática da calote esférica (o browser nunca a calcula; o preview é
/// endpoint do backend reutilizando este cálculo). NÃO é um termo do
/// <see cref="PesoCalculation"/> (a fórmula do Peso fica intacta); é o
/// «apoio separado» que o PesoCalculation já previa.
/// </summary>
public static class PesoCapCalculation
{
    /// <summary>π em precisão decimal — a mesma constante do Math.PI do html (idêntica a 3 casas exibidas).</summary>
    private const decimal Pi = 3.141592653589793238462643383m;

    /// <summary>
    /// cap(D, h): a = D/2; R = (a² + h²)/(2h) mm (null quando h = 0);
    /// V = π·h·(3a² + h²)/6000 cm³. Valores de referência (contrato): D = 50,
    /// h = 10 → R = 36,25 mm; V ≈ 10,338 cm³.
    /// </summary>
    public static PesoCapResult Cap(decimal diametroMm, decimal alturaMm)
    {
        if (diametroMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(diametroMm), "O diâmetro TP tem de ser positivo.");
        }

        if (alturaMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(alturaMm), "A calote não pode ser negativa.");
        }

        var a = diametroMm / 2m;
        decimal? raio = alturaMm == 0m ? null : (a * a + alturaMm * alturaMm) / (2m * alturaMm);
        var volume = Pi * alturaMm * (3m * a * a + alturaMm * alturaMm) / 6000m;
        return new PesoCapResult(diametroMm, alturaMm, raio, volume);
    }

    /// <summary>
    /// O apoio completo (a função <c>support</c> do html): referência com a
    /// h_ref da Tool, efetiva com a calote dada (a do Job On quando existe;
    /// null = a própria h_ref) e as diferenças com o sinal preservado.
    /// </summary>
    public static PesoCapSupport Support(
        decimal diametroMm,
        decimal raioDesenhoMm,
        decimal referenciaAlturaMm,
        decimal? caloteEfetivaMm)
    {
        if (raioDesenhoMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(raioDesenhoMm), "O raio do desenho tem de ser positivo.");
        }

        if (caloteEfetivaMm is { } efetiva && efetiva < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(caloteEfetivaMm), "A calote não pode ser negativa.");
        }

        var referencia = Cap(diametroMm, referenciaAlturaMm);
        var efetivo = Cap(diametroMm, caloteEfetivaMm ?? referenciaAlturaMm);
        return new PesoCapSupport(
            referencia,
            efetivo,
            raioDesenhoMm,
            referencia.RaioMm is { } raioReferencia ? raioReferencia - raioDesenhoMm : null,
            referencia.RaioMm is { } referenciaR && efetivo.RaioMm is { } efetivoR ? efetivoR - referenciaR : null,
            efetivo.VolumeCm3 - referencia.VolumeCm3);
    }

    /// <summary>
    /// O apoio com a geometria OPICIONAL da Tool (os Tp* do
    /// ToolTechnicalContext): null quando a referência TP está por completar
    /// (D, R do desenho ou h_ref em falta/inválidos — o estado «Referência
    /// por completar» do html) ou quando uma calote é negativa. Nunca lança:
    /// a ausência do apoio não bloqueia nada.
    /// </summary>
    public static PesoCapSupport? TrySupport(
        decimal? diametroMm,
        decimal? raioDesenhoMm,
        decimal? referenciaAlturaMm,
        decimal? caloteEfetivaMm)
    {
        if (diametroMm is not { } diametro || diametro <= 0m)
        {
            return null;
        }

        if (raioDesenhoMm is not { } raioDesenho || raioDesenho <= 0m)
        {
            return null;
        }

        if (referenciaAlturaMm is not { } referenciaAltura || referenciaAltura < 0m)
        {
            return null;
        }

        try
        {
            return Support(diametro, raioDesenho, referenciaAltura, caloteEfetivaMm);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null; // calote efetiva negativa: sem apoio (nunca um facto inventado)
        }
    }
}
