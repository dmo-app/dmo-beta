using System.Globalization;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O Tampão de um Job On como a folha o mostra (Diogo, 28/10/2026: «diz para
/// completar … se for ao Job On já lá está»): o diâmetro e a calote gravados
/// no Job On mandam; sem eles, valem o «Diâmetro» (<c>tp_diametro_ps</c>) e a
/// «Bacia PS» (<c>tp_bacia_ps</c>) escritos na folha. Os Job Ons importados
/// só têm a folha; as colunas do Job On enchem-se ao gravar a folha na página.
/// </summary>
public static class TampaoDaFolha
{
    /// <summary>O diâmetro: o do Job On, senão o da folha (maior que 0).</summary>
    public static decimal? Diametro(decimal? doJobOn, string? daFolha) =>
        doJobOn ?? (Numero(daFolha) is { } d && d > 0m ? d : null);

    /// <summary>A calote: a do Job On, senão a da folha (0 ou mais).</summary>
    public static decimal? Calote(decimal? doJobOn, string? daFolha) =>
        doJobOn ?? (Numero(daFolha) is { } c && c >= 0m ? c : null);

    // Ponto ou vírgula, e um «mm» no fim, como se escreve na folha.
    private static decimal? Numero(string? valor)
    {
        var texto = valor?.Trim();
        if (texto is not null && texto.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            texto = texto[..^2].TrimEnd();
        }

        return texto is { Length: > 0 }
            && decimal.TryParse(texto.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : null;
    }
}
