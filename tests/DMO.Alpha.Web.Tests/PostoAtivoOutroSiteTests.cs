using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Só a própria página do posto renova a sessão (audit externo, 16/11/2026,
/// DMO-763): um site de fora que abra /posto/ativo no mesmo browser (o browser
/// manda Sec-Fetch-Site: cross-site) não estende o limite do AUTH-B05.
/// </summary>
public sealed class PostoAtivoOutroSiteTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PinCerto = "7311";
    private const string Operador = "4392";
    private const string PalavraPasse = "PostoOutro2026!";

    private readonly CustomWebApplicationFactory _factory;

    public PostoAtivoOutroSiteTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    /// <summary>Garante: /posto/ativo pedido por outro site não renova; pela própria página (same-origin) renova.</summary>
    [Fact]
    public async Task PostoAtivo_DeOutroSite_NaoRenova_DaPropriaPagina_Renova()
    {
        var (factory, relogio, id) = Fabrica(minutos: null);
        using var _ = factory;
        using var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EntrarNoPostoAsync(posto, id);

        Assert.Equal(HttpStatusCode.Redirect, (await EntrarNoPostoAsync(posto, id)).StatusCode);

        // Aos 2 minutos, a própria página (same-origin) renova: o limite passa a 5.
        relogio.Avancar(TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.NoContent, (await Ativo(posto, "same-origin")).StatusCode);
        relogio.Avancar(TimeSpan.FromMinutes(2.5));
        Assert.Equal(HttpStatusCode.OK, (await posto.GetAsync("/ReparacaoInterna")).StatusCode);

        // Aos 4.5 minutos, um site de fora abre /posto/ativo: não renova.
        Assert.Equal(HttpStatusCode.NoContent, (await Ativo(posto, "cross-site")).StatusCode);
        relogio.Avancar(TimeSpan.FromMinutes(1));
        var depois = await posto.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
    }

    private static async Task<HttpResponseMessage> Ativo(HttpClient client, string secFetchSite)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/posto/ativo");
        pedido.Headers.Add("Sec-Fetch-Site", secFetchSite);
        return await client.SendAsync(pedido);
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

    private (WebApplicationFactory<Program> Factory, RelogioDoTeste Relogio, Guid Id) Fabrica(string? minutos)
    {
        var id = Guid.NewGuid();
        var relogio = new RelogioDoTeste(DateTimeOffset.UtcNow);
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IPostoRi>(_ => new PostoComUm(new PostoReparador(id, "Reparador Só RI")));
            // AUTH-B05: o posto autentica pelo SEU esquema (DMO.Alpha.Web.Services.PostoAuthentication.Scheme),
            // separado do login normal — é esse esquema que precisa do relógio de teste.
            services.PostConfigure<CookieAuthenticationOptions>(DMO.Alpha.Web.Services.PostoAuthentication.Scheme,
                o => o.TimeProvider = relogio);
        }));
        Semear(factory, id, minutos);
        return (factory, relogio, id);
    }

    private void Semear(WebApplicationFactory<Program> factory, Guid id, string? minutos)
    {
        var email = $"{Operador}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-posto-outro-site", PalavraPasse, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template
        {
            TemplateKey = "tpl-posto-outro-site",
            Name = "Reparador com Job On",
            Capabilities = [DmoCapabilities.ReparacaoInterna, DmoCapabilities.JobOnCreate]
        };
        db.Templates.Add(template);
        db.Users.Add(new User { Id = id, OperatorId = Operador, Name = "Reparador Só RI", ProviderUserId = "provider-posto-outro-site" });
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = id, TemplateId = template.Id });
        db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey == ModuleSettingKeys.ReparacaoInternaPostoMinutos));
        if (minutos is not null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.ReparacaoInternaPostoMinutos, SettingValue = minutos });
        }
        db.SaveChanges();
    }

    private sealed class RelogioDoTeste(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public void Avancar(TimeSpan quanto) => _agora += quanto;

        public override DateTimeOffset GetUtcNow() => _agora;
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
