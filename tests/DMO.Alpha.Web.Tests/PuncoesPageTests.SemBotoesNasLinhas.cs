using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests;

public partial class PuncoesPageTests
{
    // Diogo, 27/10/2026: «não quero botões em rows por todo o lado». Os
    // registos sem Job On da referência são linhas que se escolhem (o rádio
    // escondido, required); o único botão é o «Associar» do canto, ligado ao
    // formulário pelo form="…". Sem escolha, o servidor não associa nada.
    [Fact]
    public async Task RegistosSemJobOn_UmaLinhaEscolhe_EUmSoBotaoNoCantoAssocia()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4316", "Operador Linhas", "provider-puncoes-linhas", "PuncoesLin1!");
        using var client = await LoginClientAsync("4316", "PuncoesLin1!");

        var primeiro = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "1"));
        var primeiroId = primeiro.Headers.Location!.ToString().Split('#')[0].Split("puncoes_id=")[1];
        var segundo = await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "2"));
        Assert.Equal(HttpStatusCode.Redirect, segundo.StatusCode);

        var producao = $"{PagePath}?controlo_id={controloId}";
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, producao));
        var cartao = Regex.Match(html, "<section class=\"dmo-card pu-card\" id=\"puncoes-sem-jobon\"[\\s\\S]*?</section>").Value;
        Assert.NotEmpty(cartao);

        // Um só botão, no canto, ligado ao formulário; nenhum nas linhas.
        var botoes = Regex.Matches(cartao, "<button[^>]*>");
        Assert.Single(botoes);
        Assert.Contains("form=\"puAssociarForm\"", botoes[0].Value);
        Assert.Contains("<div class=\"dmo-card-topo-acoes\" data-dmo-for=\"puSemJobOnLista\">", cartao);
        var corpo = Regex.Match(cartao, "<tbody>[\\s\\S]*?</tbody>").Value;
        Assert.DoesNotContain("<button", corpo);
        Assert.DoesNotContain("dmo-button", corpo);

        // As duas linhas escolhem-se; com duas, nenhuma vem escolhida.
        Assert.Equal(2, Regex.Matches(corpo, "<tr data-dmo-row").Count);
        var radios = Regex.Matches(corpo, "<input type=\"radio\" name=\"puncoes_id\"[^>]*>");
        Assert.Equal(2, radios.Count);
        Assert.All(radios, r => Assert.Contains("required", r.Value));
        Assert.All(radios, r => Assert.DoesNotContain("checked", r.Value));
        Assert.Contains($"data-dmo-open=\"/Controlo/Puncoes?puncoes_id={primeiroId}\"", corpo);

        // O formulário leva o mesmo que o botão de cada linha levava.
        var form = Regex.Match(cartao, "<form method=\"post\" id=\"puAssociarForm\" action=\"/Controlo/Puncoes\\?handler=Associate\">").Value;
        Assert.NotEmpty(form);
        Assert.Contains($"<input type=\"hidden\" name=\"controlo_id\" value=\"{controloId}\" />", cartao);
        Assert.Contains("<input type=\"hidden\" name=\"origem\" value=\"controlo\" />", cartao);

        // Sem linha escolhida, nada se associa.
        var semEscolha = await PostFormAsync(client, producao, "Associate", ("controlo_id", controloId.ToString()), ("origem", "controlo"));
        Assert.Contains("recusa=", semEscolha.Headers.Location!.ToString());

        // Com a linha escolhida, associa essa.
        var associado = await PostFormAsync(client, producao, "Associate", ("controlo_id", controloId.ToString()), ("origem", "controlo"), ("puncoes_id", primeiroId));
        Assert.Equal($"/Controlo/Puncoes?controlo_id={controloId}#puncoes-lista", associado.Headers.Location!.ToString());
        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, producao));
        Assert.DoesNotContain("id=\"puncoes-sem-jobon\"", depois);
    }

    // Com um só registo sem Job On, a linha já vem escolhida: o «Associar» do
    // canto associa-o logo.
    [Fact]
    public async Task UmSoRegistoSemJobOn_VemEscolhido()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4317", "Operador Um", "provider-puncoes-um", "PuncoesUm1!");
        using var client = await LoginClientAsync("4317", "PuncoesUm1!");
        await PostFormAsync(client, $"{PagePath}?procura=1075C142", "Create", ("referencia_total", "1075C142"), ("referencia", "1075"), ("lote", "1"));

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        var corpo = Regex.Match(html, "id=\"puSemJobOnLista\"[\\s\\S]*?</tbody>").Value;
        Assert.Matches("<tr data-dmo-row[^>]*class=\"selected\" aria-selected=\"true\"", corpo);
        Assert.Matches("<input type=\"radio\" name=\"puncoes_id\" value=\"\\d+\" required checked=\"checked\"", corpo);
    }
}
