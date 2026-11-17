using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    // «Punção visto» / «Arrefecedor visto» (Diogo, 27/10/2026): as linhas
    // mostram o estado em texto, sem caixa nem botão; um clique escolhe a
    // punção (rádio escondido do formulário puVistoForm) e os dois botões do
    // canto trocam o estado dela no mesmo handler Seen. Sem «visto» (sem
    // script), o servidor troca o que lá está; a linha continua escolhida.
    [Fact]
    public async Task Vistos_UmaLinhaEscolhe_EOsDoisBotoesDoCantoTrocam()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4318", "Operador Vistos", "provider-puncoes-vistos", "PuncoesVis1!");
        using var client = await LoginClientAsync("4318", "PuncoesVis1!");
        foreach (var numero in new[] { "41", "42" })
        {
            await PostAsync(client, controloId, "Add", ("numero", numero), ("referencia", "1075"), ("lote", "1"));
        }

        var producao = $"{PagePath}?controlo_id={controloId}";
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, producao));
        var cartao = Regex.Match(html, "<section class=\"dmo-card pu-card\" id=\"puncoes-lista\"[\\s\\S]*?</section>").Value;
        Assert.NotEmpty(cartao);

        // R16 (Diogo, 30/10/2026): sem Punção visto / Arrefecedor visto / Remover no canto.
        var canto = Regex.Match(cartao, "<div class=\"dmo-card-topo-acoes\" data-dmo-for=\"puLista\">[\\s\\S]*?<details").Value;
        Assert.DoesNotContain("<button", canto);

        // Nas linhas: carrega-se no «Visto» / «Por verificar» da tabela; × para remover.
        var corpo = Regex.Match(cartao, "<table class=\"dmo-table pu-table pu-table-fixa\" id=\"puLista\"[\\s\\S]*?</table>").Value;
        Assert.Equal(4, Regex.Matches(corpo, "<button type=\"submit\" class=\"pu-check on\" aria-pressed=\"true\"").Count);
        Assert.Equal(4, Regex.Matches(corpo, "action=\"/Controlo/Puncoes\\?handler=Seen\"").Count);
        Assert.Equal(2, Regex.Matches(corpo, "class=\"pu-remove\" data-dmo-dialog=\"pu-remover-\\d+\"").Count);
        Assert.Equal(6, Regex.Matches(corpo, "<col ").Count);

        int primeiro;
        using (var scope = _factory.Services.CreateScope())
        {
            primeiro = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PuncaoInspecoes.AsNoTracking()
                .OrderBy(i => i.Position).Select(i => i.Id).FirstAsync();
        }
        Assert.Matches($"<tr data-dmo-row data-id=\"{primeiro}\" data-pu-puncao=\"true\" data-pu-arrefecedor=\"true\"", corpo);

        // Sem «visto», o servidor troca: o arrefecedor da 41 passa a «Por
        // verificar» e a linha continua escolhida para o outro botão.
        var trocado = await PostAsync(client, controloId, "Seen", ("inspecao_id", primeiro.ToString()), ("campo", "arrefecedor"));
        Assert.Equal($"/Controlo/Puncoes?controlo_id={controloId}&inspecao={primeiro}#puncoes-lista", trocado.Headers.Location!.ToString());
        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, trocado.Headers.Location!.ToString().Split('#')[0]));
        var linha = Regex.Match(depois, $"<tr data-dmo-row data-id=\"{primeiro}\"[\\s\\S]*?</tr>").Value;
        Assert.Matches("data-pu-arrefecedor=\"false\"\\s+class=\"selected\" aria-selected=\"true\"", linha);
        Assert.Contains("<td class=\"pu-estado\" data-pu-estado=\"arrefecedor\">", linha);
        Assert.Contains("<input type=\"hidden\" name=\"visto\" value=\"true\" />", linha);
        Assert.Contains("<span>Arrefecedores vistos</span><strong>1</strong>", depois);

        // Outra vez, volta a «Visto»; com «visto» (o script) vale o que diz.
        await PostAsync(client, controloId, "Seen", ("inspecao_id", primeiro.ToString()), ("campo", "arrefecedor"));
        await PostAsync(client, controloId, "Seen", ("inspecao_id", primeiro.ToString()), ("campo", "puncao"), ("visto", "false"));
        await PostAsync(client, controloId, "Seen", ("inspecao_id", primeiro.ToString()), ("campo", "puncao"), ("visto", "false"));
        using (var scope = _factory.Services.CreateScope())
        {
            var inspecao = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PuncaoInspecoes.AsNoTracking().SingleAsync(i => i.Id == primeiro);
            Assert.True(inspecao.CoolerSeen);
            Assert.False(inspecao.PunchSeen);
        }
    }

    // Com uma só punção, a linha já vem escolhida e os botões do canto prontos.
    [Fact]
    public async Task Vistos_UmaSoPuncao_VemEscolhida()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4319", "Operador Uma", "provider-puncoes-uma", "PuncoesUma1!");
        using var client = await LoginClientAsync("4319", "PuncoesUma1!");
        await PostAsync(client, controloId, "Add", ("numero", "41"), ("referencia", "1075"), ("lote", "1"));

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        var corpo = Regex.Match(html, "id=\"puLista\"[\\s\\S]*?</tbody>").Value;
        Assert.Matches("<tr data-dmo-row[^>]*class=\"selected\" aria-selected=\"true\"", corpo);
        Assert.Matches("<input type=\"radio\" name=\"inspecao_id\" value=\"\\d+\" form=\"puVistoForm\" required checked=\"checked\"", corpo);
    }
}
