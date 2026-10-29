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
/// Quem entra pelo posto da RI fica só com a Reparação Interna (Diogo,
/// 27/10/2026: «Só a Reparação Interna»). A mesma conta, com o nº de operador
/// e a palavra-passe, continua com todos os módulos do template.
/// O mesmo computador para várias pessoas (Diogo, 27/10/2026: «3min auto e
/// opções nas definições para aumentar»): a sessão do posto fecha ao fim de
/// 3 minutos sem uso, ou do tempo escolhido nas Definições da Reparação.
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
        Semear(factory, id, minutos: null);

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

    [Fact]
    public async Task PeloPosto_SemUsoDurante3Minutos_ASessaoFecha()
    {
        var (factory, relogio, id) = Fabrica(minutos: null);
        using var _ = factory;
        using var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EntrarNoPostoAsync(posto, id);

        var pagina = await posto.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("data-dmo-posto-minutos=\"3\"", await pagina.Content.ReadAsStringAsync());

        relogio.Avancar(TimeSpan.FromMinutes(3.5));
        var depois = await posto.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
        Assert.Contains("/login", depois.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PeloPosto_DentroDoLimite_OsPedidosContinuam()
    {
        var (factory, relogio, id) = Fabrica(minutos: null);
        using var _ = factory;
        using var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EntrarNoPostoAsync(posto, id);

        // Dentro dos 3 minutos (por omissão) qualquer pedido continua a entrar,
        // sem precisar de deslizar: o ExpiresUtc do login ainda não passou.
        relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NoContent, (await posto.GetAsync("/posto/ativo")).StatusCode);
        relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await posto.GetAsync("/ReparacaoInterna")).StatusCode);
    }

    /// <summary>
    /// AUTH-B05 (DMO-657): antes, o posto partilhava o cookie do login normal
    /// (SlidingExpiration=true, ExpireTimeSpan de 1 hora) — um pedido feito
    /// DENTRO do limite do posto (aqui, aos 2 min de um limite de 3) deslizava
    /// o cookie para a hora inteira, e a sessão continuava válida muito depois
    /// dos 3 minutos sem que mais nenhum pedido fosse preciso. Com o esquema
    /// próprio do posto, sem deslizar, esse MESMO pedido já não estende nada:
    /// o ExpiresUtc escrito no login continua a ser o único limite.
    /// </summary>
    [Fact]
    public async Task PeloPosto_UmPedidoDentroDoLimite_JaNaoEstendeParaUmaHora()
    {
        var (factory, relogio, id) = Fabrica(minutos: null);
        using var _ = factory;
        using var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EntrarNoPostoAsync(posto, id);

        // Um único pedido a meio do limite de 3 minutos — exatamente o que
        // deslizava o cookie do login partilhado para a hora inteira.
        relogio.Avancar(TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.NoContent, (await posto.GetAsync("/posto/ativo")).StatusCode);

        // Passados os 3 minutos do login (muito antes da hora que o esquema
        // partilhado teria concedido), a sessão já não vale.
        relogio.Avancar(TimeSpan.FromMinutes(2));
        var depois = await posto.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
        Assert.Contains("/login", depois.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PeloPosto_ComOTempoDasDefinicoes_ASessaoDuraEsseTempo()
    {
        var (factory, relogio, id) = Fabrica(minutos: "10");
        using var _ = factory;
        using var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EntrarNoPostoAsync(posto, id);

        relogio.Avancar(TimeSpan.FromMinutes(4));
        var pagina = await posto.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("data-dmo-posto-minutos=\"10\"", await pagina.Content.ReadAsStringAsync());

        relogio.Avancar(TimeSpan.FromMinutes(11));
        Assert.Equal(HttpStatusCode.Redirect, (await posto.GetAsync("/ReparacaoInterna")).StatusCode);
    }

    [Fact]
    public async Task SairDoPosto_VoltaAoPosto_PeloLoginNaoHaTempo()
    {
        var (factory, _, id) = Fabrica(minutos: null);
        using var __ = factory;
        using (var posto = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await EntrarNoPostoAsync(posto, id);
            var saida = await posto.GetAsync("/logout?posto=1");
            Assert.Equal(HttpStatusCode.Redirect, saida.StatusCode);
            Assert.Equal("/Posto", saida.Headers.Location?.OriginalString);
            Assert.Equal(HttpStatusCode.Redirect, (await posto.GetAsync("/ReparacaoInterna")).StatusCode);
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
        var ri = await login.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.OK, ri.StatusCode);
        // Pelo login o atributo fica vazio: o dmo-posto.js não faz nada.
        Assert.DoesNotMatch("data-dmo-posto-minutos=\"[0-9]", await ri.Content.ReadAsStringAsync());
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
