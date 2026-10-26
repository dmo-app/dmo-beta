using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// O ADMIN é o email guardado nas Definições: upsert da chave canónica
/// <see cref="ModuleSettingKeys.AdminEmail"/> na tabela ModuleSettings,
/// a mesma tabela das restantes definições.
/// </summary>
public sealed class AdminEmailSettings : IAdminEmailSettings
{
    private readonly DmoDbContext _db;

    public AdminEmailSettings(DmoDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string?> GetAdminEmailAsync(
        CancellationToken cancellationToken = default)
    {
        var value = await _db.ModuleSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        return Normalize(value);
    }

    /// <summary>O valor guardado tal como o ADMIN é comparado: sem espaços; vazio = sem ADMIN.</summary>
    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <inheritdoc />
    public async Task<SaveAdminEmailResult> SaveAdminEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var candidate = email?.Trim();
        if (candidate is null || !IsUsableEmail(candidate))
        {
            return SaveAdminEmailResult.InvalidEmail;
        }

        var existing = await _db.ModuleSettings
            .FirstOrDefaultAsync(s => s.SettingKey == ModuleSettingKeys.AdminEmail, cancellationToken);

        // Só um email com que já se entra: o do ADMIN atual ou o de um
        // utilizador criado em Admin → Utilizadores. Com um email sem conta,
        // a sessão do ADMIN atual caía no pedido seguinte e ninguém voltava
        // a entrar no Admin (sweep de 19/10/2026, AD-1).
        if (!AdminEmailRule.IsAdminEmail(candidate, Normalize(existing?.SettingValue))
            && !await HasUserWithEmailAsync(candidate, cancellationToken))
        {
            return SaveAdminEmailResult.NoAccount;
        }

        if (existing is null)
        {
            _db.ModuleSettings.Add(new ModuleSetting
            {
                SettingKey = ModuleSettingKeys.AdminEmail,
                SettingValue = candidate
            });
        }
        else
        {
            existing.SettingValue = candidate;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return SaveAdminEmailResult.Success;
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimAdminEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var candidate = email?.Trim();
        if (candidate is null || !IsUsableEmail(candidate))
        {
            return false;
        }

        // Só reivindica um campo VAZIO: nunca substitui um ADMIN já gravado
        // (dois primeiros logins em simultâneo — o segundo perde, sem 500).
        if (await _db.ModuleSettings.AsNoTracking()
                .AnyAsync(s => s.SettingKey == ModuleSettingKeys.AdminEmail, cancellationToken))
        {
            return false;
        }

        var claim = new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = candidate
        };
        _db.ModuleSettings.Add(claim);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Outro login gravou a chave entre a leitura e o insert.
            _db.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(string? Email, int SessionVersion)> GetAdminSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ModuleSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail
                || s.SettingKey == ModuleSettingKeys.AdminSessionVersion)
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);

        var email = rows.FirstOrDefault(r => r.SettingKey == ModuleSettingKeys.AdminEmail)?.SettingValue;
        var version = rows.FirstOrDefault(r => r.SettingKey == ModuleSettingKeys.AdminSessionVersion)?.SettingValue;
        return (Normalize(email), ParseVersion(version));
    }

    /// <inheritdoc />
    public async Task<int> BumpAdminSessionVersionAsync(
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.ModuleSettings
            .FirstOrDefaultAsync(s => s.SettingKey == ModuleSettingKeys.AdminSessionVersion, cancellationToken);
        var next = ParseVersion(existing?.SettingValue) + 1;
        if (existing is null)
        {
            _db.ModuleSettings.Add(new ModuleSetting
            {
                SettingKey = ModuleSettingKeys.AdminSessionVersion,
                SettingValue = next.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        else
        {
            existing.SettingValue = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return next;
    }

    // Um utilizador com este email, sem espaços e sem distinguir maiúsculas
    // (a conta no fornecedor de autenticação é criada com o mesmo email).
    private Task<bool> HasUserWithEmailAsync(string email, CancellationToken cancellationToken)
    {
        var lower = email.ToLowerInvariant();
        return _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == lower, cancellationToken);
    }

    private static int ParseVersion(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var version) ? version : 0;

    /// <summary>
    /// O formato mínimo de um email utilizável: texto de ambos os lados de
    /// um único '@' (o provider Auth é a autoridade real; aqui só se recusa
    /// o evidentemente inválido, sem inventar regras).
    /// </summary>
    public static bool IsUsableEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0
            && email.IndexOf('@', at + 1) < 0
            && at < email.Length - 1
            && email[^1] != '.';
    }
}
