using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// A troca da BQ de uma produção, a mesma no Editar do Job On e no «Alterar
/// lote» das Boquilhas (Diogo, 18/10/2026: «um tool_id nunca pode ter o mesmo
/// bq_id, senão o histórico não faz sentido»): o bq_id anterior fica com os
/// seus movimentos, na BQ antiga, ligado à produção por ReplacedJobOnId; a BQ
/// nova nasce num bq_id novo. Não grava: quem chama faz o SaveChanges.
/// </summary>
internal static class BqContextTroca
{
    public static async Task<BqContext> TrocarAsync(
        DmoDbContext db,
        JobOn jobOn,
        string toolId,
        CancellationToken cancellationToken)
    {
        var bq = jobOn.BqContext;
        if (bq is not null)
        {
            bq.JobOnId = null;
            bq.ReplacedJobOnId = jobOn.Id;
            bq.ReplacedAtUtc = DateTime.UtcNow;
        }

        // Voltar a uma BQ que esta produção já usou retoma o bq_id dela
        // (um bq_id por BQ e produção, com um só registo de reparação):
        // a produção não aparece duas vezes no histórico da BQ.
        var previous = await db.BqContexts.FirstOrDefaultAsync(
            c => c.ReplacedJobOnId == jobOn.Id && c.ToolId == toolId, cancellationToken);
        if (previous is not null)
        {
            previous.ReplacedJobOnId = null;
            previous.ReplacedAtUtc = null;
            previous.JobOnId = jobOn.Id;
            bq = previous;
        }
        else
        {
            bq = new BqContext { JobOnId = jobOn.Id, ToolId = toolId };
            db.BqContexts.Add(bq);
        }

        jobOn.BqContext = bq;
        return bq;
    }
}
