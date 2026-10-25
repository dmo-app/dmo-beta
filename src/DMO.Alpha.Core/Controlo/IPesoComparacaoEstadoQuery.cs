using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Reads what the Peso comparison screen shows around the readings: the
/// production picker for a reference, the base Peso with its influence figures,
/// and the counter-mould candidates. Nothing is created or changed.
/// </summary>
public interface IPesoComparacaoEstadoQuery
{
    /// <summary>
    /// The two-step picker: the productions of the typed reference or, without
    /// one, of the requested production. A requested production stays selected
    /// only when it belongs to that reference.
    /// </summary>
    Task<PesoComparacaoProducoes> ProducoesDaReferenciaAsync(
        string? referenciaEscrita,
        int? producaoPedida,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The base Peso of the comparison and, when it has readings, the influence
    /// of each of its CMs and of the new pairs (null pairs: no new readings yet).
    /// </summary>
    Task<PesoComparacaoBaseLida> BaseAsync(
        int pesoBaseId,
        PesoComparacaoPares? pares,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The CM candidates: the reference of the initial Peso's CM first, then
    /// by reference.
    /// </summary>
    Task<IReadOnlyList<ToolCandidate>> ContraMoldesAsync(
        string? referenciaDoInicial,
        CancellationToken cancellationToken = default);
}

public sealed record PesoComparacaoProducoes(
    string? Referencia,
    bool SemProducoes,
    IReadOnlyList<ProductionPickItem> Producoes,
    int? SelecionadaJobOnId)
{
    public static PesoComparacaoProducoes Vazio { get; } =
        new(null, false, Array.Empty<ProductionPickItem>(), null);
}

public sealed record PesoComparacaoBaseLida(
    PesoReadModel? Base,
    IReadOnlyList<PesoComparacaoInfluencia> InfluenciaDaBase,
    IReadOnlyList<PesoComparacaoInfluencia> InfluenciaDasNovas)
{
    public static PesoComparacaoBaseLida Vazia { get; } = new(
        null, Array.Empty<PesoComparacaoInfluencia>(), Array.Empty<PesoComparacaoInfluencia>());
}
