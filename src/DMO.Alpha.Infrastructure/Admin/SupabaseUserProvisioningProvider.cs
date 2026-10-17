using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// The production provisioning seam backed by the Supabase Auth admin API
/// (USERS.md §2/§5): creates a new authentication identity (POST
/// <c>auth/v1/admin/users</c>) and re-establishes the credential of an
/// existing one (PUT <c>auth/v1/admin/users/{id}</c>, the SAME proven
/// pattern as the in-repo change-password path). DMO never becomes a
/// password store: the temporary bootstrap password is handed to the
/// provider and flagged with <c>user_metadata.requires_password_change</c>,
/// so the User's permanent password is chosen through the provider on the
/// forced change after login.
///
/// Both verbs authenticate with the configured SECRET key (apikey +
/// Bearer), exactly like the provider's existing admin-surface calls —
/// the publishable key is never accepted here. A duplicate email (422
/// «already been registered») is the typed <see cref="UserProvisioningCreateStatus.EmailExists"/>
/// refusal; every other non-success is <see cref="UserProvisioningCreateStatus.ProviderFailure"/>.
/// No test contacts a real Supabase project.
/// </summary>
public sealed class SupabaseUserProvisioningProvider : IUserProvisioningProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseAuthOptions _options;
    private readonly ILogger<SupabaseUserProvisioningProvider> _logger;

    public SupabaseUserProvisioningProvider(
        HttpClient httpClient,
        IOptions<SupabaseAuthOptions> options,
        ILogger<SupabaseUserProvisioningProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.Url))
        {
            _httpClient.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        }
    }

    public async Task<UserProvisioningCreateResult> CreateAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(email);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return UserProvisioningCreateResult.ProviderFailure();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/v1/admin/users");
            request.Headers.Add("apikey", _options.SecretKey);
            if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            request.Content = JsonContent.Create(new
            {
                email,
                password = SupabasePassword.ToProvider(temporaryPassword),

                // The identity is born usable for login (USERS.md §2: the
                // account is ready for first login) and flagged so the
                // provider's authentication result reports the temporary
                // state — the forced private-password change on first use.
                email_confirm = true,
                user_metadata = new Dictionary<string, object>
                {
                    ["requires_password_change"] = true
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                using var document = JsonDocument.Parse(json);
                var providerUserId = document.RootElement.GetProperty("id").GetString();

                if (string.IsNullOrWhiteSpace(providerUserId))
                {
                    _logger.LogWarning(
                        "Supabase user provisioning succeeded without a provider identity for {Email}",
                        email);
                    return UserProvisioningCreateResult.ProviderFailure();
                }

                return UserProvisioningCreateResult.Success(providerUserId);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 422 duplicado → EmailExists (recusa tipada): o email já é uma
            // identidade do provider. Qualquer outro 422 (formato inválido
            // de email/password, etc.) é ProviderFailure.
            if (System.Net.HttpStatusCode.UnprocessableEntity == response.StatusCode
                && body.Contains("already been registered", StringComparison.OrdinalIgnoreCase))
            {
                return UserProvisioningCreateResult.EmailExists();
            }

            _logger.LogWarning(
                "Supabase user provisioning failed for {Email}: {Status} {Body}",
                email, response.StatusCode, body);
            return UserProvisioningCreateResult.ProviderFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase user provisioning request failed for {Email}", email);
            return UserProvisioningCreateResult.ProviderFailure();
        }
    }

    public async Task<UserProvisioningResetResult> ResetAsync(
        string providerUserId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return UserProvisioningResetResult.ProviderFailure();
        }

        try
        {
            // The SAME admin-user update shape proven by the in-repo
            // change-password path: password + user_metadata, by provider
            // identity. The reset re-arms the temporary state (USERS.md
            // §5: the next login requires the private choice again).
            var path = $"auth/v1/admin/users/{providerUserId}";
            using var request = new HttpRequestMessage(HttpMethod.Put, path);
            request.Headers.Add("apikey", _options.SecretKey);
            if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            request.Content = JsonContent.Create(new
            {
                password = SupabasePassword.ToProvider(temporaryPassword),
                user_metadata = new Dictionary<string, object>
                {
                    ["requires_password_change"] = true
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return UserProvisioningResetResult.Success();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return UserProvisioningResetResult.IdentityNotFound();
            }

            _logger.LogWarning(
                "Supabase user access reset failed for {ProviderUserId}: {Status} {Body}",
                providerUserId, response.StatusCode, body);
            return UserProvisioningResetResult.ProviderFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase user access reset request failed for {ProviderUserId}", providerUserId);
            return UserProvisioningResetResult.ProviderFailure();
        }
    }

    public async Task<UserProvisioningDeleteResult> DeleteAsync(
        string providerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        if (!Uri.IsWellFormedUriString(_options.Url, UriKind.Absolute) || string.IsNullOrWhiteSpace(_options.SecretKey))
            return UserProvisioningDeleteResult.ProviderFailure();

        try
        {
            // Hard-delete exactly this Auth identity. Never search by email.
            using var request = new HttpRequestMessage(
                HttpMethod.Delete, $"auth/v1/admin/users/{Uri.EscapeDataString(providerUserId)}");
            request.Headers.Add("apikey", _options.SecretKey);
            if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return UserProvisioningDeleteResult.Success();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return UserProvisioningDeleteResult.IdentityNotFound();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Supabase identity deletion failed for {ProviderUserId}: {Status} {Body}",
                providerUserId, response.StatusCode, body);
            return UserProvisioningDeleteResult.ProviderFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase identity deletion failed for {ProviderUserId}", providerUserId);
            return UserProvisioningDeleteResult.ProviderFailure();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
