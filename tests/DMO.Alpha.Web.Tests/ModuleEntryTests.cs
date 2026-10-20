using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O template dá módulos e cada módulo dá as suas páginas (o Owner
/// 2026-10-16). A entrada é verificada uma vez por pedido; o menu e a
/// página inicial saem da mesma lista; uma alteração do template vale no
/// pedido seguinte.
/// </summary>
public sealed class ModuleEntryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModuleEntryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private Guid SeedUser(string operatorId, string password, string templateKey, string[] modules)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template { TemplateKey = templateKey, Name = "Módulos " + operatorId, Capabilities = modules };
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Módulos", ProviderUserId = $"provider-{operatorId}" };
        db.Templates.Add(template);
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
        return template.Id;
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password)
    {
        var page = await client.GetStringAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)!;
        return await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
    }

    private static string PrimaryNav(string html)
    {
        var nav = Regex.Match(html, "<nav class=\"dmo-primary-nav\"[\\s\\S]*?</nav>");
        Assert.True(nav.Success, "a página não tem o menu dos módulos");
        return nav.Value;
    }

    private static string SecondaryNav(string html)
    {
        var nav = Regex.Match(html, "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>");
        Assert.True(nav.Success, "a página não tem a barra das funções do módulo");
        return nav.Value;
    }

    [Fact]
    public async Task OnlyTheTemplateModules_OpenAndAppearInTheMenu()
    {
        SeedUser("5101", "Modulos5101!", "tpl-modulos-bq", [DmoCapabilities.Boquilhas]);
        using var client = CreateClient();
        var login = await LoginAsync(client, "5101", "Modulos5101!");
        Assert.Equal("/Boquilhas", login.Headers.Location?.OriginalString);

        var boquilhas = await client.GetAsync("/Boquilhas");
        Assert.Equal(HttpStatusCode.OK, boquilhas.StatusCode);
        var nav = PrimaryNav(await boquilhas.Content.ReadAsStringAsync());
        Assert.Contains("href=\"/Boquilhas\"", nav);
        Assert.DoesNotContain("href=\"/JobOn\"", nav);
        Assert.DoesNotContain("href=\"/Controlo", nav);
        Assert.DoesNotContain("href=\"/Ferramentas\"", nav);

        foreach (var path in new[] { "/JobOn", "/Ferramentas", "/Controlo/Resumo", "/Controlo/Approve", "/JobOn?handler=HistoricoResultados&historico=1&referencia=1", "/Ferramentas?handler=Resultados&type=BQ", "/Controlo/Approve?handler=Resultados&sem_data=1" })
        {
            var refused = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/Boquilhas", refused.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task TemplateChange_AppliesOnTheNextRequest()
    {
        var templateId = SeedUser("5102", "Modulos5102!", "tpl-modulos-ferr", [DmoCapabilities.Ferramentas]);
        using var client = CreateClient();
        await LoginAsync(client, "5102", "Modulos5102!");

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/JobOn")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = await db.Templates.SingleAsync(t => t.Id == templateId);
            template.Capabilities = [DmoCapabilities.JobOnCreate];
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/JobOn")).StatusCode);
        var ferramentas = await client.GetAsync("/Ferramentas");
        Assert.Equal(HttpStatusCode.Redirect, ferramentas.StatusCode);
        Assert.Equal("/JobOn", ferramentas.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task InsideTheModule_PagesShowNoPermissionNotices()
    {
        SeedUser("5103", "Modulos5103!", "tpl-modulos-controlo", [DmoCapabilities.ControloCreate]);
        using var client = CreateClient();
        var login = await LoginAsync(client, "5103", "Modulos5103!");
        Assert.Equal("/Controlo/Resumo", login.Headers.Location?.OriginalString);

        foreach (var path in new[] { "/Controlo/Resumo", "/Controlo/Peso", "/Controlo/Pegamentos" })
        {
            var html = await client.GetStringAsync(path);
            Assert.DoesNotContain("capability", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sem permissão", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task JobOn_ShowsTheSameTools_WhateverElseTheTemplateGives()
    {
        // A página é a permissão (o Owner 2026-10-16): a folha não esconde
        // botões conforme os outros módulos do template.
        SeedUser("5105", "Modulos5105!", "tpl-modulos-jobon", [DmoCapabilities.JobOnCreate]);
        using var client = CreateClient();
        await LoginAsync(client, "5105", "Modulos5105!");

        var html = await client.GetStringAsync("/JobOn?novo=1");
        Assert.Contains(">Criar CM</a>", html);
        Assert.Contains(">Criar MF</a>", html);
        Assert.Contains(">Criar BQ</a>", html);
    }

    [Fact]
    public async Task BothControlos_OpenTheirOwnPages()
    {
        SeedUser("5106", "Modulos5106!", "tpl-modulos-ambos",
            [DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var client = CreateClient();
        await LoginAsync(client, "5106", "Modulos5106!");

        // Na interface só existe «Controlo»: uma entrada no menu, que abre
        // o Criar; a barra de cada Controlo leva ao outro.
        var resumo = await client.GetAsync("/Controlo/Resumo");
        Assert.Equal(HttpStatusCode.OK, resumo.StatusCode);
        var resumoHtml = await resumo.Content.ReadAsStringAsync();
        var nav = PrimaryNav(resumoHtml);
        Assert.Single(Regex.Matches(nav, ">Controlo</a>"));
        Assert.Contains("href=\"/Controlo/Resumo\"", nav);
        Assert.DoesNotContain("Aprovar", nav);
        Assert.Contains("href=\"/Controlo/Approve\"", SecondaryNav(resumoHtml));

        var approve = await client.GetAsync("/Controlo/Approve");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approveHtml = await approve.Content.ReadAsStringAsync();
        Assert.Contains("class=\"active\"", PrimaryNav(approveHtml));
        Assert.Contains("href=\"/Controlo/Resumo\"", SecondaryNav(approveHtml));
    }

    [Fact]
    public async Task Login_IgnoresAReturnUrlOutsideTheApp()
    {
        SeedUser("5104", "Modulos5104!", "tpl-modulos-redirect", [DmoCapabilities.JobOnCreate]);
        // Deep sweep (18/10/2026): o browser tira o TAB e a mudança de linha
        // de «/<TAB>/evil.example» e abriria «//evil.example».
        foreach (var external in new[] { "//evil.example/x", "/\\evil.example", "https://evil.example",
            "/\t/evil.example/x", "/\n/evil.example", "/\r\n/evil.example", "/ /evil.example", "/x\\..\\evil" })
        {
            using var client = CreateClient();
            var page = await client.GetStringAsync("/login");
            var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)!;
            var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
                ("identifier", "5104"),
                ("password", "Modulos5104!"),
                ("returnUrl", external),
                ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/JobOn", login.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task Suggestions_AnswerOnlyForTheModulesThatUseThem()
    {
        // As sugestões ao escrever seguem os módulos: quem só tem o Job On
        // não vê ferramentas nem reparadores ao escrever; quem tem as
        // Boquilhas vê as duas listas.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            if (!await db.Tools.AnyAsync(t => t.ToolId == "Tsug5106000"))
            {
                db.Tools.Add(new Tool { ToolId = "Tsug5106000", Type = ToolType.Bq, Reference = "ZQ51", Lot = "1" });
                db.BqRepairers.Add(new BqRepairer { Name = "Zsugest Vidros" });
                await db.SaveChangesAsync();
            }
        }

        SeedUser("5106", "Modulos5106!", "tpl-modulos-sug-jobon", [DmoCapabilities.JobOnCreate]);
        SeedUser("5107", "Modulos5107!", "tpl-modulos-sug-bq", [DmoCapabilities.Boquilhas]);

        using var jobOn = CreateClient();
        await LoginAsync(jobOn, "5106", "Modulos5106!");
        Assert.Equal("[]", await jobOn.GetStringAsync("/Sugestoes?fonte=ferramentas&q=ZQ5"));
        Assert.Equal("[]", await jobOn.GetStringAsync("/Sugestoes?fonte=reparadores&q=zsugest"));

        using var boquilhas = CreateClient();
        await LoginAsync(boquilhas, "5107", "Modulos5107!");
        Assert.Contains("ZQ51", await boquilhas.GetStringAsync("/Sugestoes?fonte=ferramentas&q=ZQ5"), StringComparison.Ordinal);
        Assert.Contains("Zsugest Vidros", await boquilhas.GetStringAsync("/Sugestoes?fonte=reparadores&q=zsugest"), StringComparison.Ordinal);
    }
}
