namespace DMO.Alpha.Core.Production;

/// <summary>
/// The data reads of the sheet of an open Job On (the «ficha»): the technical
/// facts of the tools and the saved factory sheet. Read-only; nothing is
/// created or changed. What the page makes of them (the form state, the sheet
/// views) stays in the page.
/// </summary>
public interface IJobOnFichaQuery
{
    /// <summary>
    /// Reads the sheet data of <paramref name="jobOn"/>, in this order: the tool
    /// facts — only when it is not a duplicate and the Job On has any CM, MF or
    /// BQ — and the saved factory sheet (also for a duplicate, which copies it).
    /// </summary>
    /// <param name="jobOn">The open Job On.</param>
    /// <param name="duplicar">True when the page is composing the draft of a new production from this one.</param>
    Task<JobOnFichaResult> FichaAsync(
        ProductionContextReadModel jobOn,
        bool duplicar,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The raw reads of the sheet of an open Job On. <see cref="ToolFacts"/> is
/// empty for a duplicate and for a Job On without tools.
/// </summary>
public sealed record JobOnFichaResult(
    IReadOnlyList<JobOnToolFacts> ToolFacts,
    JobOnFolhaLida Folha);
