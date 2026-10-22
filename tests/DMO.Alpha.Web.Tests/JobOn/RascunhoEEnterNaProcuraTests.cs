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
