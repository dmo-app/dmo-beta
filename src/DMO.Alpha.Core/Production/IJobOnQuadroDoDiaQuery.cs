namespace DMO.Alpha.Core.Production;

/// <summary>
/// The Job On day board: one row per machine with the productions that cover
/// the chosen day, their lot notes and changes, and the next change on each
/// line. Read-only; nothing is created or changed.
/// </summary>
public interface IJobOnQuadroDoDiaQuery
{
    /// <summary>
    /// The finished rows of the board, in machine order. <paramref name="jobOns"/>
    /// is the window the page already loaded; the lot notes, the changes after
    /// start and (when <paramref name="diaEscolhido"/>) the next change per
    /// machine are read here, in that order.
    /// </summary>
    Task<IReadOnlyList<DayMachineRow>> QuadroAsync(
        IReadOnlyList<MachineOption> machines,
        IReadOnlyList<ProductionContextReadModel> jobOns,
        int ano,
        int mes,
        int dia,
        bool diaEscolhido,
        CancellationToken cancellationToken = default);
}
