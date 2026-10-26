using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// The reads of the open Job On sheet that used to live inside the Job On page:
/// the tool facts and the saved factory sheet, in the same order as before.
/// </summary>
public sealed class JobOnFichaQuery(
    IJobOnToolFactsQuery toolFacts,
    IJobOnFolhaQuery folha) : IJobOnFichaQuery
{
    public async Task<JobOnFichaResult> FichaAsync(
        ProductionContextReadModel jobOn,
        bool duplicar,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobOnToolFacts> facts = Array.Empty<JobOnToolFacts>();
        if (!duplicar)
        {
            var toolIds = new[] { jobOn.CmToolId, jobOn.MfToolId, jobOn.BqToolId }
                .Where(id => id is not null)
                .Select(id => id!)
                .ToList();
            if (toolIds.Count > 0)
            {
                facts = await toolFacts.FindAsync(
                    toolIds, jobOn.Machine, jobOn.JobOnId, jobOn.InicioEm, jobOn.FimEm, cancellationToken);
            }
        }

        // A FOLHA DA FÁBRICA gravada, lida uma vez para a ficha em leitura, para
        // o Editar e para o Duplicar (a ficha aparece sempre que há Job On
        // aberto, menos no Duplicar).
        var saved = await folha.ForJobOnAsync(jobOn.JobOnId, cancellationToken);

        return new JobOnFichaResult(facts, saved);
    }
}
