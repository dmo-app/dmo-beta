using System.Security.Cryptography;

namespace DMO.Alpha.Infrastructure.Authentication.Local;

/// <summary>
/// Cifra as palavras-passe do login Local com PBKDF2-SHA256 e sal aleatório.
/// Formato guardado: <c>pbkdf2-sha256$iterações$sal$chave</c> (Base64), para
/// se poder subir as iterações mais tarde sem invalidar as que já existem.
/// </summary>
public static class LocalPasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    internal const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// A palavra-passe cifrada veio do Supabase (bcrypt, «$2a$…»/«$2b$…»):
    /// quem entra com ela é cifrado de novo no formato da app.
    /// </summary>
    public static bool IsLegacy(string stored) => stored.StartsWith("$2", StringComparison.Ordinal);

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (IsLegacy(stored))
        {
            // O Supabase cifrou a palavra-passe já com o prefixo das curtas.
            try
            {
                return BCrypt.Net.BCrypt.Verify(SupabasePassword.ToProvider(password), stored);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
