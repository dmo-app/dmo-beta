using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DMO.Alpha.Infrastructure.Authentication.Local;

/// <summary>
/// O login da própria app (Dmo:AuthenticationProvider = Local): confirma a
/// palavra-passe contra a tabela LocalCredentials da base PostgreSQL, sem
/// Supabase, para a app poder correr no portátil-servidor ou noutro sítio.
/// Faz também o provisionamento do Admin → Utilizadores (criar, repor e
/// apagar), com as mesmas regras do Supabase: a palavra-passe temporária
/// obriga a escolher uma nova no primeiro login.
/// </summary>
public sealed class LocalAuthenticationProvider : IAuthenticationProvider, IUserProvisioningProvider
{
    private readonly DmoDbContext _db;
    private readonly ILogger<LocalAuthenticationProvider> _logger;
    private readonly TimeProvider _time;

    public LocalAuthenticationProvider(DmoDbContext db, ILogger<LocalAuthenticationProvider> logger, TimeProvider? time = null)
    {
        _db = db;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public async Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(password);

        try
        {
            var email = Normalize(identifier);
            var credential = await _db.LocalCredentials.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

            if (credential is null || !LocalPasswordHasher.Verify(password, credential.PasswordHash))
            {
                _logger.LogWarning("Local authentication failed for {Identifier}", identifier);
                return AuthenticateResult.Failed("Invalid credentials.");
            }

            return AuthenticateResult.Succeeded(credential.ProviderUserId, credential.Email, credential.IsTemporaryPassword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Local authentication unavailable for {Identifier}", identifier);
            return AuthenticateResult.Unavailable("Authentication provider unavailable.");
        }
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        var credential = await _db.LocalCredentials.FirstOrDefaultAsync(c => c.ProviderUserId == providerUserId, cancellationToken);
        if (credential is null)
        {
            return ChangePasswordResult.IdentityNotFound;
        }

        credential.PasswordHash = LocalPasswordHasher.Hash(newPassword);
        credential.IsTemporaryPassword = false;
        credential.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
        return ChangePasswordResult.Success;
    }

    public async Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        var email = Normalize(identifier);
        return await _db.LocalCredentials.AnyAsync(
            c => c.ProviderUserId == identifier || c.Email == email, cancellationToken);
    }

    public async Task<UserProvisioningCreateResult> CreateAsync(string email, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(email);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        var normalized = Normalize(email);
        if (await _db.LocalCredentials.AnyAsync(c => c.Email == normalized, cancellationToken))
        {
            return UserProvisioningCreateResult.EmailExists();
        }

        var providerUserId = Guid.NewGuid().ToString();
        _db.LocalCredentials.Add(new LocalCredential
        {
            ProviderUserId = providerUserId,
            Email = normalized,
            PasswordHash = LocalPasswordHasher.Hash(temporaryPassword),
            IsTemporaryPassword = true,
            UpdatedAt = _time.GetUtcNow()
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return UserProvisioningCreateResult.Success(providerUserId);
        }
        catch (DbUpdateException ex)
        {
            // Duas criações ao mesmo tempo com o mesmo email: o índice único decide.
            _logger.LogWarning(ex, "Local user provisioning failed for {Email}", email);
            return UserProvisioningCreateResult.EmailExists();
        }
    }

    public async Task<UserProvisioningResetResult> ResetAsync(string providerUserId, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        var credential = await _db.LocalCredentials.FirstOrDefaultAsync(c => c.ProviderUserId == providerUserId, cancellationToken);
        if (credential is null)
        {
            return UserProvisioningResetResult.IdentityNotFound();
        }

        credential.PasswordHash = LocalPasswordHasher.Hash(temporaryPassword);
        credential.IsTemporaryPassword = true;
        credential.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
        return UserProvisioningResetResult.Success();
    }

    public async Task<UserProvisioningDeleteResult> DeleteAsync(string providerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        var credential = await _db.LocalCredentials.FirstOrDefaultAsync(c => c.ProviderUserId == providerUserId, cancellationToken);
        if (credential is null)
        {
            return UserProvisioningDeleteResult.IdentityNotFound();
        }

        _db.LocalCredentials.Remove(credential);
        await _db.SaveChangesAsync(cancellationToken);
        return UserProvisioningDeleteResult.Success();
    }
}
