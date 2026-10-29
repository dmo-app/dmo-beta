using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// DMO-577: a Consulta da Reparação põe a Referência primeiro e todos os
/// filtros no mesmo formulário (uma só linha), com a Produção logo a seguir
/// à Referência; o script que desenha a lista da Produção vem com a página.
/// </summary>
public sealed class ReparacaoInternaConsultaCoberturaBTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaConsultaCoberturaBTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private async Task<HttpClient> LoginAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"ConsultaB{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                TemplateKey = $"tpl-riconb-{operatorId}-{Guid.NewGuid():N}",
                Name = $"RI consulta B {operatorId}",
                Capabilities = [DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView]
            };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Chefe {operatorId}", ProviderUserId = $"provider-{operatorId}" };
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
    public async Task Consulta_ReferenciaPrimeiro_ProducaoLogoAseguir_ETodosOsFiltrosNumSoFormulario()
    {
        using var client = await LoginAsync("6891");

        var html = await client.GetStringAsync("/ReparacaoInterna/Consultar");

        var formulario = PosicaoNoHtml.Posicao(html, "id=\"riConsultaFiltros\"");
        var fim = html.IndexOf("</form>", formulario, StringComparison.Ordinal);
        Assert.True(fim > formulario, "o formulário dos filtros fecha");
        var filtros = html[formulario..fim];

        var referencia = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaReferencia\"");
        var producao = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaProducao\"");
        var maquina = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaMaquina\"");
        var pessoa = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaPessoa\"");
        var tipo = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaTipo\"");
        var de = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaDe\"");
        var ate = PosicaoNoHtml.Posicao(filtros, "id=\"riConsultaAte\"");

        // A Referência abre os filtros, a Produção vem logo a seguir e os outros depois.
        Assert.True(referencia < producao, "a Produção vem a seguir à Referência");
        Assert.True(producao < maquina && producao < pessoa && producao < tipo && producao < de && producao < ate,
            "os outros filtros vêm depois da Produção");
        // O script que faz a Produção mostrar só o número vem com a página.
        Assert.Contains("<script src=\"/js/ri-consulta.js", html, StringComparison.Ordinal);
    }
}
