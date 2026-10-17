namespace DMO.Alpha.Infrastructure.Authentication;

internal sealed class InMemoryAccount
{
    public required string Identifier { get; init; }

    public required string ProviderUserId { get; init; }

    public string? Email { get; init; }

    public required string Password { get; set; }

    public bool IsTemporaryPassword { get; set; }
}
