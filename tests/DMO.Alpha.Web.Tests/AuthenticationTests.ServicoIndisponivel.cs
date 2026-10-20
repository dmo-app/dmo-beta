using System.Net;
using DMO.Alpha.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests
{
    // Sweep de 19/10/2026, 5.1 (S-1): sem resposta do serviço de
    // autenticação, o ecrã dizia «Credenciais inválidas.» e a palavra-passe
    // podia estar certa. Agora diz que o serviço não respondeu.
    [Fact]
    public async Task Login_ServicoDeAutenticacaoSemResposta_DizQueOServicoNaoRespondeu()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IAuthenticationProvider, UnavailableAuthenticationProvider>()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var response = await LoginAsync(client, "operador@example.com", "talvez-certa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("O serviço de autenticação não respondeu. Tente de novo dentro de alguns minutos.", html);
        Assert.DoesNotContain("Credenciais inválidas.", html);
    }

    [Fact]
    public void Login_EmSupabase_OPedidoEsperaNoMaximo15Segundos()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dmo:AuthenticationProvider", "Supabase");
        });

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IAuthenticationProvider));
        Assert.Equal(TimeSpan.FromSeconds(15), http.Timeout);
    }

    private sealed class UnavailableAuthenticationProvider : IAuthenticationProvider
    {
        public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticateResult.Unavailable("Authentication provider unavailable."));

        public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(ChangePasswordResult.ProviderError);

        public Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
