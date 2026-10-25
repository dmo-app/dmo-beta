namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Small read-only lookups the Peso page needs before it can pick a view:
/// which Controlo belongs to a production, which production belongs to a
/// Controlo, the machine codes of the Job On picker, and whether a saved
/// comparison already exists. Nothing is created or changed.
/// </summary>
public interface IPesoEscolhasQuery
{
    Task<int?> ControloDaProducaoAsync(int jobOnId, CancellationToken cancellationToken = default);

    Task<int?> ProducaoDoControloAsync(int controloId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> CodigosDeMaquinasAsync(CancellationToken cancellationToken = default);

    Task<bool> ComparacaoGuardadaExisteAsync(
        int comparacaoId,
        int pesoInicialId,
        CancellationToken cancellationToken = default);
}
