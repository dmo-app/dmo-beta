using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Teste no browser real de 22/10/2026 (0.3.0-beta.1): recarregar o Job On
/// apagava a folha por gravar sem aviso, e o Enter na Referência da Reparação
/// Programada não procurava. A folha leva a chave do rascunho e o script que o
/// guarda neste browser (wwwroot/js/jobon-rascunho.js); o Enter nos campos
/// de texto da procura submete o rpFiltros (o «Adicionar» é do rpAssociar). O comportamento no Chromium prova-se com o Playwright.
/// </summary>
public class RascunhoEEnterNaProcuraTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RascunhoEEnterNaProcuraTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = "RascunhoPass1!";
        _factory.AuthProvider.AddAccount(email, "provider-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-rascunho-" + operatorId + "-" + Guid.NewGuid().ToString("N"), Name = "Rascunho " + operatorId, Capabilities = capabilities };
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeador " + operatorId, ProviderUserId = "provider-" + operatorId };
            db.Templates.Add(template);
            db.Users.Add(user);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task NovoJobOn_AFolhaLevaAChaveDoRascunho_EOScriptQueOGuarda()
    {
        using var client = await LoginAsync("7801", DmoCapabilities.JobOnCreate);

        var html = await client.GetStringAsync("/JobOn?novo=1");

        Assert.Matches("<form id=\"lightForm\" data-dmo-script data-jobon-rascunho=\"novo\"", html);
        Assert.Matches("<script src=\"/js/jobon-rascunho\\.js\\?v=[^\"]+\" defer></script>", html);
    }

    [Fact]
    public async Task JobOn_OEnterNaProcuraEntraNaProducao_OScriptVemNaPagina()
    {
        // DMO-751 (A-0525): o Enter na referência ou no dropdown «Produção» abre o Job On escolhido.
        using var client = await LoginAsync("7803", DmoCapabilities.JobOnCreate);

        int primeira, segunda;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new DMO.Alpha.Core.Production.Machine { Code = "B1" };
            db.Machines.Add(b1);
            await db.SaveChangesAsync();
            var antiga = new DMO.Alpha.Core.Production.JobOn { Reference = "3245C002", ProductionNumber = "202603", MachineId = b1.Id, InicioEm = new DateOnly(2026, 8, 11) };
            var nova = new DMO.Alpha.Core.Production.JobOn { Reference = "3245C002", ProductionNumber = "202604", MachineId = b1.Id, InicioEm = new DateOnly(2026, 9, 28) };
            db.JobOns.AddRange(antiga, nova);
            await db.SaveChangesAsync();
            primeira = nova.Id;
            segunda = antiga.Id;
        }

        var html = await client.GetStringAsync("/JobOn?ano=2026&mes=11&dia=10");
        var producoes = await client.GetStringAsync("/JobOn?handler=ProcurarProducao&ano=2026&mes=11&dia=10&q=3245C002");
        var sugestoes = await client.GetStringAsync("/Sugestoes?fonte=referencias&q=3245");

        Assert.Contains("id=\"lightSearch\"", html);
        Assert.Matches("<script src=\"/js/jobon-procura-enter\\.js\\?v=[^\"]+\" defer></script>", html);
        // A mais recente vem escolhida: o Enter abre-a.
        Assert.Matches($"<option value=\"/JobOn/Folha\\?jobon_id={primeira}&amp;dia=2026-11-10\" data-id=\"{primeira}\" selected=\"selected\">", producoes);
        Assert.Contains($"jobon_id={segunda}", producoes);
        Assert.Contains("\"3245C002\"", sugestoes);

        var directory = Environment.GetEnvironmentVariable("DMO_UI_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "jobon-enter.html"), html);
            File.WriteAllText(Path.Combine(directory, "jobon-enter-producoes.html"), producoes);
            File.WriteAllText(Path.Combine(directory, "jobon-enter-sugestoes.json"), sugestoes);
        }
    }

    [Fact]
    public async Task ReparacaoProgramada_OEnterNaReferenciaENoLoteSubmeteAProcura_ENaoOAdicionar()
    {
        using var client = await LoginAsync("7802", DmoCapabilities.Ferramentas);

        var html = await client.GetStringAsync("/Ferramentas/ReparacaoProgramada");

        var filtros = Regex.Match(html, "<form id=\"rpFiltros\"[\\s\\S]*?</form>").Value;
        Assert.Contains("id=\"rpReferencia\"", filtros);
        Assert.Contains("id=\"rpLote\"", filtros);
        // O único botão de submeter na linha da procura é o «Adicionar», do rpAssociar:
        // sem o script, o Enter não fazia nada.
        Assert.Matches("<button type=\"submit\" form=\"rpAssociar\"[^>]*id=\"rpAssociarBotao\"", filtros);
        Assert.Contains("campo.matches(\"#rpFiltros input:not([type=hidden]):not([type=checkbox]):not([type=radio])\")", html);
        Assert.Contains("campo.form.requestSubmit()", html);
    }
}
