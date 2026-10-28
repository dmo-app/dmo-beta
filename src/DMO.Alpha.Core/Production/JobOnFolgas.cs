using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Production;

/// <summary>
/// As FOLGAS do MF são três espaços, um número em cada (Diogo, 28/10/2026: «só
/// deve existir 1 número por espaço»): a folga, a tolerância no meio e a folga
/// da flecha (hollow mill) no último; o CM só tem os dois primeiros. Um espaço com dois números («0.12 / 0.16») e o outro da
/// ponta vazio mostra o primeiro no primeiro espaço e o segundo no último
/// («0.16 no último»). O resto fica como está. Ao gravar, uma folga com mais de um número é recusada
/// (<see cref="JobOnFolhaRegras.PrimeiraChaveLonga"/>).
/// </summary>
public static partial class JobOnFolgas
{
    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex Numero();

    /// <summary>Quantos números tem o texto («0.12 / 0.16» → 2).</summary>
    public static int Numeros(string? texto) => texto is null ? 0 : Numero().Count(texto);

    public static string?[] UmaPorEspaco(string? folga1, string? folga2, string? folga3)
    {
        if (Dois(folga1) is var (a, b) && string.IsNullOrWhiteSpace(folga3)) return [a, folga2, b];
        if (Dois(folga3) is var (c, d) && string.IsNullOrWhiteSpace(folga1)) return [c, folga2, d];
        return [folga1, folga2, folga3];
    }

    private static (string, string)? Dois(string? v)
    {
        var partes = (v ?? "").Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 2 ? (partes[0], partes[1]) : null;
    }
}
