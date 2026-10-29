using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// A Pesquisa do Armazém (DMO-641, Diogo 29/10/2026: «armazem nao tem
/// pesquisa [...] um user quiser pesquisar uma ferramenta nao consegue»): o
/// separador ao lado do Registo e dos Movimentos procura CM, MF e BQ pela
/// referência, lote ou posição; cada linha abre o registo da ferramenta
/// (Saída se está numa posição, Entrada se está fora), sem botões nas linhas.
/// O Chromium a 1290×646 prova-se em tests/visual/armazem_pesquisar_browser.py.
/// </summary>
public sealed class ArmazemPesquisarPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-pesq-cm";
    private const string Mf = "arm-pesq-mf";
    private const string Bq = "arm-pesq-bq";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemPesquisarPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Mf || t.ToolId == Bq));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "1075C142", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "1075C142", Lot = "7" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "C142", Lot = "01" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        var r = s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc), null)).GetAwaiter().GetResult();
        Assert.True(r.Gravou, r.Resultado.ToString());
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Pesquisa{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armpesq-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6801", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Pesquisar");
        using var zona = await client.GetAsync("/Armazem/Pesquisar?handler=Resultados&q=1075");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, zona.StatusCode);
    }

    [Fact]
    public async Task OSeparador_EstaAoLadoDoRegistoEDosMovimentos_EAPaginaAbreVazia()
    {
        using var client = await LoginOperatorAsync("6802", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Pesquisar");

        Assert.Matches("<a href=\"/Armazem/Pesquisar\"[^>]*aria-current=\"page\">Pesquisar</a>", html);
        Assert.Contains("href=\"/Armazem/Movimentos\"", html);
        Assert.Contains("href=\"/Armazem/Ultimos\"", html);
        Assert.Contains("data-dmo-ao-escrever=\"armPesqResultados\"", html);
        Assert.DoesNotContain("id=\"armPesqLista\"", html);
        Assert.DoesNotContain("id=\"armPesqNada\"", html);
    }

    [Fact]
    public async Task Pesquisar_ProcuraCmMfEBq_EACadaLinhaAbreORegistoDaFerramenta()
    {
        using var client = await LoginOperatorAsync("6803", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Pesquisar?q=1075");

        Assert.Contains($"data-dmo-open=\"/Armazem/Movimentos?ferramenta={Cm}&amp;acao=saida\"", html);
        Assert.Contains($"data-dmo-open=\"/Armazem/Movimentos?ferramenta={Mf}&amp;acao=entrada\"", html);
        Assert.Contains("Armazém · posição 0042", html);
        var tabela = Regex.Match(html, "<table[^>]*id=\"armPesqLista\"[\\s\\S]*?</table>").Value;
        Assert.DoesNotContain("<button", tabela);

        var bq = await client.GetStringAsync("/Armazem/Pesquisar?q=C142&tipo=BQ");
        Assert.Contains($"ferramenta={Bq}&amp;acao=entrada", bq);
        Assert.DoesNotContain($"ferramenta={Cm}&", bq);
    }

    [Fact]
    public async Task Pesquisar_PelaPosicao_EComNada_AvisaQueNaoEncontrou()
    {
        using var client = await LoginOperatorAsync("6804", DmoCapabilities.ArmazemGestao);

        var posicao = await client.GetStringAsync("/Armazem/Pesquisar?q=0042");
        var nada = await client.GetStringAsync("/Armazem/Pesquisar?q=ZZZ9");

        Assert.Contains($"data-dmo-ferramenta=\"{Cm}\"", posicao);
        Assert.DoesNotContain($"data-dmo-ferramenta=\"{Mf}\"", posicao);
        Assert.Contains("id=\"armPesqNada\"", nada);
    }

    [Fact]
    public async Task ProcuraAoEscrever_DevolveSoAZonaDosResultados()
    {
        using var client = await LoginOperatorAsync("6805", DmoCapabilities.ArmazemGestao);

        using var resposta = await client.GetAsync("/Armazem/Pesquisar?handler=Resultados&q=1075");
        var zona = (await resposta.Content.ReadAsStringAsync()).Trim();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("armPesqResultados", resposta.Headers.GetValues("X-Dmo-Region").Single());
        Assert.StartsWith("<div id=\"armPesqResultados\" data-dmo-regiao>", zona);
        Assert.DoesNotContain("<html", zona);
        Assert.Contains($"data-dmo-ferramenta=\"{Cm}\"", zona);
    }

    /// <summary>
    /// DMO-643 (Diogo, 29/10/2026: o link da «% uso» no Armazém aparecia com o
    /// azul e o sublinhado do browser, «como se faltasse UI»): o link é o mesmo
    /// em todas as páginas (UsoFerramenta.Link) e a folha global dá-lhe a cor
    /// da paleta, sem sublinhado, também no rato por cima.
    /// </summary>
    [Fact]
    public async Task LinkDaPercentagemDeUso_TemOEstiloDaApp()
    {
        var link = DMO.Alpha.Web.Presentation.UsoFerramenta.Link("t1", "18 %").ToString();
        Assert.Contains("class=\"dmo-uso-link\"", link);

        using var client = await LoginOperatorAsync("6806", DmoCapabilities.ArmazemGestao);
        var layout = await client.GetStringAsync("/Armazem/Pesquisar");
        var href = Regex.Match(layout, "href=\"(/css/dmo-design\\.css[^\"]*)\"").Groups[1].Value;
        var css = await client.GetStringAsync(href);

        Assert.Matches("a\\.dmo-uso-link[^{]*\\{color:var\\(--dmo-brand-700\\);text-decoration:none", css);
        Assert.Matches("a\\.dmo-uso-link:is\\(:hover,:focus-visible\\)\\{color:var\\(--dmo-brand-600\\);text-decoration:none", css);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}
