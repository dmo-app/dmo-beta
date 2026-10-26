namespace DMO.Alpha.Core.Production;

/// <summary>
/// The data reads of the sheet of an open Job On (the «ficha»): lot notes,
/// recorded changes, the delete preview, the technical facts of the tools and
/// the saved factory sheet. Read-only; nothing is created or changed. What the
/// page makes of them (capability checks, «O que mudou», the delete warning,
/// the sheet views) stays in the page.
/// </summary>
public interface IJobOnFichaQuery
{
    /// <summary>
    /// Reads the sheet data of <paramref name="jobOn"/>, in this order: the lot
    /// notes, the recorded changes, then — only when it is not a duplicate —
    /// the delete preview and, when the Job On has any CM, MF or BQ, the tool
    /// facts; the saved factory sheet last (also for a duplicate, which copies it).
    /// </summary>
    /// <param name="jobOn">The open Job On.</param>
    /// <param name="today">The factory day, for the delete preview.</param>
    /// <param name="duplicar">True when the page is composing the draft of a new production from this one.</param>
    Task<JobOnFichaResult> FichaAsync(
        ProductionContextReadModel jobOn,
        DateOnly today,
        bool duplicar,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The raw reads of the sheet of an open Job On. <see cref="DeletePreview"/> is
/// null for a duplicate (or when the Job On no longer exists); <see cref="ToolFacts"/>
/// is empty for a duplicate and for a Job On without tools.
/// </summary>
public sealed record JobOnFichaResult(
    IReadOnlyList<LotNoteView> LotNotes,
    IReadOnlyList<JobOnChangeView> Changes,
    JobOnDeletionPreview? DeletePreview,
    IReadOnlyList<JobOnToolFacts> ToolFacts,
    JobOnFolhaLida Folha);
