using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Os interruptores dos módulos na tabela ModuleSettings (uma linha
/// <c>Modulo:{chave}</c> por módulo mudado e a linha dos utilizadores de
/// teste). Sem linha, um módulo acabado fica ligado para todos e um módulo
/// em desenvolvimento fica com <paramref name="defaultEmDesenvolvimento"/>
/// (desligado na aplicação).
/// </summary>
public sealed class ModuleSwitches(
    DmoDbContext db,
    ModuleSwitchCache cache,
    ModuleSwitchMode defaultEmDesenvolvimento = ModuleSwitchMode.Desligado)
    : IModuleSwitches
{
    public async Task<ModuleSwitchState> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(out var cached))
        {
            return cached;
        }

        var state = await ReadAsync(cancellationToken);
        cache.Set(state);
        return state;
    }

    private async Task<ModuleSwitchState> ReadAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ModuleSettings
            .AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(ModuleSwitchKeys.Prefix) || s.SettingKey == ModuleSwitchKeys.Testers)
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);

        var modes = new Dictionary<string, ModuleSwitchMode>(StringComparer.Ordinal);
        foreach (var module in DmoCapabilities.Modules)
        {
            rows.TryGetValue(ModuleSwitchKeys.For(module.Key), out var value);
            modes[module.Key] = ModuleSwitchKeys.Parse(value)
                ?? (module.EmDesenvolvimento ? defaultEmDesenvolvimento : ModuleSwitchMode.Todos);
        }

        rows.TryGetValue(ModuleSwitchKeys.Testers, out var testers);
        var ids = (testers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .ToArray();

        return new ModuleSwitchState(modes, ids);
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<string, ModuleSwitchMode> modes,
        IReadOnlyList<Guid> testers,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var module in DmoCapabilities.Modules)
        {
            if (modes.TryGetValue(module.Key, out var mode))
            {
                values[ModuleSwitchKeys.For(module.Key)] = ModuleSwitchKeys.ToValue(mode);
            }
        }

        values[ModuleSwitchKeys.Testers] = string.Join(',', testers.Distinct());

        var keys = values.Keys.ToArray();
        var existing = await db.ModuleSettings
            .Where(s => keys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.SettingValue = value;
            }
            else
            {
                db.ModuleSettings.Add(new ModuleSetting { SettingKey = key, SettingValue = value });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        cache.Clear();
    }
}

/// <summary>
/// A última leitura dos interruptores, partilhada pelos pedidos (singleton):
/// cada página não volta à base por eles. Guardar limpa-a; noutra instância
/// da app a mudança vale ao fim de <see cref="Validade"/>.
/// </summary>
public sealed class ModuleSwitchCache
{
    public static readonly TimeSpan Validade = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private ModuleSwitchState? _state;
    private DateTime _readAt;

    public bool TryGet(out ModuleSwitchState state)
    {
        lock (_gate)
        {
            if (_state is not null && DateTime.UtcNow - _readAt < Validade)
            {
                state = _state;
                return true;
            }
        }

        state = null!;
        return false;
    }

    public void Set(ModuleSwitchState state)
    {
        lock (_gate)
        {
            _state = state;
            _readAt = DateTime.UtcNow;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _state = null;
        }
    }
}
