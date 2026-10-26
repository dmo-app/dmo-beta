using System.Net;
using System.Text.RegularExpressions;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

// Gravar sem recarregar na ficha da ferramenta (Diogo, 17/10/2026:
// «atualizar só o que muda»; pedido para a ficha: guardar os dados gerais e
// técnicos e atualizar a secção editada, com confirmação). O <main> é a raiz
// (data-dmo-troca); as mensagens, o Geral, o Editar e a Informação técnica
// são zonas; a Informação técnica fica aberta ou fechada como o operador a
// deixou (data-dmo-fica) e só o título e o formulário dela trocam. Duplicar
// e eliminar (janelas) ficam fora das zonas e abrem outra página.
public partial class FerramentasPagesTests
{
    private static readonly string[] ZonasDaFicha =
    [
        "<div id=\"ferrAvisos\" class=\"dmo-zona\" data-dmo-regiao>",
        "<section id=\"ferrGeral\" class=\"dmo-card ferr-card\" aria-labelledby=\"ferr-facts-title\" data-dmo-regiao>",
        "<section id=\"ferrEditar\" class=\"dmo-card ferr-card\" aria-labelledby=\"ferr-edit-facts-title\" data-dmo-regiao data-dmo-parcial>",
        "<section class=\"dmo-card ferr-card dmo-card-dobravel\" aria-labelledby=\"ferr-tech-title\" data-dmo-fica data-dmo-parcial>",
        "<summary id=\"ferrTecnicaTitulo\" class=\"dmo-card-topo\" data-dmo-regiao>",
        "<div id=\"ferrTecnica\" data-dmo-regiao>"
    ];

    [Fact]
    public async Task Ficha_TrocaSoAsZonas_ATecnicaFicaComoOOperadorADeixou()
    {
        SeedUser("4431", "Ferramentas Troca", "provider-ferr-troca-1", "FerrTroca1!");
        using var client = await LoginClientAsync("4431", "FerrTroca1!");
        var toolId = await SeedRealToolAsync();

        var html = (await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}")).Replace("\r\n", "\n");
        Assert.Contains("<main class=\"ferr-page dmo-workspace\" aria-labelledby=\"ferr-title\" data-dmo-troca>", html);
        foreach (var zona in ZonasDaFicha)
        {
            Assert.Contains(zona, html);
        }

        // As zonas por esta ordem; o <details> da técnica fica dentro do
        // bloco que fica (data-dmo-fica), à volta do título e do formulário.
        var indices = ZonasDaFicha.Select(zona => html.IndexOf(zona, StringComparison.Ordinal)).ToList();
        Assert.Equal(indices.OrderBy(i => i), indices);
        Assert.Contains("data-dmo-fica data-dmo-parcial>\n      <details>\n      <summary id=\"ferrTecnicaTitulo\"", html);
        Assert.True(html.IndexOf("handler=EditFacts", StringComparison.Ordinal) > indices[2]);
        Assert.True(html.IndexOf("handler=EditTechnical", StringComparison.Ordinal) > indices[5]);

        // Duplicar e eliminar: janelas antes das zonas da ficha (abrem outra
        // ficha ou a lista, como sempre). Os formulários delas gravam pela
        // fila da página (deep sweep, 18/10/2026: ver
        // FerramentasPagesTests.CliquesRepetidos). Os botões que as abrem
        // («Opções») estão no canto do Geral (Diogo, 18/10/2026), uma zona sem
        // data-dmo-parcial; ligam-se por delegação.
        var primeiraZona = indices[0];
        Assert.True(Posicao(html, "<dialog id=\"ferr-duplicate-dialog\"") < primeiraZona);
        Assert.True(Posicao(html, "<dialog id=\"ferr-delete-dialog\"") < primeiraZona);
        Assert.True(html.IndexOf("data-dmo-dialog=\"", StringComparison.Ordinal) > indices[1]);
        Assert.True(html.LastIndexOf("data-dmo-dialog=\"", StringComparison.Ordinal) < indices[2]);
        Assert.DoesNotContain("data-dmo-parcial", html[indices[1]..indices[2]]);

        // Depois de guardar o foco volta ao «Guardar» da secção (o mesmo id);
        // sem nada por ler, nenhum campo pede o foco.
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"ferr-edit-guardar\">Guardar</button>", html);
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"ferr-tech-guardar\" form=\"ferr-tech-form\">Guardar</button>", html);
        Assert.DoesNotContain(" autofocus=", html);
        Assert.DoesNotContain(" aria-invalid=", html);
    }

    // Os botões de cada card no canto superior direito, na linha do título
    // (Diogo, 18/10/2026: «metem os botões em baixo aumenta espaço, sempre
    // canto superior direito dentro dos cards»). O «Opções» e o «Voltar à
    // lista» saíram do cabeçalho da página para o canto do Geral; cada
    // «Guardar» fica no cabeçalho do seu card, antes dos campos, e é o botão
    // do mesmo formulário (o da técnica pelo atributo form, no <summary>).
    [Fact]
    public async Task Ficha_BotoesDeCadaCard_NoCantoSuperiorDireito()
    {
        SeedUser("4435", "Ferramentas Troca", "provider-ferr-troca-5", "FerrTroca5!");
        using var client = await LoginClientAsync("4435", "FerrTroca5!");
        var toolId = await SeedRealToolAsync();
        var html = (await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&tecnica=1")).Replace("\r\n", "\n");

        // O cabeçalho da página só tem o título; nenhum bloco de botões em baixo.
        Assert.Matches("<div class=\"page-head\">\\s*<div>\\s*<h2 id=\"ferr-title\">Ficha de ferramenta</h2>\\s*</div>\\s*</div>", html);
        Assert.DoesNotContain("ferr-head-acoes", html);
        Assert.DoesNotContain("class=\"ferr-actions\"", html);
        Assert.DoesNotContain("ferr-tech-actions", html);

        // Geral: título e, à direita, «Opções» e o único «Voltar», antes dos factos.
        Assert.Matches("<section id=\"ferrGeral\"[^>]*>\\s*<div class=\"dmo-card-topo\">\\s*<h3 id=\"ferr-facts-title\">Geral — [^<]*</h3>\\s*<div class=\"dmo-card-topo-acoes\">", html);
        var geral = html.IndexOf("<section id=\"ferrGeral\"", StringComparison.Ordinal);
        var opcoes = html.IndexOf("aria-label=\"Opções da ferramenta", StringComparison.Ordinal);
        var voltar = html.IndexOf("id=\"ferrVoltar\"", StringComparison.Ordinal);
        var factos = html.IndexOf("<div class=\"dmo-facts\">", StringComparison.Ordinal);
        Assert.True(geral < opcoes && opcoes < voltar && voltar < factos);
        Assert.Single(Regex.Matches(html, ">Voltar</a>"));
        Assert.DoesNotContain(">Voltar à lista</a>", html);

        // Editar: o título e o «Guardar» dentro do formulário, antes dos campos.
        Assert.Matches("handler=EditFacts\"[^>]*>\\s*<div class=\"dmo-card-topo\">\\s*<h3 id=\"ferr-edit-facts-title\">Editar</h3>\\s*<div class=\"dmo-card-topo-acoes\">\\s*<button type=\"submit\" class=\"dmo-button\" id=\"ferr-edit-guardar\">Guardar</button>", html);
        Assert.True(html.IndexOf("id=\"ferr-edit-guardar\"", StringComparison.Ordinal) < html.IndexOf("id=\"ferr-edit-type\"", StringComparison.Ordinal));

        // Informação técnica: o <summary> é a linha do título (sem nada
        // interativo lá dentro); o «Guardar» vem logo a seguir ao <details>,
        // dentro do card (.dmo-card-dobravel põe-no sobre a linha do título),
        // e grava o formulário da técnica pelo atributo form.
        Assert.Matches("<summary id=\"ferrTecnicaTitulo\" class=\"dmo-card-topo\" data-dmo-regiao><h3 id=\"ferr-tech-title\"><span class=\"dmo-caret\" aria-hidden=\"true\"></span>Informação técnica[\\s\\S]*?</h3></summary>", html);
        Assert.Matches("</details>\\s*<div class=\"dmo-card-topo-acoes\">\\s*<button type=\"submit\" class=\"dmo-button\" id=\"ferr-tech-guardar\" form=\"ferr-tech-form\">Guardar</button>\\s*</div>\\s*</section>", html);
        Assert.Contains("<form method=\"post\" id=\"ferr-tech-form\" action=\"/Ferramentas/Ficha?handler=EditTechnical\">", html);
        Assert.Single(Regex.Matches(html, "id=\"ferr-tech-form\""));
    }

    [Fact]
    public async Task Ficha_Guardar_ConfirmaNoCantoASecaoGuardada()
    {
        SeedUser("4432", "Ferramentas Troca", "provider-ferr-troca-2", "FerrTroca2!");
        using var client = await LoginClientAsync("4432", "FerrTroca2!");
        var toolId = await SeedRealToolAsync();
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");

        using var facts = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"), ("mf_reference", "5811"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"{FichaPath}?tool_id={toolId}&guardado=geral", facts.Headers.Location?.OriginalString);
        var geral = await GetPageHtmlAsync(client, facts.Headers.Location!.OriginalString);
        Assert.Matches("<div id=\"ferrAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div data-dmo-toast=\"ok\" class=\"ferr-nota\" role=\"status\">Dados gerais guardados.</div>", geral);
        Assert.DoesNotContain("Informação técnica guardada.", geral);

        using var technical = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("process_token", "NNPB"), ("diametro_gargalo", "70,1"), ("__RequestVerificationToken", token)));
        Assert.Equal($"{FichaPath}?tool_id={toolId}&tecnica=1&guardado=tecnica", technical.Headers.Location?.OriginalString);
        var tecnica = await GetPageHtmlAsync(client, technical.Headers.Location!.OriginalString);
        Assert.Contains("<div data-dmo-toast=\"ok\" class=\"ferr-nota\" role=\"status\">Informação técnica guardada.</div>", tecnica);
        Assert.DoesNotContain("Dados gerais guardados.", tecnica);

        // Um valor desconhecido não mostra nada.
        var outro = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&guardado=outra");
        Assert.DoesNotContain("Dados gerais guardados.", outro);
        Assert.DoesNotContain("Informação técnica guardada.", outro);
    }

    [Fact]
    public async Task Ficha_ValorTecnicoPorLer_FicaNoCampoParaCorrigir_ComOFoco()
    {
        SeedUser("4433", "Ferramentas Troca", "provider-ferr-troca-3", "FerrTroca3!");
        using var client = await LoginClientAsync("4433", "FerrTroca3!");
        var toolId = await SeedRealToolAsync();
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");

        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("process_token", "NNPB"), ("nominal_weight", "1.234,5"), ("volume_marisa", "12..5"),
            ("diametro_gargalo", "70,10"), ("__RequestVerificationToken", token)));
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("guardado=tecnica", location);
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));

        // O que se escreveu fica no campo, marcado; o foco vai para o primeiro
        // (pela ordem da ficha). O legível gravou e aparece como gravado.
        Assert.Contains("id=\"ferr-tech-nominal\" name=\"nominal_weight\" type=\"text\" inputmode=\"decimal\" value=\"1.234,5\" aria-invalid=\"true\" autofocus=\"autofocus\"", html);
        Assert.Contains("id=\"ferr-tech-gargalo\" name=\"diametro_gargalo\" type=\"text\" inputmode=\"decimal\" value=\"70.10\" />", html);
        Assert.Single(Regex.Matches(html, " autofocus"));
        Assert.Contains("Isso não foi gravado (fica o valor anterior)", html);
        Assert.Contains("Corrija no campo e guarde de novo", html);
    }

    [Fact]
    public async Task Ficha_AbertaDeUmFluxo_GuardarOEditarMantemORegresso()
    {
        SeedUser("4434", "Ferramentas Troca", "provider-ferr-troca-4", "FerrTroca4!");
        using var client = await LoginClientAsync("4434", "FerrTroca4!");
        var toolId = await SeedRealToolAsync();
        const string volta = "/Controlo/Pegamentos?controlo_id=7";
        var ficha = $"{FichaPath}?tool_id={toolId}&tecnica=1&continuar={Uri.EscapeDataString(volta)}";
        var fichaHtml = await GetPageHtmlAsync(client, ficha);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(fichaHtml)!;

        // Os dois formulários levam o regresso; o do Editar devolve-o à ficha.
        Assert.Equal(2, Regex.Matches(fichaHtml, "name=\"continuar\" value=\"/Controlo/Pegamentos\\?controlo_id=7\"").Count);
        using var facts = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"), ("continuar", volta),
            ("__RequestVerificationToken", token)));
        var location = facts.Headers.Location!.OriginalString;
        Assert.Equal($"{FichaPath}?tool_id={toolId}&continuar={Uri.EscapeDataString(volta)}&guardado=geral", location);
        Assert.Contains("Continuar no fluxo que abriu o Ferramentas", await GetPageHtmlAsync(client, location));

        // A recusa também o mantém; um regresso que não é local é ignorado.
        using var refused = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"), ("bq_quantity", "-5"),
            ("continuar", volta), ("__RequestVerificationToken", token)));
        Assert.Contains("continuar=", refused.Headers.Location!.OriginalString);
        using var external = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"), ("continuar", "https://example.com/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"{FichaPath}?tool_id={toolId}&guardado=geral", external.Headers.Location!.OriginalString);
    }
}
