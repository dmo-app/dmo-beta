namespace DMO.Alpha.Infrastructure.Authentication.Local;

/// <summary>
/// A palavra-passe de um utilizador quando o login é da própria app (modo
/// «Local», sem Supabase). Guarda-se só a palavra-passe cifrada
/// (<see cref="LocalPasswordHasher"/>), nunca a palavra-passe.
/// </summary>
public sealed class LocalCredential
{
    /// <summary>O mesmo valor de <c>Users.ProviderUserId</c>.</summary>
    public required string ProviderUserId { get; set; }

    /// <summary>O email em minúsculas: é com ele que o login procura.</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public bool IsTemporaryPassword { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Falhas seguidas desde o último login certo.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Até quando o login fica recusado depois de muitas falhas seguidas.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
}
