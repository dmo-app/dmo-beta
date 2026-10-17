using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Gravação das definições por módulo: o upsert por chave canónica. A
/// hora de viragem das Boquilhas é um inteiro 0–23 (nunca texto livre).
/// O MAPA DO ENCAMINHAMENTO por máquina (Boquilhas:RepairerB1..C3 — o
/// Owner: «reparadores sao as entidades para onde enviamos as
/// boquilhas»): o valor é o Id de uma ENTIDADE REPARADORA existente (o
/// dropdown da Admin oferece apenas os existentes; o Id que já não
/// resolve é recusa tipada). O VALOR VAZIO é LIMPAR a rota daquela
/// máquina — um estado válido e truthful: a saída de uma máquina sem
/// rota grava na mesma, sem reparador (a app nunca bloqueia).
/// </summary>
public sealed class SaveModuleSettingHandler : ISaveModuleSettingHandler
{
    private readonly DmoDbContext _db;

    public SaveModuleSettingHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<SaveModuleSettingOutcome> HandleAsync(
        SaveModuleSetting command,
        CancellationToken cancellationToken = default)
    {
        // O mapa do encaminhamento por máquina (as 6 chaves canónicas
        // B1..C3): o valor vazio LIMPA a rota — o caso único em que uma
        // definição pode ficar vazia (o estado «sem reparador» é válido;
        // as restantes chaves nunca ficam em branco por omissão).
        if (BqRepairerRouting.TryGetMachineCode(command.SettingKey, out _))
        {
            if (string.IsNullOrWhiteSpace(command.SettingValue))
            {
                var toClear = await _db.ModuleSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == command.SettingKey, cancellationToken);
                if (toClear is not null)
                {
                    _db.ModuleSettings.Remove(toClear);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                return new SaveModuleSettingOutcome(SaveModuleSettingResult.Success);
            }

            if (!int.TryParse(command.SettingValue.Trim(), out var repairerId)
                || !await _db.BqRepairers.AsNoTracking()
                    .AnyAsync(r => r.Id == repairerId, cancellationToken))
            {
                return new SaveModuleSettingOutcome(SaveModuleSettingResult.BoquilhasInvalidRepairerId);
            }

            await UpsertAsync(command.SettingKey, command.SettingValue.Trim(), cancellationToken);
            return new SaveModuleSettingOutcome(SaveModuleSettingResult.Success);
        }

        if (string.IsNullOrWhiteSpace(command.SettingValue))
        {
            return new SaveModuleSettingOutcome(SaveModuleSettingResult.EmptyValue);
        }

        switch (command.SettingKey)
        {
            case ModuleSettingKeys.BoquilhasDayChangeHour:
                // A hora de viragem: um inteiro 0–23 (o input da UI é um
                // select de horas; o texto inválido é recusa tipada).
                if (!int.TryParse(command.SettingValue.Trim(), out var hour) || hour is < 0 or > 23)
                {
                    return new SaveModuleSettingOutcome(SaveModuleSettingResult.BoquilhasInvalidHour);
                }

                break;

            default:
                return new SaveModuleSettingOutcome(SaveModuleSettingResult.UnknownSettingKey);
        }

        await UpsertAsync(command.SettingKey, command.SettingValue.Trim(), cancellationToken);
        return new SaveModuleSettingOutcome(SaveModuleSettingResult.Success);
    }

    /// <summary>O upsert por chave canónica (idempotente).</summary>
    private async Task UpsertAsync(
        string settingKey,
        string settingValue,
        CancellationToken cancellationToken)
    {
        var existing = await _db.ModuleSettings
            .FirstOrDefaultAsync(s => s.SettingKey == settingKey, cancellationToken);

        if (existing is null)
        {
            _db.ModuleSettings.Add(new ModuleSetting
            {
                SettingKey = settingKey,
                SettingValue = settingValue
            });
        }
        else
        {
            existing.SettingValue = settingValue;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
