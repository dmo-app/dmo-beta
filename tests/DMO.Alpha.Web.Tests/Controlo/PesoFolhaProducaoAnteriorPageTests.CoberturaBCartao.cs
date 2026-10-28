using System.Net;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // DMO-504 (Diogo, 27/10/2026): na «Produção anterior» o nome só fica no
    // título do cartão, o botão é «Abrir» e o início e o fim da produção
    // escolhida ficam no cartão do peso dela.
    [Fact]
    public async Task ProducaoAnterior_NomeSoNoTitulo_BotaoAbrir_EInicioEFimNoCartaoDoPeso()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-18", 181m, 12);
        SeedUser("4877", "Peso Operador", "provider-folha-cartao", "FolhaCartao14!");
        using var client = await LoginClientAsync("4877", "FolhaCartao14!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));

        var inicio = html.IndexOf("peso-producao-anterior\" aria-labelledby", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a página não tem o cartão da produção anterior");
        var cartao = html[inicio..html.IndexOf("</section>", inicio, StringComparison.Ordinal)];

        Assert.Contains("<h3 id=\"peso-producao-anterior-title\">Produção anterior</h3>", cartao, StringComparison.Ordinal);
        Assert.Contains("id=\"producaoAnterior\" name=\"anterior\" aria-label=\"Produção anterior\"", cartao, StringComparison.Ordinal);
        Assert.DoesNotContain("<label for=\"producaoAnterior\">", cartao, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Abrir o Peso desta produção\">Abrir</button>", cartao, StringComparison.Ordinal);
        Assert.DoesNotContain(">Abrir o Peso desta produção</button>", cartao, StringComparison.Ordinal);

        // O início e o fim estão dentro do cartão do peso da produção, não à parte.
        var peso = PosicaoNoHtml.Posicao(cartao, "id=\"pesoAnteriorSap\"");
        var periodo = PosicaoNoHtml.Posicao(cartao, "data-producao-anterior-periodo");
        Assert.True(periodo > peso, "o início e o fim vêm dentro do cartão do peso da produção");
        Assert.Contains("Início <span data-inicio>", cartao, StringComparison.Ordinal);
        Assert.Contains("· Fim <span data-fim>", cartao, StringComparison.Ordinal);
    }
}
