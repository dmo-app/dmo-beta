using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Authentication;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// The deterministic in-memory provisioning seam for local development and
/// integration tests (the same seam role <see cref="InMemoryAuthenticationProvider"/>
/// plays for authentication): the dictionary IS the provider's account set,
/// so a user provisioned here is immediately usable by the in-memory login
/// surface — the worked examples (create → temporary → first login forces
/// the private change) run end-to-end without any real Supabase project.
///
/// DMO never becomes a password store: the temporary password lives only
/// in this test seam (and, in production, in Supabase Auth). Duplicates are
/// the typed EmailExists refusal; an unknown identity on reset is the typed
/// IdentityNotFound refusal. No live-service behaviour is simulated beyond
/// what the seam contract promises.
/// </summary>
public sealed class InMemoryUserProvisioningProvider : IUserProvisioningProvider
{
    private readonly InMemoryAuthenticationProvider _authenticationProvider;

    public InMemoryUserProvisioningProvider(InMemoryAuthenticationProvider authenticationProvider)
    {
        _authenticationProvider = authenticationProvider;
    }

    public async Task<UserProvisioningCreateResult> CreateAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(email);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        // Duplicado → EmailExists (recusa tipada determinística): o email
        // já é uma identidade conhecida do seam.
        if (await _authenticationProvider.IdentityExistsAsync(email, cancellationToken))
        {
            return UserProvisioningCreateResult.EmailExists();
        }

        // A identidade nasce pronta a autenticar (login pass-through) e
        // marcada como temporária — o 1.º login força a mudança para a
        // password privada.
        var providerUserId = Guid.NewGuid().ToString();
        _authenticationProvider.AddAccount(
            email,
            providerUserId,
            temporaryPassword,
            email,
            isTemporaryPassword: true);

        return UserProvisioningCreateResult.Success(providerUserId);
    }

    public Task<UserProvisioningDeleteResult> DeleteAsync(
        string providerUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(_authenticationProvider.TryDeleteAccount(providerUserId)
            ? UserProvisioningDeleteResult.Success()
            : UserProvisioningDeleteResult.IdentityNotFound());

    public Task<UserProvisioningResetResult> ResetAsync(
        string providerUserId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerUserId);
        ArgumentException.ThrowIfNullOrEmpty(temporaryPassword);

        return Task.FromResult(
            _authenticationProvider.TrySetTemporaryPassword(providerUserId, temporaryPassword)
                ? UserProvisioningResetResult.Success()
                : UserProvisioningResetResult.IdentityNotFound());
    }
}
