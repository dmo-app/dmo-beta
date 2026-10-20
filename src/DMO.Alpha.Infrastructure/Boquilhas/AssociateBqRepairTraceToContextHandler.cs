using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Associa um bq_repair_trace_id existente (ainda não resolvido) ao seu
/// bq_id (contexto BQ de produção existente). Define BqContextId no trace
/// existente e LIMPA a âncora direta temporária (ToolId = null): preserva o
/// bq_repair_trace_id e todos os movimentos; após a associação a Tool é
/// alcançada via bq_id → BqContext.ToolId. Não recria nem copia o trace;
/// não altera JobOn nem BqContext; a associação dá apenas contexto de
/// produção — não arranca, para, fecha nem reabre o trace. Não escolhe
/// automaticamente qual trace não resolvido associar: o caller indica o
/// trace explicitamente. Um trace já associado nunca é reassociado (uma
/// produção posterior nunca toma posse de um trace anterior) e um bq_id
/// nunca recebe um segundo trace (um bq_id tem um bq_repair_trace_id).
/// </summary>
public sealed class AssociateBqRepairTraceToContextHandler : IAssociateBqRepairTraceToContextHandler
{
    private readonly DmoDbContext _db;

    public AssociateBqRepairTraceToContextHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<AssociateBqRepairTraceToContextOutcome> HandleAsync(
        AssociateBqRepairTraceToContext command,
        CancellationToken cancellationToken = default)
    {
        // ASSOCIAR AO MESMO TEMPO (deep sweep, 18/10/2026, I3-M-8): dois
        // pedidos liam o registo ainda por associar e gravavam os dois, ficando
        // o último; um 1.º movimento de outra produção com a mesma BQ adotava
        // o registo e a associação gravava por cima. O trinco do registo (o
        // mesmo do movimento) antes de o ler: o segundo espera e já o vê
        // associado.
        await using var transaction = await BqTraceLock.AcquireAsync(
            _db, command.BqRepairTraceId, cancellationToken);

        // 1. O trace tem de existir.
        var trace = await _db.BqRepairTraces
            .FirstOrDefaultAsync(t => t.Id == command.BqRepairTraceId, cancellationToken);
        if (trace is null)
        {
            return Refused(AssociateBqRepairTraceToContextResult.TraceNotFound);
        }

        // 2. O bq_id (BqContext) tem de existir.
        var context = await _db.BqContexts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.BqContextId, cancellationToken);
        if (context is null)
        {
            return Refused(AssociateBqRepairTraceToContextResult.BqContextNotFound);
        }

        // 3. Um trace já associado nunca é reassociado: os retornos tardios
        //    ficam no trace de origem e uma produção posterior nunca toma
        //    posse de um trace anterior.
        if (trace.BqContextId is not null)
        {
            return Refused(AssociateBqRepairTraceToContextResult.TraceAlreadyAssociated);
        }

        // 4. Um bq_id tem um único bq_repair_trace_id: recusa tipada limpa
        //    em vez de violar o índice único persistido.
        var contextAlreadyTraced = await _db.BqRepairTraces
            .AsNoTracking()
            .AnyAsync(t => t.BqContextId == command.BqContextId, cancellationToken);
        if (contextAlreadyTraced)
        {
            return Refused(AssociateBqRepairTraceToContextResult.BqContextAlreadyTraced);
        }

        // 5. A correlação trace ↔ bq_id faz-se pelo mesmo tool_id canónico:
        //    o bq_id referencia a mesma Tool física à qual o trace está
        //    ancorado. A comparação usa a âncora direta do trace ANTES da
        //    associação (é limpa no sucesso); um trace não resolvido tem
        //    sempre a âncora preenchida.
        if (context.ToolId != trace.ToolId)
        {
            return Refused(AssociateBqRepairTraceToContextResult.TraceToolDoesNotMatchBqContextTool);
        }

        // Associação: o bq_id é resolvido no trace existente e a âncora
        // direta temporária é LIMPA — a Tool passa a alcançar-se via
        // bq_id → BqContext.ToolId. O bq_repair_trace_id e os movimentos
        // são preservados; nada é recriado e nada é escrito no Job On nem
        // no BqContext.
        trace.BqContextId = command.BqContextId;
        trace.ToolId = null;
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        // O outcome reporta o tool_id EFETIVO já em mão (context.ToolId, o
        // tool_id canónico alcançado via bq_id) para se manter
        // auto-descritivo, SEM reintroduzir uma âncora direta persistida: a
        // coluna ToolId do trace continua null.
        return new AssociateBqRepairTraceToContextOutcome(
            AssociateBqRepairTraceToContextResult.Success,
            new AssociatedBqRepairTrace(trace.Id, context.ToolId, trace.BqContextId!.Value));
    }

    private static AssociateBqRepairTraceToContextOutcome Refused(
        AssociateBqRepairTraceToContextResult result) => new(result);
}
