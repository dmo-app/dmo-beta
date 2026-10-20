using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

public sealed class ToolLotQuantitySettings(DmoDbContext db) : IToolLotQuantitySettings
{
    // A chave da definição é o código da máquina, e o código não é único na
    // base: na lista (página e gravação) duas máquinas com o mesmo código
    // partilham uma linha, a de menor Id, em vez de rebentar com uma chave
    // repetida. A sugestão continua a ver cada máquina pelo seu Id.
    public async Task<IReadOnlyList<ToolLotQuantityDefault>> ListAsync(CancellationToken cancellationToken = default)
        => (await ReadPerMachineAsync(cancellationToken))
            .GroupBy(d => d.SettingKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

    private async Task<IReadOnlyList<ToolLotQuantityDefault>> ReadPerMachineAsync(CancellationToken cancellationToken)
    {
        var machines = await db.Machines.AsNoTracking().OrderBy(m => m.Code).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.Code }).ToListAsync(cancellationToken);
        var values = await db.ModuleSettings.AsNoTracking().Where(s => s.SettingKey.StartsWith(ToolLotQuantityKeys.Prefix))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);
        return machines.SelectMany(m => new[] { ToolType.Cm, ToolType.Mf, ToolType.Bq }.Select(type =>
        {
            var key = ToolLotQuantityKeys.For(type, m.Code);
            int? quantity = values.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
            return new ToolLotQuantityDefault(m.Id, m.Code, type, quantity);
        })).ToList();
    }

    public async Task<ToolLotQuantitySuggestion> SuggestAsync(ToolType type, IReadOnlyList<int> machineIds, CancellationToken cancellationToken = default)
    {
        var ids = machineIds.Distinct().ToList();
        var matches = (await ReadPerMachineAsync(cancellationToken)).Where(d => d.Type == type && ids.Contains(d.MachineId)).ToList();
        // Uma máquina sem total não apaga o total das outras (teste do agente
        // de 19/10/2026, T2945d11): B1 com 120 e B2 sem total sugere 120, e a
        // página avisa «Sem total do lote: B2.». Só totais diferentes ficam por escolher.
        var quantities = matches.Where(d => d.Quantity is not null).Select(d => d.Quantity).Distinct().ToList();
        var common = matches.Count == ids.Count && quantities.Count == 1 ? quantities[0] : null;
        return new ToolLotQuantitySuggestion(common, matches);
    }

    public async Task<SaveToolLotQuantitiesOutcome> SaveAsync(IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        var known = (await ListAsync(cancellationToken)).ToDictionary(d => d.SettingKey, StringComparer.Ordinal);
        var parsed = new Dictionary<string, int?>();
        var ignored = new List<IgnoredToolLotQuantity>();
        // Uma chave desconhecida (máquina que já não existe) recusa antes de
        // qualquer escrita. Uma quantidade ilegível ou negativa não recusa as
        // outras: fica de fora, a definição mantém o valor anterior e o
        // resultado diz qual foi ignorada.
        foreach (var (key, text) in values)
        {
            if (!known.TryGetValue(key, out var definition)) return SaveToolLotQuantitiesOutcome.Refused(SaveToolLotQuantitiesResult.UnknownDefinition);
            if (string.IsNullOrWhiteSpace(text)) { parsed[key] = null; continue; }
            if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0)
            {
                ignored.Add(new IgnoredToolLotQuantity(definition.MachineCode, definition.Type, text.Trim()));
                continue;
            }
            parsed[key] = number;
        }
        var keys = parsed.Keys.ToList();
        var existing = await db.ModuleSettings.Where(s => keys.Contains(s.SettingKey)).ToDictionaryAsync(s => s.SettingKey, cancellationToken);
        foreach (var (key, value) in parsed)
        {
            existing.TryGetValue(key, out var setting);
            if (value is null)
            {
                if (setting is not null) db.ModuleSettings.Remove(setting);
            }
            else if (setting is not null) setting.SettingValue = value.Value.ToString(CultureInfo.InvariantCulture);
            else db.ModuleSettings.Add(new ModuleSetting { SettingKey = key, SettingValue = value.Value.ToString(CultureInfo.InvariantCulture) });
        }
        await db.SaveChangesAsync(cancellationToken);
        return new SaveToolLotQuantitiesOutcome(SaveToolLotQuantitiesResult.Success, ignored);
    }
}
