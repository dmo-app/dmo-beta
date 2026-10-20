namespace DMO.Alpha.Core.Authentication;

public sealed record AuthenticateResult
{
    public bool Success { get; init; }

    public string? ProviderUserId { get; init; }

    public string? Email { get; init; }

    public bool IsTemporaryPassword { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>
    /// O serviço de autenticação não respondeu ou recusou o pedido da app
    /// (sem ligação, tempo esgotado, limite de pedidos, erro do serviço,
    /// chaves da app erradas): a palavra-passe pode estar certa, por isso o
    /// ecrã não diz que está errada (sweep de 19/10/2026, 5.1).
    /// </summary>
    public bool ProviderUnavailable { get; init; }

    public static AuthenticateResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static AuthenticateResult Unavailable(string message) =>
        new() { Success = false, ProviderUnavailable = true, ErrorMessage = message };

    public static AuthenticateResult Succeeded(string providerUserId, string email, bool isTemporaryPassword = false) =>
        new() { Success = true, ProviderUserId = providerUserId, Email = email, IsTemporaryPassword = isTemporaryPassword };
}
