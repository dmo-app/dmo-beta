namespace DMO.Alpha.Core.Authentication;

public sealed record AuthenticateResult
{
    public bool Success { get; init; }

    public string? ProviderUserId { get; init; }

    public string? Email { get; init; }

    public bool IsTemporaryPassword { get; init; }

    public string? ErrorMessage { get; init; }

    public static AuthenticateResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static AuthenticateResult Succeeded(string providerUserId, string email, bool isTemporaryPassword = false) =>
        new() { Success = true, ProviderUserId = providerUserId, Email = email, IsTemporaryPassword = isTemporaryPassword };
}
