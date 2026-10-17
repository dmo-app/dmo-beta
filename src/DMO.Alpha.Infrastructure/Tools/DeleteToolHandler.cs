using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Elimina uma Tool do registo canónico numa TRANSAÇÃO com ordem de
/// afirmações EXPLÍCITA (a ordenação de operações é explícita: o EF
/// Core não garante a ordenação interna de batching, pelo que cada fase
/// SaveChanges carrega exatamente um tipo de afirmação — a ordenação é
/// sempre determinante). Decisão literal do Owner (DUMMY tool_id):
/// (0) guardas — a Tool existe e não é a DUMMY; a linha DUMMY canónica
///     existe (semeada pela migração Baseline — sem ela a reatribuição é
///     impossível: erro de implantação LOUD, nunca silencioso);
/// (1) [UPDATE] backfill dos factos congelados em falta nos registos de
///     consumo que referem a tool (registos anteriores a estas colunas):
///     Pesos (CM/BQ), Pegamentos (CM/BQ/MF), traces — a referência/lote
///     da tool a apagar ficam congeladas como evidência histórica;
/// (2) [UPDATE] reatribuição à DUMMY: Pesos (CmToolId/BqToolId) e
///     Pegamentos (Cm/Bq/MfToolId) que referem a tool → "REMOVED";
/// (3) [UPDATE] traces com âncora direta à tool → ToolId = NULL (o
///     índice único parcial aceita NULLs; o trace e os movimentos ficam
///     intactos, os factos congelados ficam no trace);
/// (4) [DELETE] ligações Tool–máquina, linha de contexto técnico e a
///     Tool FISICAMENTE.
/// Os contextos de produção (Cm/Mf/BqContexts) e as notas de lote também
/// passam para a DUMMY na fase (2): desde a AddToolFksToProduction têm FK
/// Restrict para Tools (estudo da base de dados, 17/10/2026). No provider relacional a transação é real com
/// rollback integral; no InMemory (seam dev/test) as fases correm na
/// mesma ordem pinada, sem rollback (a atomicidade é a garantia do
/// provider relacional).
/// A ameaça real evitada: o delete que destrói o histórico — nenhum
/// registo de consumo é apagado, os factos humanos congelados sobrevivem
/// à tool.
/// </summary>
public sealed class DeleteToolHandler : IDeleteToolHandler
{
    private readonly DmoDbContext _db;

    public DeleteToolHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<DeleteToolOutcome> HandleAsync(
        DeleteTool command,
        CancellationToken cancellationToken = default)
    {
        // (0) Guardas: a Tool existe e não é a DUMMY (a tombstone recebe
        // reatribuições; nunca é eliminada).
        if (string.IsNullOrEmpty(command.ToolId)
            || command.ToolId == ToolRegistryConstants.RemovedToolId)
        {
            return Refused(DeleteToolResult.ToolNotFound);
        }

        var tool = await _db.Tools
            .FirstOrDefaultAsync(t => t.ToolId == command.ToolId, cancellationToken);
        if (tool is null)
        {
            return Refused(DeleteToolResult.ToolNotFound);
        }

        // A linha DUMMY canónica tem de existir: é o alvo da reatribuição
        // (semeada pela migração Baseline). Sem ela, a transação não pode
        // prometer a preservação do histórico — erro de implantação LOUD.
        var dummyExists = await _db.Tools
            .AsNoTracking()
            .AnyAsync(t => t.ToolId == ToolRegistryConstants.RemovedToolId, cancellationToken);
        if (!dummyExists)
        {
            throw new InvalidOperationException(
                "A linha DUMMY canónica (ToolId = \"REMOVED\") não existe no registo de Tools — " +
                "a migração Baseline não foi aplicada. A eliminação de uma Tool " +
                "com reatribuição de histórico é impossível sem a tombstone; nenhuma escrita foi feita.");
        }

        var relational = _db.Database.IsRelational();
        var transaction = relational
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // (1) [UPDATE] Backfill dos factos congelados em falta — os
            // registos de consumo criados antes destas colunas herdaram
            // NULL; a referência/lote CORRENTES da tool ficam congeladas
            // neles antes de qualquer reatribuição (evidência histórica).
            var pesos = await _db.Pesos
                .Where(p => p.CmToolId == tool.ToolId || p.BqToolId == tool.ToolId)
                .ToListAsync(cancellationToken);
            foreach (var peso in pesos)
            {
                if (peso.CmToolId == tool.ToolId)
                {
                    peso.CmToolReferenceFrozen ??= tool.Reference;
                    peso.CmToolLotFrozen ??= tool.Lot;
                }

                if (peso.BqToolId == tool.ToolId)
                {
                    peso.BqToolReferenceFrozen ??= tool.Reference;
                    peso.BqToolLotFrozen ??= tool.Lot;
                }
            }

            var pegamentos = await _db.Pegamentos
                .Where(p => p.CmToolId == tool.ToolId
                    || p.BqToolId == tool.ToolId
                    || p.MfToolId == tool.ToolId)
                .ToListAsync(cancellationToken);
            foreach (var registro in pegamentos)
            {
                if (registro.CmToolId == tool.ToolId)
                {
                    registro.CmToolReferenceFrozen ??= tool.Reference;
                    registro.CmToolLotFrozen ??= tool.Lot;
                }

                if (registro.BqToolId == tool.ToolId)
                {
                    registro.BqToolReferenceFrozen ??= tool.Reference;
                    registro.BqToolLotFrozen ??= tool.Lot;
                }

                if (registro.MfToolId == tool.ToolId)
                {
                    registro.MfToolReferenceFrozen ??= tool.Reference;
                    registro.MfToolLotFrozen ??= tool.Lot;
                }
            }

            var traces = await _db.BqRepairTraces
                .Where(t => t.ToolId == tool.ToolId)
                .ToListAsync(cancellationToken);
            foreach (var trace in traces)
            {
                trace.ToolReferenceFrozen ??= tool.Reference;
                trace.ToolLotFrozen ??= tool.Lot;
            }

            if (pesos.Count > 0 || pegamentos.Count > 0 || traces.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            // (2) [UPDATE] Reatribuição à DUMMY (decisão literal do
            // Owner): os registos de consumo que referem a tool passam a
            // apontar a tombstone — a FK Restrict fica satisfeita e os
            // registos ficam consultáveis a partir do registo canónico.
            foreach (var peso in pesos)
            {
                if (peso.CmToolId == tool.ToolId)
                {
                    peso.CmToolId = ToolRegistryConstants.RemovedToolId;
                }

                if (peso.BqToolId == tool.ToolId)
                {
                    peso.BqToolId = ToolRegistryConstants.RemovedToolId;
                }
            }

            foreach (var registro in pegamentos)
            {
                if (registro.CmToolId == tool.ToolId)
                {
                    registro.CmToolId = ToolRegistryConstants.RemovedToolId;
                }

                if (registro.BqToolId == tool.ToolId)
                {
                    registro.BqToolId = ToolRegistryConstants.RemovedToolId;
                }

                if (registro.MfToolId == tool.ToolId)
                {
                    registro.MfToolId = ToolRegistryConstants.RemovedToolId;
                }
            }

            // As produções que usaram a tool (contextos CM/MF/BQ e as notas
            // de lote) passam também para a DUMMY: a FK Restrict deixa de
            // permitir um tool_id pendurado. As notas de lote já congelam a
            // referência e o lote.
            var cmContexts = await _db.CmContexts.Where(c => c.ToolId == tool.ToolId).ToListAsync(cancellationToken);
            var mfContexts = await _db.MfContexts.Where(c => c.ToolId == tool.ToolId).ToListAsync(cancellationToken);
            var bqContexts = await _db.BqContexts.Where(c => c.ToolId == tool.ToolId).ToListAsync(cancellationToken);
            var lotNotes = await _db.JobOnLotNotes.Where(n => n.NewToolId == tool.ToolId).ToListAsync(cancellationToken);
            cmContexts.ForEach(c => c.ToolId = ToolRegistryConstants.RemovedToolId);
            mfContexts.ForEach(c => c.ToolId = ToolRegistryConstants.RemovedToolId);
            bqContexts.ForEach(c => c.ToolId = ToolRegistryConstants.RemovedToolId);
            lotNotes.ForEach(n => n.NewToolId = ToolRegistryConstants.RemovedToolId);

            if (pesos.Count > 0 || pegamentos.Count > 0
                || cmContexts.Count > 0 || mfContexts.Count > 0 || bqContexts.Count > 0 || lotNotes.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            // (3) [UPDATE] Traces com âncora direta à tool → NULL: o caso
            // canónico é o trace NÃO resolvido (BqContextId null); qualquer
            // âncora suja de trace já resolvido normaliza para o mesmo
            // estado pós-associação (a âncora direta é limpa na
            // associação). O índice único parcial aceita NULLs; trace e
            // movimentos ficam intactos, os factos congelados ficam.
            foreach (var trace in traces)
            {
                trace.ToolId = null;
            }

            if (traces.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            // (4) [DELETE] A ficha técnica é o registo da tool — PK 1:1
            // colidiria na DUMMY, pelo que TTC + ligações são apagados
            // COM a tool (os Pesos já congelaram o que consumiram); a
            // tool desaparece FISICAMENTE do registo.
            var links = await _db.ToolMachineLinks
                .Where(link => link.ToolId == tool.ToolId)
                .ToListAsync(cancellationToken);
            _db.ToolMachineLinks.RemoveRange(links);

            var technical = await _db.ToolTechnicalContexts
                .FirstOrDefaultAsync(t => t.ToolId == tool.ToolId, cancellationToken);
            if (technical is not null)
            {
                _db.ToolTechnicalContexts.Remove(technical);
            }

            _db.Tools.Remove(tool);
            await _db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return new DeleteToolOutcome(DeleteToolResult.Success);
    }

    private static DeleteToolOutcome Refused(DeleteToolResult result) => new(result);
}
