using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// The reads of the open Job On sheet that used to live inside the Job On page.
/// Same reads as before, in the same order, without the capability checks the
/// page used to make between them (those do not touch the database again: the
/// page's capabilities are resolved once per request).
/// </summary>
public sealed class JobOnFichaQuery(
    JobOnLotNoteHandler lotNotes,
    JobOnChangeQuery changes,
    IDeleteJobOnHandler deleteJobOn,
    IJobOnToolFactsQuery toolFacts,
    IJobOnFolhaQuery folha) : IJobOnFichaQuery
{
    public async Task<JobOnFichaResult> FichaAsync(
        ProductionContextReadModel jobOn,
        DateOnly today,
        bool duplicar,
        CancellationToken cancellationToken = default)
    {
        var notes = await lotNotes.ForJobOnAsync(jobOn.JobOnId, cancellationToken);
        var changesOfJobOn = await changes.ForJobOnAsync(jobOn.JobOnId, cancellationToken);

        JobOnDeletionPreview? preview = null;
        IReadOnlyList<JobOnToolFacts> facts = Array.Empty<JobOnToolFacts>();
        if (!duplicar)
        {
            preview = await deleteJobOn.PreviewAsync(jobOn.JobOnId, today, cancellationToken);

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

        return new JobOnFichaResult(notes, changesOfJobOn, preview, facts, saved);
    }
}
