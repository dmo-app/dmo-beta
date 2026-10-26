using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Authentication.Local;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests
{
    [Fact]
    public async Task LoginLocal_RepeatedFailures_WarnButNeverLockTheUser()
    {
        // Diogo, 26/10/2026: «não bloquear porque outra pessoa pode tentar
        // usar». O mesmo que UserLogin_RepeatedFailures_NeverLockTheUser,
        // mas com o login próprio da app (LocalAuthenticationProvider).
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthenticationProvider>();
            services.RemoveAll<IUserProvisioningProvider>();
            services.AddScoped<LocalAuthenticationProvider>();
            services.AddScoped<IAuthenticationProvider>(sp => sp.GetRequiredService<LocalAuthenticationProvider>());
            services.AddScoped<IUserProvisioningProvider>(sp => sp.GetRequiredService<LocalAuthenticationProvider>());
        }));

        using (var scope = factory.Services.CreateScope())
        {
            var local = scope.ServiceProvider.GetRequiredService<LocalAuthenticationProvider>();
            var providerUserId = (await local.CreateAsync("9891@dmo.local", "temporaria")).ProviderUserId!;
            await local.ChangePasswordAsync(providerUserId, "right");

            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "9891",
                Name = "Rita",
                ProviderUserId = providerUserId
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        for (var i = 1; i <= 6; i++)
        {
            var failed = await LoginAsync(client, "9891", "wrong");
            Assert.Contains(i < 5 ? "Credenciais inv" : "Consulte o ADMIN para rever a palavra-passe", await failed.Content.ReadAsStringAsync());
        }

        var ok = await LoginAsync(client, "9891", "right");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
    }
}
