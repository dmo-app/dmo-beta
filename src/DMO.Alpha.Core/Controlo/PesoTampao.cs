using System.Globalization;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O tampão (TP) de um Peso: o diâmetro D e a calote h, em mm, como a oficina
/// os escreve («24.95 x 4.5mm»).
/// </summary>
public sealed partial record PesoTampao(decimal DiametroMm, decimal CaloteMm)
{
    /// <summary>«24.95 x 4.5», como as referências dos tampões, sem casas a mais.</summary>
    public string Texto =>
        $"{DiametroMm.ToString("0.###", CultureInfo.InvariantCulture)} x {CaloteMm.ToString("0.###", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Lê «24.95 x 4.5mm», «24,95x4,5» ou «24.95 × 4.5 mm». Só um número muda
    /// a calote e fica o diâmetro atual (<paramref name="diametroAtual"/>).
    /// Null quando o texto não se lê, o diâmetro não é positivo ou a calote é
    /// negativa.
    /// </summary>
    public static PesoTampao? Ler(string? texto, decimal? diametroAtual = null)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpo = texto.Trim().ToLowerInvariant().Replace("mm", string.Empty).Replace(',', '.');
        var partes = Separador().Split(limpo).Where(p => p.Length > 0).ToArray();
        decimal? diametro;
        decimal? calote;
        switch (partes.Length)
        {
            case 1:
                diametro = diametroAtual;
                calote = Numero(partes[0]);
                break;
            case 2:
                diametro = Numero(partes[0]);
                calote = Numero(partes[1]);
                break;
            default:
                return null;
        }

        return diametro is > 0m && calote is >= 0m ? new PesoTampao(diametro.Value, calote.Value) : null;
    }

    private static decimal? Numero(string texto) =>
        decimal.TryParse(texto.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    [GeneratedRegex(@"\s*[x×*]\s*|\s+")]
    private static partial Regex Separador();
}

/// <summary>
/// O peso de um Peso com outro tampão: só exibição, nada é gravado.
/// </summary>
public sealed record PesoTampaoSimulado(
    PesoTampao Atual,
    PesoTampao Simulado,
    decimal VolumeAtualCm3,
    decimal VolumeSimuladoCm3,
    /// <summary>V(simulado) − V(atual), com o sinal.</summary>
    decimal DeltaVolumeCm3,
    decimal PesoMedioGrams,
    decimal PesoSimuladoGrams,
    /// <summary>Peso simulado − peso médio, com o sinal.</summary>
    decimal DiferencaGrams,
    decimal? VariacaoPercent);
