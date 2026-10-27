using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Quem entra pelo posto da RI fica só com a Reparação Interna (Diogo,
/// 27/10/2026: «Só a Reparação Interna»). A mesma conta, com o nº de operador
/// e a palavra-passe, continua com todos os módulos do template.
/// </summary>
public sealed class PostoSoRiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PinCerto = "7310";
    private const string Operador = "4391";
    private const string PalavraPasse = "PostoSoRi2026!";

    private readonly CustomWebApplicationFactory _factory;

    public PostoSoRiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    [Fact]
    public async Task PeloPosto_SoAReparacaoInterna_PeloLoginTodosOsModulos()
    {
        var id = Guid.NewGuid();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IPostoRi>(_ => new PostoComUm(new PostoReparador(id, "Reparador Só RI")))));
        Semear(factory, id);

        using (var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            var entrada = await EntrarNoPostoAsync(posto, id);
            Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
            Assert.StartsWith("/ReparacaoInterna", entrada.Headers.Location?.OriginalString);

            Assert.Equal(HttpStatusCode.OK, (await posto.GetAsync("/ReparacaoInterna")).StatusCode);
            var jobOn = await posto.GetAsync("/JobOn");
            Assert.Equal(HttpStatusCode.Redirect, jobOn.StatusCode);
            Assert.StartsWith("/ReparacaoInterna", jobOn.Headers.Location?.OriginalString);
        }

        using var login = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var pagina = await login.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await pagina.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
        var entrou = await login.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", Operador),
            ("password", PalavraPasse),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, entrou.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await login.GetAsync("/JobOn")).StatusCode);
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

    private void Semear(WebApplicationFactory<Program> factory, Guid id)
    {
        var email = $"{Operador}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-posto-so-ri", PalavraPasse, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template
        {
            TemplateKey = "tpl-posto-so-ri",
            Name = "Reparador com Job On",
            Capabilities = [DmoCapabilities.ReparacaoInterna, DmoCapabilities.JobOnCreate]
        };
        db.Templates.Add(template);
        db.Users.Add(new User { Id = id, OperatorId = Operador, Name = "Reparador Só RI", ProviderUserId = "provider-posto-so-ri" });
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = id, TemplateId = template.Id });
        db.SaveChanges();
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
