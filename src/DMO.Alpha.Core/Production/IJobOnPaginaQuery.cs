namespace DMO.Alpha.Core.Production;

/// <summary>
/// Small read-only lookups the Job On page needs around its main reads: an
/// older production asked by link, the overlap warning, the MF reference of a
/// tool, the production that already has the same reference and number, the
/// saved calote and the machine code. Nothing is created or changed.
/// </summary>
public interface IJobOnPaginaQuery
{
    /// <summary>
    /// The most recent production of the reference (start-of-reference rule)
    /// with this number, optionally narrowed by exact reference and machine;
    /// null when none. Serves a history link older than the dropdown.
    /// </summary>
    Task<JobOnProductionOption?> ProducaoDoHistoricoAsync(
        string reference,
        string productionNumber,
        string? exactReference,
        int? machineId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The overlap warning recomputed from the persisted production; null when
    /// the production no longer exists or nothing overlaps.
    /// </summary>
    Task<OverlapWarning?> AvisoDeSobreposicaoAsync(
        int jobOnId,
        string machineCode,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken = default);

    /// <summary>The reference of this MF tool; null when it is not an MF.</summary>
    Task<string?> ReferenciaDaMfAsync(string mfToolId, CancellationToken cancellationToken = default);

    /// <summary>The MF reference a CM fits (its associated MF reference, else its own); null when it is not a CM.</summary>
    Task<string?> ReferenciaMfDaCmAsync(string cmToolId, CancellationToken cancellationToken = default);

    /// <summary>The production with this exact reference (upper case) and number, with its tools and dates.</summary>
    Task<JobOnExistente?> ProducaoComReferenciaENumeroAsync(
        string reference,
        string productionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>The calote saved on a production; null when it has none or does not exist.</summary>
    Task<decimal?> CaloteGuardadaAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>The code of a machine; null when it does not exist.</summary>
    Task<string?> CodigoDaMaquinaAsync(int machineId, CancellationToken cancellationToken = default);

    /// <summary>The machine id of a production; 0 when it does not exist.</summary>
    Task<int> MaquinaDoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);
}

/// <summary>A saved production as the create form compares it against what was submitted.</summary>
public sealed record JobOnExistente(
    int Id,
    int MachineId,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    decimal? CaloteMm,
    string? Cm,
    string? Mf,
    string? Bq);
