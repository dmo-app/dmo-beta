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

    /// <summary>
    /// O formato mínimo de um email utilizável: texto de ambos os lados de
    /// um único '@' (o provider Auth é a autoridade real; aqui só se recusa
    /// o evidentemente inválido, sem inventar regras).
    /// </summary>
    private static bool IsUsableEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0
            && email.IndexOf('@', at + 1) < 0
            && at < email.Length - 1
            && email[^1] != '.';
    }
}
