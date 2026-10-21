using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// A página dos Próximos Job On do Armazém (0.2.0-beta.13): as produções que
/// começam no período, com cada CM, MF e BQ, onde está e o que falta (tirar do
/// armazém, não disponível, pronta); o período aplica-se ao escolher.
/// </summary>
public sealed class ArmazemProximosPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-prox-cm";
    private const string Mf = "arm-prox-mf";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemProximosPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.ToolId == Cm));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.ToolId == Mf));
        db.JobOns.RemoveRange(db.JobOns.Where(j => j.ProductionNumber.StartsWith("ARMPROX")));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Mf));
        db.SaveChanges();
        var maquina = db.Machines.FirstOrDefault(m => m.Code == "XP1") ?? new Machine { Code = "XP1" };
        if (maquina.Id == 0)
        {
            db.Machines.Add(maquina);
        }

        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" });
        var posicao = new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.Add(posicao);
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var proxima = new JobOn { Reference = "5447T173", ProductionNumber = "ARMPROX-1", Machine = maquina, InicioEm = hoje.AddDays(3) };
        var longe = new JobOn { Reference = "6012A118", ProductionNumber = "ARMPROX-2", Machine = maquina, InicioEm = hoje.AddDays(20) };
        db.JobOns.AddRange(proxima, longe);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = proxima.Id, ToolId = Cm });
        db.MfContexts.Add(new MfContext { JobOnId = proxima.Id, ToolId = Mf });
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = Cm, PosicaoId = posicao.Id, MovimentoId = 1, DesdeUtc = DateTime.UtcNow });
        db.FerramentaLocalizacoes.Add(new FerramentaLocalizacao
        {
            ToolId = Mf, Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Moldes Lda", DesdeUtc = DateTime.UtcNow, MovimentoId = 2
        });
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
            var template = new Template { TemplateKey = $"tpl-armprox-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
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
        using var client = await LoginOperatorAsync("6601", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Proximos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task MostraAsProducoesDoPeriodo_ComOQueFaltaTirar()
    {
        using var client = await LoginOperatorAsync("6602", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Proximos");

        Assert.Matches("<a href=\"/Armazem/Proximos\"[^>]*aria-current=\"page\">Próximos Job On</a>", html);
        var lista = Elemento(html, "id=\"armProxLista\"");
        Assert.Contains("ARMPROX-1", lista);
        Assert.DoesNotContain("ARMPROX-2", lista);
        var cm = Elemento(lista, $"data-dmo-ferramenta=\"{Cm}\"");
        Assert.Contains("data-dmo-acao=\"tirar\"", cm);
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\">CM 5447T173 · lote 3</a>", cm);
        Assert.Contains("Armazém · posição 0042", Texto(cm));
        Assert.Contains("Tirar do armazém", Texto(cm));
        var dia = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(3);
        Assert.Contains(DMO.Alpha.Web.Pages.Armazem.ProximosModel.Dia(dia), Texto(lista));
        var mf = Elemento(lista, $"data-dmo-ferramenta=\"{Mf}\"");
        Assert.Contains("A reparar · Moldes Lda", Texto(mf));
        Assert.Contains("Não disponível", Texto(mf));
        var filtros = Elemento(html, "id=\"armProxFiltros\"");
        Assert.Contains("data-dmo-autosubmit", filtros);
        Assert.DoesNotContain("<button", filtros);
    }

    [Fact]
    public async Task OPeriodoMaisLongo_MostraMais()
    {
        using var client = await LoginOperatorAsync("6603", DmoCapabilities.ArmazemGestao);

        var trinta = await client.GetStringAsync("/Armazem/Proximos?dias=30");
        var invalido = await client.GetStringAsync("/Armazem/Proximos?dias=999");

        Assert.Contains("ARMPROX-2", Elemento(trinta, "id=\"armProxLista\""));
        Assert.Matches("<option value=\"30\" selected=\"selected\">", trinta);
        Assert.Matches("<option value=\"14\" selected=\"selected\">", invalido);
    }
}
