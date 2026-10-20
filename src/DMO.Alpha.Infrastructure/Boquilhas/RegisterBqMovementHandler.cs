using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Regista um movimento real de Boquilhas num bq_repair_trace_id existente.
/// O movimento é acrescentado ao trace: não altera o Job On, não altera o
/// BqContext, não cria nem altera estados do trace e as datas de produção
/// não controlam o registo. A quantidade observada é registada na
/// totalidade — nunca truncada ou reescrita; um retorno que exceda a
/// quantidade explicável é aceite na totalidade (proibido rejeitar).
/// entrada_sem_reparacao é um token histórico distinto que, para efeitos de
/// contabilidade de quantidade, se comporta exatamente como entrada: reduz
/// a quantidade legitimamente fora e produz a mesma discrepância negativa
/// quando o retorno observado excede o explicável. Nenhum outro significado
/// (estado de reparação, qualidade, próxima ação, lifecycle) é inferido do
/// token. O trace já transporta o tool_id relevante; o registo não volta a
/// decidir se essa Tool é "realmente BQ" — o Tool.Type não é um gate
/// comportamental.
/// O REPARADOR DA SAÍDA (o Owner 2026-10-12, verbatim: «reparadores sao as
/// entidades para onde enviamos as boquilhas»): a saída NUNCA pergunta o
/// reparador ao operador (blueprint boquilhas/OVERVIEW.md L357) — o handler
/// RESOLVE PELA ROTA (L359-366): BQ tool_id → a máquina do contexto de
/// produção do trace, quando nenhuma foi escolhida pelo operador; antes de
/// produção, apenas uma compatibilidade única permite resolver a máquina) → a config máquina→reparador (ModuleSettings,
/// Boquilhas:Repairer&lt;código B1..C3&gt;) → O REPARADOR usado pela Saída,
/// gravado no movimento (BqRepairerId) com o NOME CONGELADO
/// (BqRepairerNameFrozen — L372-374: «The repairer that applied when the
/// real movement occurred remains part of that movement's historical
/// truth»; a mudança posterior da config NUNCA reescreve o movimento
/// antigo). SEM config para a máquina (ou a entidade já não existe): o
/// movimento grava na mesma SEM reparador (null — truthful, nunca
/// bloqueia). Os movimentos entrada/entrada_sem_reparacao não transportam
/// reparador: só a saída o define.
/// </summary>
public sealed class RegisterBqMovementHandler : IRegisterBqMovementHandler
{
    public const string Saida = "saida";
    public const string Entrada = "entrada";
    public const string EntradaSemReparacao = "entrada_sem_reparacao";

    private readonly DmoDbContext _db;
    private readonly IModuleSettingsQuery _settings;
    private readonly TimeProvider _clock;

    public RegisterBqMovementHandler(DmoDbContext db, IModuleSettingsQuery settings, TimeProvider? clock = null)
    {
        _db = db;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<RegisterBqMovementOutcome> HandleAsync(
        RegisterBqMovement command,
        CancellationToken cancellationToken = default)
    {
        // 1. O trace tem de existir.
        var trace = await _db.BqRepairTraces
            .FirstOrDefaultAsync(t => t.Id == command.BqRepairTraceId, cancellationToken);
        if (trace is null)
        {
            return Refused(RegisterBqMovementResult.TraceNotFound);
        }

        return await RegisterOnTraceAsync(trace, isNewTrace: false, command, cancellationToken);
    }

    /// <summary>
    /// O primeiro movimento cria o registo da produção (decisão do
    /// proprietário de 16/10/2026: abrir a máquina já não o cria). Com o
    /// registo já associado ao bq_id, é o registo normal. Sem ele, a mesma
    /// ancoragem do fluxo antigo (get-or-create pela ferramenta + associação
    /// ao bq_id): o registo pré-produção não resolvido da mesma ferramenta é
    /// reutilizado e associado; sem ele, nasce um registo já associado, com a
    /// referência e o lote congelados. O registo e o movimento gravam no
    /// mesmo SaveChanges; uma recusa não deixa registo nenhum.
    /// </summary>
    public Task<RegisterBqMovementOutcome> HandleForContextAsync(
        RegisterBqMovementForContext command,
        CancellationToken cancellationToken = default) =>
        HandleForContextAsync(command, cancellationToken, retried: false);

    private async Task<RegisterBqMovementOutcome> HandleForContextAsync(
        RegisterBqMovementForContext command,
        CancellationToken cancellationToken,
        bool retried)
    {
        var movement = new RegisterBqMovement(
            0,
            command.MovementType,
            command.Quantity,
            command.RegisteredByOperatorId,
            command.OccurredAtUtc,
            command.MachineId);

        var associated = await _db.BqRepairTraces
            .FirstOrDefaultAsync(t => t.BqContextId == command.BqContextId, cancellationToken);
        if (associated is not null)
        {
            return await RegisterOnTraceAsync(associated, isNewTrace: false, movement, cancellationToken);
        }

        // O bq_id e a ferramenta dele têm de existir (a linha DUMMY nunca é
        // utilizável) — as mesmas condições do get-or-create do trace.
        var context = await _db.BqContexts
            .AsNoTracking()
            .Where(c => c.Id == command.BqContextId)
            .Select(c => new { c.Id, c.ToolId })
            .FirstOrDefaultAsync(cancellationToken);
        if (context is null)
        {
            return Refused(RegisterBqMovementResult.TraceNotFound);
        }

        var tool = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == context.ToolId
                && t.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return Refused(RegisterBqMovementResult.TraceToolNotRegistered);
        }

        var trace = await _db.BqRepairTraces
            .FirstOrDefaultAsync(t => t.ToolId == context.ToolId && t.BqContextId == null, cancellationToken);
        var isNewTrace = trace is null;
        if (trace is null)
        {
            trace = new BqRepairTrace
            {
                ToolReferenceFrozen = tool.Reference,
                ToolLotFrozen = tool.Lot
            };
            _db.BqRepairTraces.Add(trace);
        }

        // A associação ao bq_id (como o AssociateBqRepairTraceToContextHandler):
        // o bq_id fica no registo e a âncora direta é limpa.
        trace.BqContextId = context.Id;
        trace.ToolId = null;

        try
        {
            var outcome = await RegisterOnTraceAsync(trace, isNewTrace, movement, cancellationToken);
            if (outcome.Result != RegisterBqMovementResult.Success)
            {
                // Movimento recusado: nada fica por gravar (nem o registo).
                var entry = _db.Entry(trace);
                if (isNewTrace)
                {
                    entry.State = EntityState.Detached;
                }
                else
                {
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                }
            }

            return outcome;
        }
        catch (TraceClaimedByAnotherProduction) when (!retried)
        {
            // Concorrência: outro pedido associou o registo pré-produção a
            // outra produção com a mesma BQ enquanto este esperava o
            // trinco. Nada deste pedido ficou gravado; volta a resolver o
            // registo (agora fica com um registo próprio).
            _db.ChangeTracker.Clear();
            return await HandleForContextAsync(command, cancellationToken, retried: true);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is Npgsql.PostgresException
            {
                SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
            } pg
            && pg.ConstraintName is "IX_BqRepairTraces_BqContextId" or "IX_BqRepairTraces_ToolId" or null)
        {
            // Concorrência: outro pedido criou o registo deste bq_id ao mesmo
            // tempo. Nada deste pedido ficou gravado; o movimento vai para o
            // registo vencedor.
            _db.ChangeTracker.Clear();
            var winner = await _db.BqRepairTraces
                .FirstOrDefaultAsync(t => t.BqContextId == command.BqContextId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return await RegisterOnTraceAsync(winner, isNewTrace: false, movement, cancellationToken);
        }
    }

    private async Task<RegisterBqMovementOutcome> RegisterOnTraceAsync(
        BqRepairTrace trace,
        bool isNewTrace,
        RegisterBqMovement command,
        CancellationToken cancellationToken)
    {
        // 2. O tool_id efetivo do trace: a âncora direta temporária quando
        //    o trace ainda está pré-produção (não resolvido); após a
        //    associação a âncora é limpa e a Tool alcança-se via
        //    bq_id → BqContext.ToolId. Apenas a existência no registo
        //    canónico é verificada — o Tool.Type não é um gate
        //    comportamental.
        var effectiveToolId = trace.ToolId;
        if (effectiveToolId is null && trace.BqContextId is int anchorContextId)
        {
            effectiveToolId = await _db.BqContexts
                .AsNoTracking()
                .Where(c => c.Id == anchorContextId)
                .Select(c => c.ToolId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (effectiveToolId is null)
        {
            return Refused(RegisterBqMovementResult.TraceToolNotRegistered);
        }

        var resolvedToolId = effectiveToolId;
        var tool = await _db.Tools
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ToolId == resolvedToolId && t.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId, cancellationToken);
        if (tool is null)
        {
            return Refused(RegisterBqMovementResult.TraceToolNotRegistered);
        }

        // 3. O tipo tem de ser um dos três tokens canónicos autorizados.
        var typeToken = command.MovementType;
        if (typeToken is not (Saida or Entrada or EntradaSemReparacao))
        {
            return Refused(RegisterBqMovementResult.UnknownMovementType);
        }

        var type = BqMovementTypeTokens.FromStorage(typeToken);

        // 4. Uma data real no futuro grava e a página avisa (Diogo,
        //    19/10/2026: «as datas têm de ser livres»; na beta.241 era
        //    recusada). A folga entre relógios não avisa no uso diário.
        var now = _clock.GetUtcNow();
        var occurredInTheFuture = command.OccurredAtUtc is DateTime occurredAtUtc
            && BqMovementOccurrence.IsInTheFuture(occurredAtUtc, now);

        // A produção determina a rota. Sem produção, só uma compatibilidade única
        // permite determinar uma máquina; várias não autorizam escolher a primeira.
        // Sem máquina/rota inequívoca, grava o movimento sem reparador.
        string? machineCode;
        if (command.MachineId is int selectedMachineId)
        {
            machineCode = await _db.Machines.AsNoTracking()
                .Where(m => m.Id == selectedMachineId).Select(m => m.Code)
                .SingleOrDefaultAsync(cancellationToken);
            if (machineCode is null) return Refused(RegisterBqMovementResult.MachineNotFound);
        }
        else
        {
            machineCode = await ResolveMachineCodeAsync(trace, resolvedToolId, cancellationToken);
        }
        int? repairerId = null;
        string? repairerNameFrozen = null;
        if (type == BqMovementType.Saida)
        {
            if (machineCode is not null)
            {
                var configuredRepairerId = await _settings.FindValueAsync(
                    BqRepairerRouting.SettingKeyFor(machineCode),
                    cancellationToken);

                if (configuredRepairerId is not null
                    && int.TryParse(configuredRepairerId, out var parsedRepairerId))
                {
                    var repairer = await _db.BqRepairers
                        .AsNoTracking()
                        .Where(r => r.Id == parsedRepairerId)
                        .Select(r => new { r.Id, r.Name })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (repairer is not null)
                    {
                        repairerId = repairer.Id;
                        repairerNameFrozen = repairer.Name;
                    }
                }
            }
        }

        // O movimento é acrescentado ao trace com a quantidade observada na
        // totalidade e o token de movimento distinto persistido. A DATA/HORA
        // REAL do movimento (o Owner 2026-10-14 — o backfill dos ~2 meses de
        // registos passados: a verdade do registo antigo, a app não estava
        // feita quando aconteceram) vem do comando, já em UTC (o campo «Data
        // e hora reais» do form); SEM data fornecida o agora do backend
        // manda — o uso diário fica intacto e a regra antiga contra a
        // falsificação mantém-se no default. O CreatedAtUtc é o AGORA
        // TÉCNICO do registo (a auditoria: sempre UtcNow, nunca reescrito
        // pela data real). O HISTÓRICO e os CONTADORES agrupam e ordenam
        // pela data real (OccurredAtUtc). O REPARADOR DA SAÍDA é o
        // resolvido pela rota, com o nome CONGELADO — a verdade histórica
        // do movimento.
        var registeredAtUtc = now.UtcDateTime;
        var persisted = new BqMovement
        {
            // A navegação liga o movimento ao registo novo, ainda por gravar.
            Trace = trace,
            Type = type,
            Quantity = command.Quantity,
            CreatedAtUtc = registeredAtUtc,
            OccurredAtUtc = command.OccurredAtUtc ?? registeredAtUtc,
            RegisteredByOperatorId = command.RegisteredByOperatorId,
            BqRepairerId = repairerId,
            BqRepairerNameFrozen = repairerNameFrozen,
            MachineCodeFrozen = machineCode
        };

        // Discrepância: a regra matched/unmatched recalculada em todo o
        // registo pela data real (um movimento com data antiga pode
        // explicar retornos já gravados). O novo movimento entra no fold antes do Add.
        // O trinco do registo serializa os pedidos simultâneos (BqTraceLock);
        // um registo novo ainda não tem linha nem movimentos.
        await using var transaction = isNewTrace
            ? null
            : await BqTraceLock.AcquireAsync(_db, trace.Id, cancellationToken);
        if (transaction is not null && _db.Entry(trace).Property(t => t.BqContextId).IsModified)
        {
            // O registo pré-produção é associado neste pedido: já com o
            // trinco, confirma que outro pedido não o associou entretanto a
            // outra produção (gravar por cima juntava as duas num registo).
            var storedContextId = await _db.BqRepairTraces
                .AsNoTracking()
                .Where(t => t.Id == trace.Id)
                .Select(t => t.BqContextId)
                .FirstOrDefaultAsync(cancellationToken);
            if (storedContextId is not null && storedContextId != trace.BqContextId)
            {
                throw new TraceClaimedByAnotherProduction();
            }
        }

        var traceMovements = isNewTrace
            ? new List<BqMovement>()
            : await _db.BqMovements
                .Where(m => m.BqRepairTraceId == trace.Id)
                .ToListAsync(cancellationToken);
        traceMovements.Add(persisted);
        BqDiscrepancyFold.Apply(traceMovements);

        _db.BqMovements.Add(persisted);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new RegisterBqMovementOutcome(
            RegisterBqMovementResult.Success,
            new RegisteredBqMovement(
                persisted.Id,
                trace.Id,
                typeToken,
                command.Quantity,
                persisted.Discrepancy),
            occurredInTheFuture);
    }

    private sealed class TraceClaimedByAnotherProduction : Exception;

    private static RegisterBqMovementOutcome Refused(RegisterBqMovementResult result) =>
        new(result);

    /// <summary>
    /// Máquina de fallback para chamadas sem seleção: produção ou compatibilidade única.
    /// </summary>
    private async Task<string?> ResolveMachineCodeAsync(
        BqRepairTrace trace,
        string effectiveToolId,
        CancellationToken cancellationToken)
    {
        // A máquina do CONTEXTO DE PRODUÇÃO (a do momento da saída — o
        // trace é do bq_id corrente dele, nunca de um contexto futuro).
        if (trace.BqContextId is int contextId)
        {
            var contextMachineCode = await _db.BqContexts
                .AsNoTracking()
                .Where(c => c.Id == contextId)
                .Select(c => c.JobOn != null ? c.JobOn.Machine.Code : c.ReplacedJobOn!.Machine.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (contextMachineCode is not null)
            {
                return contextMachineCode;
            }
        }

        // Compatibilidade não identifica a máquina real quando existem várias.
        var machines = await _db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => link.ToolId == effectiveToolId)
            .Select(link => link.Machine.Code)
            .Take(2)
            .ToListAsync(cancellationToken);
        return machines.Count == 1 ? machines[0] : null;
    }
}
