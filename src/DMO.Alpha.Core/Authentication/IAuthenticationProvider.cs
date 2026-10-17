namespace DMO.Alpha.Core.Authentication;

public interface IAuthenticationProvider
{
    Task<AuthenticateResult> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken = default);

    Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword, CancellationToken cancellationToken = default);

    Task<bool> IdentityExistsAsync(string identifier, CancellationToken cancellationToken = default);
}
