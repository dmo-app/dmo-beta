using DMO.Alpha.Core.Authentication;

namespace DMO.Alpha.Infrastructure.Authentication;

/// <summary>
/// Deterministic authentication provider seam for local development and integration tests.
/// Credentials are held only by this test seam; DMO product code never persists passwords.
/// </summary>
public sealed class InMemoryAuthenticationProvider : IAuthenticationProvider
{
    private readonly Dictionary<string, InMemoryAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public void AddAccount(string identifier, string providerUserId, string password, string? email = null, bool isTemporaryPassword = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(password);

        _accounts[identifier] = new InMemoryAccount
        {
            Identifier = identifier,
            ProviderUserId = providerUserId,
            Email = email ?? identifier,
            Password = password,
            IsTemporaryPassword = isTemporaryPassword
        };
    }

    public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(password);

        if (!_accounts.TryGetValue(identifier, out var account))
        {
            return Task.FromResult(AuthenticateResult.Failed("Invalid credentials."));
        }

        if (account.Password != password)
        {
            return Task.FromResult(AuthenticateResult.Failed("Invalid credentials."));
        }

        return Task.FromResult(AuthenticateResult.Succeeded(
            account.ProviderUserId,
            account.Email ?? identifier,
            account.IsTemporaryPassword));
    }

    public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        var account = _accounts.Values.FirstOrDefault(a =>
            string.Equals(a.ProviderUserId, providerUserId, StringComparison.Ordinal));

        if (account is null)
        {
            return Task.FromResult(ChangePasswordResult.IdentityNotFound);
        }

        account.Password = newPassword;
        account.IsTemporaryPassword = false;
        return Task.FromResult(ChangePasswordResult.Success);
    }

    public Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (_accounts.ContainsKey(identifier))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(_accounts.Values.Any(a =>
            string.Equals(a.ProviderUserId, identifier, StringComparison.OrdinalIgnoreCase)));
    }

    public void Clear()
    {
        _accounts.Clear();
    }

    public bool TryDeleteAccount(string providerUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        var entry = _accounts.FirstOrDefault(p =>
            string.Equals(p.Value.ProviderUserId, providerUserId, StringComparison.Ordinal));
        return entry.Key is not null && _accounts.Remove(entry.Key);
    }

    /// <summary>
    /// Provisioning seam (ADMIN reset flow, USERS.md §5): establishes a
    /// new TEMPORARY password for the account with the given provider
    /// identity, re-arming the forced private-password change. Unlike
    /// <see cref="ChangePasswordAsync"/> (the user's own private choice,
    /// which CLEARS the temporary flag), this is the deterministic in-memory
    /// analogue of the provider's admin reset. Returns false when no
    /// account carries that provider identity.
    /// </summary>
    public bool TrySetTemporaryPassword(string providerUserId, string temporaryPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        var account = _accounts.Values.FirstOrDefault(a =>
            string.Equals(a.ProviderUserId, providerUserId, StringComparison.Ordinal));

        if (account is null)
        {
            return false;
        }

        account.Password = temporaryPassword;
        account.IsTemporaryPassword = true;
        return true;
    }
}
