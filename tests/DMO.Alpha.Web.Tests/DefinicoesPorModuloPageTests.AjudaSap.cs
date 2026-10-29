using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A ajuda do SAP numa página própria (Diogo, 22/10/2026 06:57: «isso tem de
/// ficar numa página readme de ajuda; o que deve ficar nas opções são as
/// opções; nada de textos gigantes, isso é acedido através de um helpme»).
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda()
    {
        using var client = await LoginOperatorAsync("7121", DmoCapabilities.Ferramentas);

        var definicoes = await client.GetStringAsync("/Definicoes");
        var inicio = definicoes.IndexOf("id=\"ferrDefSap\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Falta o cartão Utilização pelo SAP.");
        var cartao = definicoes[inicio..definicoes.IndexOf("</section>", inicio, StringComparison.Ordinal)];
        Assert.Contains("id=\"ferrSapAjuda\" href=\"/Ferramentas/AjudaSap\"", cartao, StringComparison.Ordinal);
        Assert.DoesNotContain("dmo-codigos.txt com os códigos", cartao, StringComparison.Ordinal);

        var ajuda = await client.GetStringAsync("/Ferramentas/AjudaSap");
        var inicioAjuda = ajuda.IndexOf("id=\"ajudaSap\"", StringComparison.Ordinal);
        Assert.True(inicioAjuda >= 0, "Falta a página de ajuda do SAP.");
        var texto = ajuda[inicioAjuda..ajuda.IndexOf("</article>", inicioAjuda, StringComparison.Ordinal)];
        Assert.Contains("DMO_USO_SAP.vbs", texto, StringComparison.Ordinal);
        Assert.Contains("dmo-codigos.txt", texto, StringComparison.Ordinal);
        Assert.Contains("Se alguma coisa mudar no SAP", texto, StringComparison.Ordinal);

        // A1/SEC-01: a senha fica embebida nos scripts gerados (Services/SapScripts.cs);
        // a ajuda não pode dizer o contrário.
        Assert.DoesNotContain("Nenhuma palavra-passe fica nos ficheiros", texto, StringComparison.Ordinal);
        Assert.Contains("senha do SAP fica cifrada", texto, StringComparison.Ordinal);
    }

    // Uma só pasta do SAP, corrida às 18:00 e «Apagar lidos» (Diogo,
    // 22/10/2026 07:43: «uma pasta SAP e corremos os scripts sempre que
    // queremos ou final do dia auto. Depois uma opção para apagar»).
    [Fact]
    public async Task FerramentasDefinicoes_OCartaoDoSapTemApagarLidos_EAAjudaExplica()
    {
        using var client = await LoginOperatorAsync("7122", DmoCapabilities.Ferramentas);

        var definicoes = await client.GetStringAsync("/Definicoes");
        var inicio = definicoes.IndexOf("id=\"ferrDefSap\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Falta o cartão do SAP.");
        var cartao = definicoes[inicio..definicoes.IndexOf("</section>", inicio, StringComparison.Ordinal)];
        Assert.Contains("id=\"ferrSapApagar\"", cartao, StringComparison.Ordinal);
        Assert.Contains(">Apagar lidos</button>", cartao, StringComparison.Ordinal);

        var ajuda = await client.GetStringAsync("/Ferramentas/AjudaSap");
        var inicioAjuda = ajuda.IndexOf("id=\"ajudaSap\"", StringComparison.Ordinal);
        var texto = ajuda[inicioAjuda..ajuda.IndexOf("</article>", inicioAjuda, StringComparison.Ordinal)];
        Assert.Contains("INSTALAR.cmd", texto, StringComparison.Ordinal);
        Assert.Contains("«Apagar lidos»", texto, StringComparison.Ordinal);
    }
}
