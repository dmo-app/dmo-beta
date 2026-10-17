using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DMO.Alpha.Core.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Authentication;

/// <summary>
/// Production authentication provider backed by Supabase Auth.
/// DMO never creates or stores passwords; Supabase owns the credential lifecycle.
/// </summary>
public sealed class SupabaseAuthenticationProvider : IAuthenticationProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseAuthOptions _options;
    private readonly ILogger<SupabaseAuthenticationProvider> _logger;

    public SupabaseAuthenticationProvider(HttpClient httpClient, IOptions<SupabaseAuthOptions> options, ILogger<SupabaseAuthenticationProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.Url))
        {
            _httpClient.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        }
    }

    public async Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(password);

        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.PublishableKey))
        {
            return AuthenticateResult.Failed("Supabase authentication is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/v1/token?grant_type=password");
            request.Headers.Add("apikey", _options.PublishableKey);
            if (!_options.PublishableKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PublishableKey);
            request.Content = JsonContent.Create(new { email = identifier, password = SupabasePassword.ToProvider(password) });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Supabase authentication failed for {Identifier}: {Status} {Body}",
                    identifier, response.StatusCode, json);
                return AuthenticateResult.Failed("Invalid credentials.");
            }

            using var document = JsonDocument.Parse(json);
            var user = document.RootElement.GetProperty("user");
            var providerUserId = user.GetProperty("id").GetString()!;
            var email = user.GetProperty("email").GetString()!;
            var isTemporaryPassword = false;

            if (user.TryGetProperty("user_metadata", out var metadata) &&
                metadata.ValueKind == JsonValueKind.Object)
            {
                if (metadata.TryGetProperty("requires_password_change", out var requiresChange))
                {
                    isTemporaryPassword = requiresChange.GetBoolean();
                }
            }

            return AuthenticateResult.Succeeded(providerUserId, email, isTemporaryPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase authentication request failed for {Identifier}", identifier);
            return AuthenticateResult.Failed("Authentication provider unavailable.");
        }
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return ChangePasswordResult.ProviderError;
        }

        try
        {
            var path = $"auth/v1/admin/users/{providerUserId}";
            using var request = new HttpRequestMessage(HttpMethod.Put, path);
            request.Headers.Add("apikey", _options.SecretKey);
            if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            request.Content = JsonContent.Create(new
            {
                password = SupabasePassword.ToProvider(newPassword),
                user_metadata = new Dictionary<string, object>
                {
                    ["requires_password_change"] = false
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return ChangePasswordResult.Success;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Supabase change-password failed for {ProviderUserId}: {Status} {Body}",
                providerUserId, response.StatusCode, body);

            // 422 / weak_password: the GoTrue password policy refused the new
            // password — a distinct, operator-visible outcome (2026-10-11: the
            // generic provider error hid the cause from the Owner).
            if ((int)response.StatusCode == 422 || body.Contains("weak_password", StringComparison.Ordinal))
            {
                return ChangePasswordResult.PolicyViolation;
            }

            return ChangePasswordResult.ProviderError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase change-password request failed for {ProviderUserId}", providerUserId);
            return ChangePasswordResult.ProviderError;
        }
    }

    public async Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return false;
        }

        // This provider treats identifiers that are UUIDs as provider-side user ids and uses
        // the admin-by-id endpoint. Other values are not resolvable without additional API support.
        if (!Guid.TryParse(identifier, out var providerUserId))
        {
            _logger.LogWarning("Supabase identity-existence check requires a provider user id (UUID); received {Identifier}", identifier);
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"auth/v1/admin/users/{providerUserId:n}");
            request.Headers.Add("apikey", _options.SecretKey);
            if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase identity-existence check failed for {Identifier}", identifier);
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
