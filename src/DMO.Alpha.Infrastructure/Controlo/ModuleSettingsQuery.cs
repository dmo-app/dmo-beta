using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Leitura das definições por módulo: a chave canónica namespaced
/// (Controlo:… / Boquilhas:…) na tabela ModuleSettings — o schema não é
/// prescrito pelo Blueprint (CONFIGURATION.md L28).
/// </summary>
public sealed class ModuleSettingsQuery : IModuleSettingsQuery
{
    private readonly DmoDbContext _db;

    public ModuleSettingsQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
    {
        return await _db.ModuleSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == settingKey)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
