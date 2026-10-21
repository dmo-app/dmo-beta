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
/// O Histórico do Armazém e a Localização na ficha da ferramenta
/// (0.2.0-beta.12): o Histórico lista os movimentos com os filtros a aplicar
/// ao escolher (o texto com Enter) e mostra só uma ferramenta quando se vem da
/// ficha; a ficha mostra onde está e o estado só a quem tem o Armazém.
/// </summary>
public sealed class ArmazemHistoricoPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-hist-cm";
    private const string Bq = "arm-hist-bq";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemHistoricoPaginaTests(CustomWebApplicationFactory factory)
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
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Bq));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Codigo = "A001", CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado,
            new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc), "veio da fábrica 2")).GetAwaiter().GetResult();
        s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Bq, "A001", ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo,
            new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc), null)).GetAwaiter().GetResult();
        s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Bq, ArmazemDestino.Producao, null, null, null,
            new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc), null)).GetAwaiter().GetResult();
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armhist-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
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

    /// <summary>O elemento que abre com este atributo, até ao seu fecho. Falha se não existir.</summary>
    private static string Elemento(string html, string atributo)
    {
        var inicio = html.IndexOf(atributo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem {atributo}");
        inicio = html.LastIndexOf('<', inicio);
        var tag = Regex.Match(html[(inicio + 1)..], "^[a-z][a-z0-9]*").Value;
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"{atributo} ficou por fechar");
    }

    private static string Texto(string html) =>
        System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), "\\s+", " ")).Trim();

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6501", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Historico");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ListaOsMovimentos_DoMaisRecente_ComOsFiltrosSemBotao()
    {
        using var client = await LoginOperatorAsync("6502", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Historico");

        Assert.Matches("<a href=\"/Armazem/Historico\"[^>]*aria-current=\"page\">Histórico</a>", html);
        var lista = Elemento(html, "id=\"armHistLista\"");
        var linhas = Regex.Matches(lista, "<tr data-dmo-movimento").Count;
        Assert.Equal(3, linhas);
        var texto = Texto(lista);
        Assert.True(texto.IndexOf("Saída da posição A001 para Produção", StringComparison.Ordinal)
            < texto.IndexOf("Entrada na posição 0042 · Já estava no armazém · Reparado", StringComparison.Ordinal));
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\">CM 5447T173 · lote 3</a>", lista);
        Assert.Contains("veio da fábrica 2", texto);
        var filtros = Elemento(html, "id=\"armHistFiltros\"");
        Assert.DoesNotContain("<button", filtros);
        Assert.Equal(4, Regex.Matches(filtros, "data-dmo-autosubmit").Count);
        Assert.Contains("data-dmo-suggest-submit", filtros);
    }

    [Fact]
    public async Task OsFiltros_TextoDestinoEDias()
    {
        using var client = await LoginOperatorAsync("6503", DmoCapabilities.ArmazemGestao);

        var porTexto = await client.GetStringAsync("/Armazem/Historico?q=5447");
        var porDestino = await client.GetStringAsync("/Armazem/Historico?destino=Producao");
        var porDias = await client.GetStringAsync("/Armazem/Historico?de=2026-10-15&ate=2026-10-15");
        var nada = await client.GetStringAsync("/Armazem/Historico?movimento=Corrigir");

        Assert.Single(Regex.Matches(Elemento(porTexto, "id=\"armHistLista\""), "<tr data-dmo-movimento"));
        Assert.Contains("Saída da posição A001 para Produção", Texto(Elemento(porDestino, "id=\"armHistLista\"")));
        Assert.Single(Regex.Matches(Elemento(porDestino, "id=\"armHistLista\""), "<tr data-dmo-movimento"));
        Assert.Contains("Entrada na posição A001 · Nova · Novo", Texto(Elemento(porDias, "id=\"armHistLista\"")));
        Assert.Single(Regex.Matches(Elemento(porDias, "id=\"armHistLista\""), "<tr data-dmo-movimento"));
        Assert.Matches("<input id=\"armHistDe\"[^>]*value=\"2026-10-15\"", porDias);
        Assert.Contains("Nenhum movimento com esse filtro.", Elemento(nada, "id=\"armHistVazio\""));
    }

    [Fact]
    public async Task AFicha_MostraALocalizacaoEOHistorico_SoAQuemTemOArmazem()
    {
        using var comArmazem = await LoginOperatorAsync("6504", DmoCapabilities.Ferramentas, DmoCapabilities.ArmazemGestao);
        using var semArmazem = await LoginOperatorAsync("6505", DmoCapabilities.Ferramentas);

        var ficha = await comArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Cm}");
        var fichaBq = await comArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Bq}");
        var fichaSem = await semArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Cm}");

        Assert.Contains("Armazém · posição 0042", Texto(Elemento(ficha, "id=\"ferrLocalizacao\"")));
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\"", Elemento(ficha, "id=\"ferrLocalizacao\""));
        Assert.Contains("Reparado", Texto(Elemento(ficha, "id=\"ferrEstadoArmazem\"")));
        Assert.Contains("Fabrico", Texto(Elemento(fichaBq, "id=\"ferrLocalizacao\"")));
        Assert.Contains($"href=\"/Armazem/Historico?ferramenta={Cm}\"", ficha);
        Assert.DoesNotContain("ferrLocalizacao", fichaSem);
        Assert.DoesNotContain("/Armazem/", fichaSem);

        var historico = await comArmazem.GetStringAsync($"/Armazem/Historico?ferramenta={Cm}");
        Assert.Contains("Só CM 5447T173 · lote 3.", Texto(Elemento(historico, "id=\"armHistFerramenta\"")));
        Assert.Single(Regex.Matches(Elemento(historico, "id=\"armHistLista\""), "<tr data-dmo-movimento"));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}
