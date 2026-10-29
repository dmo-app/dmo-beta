using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// PERM-B01 (DMO-654): a sessão do Posto fica confinada à Reparação Interna
/// (<see cref="PostoRi"/>); sem a capability Ferramentas, pedir diretamente
/// /Ferramentas/Utilizacao?tool_id=… também é recusado — não só escondido do
/// menu do Posto.
/// </summary>
public sealed class PostoFerramentasUtilizacaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PinCerto = "7311";
    private const string Operador = "4398";
    private const string PalavraPasse = "PostoFerram2026!";

    private readonly CustomWebApplicationFactory _factory;

    public PostoFerramentasUtilizacaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    [Fact]
    public async Task PeloPosto_FerramentasUtilizacao_EhRecusada_PeloLoginContinuaAAbrir()
    {
        var id = Guid.NewGuid();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IPostoRi>(_ => new PostoComUm(new PostoReparador(id, "Reparador Só RI")))));
        var toolId = Semear(factory, id);

        using (var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await EntrarNoPostoAsync(posto, id);

            var utilizacao = await posto.GetAsync($"/Ferramentas/Utilizacao?tool_id={toolId}");
            Assert.Equal(HttpStatusCode.Redirect, utilizacao.StatusCode);
            Assert.DoesNotContain("/Ferramentas", utilizacao.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        }

        using var login = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var pagina = await login.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await pagina.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
        await login.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", Operador),
            ("password", PalavraPasse),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        var depoisDoLogin = await login.GetAsync($"/Ferramentas/Utilizacao?tool_id={toolId}");
        Assert.Equal(HttpStatusCode.OK, depoisDoLogin.StatusCode);
    }

    private static async Task<HttpResponseMessage> EntrarNoPostoAsync(HttpClient client, Guid utilizador)
    {
        var pagina = await client.GetAsync("/Posto");
        pagina.EnsureSuccessStatusCode();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await pagina.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
        return await client.PostAsync("/Posto", HtmlFormHelpers.BuildFormPost(
            ("utilizador", utilizador.ToString()),
            ("pin", PinCerto),
            ("__RequestVerificationToken", token)));
    }

    private string Semear(WebApplicationFactory<Program> factory, Guid id)
    {
        var email = $"{Operador}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-posto-ferramentas", PalavraPasse, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        // O template da conta tem Reparação Interna e Ferramentas: pelo
        // login normal continua a abrir; só a sessão do Posto fica
        // reduzida à RI (CurrentUserCapabilities.SoDoPosto).
        var template = new Template
        {
            TemplateKey = "tpl-posto-ferramentas",
            Name = "Reparador com Ferramentas",
            Capabilities = [DmoCapabilities.ReparacaoInterna, DmoCapabilities.Ferramentas]
        };
        db.Templates.Add(template);
        db.Users.Add(new User { Id = id, OperatorId = Operador, Name = "Reparador Só RI", ProviderUserId = "provider-posto-ferramentas" });
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = id, TemplateId = template.Id });

        var tool = new Tool { ToolId = "F-POSTO-CM", Type = ToolType.Cm, Reference = "5820", Lot = "01" };
        db.Tools.Add(tool);
        db.SaveChanges();
        return tool.ToolId;
    }

    private sealed class PostoComUm(PostoReparador reparador) : IPostoRi
    {
        public Task<IReadOnlyList<PostoReparador>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PostoReparador>>([reparador]);

        public Task<bool> TemPinAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == reparador.Id);

        public Task<PinGravacao> DefinirPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(PinGravacao.Invalido);

        public Task<bool> ConfereAsync(Guid userId, string? pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == reparador.Id && pin == PinCerto);
    }
}
