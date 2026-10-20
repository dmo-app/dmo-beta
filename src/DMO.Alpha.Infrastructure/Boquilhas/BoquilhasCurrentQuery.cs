using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// A produção corrente de cada máquina (A REGRA F4-4 COMPLETA — o corte B
/// aterrou): a corrente é a produção cujas DATAS DO JOB ON cobrem o DIA
/// OPERACIONAL (o Owner: «quando digo em produçao é a que esta hj em
/// maquina e segue as datas do jobon») — o dia operacional resolve pela
/// hora de viragem configurada (BoquilhasDayChange, o padrão 8h: antes
/// das 8 ainda é o dia de ontem). O FALLBACK de transição: sem produções
/// DATADAS a cobrir o dia, a última produção SEM janela definida (os
/// Job Ons anteriores ao corte B); sem nada, «Sem produção». A referência
/// da boquilha vem do BqContext do Job On (a HERANÇA do canal jobon),
/// com a REFERÊNCIA da tool (nunca o tool_id).
/// </summary>
public sealed class BoquilhasCurrentQuery : IBoquilhasCurrentQuery
{
    private readonly DmoDbContext _db;
    private readonly IModuleSettingsQuery _settings;
    private readonly TimeProvider _clock;

    public BoquilhasCurrentQuery(DmoDbContext db, IModuleSettingsQuery settings, TimeProvider? clock = null)
    {
        _db = db;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DateOnly> OperationalDayAsync(CancellationToken cancellationToken = default)
    {
        // O DIA OPERACIONAL pela hora de viragem configurada (o default 8h
        // — a manhã da fábrica; nunca a meia-noite).
        var configuredHour = await _settings.FindValueAsync(
            ModuleSettingKeys.BoquilhasDayChangeHour, cancellationToken);
        var dayChangeHour = int.TryParse(configuredHour, out var hour) ? hour : 8;
        return BoquilhasDayChange.ResolveOperationalDay(
            DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(_clock.GetUtcNow().UtcDateTime), dayChangeHour);
    }

    public async Task<IReadOnlyList<MachineCurrentBq>> ListAsync(CancellationToken cancellationToken = default)
    {
        var operationalDay = await OperationalDayAsync(cancellationToken);

        var machines = await _db.Machines
            .AsNoTracking()
            .OrderBy(m => m.Code)
            .Select(m => new { m.Id, m.Code })
            .ToListAsync(cancellationToken);

        // LEITURA EM LOTE (a lentidão do calendário, 16/10/2026): as
        // máquinas e as candidatas com a BQ (o BqContext, único por Job On)
        // e a referência da Tool numa consulta; a escolha por máquina faz-se
        // em memória com as mesmas regras. As candidatas: as produções
        // datadas que cobrem o dia operacional e as SEM datas (o fallback de
        // transição).
        var candidates = await (
            from j in _db.JobOns.AsNoTracking()
            where (j.InicioEm != null
                    && j.InicioEm <= operationalDay
                    && (j.FimEm == null || j.FimEm >= operationalDay))
                || j.InicioEm == null
            join c in _db.BqContexts.AsNoTracking() on j.Id equals c.JobOnId into contexts
            from c in contexts.DefaultIfEmpty()
            join t in _db.Tools.AsNoTracking().Where(x => x.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId) on (c != null ? c.ToolId : null) equals t.ToolId into tools
            from t in tools.DefaultIfEmpty()
            select new
            {
                j.Id,
                j.MachineId,
                j.ProductionNumber,
                j.Reference,
                Dated = j.InicioEm != null,
                BqContextId = c != null ? c.Id : (int?)null,
                BqToolId = c != null ? c.ToolId : null,
                BqReference = t != null ? t.Reference : null
            })
            .ToListAsync(cancellationToken);

        // A REGRA F4-4: a produção cuja janela temporal COBRE o dia
        // operacional desta máquina — a última que cobre (o mais recente
        // registro de uma sobreposição legítima); sem nenhuma, a última
        // produção SEM datas da máquina.
        var currentByMachine = candidates
            .GroupBy(j => j.MachineId)
            .ToDictionary(
                group => group.Key,
                group => group.Where(j => j.Dated).MaxBy(j => j.Id)
                    ?? group.Where(j => !j.Dated).MaxBy(j => j.Id));

        var result = new List<MachineCurrentBq>(machines.Count);
        foreach (var machine in machines)
        {
            var current = currentByMachine.GetValueOrDefault(machine.Id);

            // A REFERÊNCIA da boquilha (o fato humano; o tool_id nunca na
            // folha).
            result.Add(new MachineCurrentBq(
                machine.Id,
                machine.Code,
                current?.Id,
                current?.ProductionNumber,
                current?.Reference,
                current?.BqReference,
                current?.BqToolId,
                current?.BqContextId));
        }

        return result;
    }
}
