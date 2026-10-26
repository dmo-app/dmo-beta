using System.Net;
using DMO.Alpha.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests
{
    // Diogo, 26/10/2026: à 5.ª palavra-passe errada seguida, o ecrã manda
    // consultar o ADMIN (a conta nunca fica bloqueada).
    [Fact]
    public async Task Login_CincoPalavrasPasseErradas_MandaConsultarOAdmin()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IAuthenticationProvider, WrongPasswordAuthenticationProvider>()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        for (var i = 1; i <= 4; i++)
        {
            var html = WebUtility.HtmlDecode(await (await LoginAsync(client, "tentativas@example.com", "errada")).Content.ReadAsStringAsync());
            Assert.Contains("Credenciais inválidas.", html);
        }

        var quinta = WebUtility.HtmlDecode(await (await LoginAsync(client, "tentativas@example.com", "errada")).Content.ReadAsStringAsync());
        Assert.Contains("Consulte o ADMIN para rever a palavra-passe.", quinta);
    }

    private sealed class WrongPasswordAuthenticationProvider : IAuthenticationProvider
    {
        public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticateResult.Failed("Invalid credentials."));

        public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(ChangePasswordResult.ProviderError);

        public Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
