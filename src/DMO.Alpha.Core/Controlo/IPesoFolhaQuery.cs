using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Loads the initial production-scoped Peso sheet without creating or changing
/// records. Comparisons, history, documents, and HTTP presentation are separate.
/// </summary>
public interface IPesoFolhaQuery
{
    Task<PesoFolhaInicial> CarregarAsync(
        int controloId,
        int? pesoPedidoId,
        decimal? caloteSimulada,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only sheet data. A missing current Peso remains null after a tool change;
/// the caller can then offer creation or explicit association. Tool facts describe
/// the current production, while the selected Peso retains its frozen values.
/// </summary>
public sealed record PesoFolhaInicial(
    ProductionContextReadModel? ProductionContext,
    IReadOnlyList<PesoReadModel> Pesos,
    PesoReadModel? Peso,
    PesoToolFacts? ToolFacts,
    PesoCapSupportReadModel? CapSupport,
    IReadOnlyList<UnassociatedPesoCandidate> AssociationCandidates);
