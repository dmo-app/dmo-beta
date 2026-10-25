using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Search;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// The comparison-screen reads that used to live in the Peso page: same calls,
/// same order, same results. Calls stay sequential (shared scoped DbContext).
/// </summary>
public sealed class PesoComparacaoEstadoQuery(
    IProductionPickerQuery productionPicker,
    IPesoQuery pesoQuery,
    IToolCandidateQuery toolCandidates) : IPesoComparacaoEstadoQuery
{
    public async Task<PesoComparacaoProducoes> ProducoesDaReferenciaAsync(
        string? referenciaEscrita,
        int? producaoPedida,
        CancellationToken cancellationToken = default)
    {
        var pedida = producaoPedida is int pedidaId
            ? await productionPicker.FindAsync(pedidaId, cancellationToken)
            : null;
        var referencia = referenciaEscrita ?? pedida?.Reference;
        if (referencia is null)
        {
            return PesoComparacaoProducoes.Vazio;
        }

        var producoes = (await productionPicker.ListByReferenceAsync(referencia, cancellationToken)).ToList();
        var semProducoes = producoes.Count == 0;

        // A produção pedida desta referência fica no select mesmo além das
        // mais recentes (o atalho da folha numa produção antiga), com a mesma
        // regra do select: o início da referência ou da parte BQ, nunca a meio.
        if (pedida is not null
            && producoes.All(p => p.JobOnId != pedida.JobOnId)
            && ReferenceMatch.Matches(pedida.Reference, referencia))
        {
            producoes.Add(pedida);
        }

        var selecionada = pedida is not null && producoes.Any(p => p.JobOnId == pedida.JobOnId)
            ? pedida.JobOnId
            : (int?)null;
        return new PesoComparacaoProducoes(referencia, semProducoes, producoes, selecionada);
    }

    public async Task<PesoComparacaoBaseLida> BaseAsync(
        int pesoBaseId,
        PesoComparacaoPares? pares,
        CancellationToken cancellationToken = default)
    {
        var peso = await pesoQuery.FindPesoAsync(pesoBaseId, cancellationToken);
        if (peso is not { Averages: { } mediasBase })
        {
            return new PesoComparacaoBaseLida(
                peso, Array.Empty<PesoComparacaoInfluencia>(), Array.Empty<PesoComparacaoInfluencia>());
        }

        var daBase = PesoComparacaoInfluenciaCalculation.DaBase(
            peso.Measurements.OrderBy(m => m.RowPosition)
                .Select(m => (m.CmSubject, m.GlassWeightGrams)).ToList());
        var dasNovas = pares is not null
            ? PesoComparacaoInfluenciaCalculation.Novas(
                pares.Rows, mediasBase.AverageGlassWeightGrams, peso.Measurements.Count)
            : Array.Empty<PesoComparacaoInfluencia>();
        return new PesoComparacaoBaseLida(peso, daBase, dasNovas);
    }

    public async Task<IReadOnlyList<ToolCandidate>> ContraMoldesAsync(
        string? referenciaDoInicial,
        CancellationToken cancellationToken = default) =>
        (await toolCandidates.FindCandidatesAsync(new ToolCandidateFilter(ToolType.Cm), cancellationToken))
            .OrderBy(t => referenciaDoInicial is { } referencia && t.Reference == referencia ? 0 : 1)
            .ThenBy(t => t.Reference, StringComparer.Ordinal)
            .ToList();
}
