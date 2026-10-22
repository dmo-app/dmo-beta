using System.Net;
using System.Security.Claims;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova a policy <c>RequireAdmin</c> da fundação Admin (Step B) e o gate
/// da área /Admin (aplicado por convenção de pasta às Razor Pages):
///
/// - ADMIN authority é o claim <see cref="DmoClaimTypes.IsAdmin"/>
///   validado por pedido — um principal SEM o claim é negado na policy
///   (a manifestação HTTP com a cookie configuration vigente é o
///   challenge/forbid para /login, nunca um render de página);
/// - instalação CONFIGURADA: sessão anónima e sessão de operador (sem
///   claim) são negadas em todas as rotas /Admin; a sessão ADMIN entra;
/// - instalação NÃO CONFIGURADA: a área Admin continua protegida; nunca pública.
///   sem exceções de acesso anónimo.
/// - composição com a validação de época: um cookie ADMIN cuja época não
///   é mais a corrente é rejeitado pelo <c>AdminEpochCookieValidator</c>
///   e a policy nega como anónimo (pós-ativação).
/// </summary>
public sealed class AdminAuthorizationPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminAuthorizationPolicyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password)
    {
        var token = await GetVerificationTokenAsync(client, "/login");
        var content = HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token));
        return await client.PostAsync("/login", content);
    }

    /// <summary>
    /// Semeia o ADMIN: o email das Definições (o ADMIN é o email —
    /// o Owner 2026-10-16) mais a conta Auth que autentica com ele.
    /// </summary>
    private void SeedConfiguredAdmin(string email, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        dbContext.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = email
        });
        dbContext.SaveChanges();
    }

    private void SeedOperator(string operatorId, string providerUserId, string password)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = "Operador Sem Claim",
            ProviderUserId = providerUserId
        });
        dbContext.SaveChanges();
    }

    // ------------------------------------------------------------------
    // The policy itself: the ADMIN claim decides
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequireAdminPolicy_PrincipalWithoutAdminClaim_IsDenied()
    {
        // A área Admin exige uma sessão Admin autenticada.
        SeedConfiguredAdmin("policy-admin@example.test", "policy-admin-provider", "PolicyAdmin123!");

        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        // Um principal autenticado SEM o claim ADMIN — negado (a forma
        // endpoint desta negação com a cookie configuration vigente é o
        // redirect de challenge/forbid para /login provado nos testes
        // HTTP abaixo; a avaliação direta da policy prova a negação em si).
        var operatorPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(DmoClaimTypes.OperatorId, "1234"),
                new Claim(DmoClaimTypes.DisplayName, "Operador")
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));

        var operatorResult = await authorization.AuthorizeAsync(operatorPrincipal, policyName: AdminAuthorizationPolicies.RequireAdmin);
        Assert.False(operatorResult.Succeeded);

        // Um principal anónimo — negado.
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var anonymousResult = await authorization.AuthorizeAsync(anonymous, policyName: AdminAuthorizationPolicies.RequireAdmin);
        Assert.False(anonymousResult.Succeeded);

        // O principal com o claim ADMIN — autorizado.
        var adminPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(DmoClaimTypes.IsAdmin, bool.TrueString),
                new Claim(DmoClaimTypes.DisplayName, "admin@example.test")
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));

        var adminResult = await authorization.AuthorizeAsync(adminPrincipal, policyName: AdminAuthorizationPolicies.RequireAdmin);
        Assert.True(adminResult.Succeeded);
    }

    [Fact]
    public async Task RequireAdminPolicy_UnconfiguredInstallation_DeniesAnonymous()
    {
        // Even without a persisted InstallationState, Admin is never public.
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var result = await authorization.AuthorizeAsync(anonymous,
            policyName: AdminAuthorizationPolicies.RequireAdmin);
        Assert.False(result.Succeeded);
    }

    // ------------------------------------------------------------------
    // Endpoint level: every /Admin route, configured installation
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied()
    {
        SeedConfiguredAdmin("denied-admin@example.test", "denied-admin-provider", "DeniedAdmin123!");
        SeedOperator("1234", "denied-operator-provider", "OperatorPass123!");

        using var anonymousClient = CreateClient();
        using var operatorClient = CreateClient();

        var operatorLogin = await LoginAsync(operatorClient, "1234", "OperatorPass123!");
        Assert.Equal(HttpStatusCode.Redirect, operatorLogin.StatusCode);

        foreach (var path in new[] { "/Admin", "/Admin/Index", "/Admin/Definicoes", "/Admin/Definicoes/Index" })
        {
            // Sessão não autenticada: challenge para a surface de login —
            // nunca um render de página Admin.
            var anonymous = await anonymousClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            Assert.Equal("/login", anonymous.Headers.Location?.AbsolutePath);

            // Sessão de operador autenticada SEM o claim ADMIN: negada
            // (forbid → AccessDeniedPath=/login) em todas as rotas Admin.
            var refused = await operatorClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/login", refused.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public async Task AdminPages_ConfiguredInstallation_AdminSessionIsAllowed()
    {
        SeedConfiguredAdmin("allowed-admin@example.test", "allowed-admin-provider", "AllowedAdmin123!");

        using var adminClient = CreateClient();
        var login = await LoginAsync(adminClient, "allowed-admin@example.test", "AllowedAdmin123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // O entry redireciona para as definições; as superfícies Admin
        // renderizam para o ADMIN (o claim autoriza, a época é a corrente).
        var entry = await adminClient.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, entry.StatusCode);
        Assert.Equal("/Admin/Users", entry.Headers.Location?.OriginalString);

        // O Setup vive nas Definições → App (Diogo, 22/10/2026).
        var definicoes = await adminClient.GetAsync("/Admin/Definicoes/Index");
        Assert.Equal("/Definicoes?secao=app", definicoes.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Definicoes?secao=app")).StatusCode);

        // The removed Setup endpoint cannot be rendered, even by Admin.
        var removedSetup = await adminClient.GetAsync("/Admin/Definicoes/Sistema");
        Assert.Equal(HttpStatusCode.NotFound, removedSetup.StatusCode);
    }

    // ------------------------------------------------------------------
    // BETA: removing Setup must not expose Admin anonymously.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminPages_UnconfiguredInstallation_DoesNotAllowAnonymous()
    {
        using var client = CreateClient();
        foreach (var path in new[] { "/Admin", "/Admin/Definicoes/Index" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
        }
    }

    // ------------------------------------------------------------------
    // Epoch composition: a replaced ADMIN's cookie dies at /Admin too
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminPages_AdminEmailSettingRemoved_RevokesAdminCookie()
    {
        SeedConfiguredAdmin("epoch-admin@example.test", "epoch-admin-provider", "EpochAdmin123!");

        using var adminClient = CreateClient();
        var login = await LoginAsync(adminClient, "epoch-admin@example.test", "EpochAdmin123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var before = await adminClient.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // O email do ADMIN sai das Definições: a sessão antiga deixa de
        // estar autorizada, sem depender de qualquer Setup.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var setting = db.ModuleSettings.Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
            db.ModuleSettings.Remove(setting);
            db.SaveChanges();
        }

        var after = await adminClient.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/login", after.Headers.Location?.AbsolutePath);
    }

    // ------------------------------------------------------------------
    // Foundation service wiring
    // ------------------------------------------------------------------

    [Fact]
    public void AdminFoundationServices_RegisterWithScopedLifetime()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        // A semântica de autorização e as duas vistas de leitura da
        // fundação registam scoped (consomem o DmoDbContext por pedido).
        var authorization = scope1.ServiceProvider.GetRequiredService<Core.Admin.IAdminAuthorizationService>();
        Assert.IsType<Infrastructure.Admin.AdminAuthorizationService>(authorization);
        Assert.Same(authorization, scope1.ServiceProvider.GetRequiredService<Core.Admin.IAdminAuthorizationService>());
        Assert.NotSame(authorization, scope2.ServiceProvider.GetRequiredService<Core.Admin.IAdminAuthorizationService>());

        Assert.IsType<Infrastructure.Admin.TemplateAdminService>(
            scope1.ServiceProvider.GetRequiredService<Core.Admin.ITemplateAdminService>());
        Assert.IsType<Infrastructure.Admin.UserAdminService>(
            scope1.ServiceProvider.GetRequiredService<Core.Admin.IUserAdminService>());
    }
}
