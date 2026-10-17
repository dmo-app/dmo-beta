using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// Cria um registo de reparação de Boquilhas (bq_repair_trace_id) ancorado a
/// um tool_id canónico existente no registo Ferramentas. O trace nasce com
/// bq_id não resolvido (null): nenhum Job On é inferido, exigido ou
/// criado. O Tool.Type não é um gate comportamental — apenas a existência
/// da Tool no registo canónico é verificada; a linha DUMMY (REMOVED) NÃO
/// é utilizável (excluída por constante: nunca é uma Tool real de
/// reparação). Os FATOS HUMANOS da tool (referência + lote) são congelados
/// no trace à criação como evidência histórica — sobrevivem à
/// edição/eliminação da Tool. Existe no máximo um trace pré-produção não
/// resolvido por tool_id: se já existir um (âncora direta tool_id, bq_id
/// ainda null), o handler REUTILIZA-o e devolve o bq_repair_trace_id
/// existente em vez de criar um segundo. Sob concorrência (violação do
/// índice único parcial) a exceção nunca escapa: o trace vencedor é
/// re-consultado e reutilizado.
/// </summary>
public sealed class CreateBqRepairTraceHandler : ICreateBqRepairTraceHandler
{
    private readonly DmoDbContext _db;

    public CreateBqRepairTraceHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<CreateBqRepairTraceOutcome> HandleAsync(
        CreateBqRepairTrace command,
        CancellationToken cancellationToken = default)
    {
        // Guarda de entrada: com ToolId agora nullable, um argumento
        // nulo/vazio casaria com uma linha órfã via IS NULL. Um tool_id vazio
        // ou não resolvido é um caso de NEGÓCIO, não um erro de programação:
        // devolve recusa tipada (ToolNotFound) em vez de lançar, consistente
        // com os handlers irmãos que nunca lançam em violações de negócio.
        if (string.IsNullOrEmpty(command.ToolId))
        {
            return Refused(CreateBqRepairTraceResult.ToolNotFound);
        }

        // O tool_id tem de resolver a Tool canónica no registo Ferramentas
        // — a linha DUMMY (REMOVED) nunca é utilizável. Os factos humanos
        // (referência/lote) são carregados para o congelamento à criação.
        var tool = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == command.ToolId
                && t.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return Refused(CreateBqRepairTraceResult.ToolNotFound);
        }

        // No máximo um trace pré-produção não resolvido por tool_id: se já
        // existir um (âncora direta tool_id, bq_id ainda null), reutiliza-o
        // em vez de criar um segundo.
        var existing = await _db.BqRepairTraces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.ToolId == command.ToolId && t.BqContextId == null,
                cancellationToken);
        if (existing is not null)
        {
            return Reused(existing);
        }

        // Novo bq_repair_trace_id canónico, ancorado ao tool_id, sem
        // contexto de produção — com os FATOS HUMANOS da tool congelados
        // como evidência histórica (o trace mostra-os mesmo depois de a
        // Tool ser editada ou eliminada).
        var trace = new BqRepairTrace
        {
            ToolId = command.ToolId,
            BqContextId = null,
            ToolReferenceFrozen = tool.Reference,
            ToolLotFrozen = tool.Lot
        };
        _db.BqRepairTraces.Add(trace);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } pg
            && pg.ConstraintName is "IX_BqRepairTraces_ToolId" or null)
        {
            // Concorrência: o índice único parcial (ToolId WHERE
            // "BqContextId" IS NULL) foi violado por uma criação
            // simultânea. O filtro restringe este catch À violação desse
            // índice único (UniqueViolation 23505 na constraint esperada, ou
            // sem nome de constraint reportado) — qualquer outra falha de
            // SaveChanges não é engolida e propaga. Nunca deixa escapar a
            // exceção de negócio: destaca a tentativa, re-consulta e
            // reutiliza o trace que ganhou a corrida.
            _db.Entry(trace).State = EntityState.Detached;
            var winner = await _db.BqRepairTraces
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.ToolId == command.ToolId && t.BqContextId == null,
                    cancellationToken);
            if (winner is not null)
            {
                return Reused(winner);
            }

            throw;
        }

        return new CreateBqRepairTraceOutcome(
            CreateBqRepairTraceResult.Success,
            new CreatedBqRepairTrace(trace.Id, trace.ToolId, trace.BqContextId));
    }

    private static CreateBqRepairTraceOutcome Reused(BqRepairTrace trace) =>
        new(
            CreateBqRepairTraceResult.ReusedExistingUnresolvedTrace,
            new CreatedBqRepairTrace(trace.Id, trace.ToolId, trace.BqContextId));

    private static CreateBqRepairTraceOutcome Refused(CreateBqRepairTraceResult result) =>
        new(result);
}
