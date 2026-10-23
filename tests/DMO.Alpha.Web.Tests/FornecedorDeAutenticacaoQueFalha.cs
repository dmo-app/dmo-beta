using DMO.Alpha.Core.Authentication;

namespace DMO.Alpha.Web.Tests;

/// <summary>Fornecedor que lança sempre, para exercitar o ramo de erro da medição.</summary>
internal sealed class FornecedorDeAutenticacaoQueFalha : IAuthenticationProvider
{
    public Task<AuthenticateResult> AuthenticateAsync(string identifier, string password,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException("falha de teste");

    public Task<ChangePasswordResult> ChangePasswordAsync(string providerUserId, string newPassword,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> IdentityExistsAsync(string identifier,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
