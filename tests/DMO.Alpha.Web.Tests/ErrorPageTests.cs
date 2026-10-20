using System.Net;
using System.Security.Claims;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed class ErrorPageTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Dmo:AuthenticationProvider", "Supabase");
            // An error page must render even when PostgreSQL is unavailable.
            builder.UseSetting("ConnectionStrings:DmoDatabase",
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1");
            builder.UseSetting("Dmo:DevConnectionProbe", "false");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath",
                Path.Combine(Path.GetTempPath(), "dmo-error-tests", Guid.NewGuid().ToString("N")));
            builder.ConfigureServices(services =>
                services.AddSingleton<IAuthenticationProvider, ThrowingAuthenticationProvider>());
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task ErrorPage_IsAnonymous_Uncached_AndIndependentOfDatabase(string method)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/Error"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Não foi possível concluir o pedido", html);
        Assert.Contains("Referência do erro", html);
        Assert.Contains("href=\"/login\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain("unavailable", html);
    }

    [Fact]
    public async Task ErrorPage_DoesNotRedirectWhenPasswordChangeIsRequired()
    {
        using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, PasswordChangePrincipalFilter>()));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("Referência do erro", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnhandledLoginException_RendersSafeErrorPage_InsteadOfMissingEndpoint()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var login = await client.GetStringAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(login)!;

        using var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "audit@example.invalid"), ("password", "test-only"),
            ("__RequestVerificationToken", token)));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Não foi possível concluir o pedido", html);
        Assert.Contains("Referência do erro", html);
        Assert.DoesNotContain(ThrowingAuthenticationProvider.ExceptionDetail, html);
        Assert.DoesNotContain(nameof(InvalidOperationException), html);
        Assert.DoesNotContain("Stack Trace", html);
    }

    private sealed class ThrowingAuthenticationProvider : IAuthenticationProvider
    {
        public const string ExceptionDetail = "internal-error-test-detail-must-not-be-rendered";

        public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(ExceptionDetail);

        public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IdentityExistsAsync(string identifier,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PasswordChangePrincipalFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString)], "error-test"));
                await continuation(context);
            });
            next(app);
        };
    }
}
