using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Guarda o HTML das páginas que tests/visual/cobertura_b_browser.py mede no
/// Chromium (DMO_UI_EVIDENCE_DIR): o login com erro (DMO-505), as Definições
/// do Controlo (DMO-467) e o Registo do Armazém (DMO-487). Sem a variável não
/// guarda nada; o que cada página tem de ter para a medição verifica-se aqui.
/// </summary>
public sealed class CoberturaBExportaParaOBrowserTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CoberturaBExportaParaOBrowserTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient Cliente() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<HttpClient> OperadorAsync(string operatorId, params string[] capacidades)
    {
        var email = $"{operatorId}@dmo.local";
        var palavraPasse = $"CobB{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-cobb-{operatorId}", palavraPasse, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-cobb-{operatorId}", Name = $"Cobertura B {operatorId}", Capabilities = capacidades };
            db.Templates.Add(template);
            var utilizador = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-cobb-{operatorId}" };
            db.Users.Add(utilizador);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = utilizador.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var cliente = Cliente();
        var login = await (await cliente.GetAsync("/login")).Content.ReadAsStringAsync();
        var resposta = await cliente.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email), ("password", palavraPasse), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        return cliente;
    }

    [Fact]
    public async Task ExportaOLoginComAPalavraPasseErrada()
    {
        using var cliente = Cliente();
        var login = await (await cliente.GetAsync("/login")).Content.ReadAsStringAsync();
        var resposta = await cliente.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "ninguem@dmo.local"), ("password", "errada-1234"), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("class=\"alert alert-danger form-message\" role=\"alert\"", html, StringComparison.Ordinal);
        ExportarParaOBrowser.Gravar("cobertura-b-login-erro.html", html);
    }

    [Fact]
    public async Task ExportaAsDefinicoesDoControlo()
    {
        var cliente = await OperadorAsync("7881", DmoCapabilities.ControloCreate);

        var html = await (await cliente.GetAsync("/Definicoes?secao=controlo")).Content.ReadAsStringAsync();

        foreach (var cartao in new[] { "ctlDefDensidade", "ctlDefTolerancia", "ctlDefPuncoesSeccoes", "ctlDefListas", "ctlDefModelos" })
        {
            Assert.Contains($"id=\"{cartao}\"", html, StringComparison.Ordinal);
        }

        ExportarParaOBrowser.Gravar("cobertura-b-definicoes-controlo.html", html);
    }

    [Fact]
    public async Task ExportaORegistoDoArmazemComUmaFerramentaAberta()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "cobb-arm-cm", Type = ToolType.Cm, Reference = "8802T001", Lot = "4" });
            db.SaveChanges();
        }

        var cliente = await OperadorAsync("7882", DmoCapabilities.ArmazemGestao);

        var html = await (await cliente.GetAsync("/Armazem/Movimentos?ferramenta=cobb-arm-cm&acao=entrada")).Content.ReadAsStringAsync();

        Assert.Contains("id=\"armEntRegistar\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"armProcuraModoEntrada\"", html, StringComparison.Ordinal);
        ExportarParaOBrowser.Gravar("cobertura-b-armazem-registo.html", html);
    }
}
