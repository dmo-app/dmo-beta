using System.Text.RegularExpressions;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// Gravar sem recarregar no Registo das Boquilhas (Diogo, 17/10/2026:
// «atualizar só o que muda»; as guias em atraso registam-se movimento atrás
// de movimento). A vista Registo é a raiz (data-dmo-troca); o cabeçalho, o
// formulário do movimento, os números e os últimos movimentos são zonas que
// existem sempre; a procura fica como o operador a deixou; o foco volta ao
// tipo do próximo movimento.
public partial class BoquilhasRegistoPageTests
{
    private static readonly string[] ZonasDoRegisto =
    [
        "<div id=\"bqRegistoAberto\" class=\"dmo-zona\" data-dmo-regiao>",
        "<div id=\"bqRegistoMovimento\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>",
        "<div id=\"bqRegistoNumeros\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>",
        "<div id=\"bqRegistoMovimentos\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>"
    ];

    [Fact]
    public async Task Registo_TrocaSoAsZonas_ComOFocoNoTipoDoProximoMovimento()
    {
        await SeedToolAsync("BQ-TROCA-1", "QT701", "2");
        var bq = await SeedProductionAsync("BQ-TROCA-1", "5447QT701", "PN-770001", "B7");
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 3);
        SeedUser("4301", "Registo Troca", "provider-registo-troca-1", "RegistoTroca1!");
        using var client = await LoginClientAsync("4301", "RegistoTroca1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("<section id=\"registo\" class=\"view active\" data-dmo-troca>", html);
        foreach (var zona in ZonasDoRegisto)
        {
            Assert.Contains(zona, html);
        }

        // A procura fica (data-dmo-fica); um clique numa boquilha encontrada,
        // ou numa máquina do painel lateral, troca só o registo.
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica>\\s*<details class=\"card bq-register-search\"", html);
        Assert.Contains("<div id=\"bqProcura\" class=\"bq-procura\" aria-live=\"polite\" data-dmo-parcial></div>", html);
        Assert.Contains("<div class=\"dmo-zona\" data-dmo-parcial><aside class=\"beta-light-rail beta-production-rail\" id=\"bqCurrentLines\"", html);
        Assert.Contains("/js/bq-registo.js", html);

        // O foco no tipo escolhido do próximo movimento; uma só vez na página.
        Assert.Contains("name=\"movement_type\" value=\"saida\" checked=\"checked\" autofocus=\"autofocus\" required", html);
        Assert.Single(Regex.Matches(html, " autofocus"));

        // As zonas por esta ordem, cada cartão na sua.
        var indices = ZonasDoRegisto.Select(zona => html.IndexOf(zona, StringComparison.Ordinal)).ToList();
        Assert.Equal(indices.OrderBy(i => i), indices);
        Assert.True(html.IndexOf("class=\"card register-open\"", StringComparison.Ordinal) > indices[0]);
        Assert.True(html.IndexOf("class=\"card bq-movement-card\"", StringComparison.Ordinal) > indices[1]);
        Assert.True(html.IndexOf("class=\"card bq-metrics-card\"", StringComparison.Ordinal) > indices[2]);
        Assert.True(html.IndexOf("class=\"card bq-latest-card\"", StringComparison.Ordinal) > indices[3]);

        // Sem registo aberto as zonas existem na mesma: escolher a primeira
        // boquilha também troca só o registo.
        var vazio = await GetPageHtmlAsync(client, PagePath);
        foreach (var zona in ZonasDoRegisto)
        {
            Assert.Contains(zona, vazio);
        }
        Assert.Contains("<div class=\"register-empty\">", vazio);
        Assert.DoesNotContain("autofocus", vazio);

        // O Histórico troca só as zonas do calendário (Diogo, 18/10: sem
        // flash); os Ficheiros trocam só a lista e a ficha (beta.178,
        // paginação sem recarregar).
        Assert.Contains("<section id=\"historico\" class=\"view active\" data-dmo-troca>", await GetPageHtmlAsync(client, $"{PagePath}?historico=1"));
        Assert.Contains("<section id=\"boquilhas\" class=\"view active\" data-dmo-troca>", await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1"));
    }

    [Fact]
    public async Task Registo_ComUmaJanelaAberta_OFocoVaiParaAJanela()
    {
        await SeedToolAsync("BQ-TROCA-2", "QT702", "1");
        var trace = await SeedTraceAsync("BQ-TROCA-2");
        await SeedMovementAsync(trace.Id, "saida", 2);
        var movimento = (await ReadMovementsAsync(trace.Id)).Single();
        SeedUser("4302", "Registo Troca", "provider-registo-troca-2", "RegistoTroca2!");
        using var client = await LoginClientAsync("4302", "RegistoTroca2!");

        // «Corrigir movimentos» aberto: o foco no «Fechar», não no formulário por baixo.
        var janela = await GetPageHtmlAsync(client, $"{PagePath}?trace_id={trace.Id}&editar_mov=1");
        Assert.Single(Regex.Matches(janela, " autofocus"));
        Assert.Contains($"class=\"btn bq-correct-close\" autofocus=\"autofocus\" href=\"/Boquilhas?trace_id={trace.Id}#bqLatest\">Fechar</a>", janela);

        // O movimento aberto: o foco no tipo dele.
        var aberto = await GetPageHtmlAsync(client, $"{PagePath}?trace_id={trace.Id}&editar_mov=1&mov_id={movimento.Id}");
        Assert.Single(Regex.Matches(aberto, " autofocus"));
        Assert.Matches("id=\"bqEditMovement\"[\\s\\S]*value=\"saida\" checked=\"checked\" autofocus=\"autofocus\"", aberto);

        // Os movimentos e os formulários que gravam estão nas zonas parciais.
        // As ações da correção (com o «Apagar») ficam no canto, antes do formulário.
        Assert.Matches("<div id=\"bqRegistoMovimentos\" class=\"dmo-zona\" data-dmo-regiao data-dmo-parcial>[\\s\\S]*handler=DeleteMovement[\\s\\S]*handler=EditMovement", aberto);
    }

    [Fact]
    public async Task Registo_AssociarJobOn_GravaSemRecarregar()
    {
        await SeedToolAsync("BQ-TROCA-3", "QT703", "4");
        var trace = await SeedTraceAsync("BQ-TROCA-3");
        await SeedProductionAsync("BQ-TROCA-3", "ZQREF-703", "PN-770003", "B8");
        SeedUser("4303", "Registo Troca", "provider-registo-troca-3", "RegistoTroca3!");
        using var client = await LoginClientAsync("4303", "RegistoTroca3!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=ZQREF-703");
        var form = Regex.Match(html, "<form[^>]*handler=AssociateJobOn[^>]*>");
        Assert.True(form.Success);
        Assert.Contains("data-dmo-parcial", form.Value);
        // O 1.º passo (a referência, com sugestões) também troca só o registo
        // (deep sweep, 18/10/2026; antes abria a página inteira): fora das
        // zonas a página é igual com e sem a referência procurada, e o campo
        // da referência mantém o id (o foco fica nele).
        var passo1 = Regex.Match(html, "<form method=\"get\"[^>]*>[\\s\\S]*?</form>");
        Assert.Contains("name=\"associar_ref\"", passo1.Value);
        Assert.Contains("data-dmo-parcial", passo1.Value);
        Assert.Contains("id=\"jobRef\"", passo1.Value);
        var semReferencia = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        var naoExiste = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=NAOEXISTE");
        var fora = Controlo.PesoZonas.ForaDasZonas(semReferencia);
        Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
        Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(naoExiste));
        Assert.NotEqual(Controlo.PesoZonas.Zona(semReferencia, "bqRegistoAberto"), Controlo.PesoZonas.Zona(html, "bqRegistoAberto"));
    }
}
