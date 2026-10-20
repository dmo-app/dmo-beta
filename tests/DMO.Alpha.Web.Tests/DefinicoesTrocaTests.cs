using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

// E10 de «Atualizar só o que muda» (Diogo, 17/10/2026): as Definições dos
// módulos e do Admin gravam sem recarregar. Cada página tem a raiz
// (data-dmo-troca) e as zonas que a gravação muda (data-dmo-regiao, com
// data-dmo-parcial à volta dos formulários); as mensagens vão para a pilha do
// canto (data-dmo-toast) em vez de empurrarem os cartões; os botões e os
// campos têm id, para o foco ficar onde estava depois da troca. O que fica fora
// das zonas não muda com a gravação (a pasta dos PDFs do Controlo, o título).
public sealed partial class DefinicoesPorModuloPageTests
{
    // Regra do Diogo (18/10/2026): «sempre canto superior direito dentro dos
    // cards». O botão do formulário vai para o cabeçalho do cartão
    // (.dmo-card-topo, título à esquerda, .dmo-card-topo-acoes à direita),
    // ligado ao formulário por form="id"; não fica nada no fundo do cartão.
    private static void BotaoNoTopo(string html, string cartao, string botao, string formulario)
    {
        var inicio = Posicao(html, cartao);
        var topo = html.IndexOf("<div class=\"dmo-card-topo\">", inicio, StringComparison.Ordinal);
        var acoes = html.IndexOf("<div class=\"dmo-card-topo-acoes\">", topo, StringComparison.Ordinal);
        var botaoEm = html.IndexOf(botao, acoes, StringComparison.Ordinal);
        var form = html.IndexOf($"<form id=\"{formulario}\" method=\"post\"", botaoEm, StringComparison.Ordinal);
        Assert.True(topo > inicio && acoes > topo && botaoEm > acoes && form > botaoEm,
            $"o botão «{botao}» não está no cabeçalho do cartão, antes do formulário {formulario}");
        Assert.Contains($"form=\"{formulario}\"", botao);
        Assert.Equal(1, Regex.Matches(html, Regex.Escape(botao)).Count);
    }

    [Fact]
    public void E10_CabecalhoDeCartaoComAcoes_NoDesignSystem()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web", "wwwroot", "0_ASSET_CANONICAL_DESIGN_SYSTEM.css"));
        Assert.Contains("/* Cabeçalho de card com ações no canto superior direito (regra do Diogo, 18/10) */", css);
        // As regras exatas são testadas em SharedComponentsTests (vieram com a ficha
        // das Ferramentas); aqui basta que as classes usadas no Admin existam.
        Assert.Contains(".dmo-card-topo{", css);
        Assert.Contains(".dmo-card-topo-acoes{", css);
    }

    private static void SemAvisosNoFluxo(string html)
    {
        Assert.DoesNotContain("<strong role=\"alert\">", html);
        Assert.DoesNotContain("ferr-def-aviso", html);
        Assert.DoesNotContain("data-dmo-valor-aviso", html);
    }

    [Fact]
    public async Task E10_ControloDefinicoes_ToleranciaNumaZona_APastaDosPdfsFica()
    {
        using var client = await LoginOperatorAsync("5110", DmoCapabilities.ControloCreate);
        var html = await client.GetStringAsync("/Controlo/Definicoes?save=ok-tolerancia");

        var raiz = Posicao(html, "<main class=\"ctl-def-page dmo-workspace\" aria-labelledby=\"ctl-def-title\" data-dmo-troca>");
        var pasta = Posicao(html, "data-dmo-pasta-pdf>");
        var zona = Posicao(html, "<section id=\"ctlDefTolerancia\" class=\"dmo-card ctl-def-card\" aria-labelledby=\"pegamentos-settings-title\" data-dmo-regiao data-dmo-parcial>");
        Assert.True(raiz < pasta && pasta < zona, "a pasta dos PDFs fica fora da zona, antes dela");
        Assert.True(Posicao(html, "handler=SavePegamentosTolerance") > zona);
        Assert.True(Posicao(html, "<p data-dmo-toast=\"ok\" role=\"status\">Tolerância guardada. Vale para") > zona);
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"ctlDefGuardar\">Guardar</button>", html);
        SemAvisosNoFluxo(html);

        var recusa = await client.GetStringAsync("/Controlo/Definicoes?save=tolerancia-invalida");
        Assert.Contains("<p data-dmo-toast=\"erro\" role=\"alert\">A tolerância é um número", recusa);

        var aviso = await client.GetStringAsync("/Controlo/Definicoes?save=aviso-tolerancia-fora");
        Assert.True(Posicao(aviso, "<p data-dmo-toast=\"aviso\" role=\"status\">Tolerância guardada: ± ") > Posicao(aviso, "<section id=\"ctlDefTolerancia\""));
        SemAvisosNoFluxo(aviso);
    }

    [Fact]
    public async Task E10_FerramentasDefinicoes_QuadroNumaZona_OAvisoVaiParaOCanto()
    {
        var added = await SeedMachinesAsync("B1");
        var b1 = ToolLotQuantityKeys.For(ToolType.Bq, "B1");
        try
        {
            using var client = await LoginOperatorAsync("5111", DmoCapabilities.Ferramentas);
            var html = await client.GetStringAsync(FerramentasPath);
            Posicao(html, "<main class=\"ferr-def-page dmo-workspace\" aria-labelledby=\"ferr-def-title\" data-dmo-troca>");
            var zona = Posicao(html, "<section id=\"ferrDefTotais\" class=\"dmo-card ferr-def-card\" aria-labelledby=\"tool-quantities-title\" data-dmo-regiao data-dmo-parcial>");
            Assert.True(Posicao(html, "handler=SaveToolLotQuantities") > zona);
            Assert.Contains($"<input id=\"ferr-total-BQ-B1\" name=\"quantities[{b1}]\"", html);
            Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"ferrDefGuardar\" form=\"ferrDefTotaisForm\">Guardar</button>", html);

            // Gravar com uma quantidade má desenha a página com o aviso: na
            // pilha do canto (aviso, fica até se fechar), não no quadro.
            var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
            using var partial = await client.PostAsync($"{FerramentasPath}?handler=SaveToolLotQuantities",
                HtmlFormHelpers.BuildFormPost(($"quantities[{b1}]", "x"), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
            var warned = WebUtility.HtmlDecode(await partial.Content.ReadAsStringAsync());
            Assert.Contains("<p data-dmo-toast=\"aviso\" role=\"status\">Os outros totais foram guardados, exceto: BQ na B1", warned);
            SemAvisosNoFluxo(warned);
        }
        finally
        {
            await RemoveAsync(added, b1);
        }
    }

    [Fact]
    public async Task E10_BoquilhasDefinicoes_ReparadoresERotasEmZonas_UmaMensagemNoCanto()
    {
        using var client = await LoginOperatorAsync("5112", DmoCapabilities.Boquilhas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await client.GetStringAsync("/Boquilhas/Definicoes"))!;
        using var created = await client.PostAsync("/Boquilhas/Definicoes?handler=CreateRepairer",
            HtmlFormHelpers.BuildFormPost(("name", "Reparador E10"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var html = await client.GetStringAsync(created.Headers.Location!.ToString());

        Posicao(html, "<main class=\"bq-def-page dmo-workspace\" aria-labelledby=\"bq-def-title\" data-dmo-troca>");
        var avisos = Posicao(html, "<div id=\"bqDefAvisos\" class=\"dmo-zona\" data-dmo-regiao>");
        var reparadores = Posicao(html, "<section id=\"bqDefReparadores\" class=\"dmo-card bq-def-card\" aria-labelledby=\"boquilhas-repairers-title\" data-dmo-regiao data-dmo-parcial>");
        var rotas = Posicao(html, "<section id=\"bqDefRotas\" class=\"dmo-card bq-def-card\" aria-labelledby=\"boquilhas-routing-title\" data-dmo-regiao data-dmo-parcial>");
        Assert.True(avisos < reparadores && reparadores < rotas);

        // Uma mensagem só, no canto; o reparador novo na lista (valor
        // editável no sítio, com id) e nos dropdowns das rotas.
        Assert.Single(Regex.Matches(html, "data-dmo-toast="));
        Assert.True(Posicao(html, "<p data-dmo-toast=\"ok\" role=\"status\">Definição gravada.</p>") < reparadores);
        Assert.Matches("<input id=\"bq-reparador-\\d+\" class=\"dmo-valor\" data-dmo-valor name=\"name\" value=\"Reparador E10\"", html);
        Assert.True(Posicao(html, "id=\"machine-route-B1\"") > rotas);
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"bqDefCriar\">Criar reparador</button>", html);
        SemAvisosNoFluxo(html);

        var recusa = await client.GetStringAsync("/Boquilhas/Definicoes?save=reparador-inexistente");
        Assert.Single(Regex.Matches(recusa, "data-dmo-toast="));
        Assert.Contains("<p data-dmo-toast=\"erro\" role=\"alert\">O reparador indicado já não existe.", recusa);
    }

    [Fact]
    public async Task E10_SetupECanalJobon_CartaoNumaZona_MensagemNoCanto()
    {
        using var admin = await LoginAdminAsync();

        var setup = await admin.GetStringAsync("/Admin/Definicoes?save=ok-admin");
        Posicao(setup, "<main class=\"adm-page\" aria-labelledby=\"settings-title\" data-dmo-troca>");
        var zona = Posicao(setup, "<section id=\"admEmail\" class=\"dmo-card adm-card\" aria-labelledby=\"admin-email-title\" data-dmo-regiao data-dmo-parcial>");
        Assert.True(Posicao(setup, "<p data-dmo-toast=\"ok\" role=\"status\">Email do ADMIN guardado.</p>") > zona);
        Assert.True(Posicao(setup, "handler=SaveAdminEmail") > zona);
        BotaoNoTopo(setup, "<section id=\"admEmail\"",
            "<button type=\"submit\" class=\"dmo-button\" id=\"admEmailGuardar\" form=\"admEmailForm\">Guardar</button>", "admEmailForm");
        SemAvisosNoFluxo(setup);
        Assert.Contains("<p data-dmo-toast=\"erro\" role=\"alert\">Os dois emails não coincidem.",
            await admin.GetStringAsync("/Admin/Definicoes?save=admin-email-diferente"));

        var canal = await admin.GetStringAsync($"{CanalJobonPath}?save=ok");
        Posicao(canal, "<main class=\"adm-page\" aria-labelledby=\"canal-jobon-title\" data-dmo-troca>");
        var cartao = Posicao(canal, "<section id=\"canalJobonViragem\" class=\"dmo-card adm-card\" aria-labelledby=\"boquilhas-settings-title\" data-dmo-regiao data-dmo-parcial>");
        Assert.True(Posicao(canal, "<p data-dmo-toast=\"ok\" role=\"status\">Hora de viragem gravada.</p>") > cartao);
        Assert.True(Posicao(canal, "<select id=\"dayChangeHour\" name=\"dayChangeHour\" data-dmo-autosubmit>") > cartao);
        SemAvisosNoFluxo(canal);
        Assert.Contains("<p data-dmo-toast=\"erro\" role=\"alert\">A hora de viragem é um inteiro entre 0 e 23.</p>",
            await admin.GetStringAsync($"{CanalJobonPath}?save=hora-invalida"));
    }

    [Fact]
    public async Task E10_Utilizadores_CriarTrocaAsZonas_OPinFicaNoCanto()
    {
        using var admin = await LoginAdminAsync();
        var html = await admin.GetStringAsync("/Admin/Users");
        var avisos = Posicao(html, "<div id=\"dmuAvisos\" class=\"dmo-zona\" data-dmo-regiao>");
        var criar = Posicao(html, "<section id=\"dmuCriar\" class=\"dmo-card dmu-card\" aria-labelledby=\"dmu-create-title\" data-dmo-regiao data-dmo-parcial>");
        var lista = Posicao(html, "id=\"dmu-resultados\" data-dmo-regiao");
        Assert.True(avisos < criar && criar < lista);
        Assert.True(Posicao(html, "handler=Create") > criar);
        BotaoNoTopo(html, "<section id=\"dmuCriar\"",
            "<button type=\"submit\" class=\"dmo-button\" id=\"dmuCriarBotao\" form=\"dmuCriarForm\">Criar utilizador</button>", "dmuCriarForm");

        using var created = await admin.PostAsync("/Admin/Users?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("name", "Utilizador E10"), ("operator_id", "5113"), ("email", "e10.5113@example.test"), ("template_key", ""),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var after = await admin.GetStringAsync(created.Headers.Location!.ToString());

        // O email e o PIN vão para a pilha do canto (aviso: ficam até se
        // fecharem, também com a criação seguinte, data-dmo-toast-fica; deep
        // sweep, 18/10/2026, B-admin-1), dentro da zona das mensagens; o
        // utilizador novo na lista.
        var entrega = Posicao(after, "<section class=\"dmu-entrega\" data-dmo-toast=\"aviso\" data-dmo-toast-fica role=\"status\" aria-label=\"Utilizador criado\">");
        Assert.True(entrega > Posicao(after, "<div id=\"dmuAvisos\"") && entrega < Posicao(after, "<section id=\"dmuCriar\""));
        Assert.Contains("e10.5113@example.test", after);
        Assert.Matches("<td>5113</td>", after);
    }

    [Fact]
    public async Task E10_Templates_ListaEDetalheEmZonas_OFocoDepoisDeCriarEEliminar()
    {
        using var admin = await LoginAdminAsync();
        var html = await admin.GetStringAsync("/Admin/Templates");
        Posicao(html, "<main class=\"dmt-page\" aria-labelledby=\"dmt-title\" data-dmo-troca>");
        var avisos = Posicao(html, "<div id=\"dmtAvisos\" class=\"dmo-zona\" data-dmo-regiao>");
        var lista = Posicao(html, "<aside id=\"dmtLista\" class=\"dmo-card dmt-list\" aria-label=\"Templates registadas\" data-dmo-regiao data-dmo-parcial>");
        var detalhe = Posicao(html, "<section id=\"dmtDetalhe\" class=\"dmo-card dmt-detail\" aria-label=\"Criar ou editar template\" data-dmo-regiao data-dmo-parcial>");
        Assert.True(avisos < lista && lista < detalhe);
        BotaoNoTopo(html, "<section id=\"dmtDetalhe\"",
            "<button type=\"submit\" class=\"dmo-button\" id=\"dmtCriar\" form=\"dmtCriarForm\">Criar template</button>", "dmtCriarForm");
        Assert.DoesNotContain("dmt-actions", html);
        Assert.DoesNotContain(" autofocus", html);

        using var created = await admin.PostAsync("/Admin/Templates?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("name", "Template E10"), ("capabilities", DmoCapabilities.Boquilhas),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        var key = Regex.Match(location, "template=([^&]+)").Groups[1].Value;
        var after = await admin.GetStringAsync(location);

        // A template criada fica aberta: o foco vai para o nome dela; a
        // escolha na lista tem id (o foco fica nela ao abrir outra).
        Assert.Contains($"<a id=\"dmt-escolha-{key}\" class=\"dmt-choice-link\"", after);
        Assert.Contains($"<a id=\"dmt-contagem-{key}\" class=\"dmo-pill dmt-users-link\"", after);
        Assert.Contains("id=\"dmt-edit-name\" name=\"name\" value=\"Template E10\" required maxlength=\"100\" autofocus=\"autofocus\"", after);
        BotaoNoTopo(after, "<section id=\"dmtDetalhe\"",
            "<button type=\"submit\" class=\"dmo-button\" id=\"dmtGuardar\" form=\"dmtEditarForm\">Guardar template</button>", "dmtEditarForm");
        // O «Opções» fica ao lado, no mesmo canto.
        Assert.True(Posicao(after, "<details class=\"dmo-options\" data-dmo-tab-menu>") > Posicao(after, "id=\"dmtGuardar\""));
        Assert.True(Posicao(after, "<div data-dmo-toast=\"ok\" class=\"dmt-nota\" role=\"status\">Template criada.</div>") < Posicao(after, "<aside id=\"dmtLista\""));
        // A janela de eliminar é da casa (dmo-dialog): pode vir na zona.
        Assert.True(Posicao(after, "<dialog id=\"dmt-delete-dialog\" class=\"dmo-dialog\"") > Posicao(after, "<section id=\"dmtDetalhe\""));

        // Abrir a mesma template sem acabar de a criar não puxa o foco.
        Assert.DoesNotContain(" autofocus", await admin.GetStringAsync($"/Admin/Templates?template={key}"));

        using var deleted = await admin.PostAsync("/Admin/Templates?handler=Delete", HtmlFormHelpers.BuildFormPost(
            ("template_key", key), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(after)!)));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        var gone = await admin.GetStringAsync(deleted.Headers.Location!.ToString());
        Assert.Matches("<input id=\"dmt-new-name\" name=\"name\"[^>]* autofocus=\"autofocus\"", gone);
        Assert.DoesNotContain("Template E10", gone);
    }
}
