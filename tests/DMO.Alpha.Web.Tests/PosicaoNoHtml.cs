namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Onde começa uma marca no HTML, para dizer que uma parte da página vem
/// antes de outra. Falha quando a marca não aparece: com o IndexOf direto, o
/// -1 de uma marca que saiu da página fazia passar «a vem antes de b» sem «a»
/// na página (estudo dos testes, Fase 1, 19/10/2026).
/// </summary>
internal static class PosicaoNoHtml
{
    public static int Posicao(string html, string marca)
    {
        var indice = html.IndexOf(marca, StringComparison.Ordinal);
        Assert.True(indice >= 0, $"falta «{marca}»");
        return indice;
    }
}
