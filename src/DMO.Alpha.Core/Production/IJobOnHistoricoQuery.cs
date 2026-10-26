using DMO.Alpha.Core.Paging;

namespace DMO.Alpha.Core.Production;

/// <summary>
/// The Job On history search: turns the raw filter values of the page (reference,
/// machine, production, dates) into the cleaned filter, the production dropdown
/// and one page of results. Read-only; nothing is created or changed.
/// </summary>
public interface IJobOnHistoricoQuery
{
    /// <summary>
    /// Composes the history from the raw query values. Blank text counts as
    /// absent (the same trimming the page applies to every free-text value),
    /// so the page passes the values exactly as received. The reads, in order:
    /// the productions of the reference (only with a reference), the older
    /// production asked by link (only when it is not in the dropdown) and the
    /// page of results (only when the cleaned filter is not empty).
    /// </summary>
    /// <param name="machines">The machines the page already loaded; an unknown machine id does not filter.</param>
    Task<JobOnHistoricoResult> HistoricoAsync(
        string? referencia,
        int? maquina,
        string? producao,
        string? desde,
        string? ate,
        int? pagina,
        IReadOnlyList<MachineOption> machines,
        CancellationToken cancellationToken = default);
}

/// <summary>The separator of the production dropdown value, «nº|referência».</summary>
public static class JobOnHistorico
{
    public const char Separador = '|';
}

/// <summary>
/// The composed history: the cleaned filter values, the date warnings, the
/// production dropdown with the chosen production, and the page of results
/// (empty when no filter was given).
/// </summary>
public sealed record JobOnHistoricoResult(
    string? Reference,
    int? MachineId,
    DateOnly? Desde,
    DateOnly? Ate,
    IReadOnlyList<string> DateWarnings,
    IReadOnlyList<JobOnProductionOption> Productions,
    string? Production,
    string? ProductionReference,
    bool SearchApplied,
    PagedList<ProductionContextReadModel> Page);
