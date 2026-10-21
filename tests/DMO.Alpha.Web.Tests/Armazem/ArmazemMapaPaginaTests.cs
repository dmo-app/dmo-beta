using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// A página do Mapa do Armazém (0.2.0-beta.10): o armário e o filtro
/// aplicam-se ao escolher (sem botão), a ocupação aparece em percentagem, a CM
/// e o MF da mesma referência e lote ficam numa linha, cada ferramenta abre os
/// seus movimentos e a ferramenta por reparar tem a marca.
/// </summary>
public sealed class ArmazemMapaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-mapa-cm";
    private const string Mf = "arm-mapa-mf";
    private const string Bq = "arm-mapa-bq";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemMapaPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Mf || t.ToolId == Bq));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        var p4001 = new ArmazemPosicao { Codigo = "4001", CriadaEmUtc = DateTime.UtcNow };
        var a001 = new ArmazemPosicao { Codigo = "5001", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.AddRange(p4001, new ArmazemPosicao { Codigo = "4002", CriadaEmUtc = DateTime.UtcNow }, a001);
        db.SaveChanges();
        var desde = new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);
        db.ArmazemOcupacoes.AddRange(
            new ArmazemOcupacao { ToolId = Cm, PosicaoId = p4001.Id, MovimentoId = 1, DesdeUtc = desde },
            new ArmazemOcupacao { ToolId = Mf, PosicaoId = p4001.Id, MovimentoId = 2, DesdeUtc = desde },
            new ArmazemOcupacao { ToolId = Bq, PosicaoId = a001.Id, MovimentoId = 3, DesdeUtc = desde });
        db.FerramentaEstados.Add(new FerramentaEstado { ToolId = Bq, Estado = FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = desde });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armmapa-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
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
        using var client = await LoginOperatorAsync("6301", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Mapa");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AbreOPrimeiroArmario_ComAOcupacaoEOParNumaLinha()
    {
        using var client = await LoginOperatorAsync("6302", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Mapa");

        Assert.Matches("<a href=\"/Armazem/Mapa\"[^>]*aria-current=\"page\">Mapa</a>", html);
        // Por corredor e estante (Diogo 21/10, AR-03): 4001 = corredor 4, estante 00, prateleira 1.
        var escolha = Elemento(html, "id=\"armMapaArmario\"");
        Assert.Contains("<optgroup label=\"Corredor 4\">", escolha);
        Assert.Contains("Corredor 4 inteiro · 1 de 2 ocupadas · 50%", escolha);
        Assert.Matches("<option value=\"400\" selected=\"selected\">Estante 00 · 1 de 2 ocupadas · 50%</option>", escolha);
        Assert.Contains("Todo o armazém", escolha);
        Assert.Contains("50% ocupado · 1 de 2 posições", Texto(Elemento(html, "id=\"armMapaOcupacao\"")));
        // O armário e o filtro aplicam-se ao escolher: sem botão no formulário.
        var filtros = Elemento(html, "id=\"armMapaFiltros\"");
        Assert.Equal(2, Regex.Matches(filtros, "data-dmo-autosubmit").Count);
        Assert.DoesNotContain("<button", filtros);

        var par = Elemento(html, "data-dmo-posicao=\"4001\"");
        Assert.Contains("data-dmo-estado=\"ocupada\"", par);
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\">", par);
        Assert.Contains("CM+MF 5447T173 · lote 3", Texto(par));
        Assert.Contains("Prateleira 1", Texto(par));
        Assert.Contains("desde 15/10/2026", Texto(par));
        Assert.Contains("Livre", Texto(Elemento(html, "data-dmo-posicao=\"4002\"")));
        Assert.DoesNotContain("data-dmo-posicao=\"5001\"", html);
    }

    [Fact]
    public async Task OutroArmarioEOFiltro_PorReparar()
    {
        using var client = await LoginOperatorAsync("6303", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Mapa?armario=500&mostrar=por-reparar");

        var bq = Elemento(html, "data-dmo-posicao=\"5001\"");
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Bq}\">BQ T173 · lote 01</a>", bq);
        Assert.Contains("data-dmo-por-reparar", bq);
        Assert.DoesNotContain("data-dmo-posicao=\"4001\"", html);

        var livres = await client.GetStringAsync("/Armazem/Mapa?armario=*&mostrar=livres");
        Assert.Contains("data-dmo-posicao=\"4002\"", livres);
        Assert.DoesNotContain("data-dmo-posicao=\"4001\"", livres);
        Assert.Contains("67% ocupado · 2 de 3 posições", Texto(Elemento(livres, "id=\"armMapaOcupacao\"")));
    }
}
