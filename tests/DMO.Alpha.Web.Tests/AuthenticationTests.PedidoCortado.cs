using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests
{
    [Fact]
    public async Task AdminPasswordChange_ClientLeavesAfterTheProviderChange_TheOtherAdminSessionsStillEnd()
    {
        // Deep sweep (18/10/2026): o ADMIN muda a palavra-passe e o browser
        // sai (fecha o separador, a rede cai) depois de o provider a mudar.
        // A versão da sessão subia com o token do pedido, já cancelado, e as
        // outras sessões do ADMIN continuavam abertas.
        SeedAdmin("cortado-admin@example.test", "provider-admin-cortado", "AdminOld123!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAdminEmailSettings>(provider => new AbortsBeforeTheBump(
                ActivatorUtilities.CreateInstance<AdminEmailSettings>(provider),
                provider.GetRequiredService<IHttpContextAccessor>()))));
        var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true };
        using var otherDevice = factory.CreateClient(options);
        Assert.Equal("/Admin/Users", (await LoginAsync(otherDevice, "cortado-admin@example.test", "AdminOld123!")).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/Admin/Users")).StatusCode);
        using var owner = factory.CreateClient(options);
        Assert.Equal("/Admin/Users", (await LoginAsync(owner, "cortado-admin@example.test", "AdminOld123!")).Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(owner, "/change-password");
        try
        {
            await owner.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
                ("newPassword", "AdminNew456!"), ("confirmPassword", "AdminNew456!"),
                ("__RequestVerificationToken", token)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            // O cliente foi-se embora: é o browser a sair da página.
        }

        // O servidor continua depois de o cliente sair: a outra sessão cai.
        HttpResponseMessage? after = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(100);
            after = await otherDevice.GetAsync("/Admin/Users");
            if (after.StatusCode == HttpStatusCode.Redirect) break;
        }

        Assert.Equal(HttpStatusCode.Redirect, after!.StatusCode);
        Assert.Contains("/login", after.Headers.Location?.OriginalString);
    }

    /// <summary>Corta o pedido logo antes de subir a versão da sessão do ADMIN.</summary>
    private sealed class AbortsBeforeTheBump(IAdminEmailSettings inner, IHttpContextAccessor accessor) : IAdminEmailSettings
    {
        public Task<string?> GetAdminEmailAsync(CancellationToken cancellationToken = default) =>
            inner.GetAdminEmailAsync(cancellationToken);

        public Task<SaveAdminEmailResult> SaveAdminEmailAsync(string email, CancellationToken cancellationToken = default) =>
            inner.SaveAdminEmailAsync(email, cancellationToken);

        public Task<bool> TryClaimAdminEmailAsync(string email, CancellationToken cancellationToken = default) =>
            inner.TryClaimAdminEmailAsync(email, cancellationToken);

        public Task<(string? Email, int SessionVersion)> GetAdminSessionAsync(CancellationToken cancellationToken = default) =>
            inner.GetAdminSessionAsync(cancellationToken);

        public Task<int> BumpAdminSessionVersionAsync(CancellationToken cancellationToken = default)
        {
            accessor.HttpContext?.Abort();
            return inner.BumpAdminSessionVersionAsync(cancellationToken);
        }
    }
}
