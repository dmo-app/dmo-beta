using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests;

/// <summary>
/// Live Supabase Auth integration tests for <see cref="SupabaseAuthenticationProvider"/>.
///
/// These tests are skipped unless the Supabase project credentials are supplied through
/// environment variables. They exercise the real REST endpoints and prove that the
/// production authentication provider works against a live provider.
///
/// CURRENT key system staging: the existing <c>DMO_SUPABASE_*</c> environment
/// variable names are kept (handoff staging convention) but their VALUES are the
/// project's current-system keys — <c>DMO_SUPABASE_ANON_KEY</c> carries the
/// PUBLISHABLE key, <c>DMO_SUPABASE_SERVICE_KEY</c> carries the SECRET key. No
/// legacy anon/service_role key is expected or used; these tests prove the
/// password-grant WITH THE PUBLISHABLE KEY and the admin endpoint pattern with
/// the SECRET key against the real service (the live authority for the
/// non-JWT current-key header pattern).
/// </summary>
public sealed class SupabaseAuthenticationProviderIntegrationTests
{
    private static string? SupabaseUrl => Environment.GetEnvironmentVariable("DMO_SUPABASE_URL");
    private static string? PublishableKey => Environment.GetEnvironmentVariable("DMO_SUPABASE_ANON_KEY");
    private static string? SecretKey => Environment.GetEnvironmentVariable("DMO_SUPABASE_SERVICE_KEY");

    private static string? AdminEmail => Environment.GetEnvironmentVariable("DMO_SUPABASE_ADMIN_EMAIL");
    private static string? AdminPassword => Environment.GetEnvironmentVariable("DMO_SUPABASE_ADMIN_PASSWORD");
    private static string? AdminProviderUserId => Environment.GetEnvironmentVariable("DMO_SUPABASE_ADMIN_PROVIDER_USER_ID");

    private static string? UserOperatorId => Environment.GetEnvironmentVariable("DMO_SUPABASE_USER_OPERATOR_ID");
    private static string? UserPassword => Environment.GetEnvironmentVariable("DMO_SUPABASE_USER_PASSWORD");
    private static string? UserProviderUserId => Environment.GetEnvironmentVariable("DMO_SUPABASE_USER_PROVIDER_USER_ID");

    private static SupabaseAuthenticationProvider CreateProvider()
    {
        var options = Options.Create(new SupabaseAuthOptions
        {
            Url = SupabaseUrl!,
            PublishableKey = PublishableKey!,
            SecretKey = SecretKey!
        });

        var client = new HttpClient();
        return new SupabaseAuthenticationProvider(client, options, NullLogger<SupabaseAuthenticationProvider>.Instance);
    }

    private static async Task<(string UserId, string Email)> CreateTestUserAsync(string operatorId, string password)
    {
        var email = $"{operatorId}@dmo.local";

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{SupabaseUrl!.TrimEnd('/')}/auth/v1/admin/users");
        request.Headers.Add("apikey", SecretKey!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey!);
        request.Content = JsonContent.Create(new
        {
            email,
            password,
            email_confirm = true,
            user_metadata = new { requires_password_change = true }
        });

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var userId = document.RootElement.GetProperty("id").GetString()!;

        return (userId, email);
    }

    private static async Task DeleteTestUserAsync(string userId)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{SupabaseUrl!.TrimEnd('/')}/auth/v1/admin/users/{userId}");
        request.Headers.Add("apikey", SecretKey!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey!);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task NormalUser_CreatedViaAdminApi_CanAuthenticateAndChangeTemporaryPassword()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(PublishableKey), "DMO_SUPABASE_ANON_KEY (current publishable key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(SecretKey), "DMO_SUPABASE_SERVICE_KEY (current secret key) is not configured.");

        var operatorId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        const string initialPassword = "TempP@ss1!";
        const string newPassword = "RealP@ss2!";

        string? providerUserId = null;

        try
        {
            (_, var email) = await CreateTestUserAsync(operatorId, initialPassword);

            using (var provider = CreateProvider())
            {
                var first = await provider.AuthenticateAsync(email, initialPassword);

                Assert.True(first.Success);
                Assert.True(first.IsTemporaryPassword);
                Assert.Equal(email, first.Email);
                providerUserId = first.ProviderUserId;
            }

            using (var provider = CreateProvider())
            {
                var changeResult = await provider.ChangePasswordAsync(providerUserId!, newPassword);
                Assert.Equal(ChangePasswordResult.Success, changeResult);
            }

            using (var provider = CreateProvider())
            {
                var second = await provider.AuthenticateAsync(email, newPassword);
                Assert.True(second.Success);
                Assert.False(second.IsTemporaryPassword);
                Assert.Equal(providerUserId, second.ProviderUserId);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(providerUserId))
            {
                await DeleteTestUserAsync(providerUserId);
            }
        }
    }

    [SkippableFact]
    public async Task AuthenticateAsync_Admin_WithValidCredentials_Succeeds()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(PublishableKey), "DMO_SUPABASE_ANON_KEY (current publishable key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(AdminEmail), "DMO_SUPABASE_ADMIN_EMAIL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(AdminPassword), "DMO_SUPABASE_ADMIN_PASSWORD is not configured.");

        using var provider = CreateProvider();
        var result = await provider.AuthenticateAsync(AdminEmail!, AdminPassword!);

        Assert.True(result.Success);
        Assert.Equal(AdminEmail, result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderUserId));
    }

    [SkippableFact]
    public async Task AuthenticateAsync_Admin_WithInvalidPassword_Fails()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(PublishableKey), "DMO_SUPABASE_ANON_KEY (current publishable key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(AdminEmail), "DMO_SUPABASE_ADMIN_EMAIL is not configured.");

        using var provider = CreateProvider();
        var result = await provider.AuthenticateAsync(AdminEmail!, "ThisIsNotThePassword123!");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [SkippableFact]
    public async Task IdentityExistsAsync_ExistingAdmin_ReturnsTrue()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(SecretKey), "DMO_SUPABASE_SERVICE_KEY (current secret key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(AdminProviderUserId), "DMO_SUPABASE_ADMIN_PROVIDER_USER_ID is not configured.");

        using var provider = CreateProvider();
        var exists = await provider.IdentityExistsAsync(AdminProviderUserId!);

        Assert.True(exists);
    }

    [SkippableFact]
    public async Task IdentityExistsAsync_RandomGuid_ReturnsFalse()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(SecretKey), "DMO_SUPABASE_SERVICE_KEY (current secret key) is not configured.");

        using var provider = CreateProvider();
        var exists = await provider.IdentityExistsAsync(Guid.NewGuid().ToString());

        Assert.False(exists);
    }

    [SkippableFact]
    public async Task ChangePasswordAsync_ExistingUser_ChangesPasswordAndAllowsAuthentication()
    {
        Skip.If(string.IsNullOrWhiteSpace(SupabaseUrl), "DMO_SUPABASE_URL is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(PublishableKey), "DMO_SUPABASE_ANON_KEY (current publishable key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(SecretKey), "DMO_SUPABASE_SERVICE_KEY (current secret key) is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(UserOperatorId), "DMO_SUPABASE_USER_OPERATOR_ID is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(UserPassword), "DMO_SUPABASE_USER_PASSWORD is not configured.");
        Skip.If(string.IsNullOrWhiteSpace(UserProviderUserId), "DMO_SUPABASE_USER_PROVIDER_USER_ID is not configured.");

        var email = $"{UserOperatorId}@dmo.local";
        var newPassword = $"NewP@ss_{Guid.NewGuid():N}";

        using (var provider = CreateProvider())
        {
            var before = await provider.AuthenticateAsync(email, UserPassword!);
            Assert.True(before.Success);
        }

        try
        {
            using var serviceProvider = CreateProvider();
            var changeResult = await serviceProvider.ChangePasswordAsync(UserProviderUserId!, newPassword);
            Assert.Equal(ChangePasswordResult.Success, changeResult);

            using var anonProvider = CreateProvider();
            var after = await anonProvider.AuthenticateAsync(email, newPassword);
            Assert.True(after.Success);
            Assert.Equal(UserProviderUserId, after.ProviderUserId);
        }
        finally
        {
            // Restore the original password so the test is repeatable.
            using var serviceProvider = CreateProvider();
            await serviceProvider.ChangePasswordAsync(UserProviderUserId!, UserPassword!);
        }
    }
}
