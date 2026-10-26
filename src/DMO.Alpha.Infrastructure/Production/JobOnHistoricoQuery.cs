using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// The history composition that used to live inside the Job On page. Same
/// reads, in the same order, same warnings and same results as before.
/// </summary>
public sealed class JobOnHistoricoQuery(
    IJobOnListQuery jobOnList,
    IJobOnPaginaQuery paginaQuery) : IJobOnHistoricoQuery
{
    /// <summary>
    /// O HISTÓRICO abre em branco (decisão do proprietário, 16/10/2026): só
    /// lê produções depois de uma procura pela referência, máquina ou data.
    /// A procura de uma produção faz-se em dois passos: a referência (e a
    /// máquina, se quiser) e depois o dropdown só com as produções dela,
    /// lido só para ela. Uma data ilegível é ignorada com um aviso amarelo,
    /// não recusada.
    /// </summary>
    public async Task<JobOnHistoricoResult> HistoricoAsync(
        string? referencia,
        int? maquina,
        string? producao,
        string? desde,
        string? ate,
        int? pagina,
        IReadOnlyList<MachineOption> machines,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var historyReference = Sanitize(referencia);
        // Uma máquina que não existe não filtra.
        int? historyMachineId = maquina is int id && machines.Any(m => m.MachineId == id) ? id : null;
        var historyDesde = ParseHistoryDate(desde, "Desde", warnings);
        var historyAte = ParseHistoryDate(ate, "Até", warnings);
        if (historyDesde is { } d && historyAte is { } a && d > a)
        {
            (historyDesde, historyAte) = (a, d);
            warnings.Add("«Desde» era depois de «Até»: as datas foram trocadas.");
        }

        IReadOnlyList<JobOnProductionOption> historyProductions = Array.Empty<JobOnProductionOption>();
        string? historyProduction = null;
        string? historyProductionReference = null;
        if (historyReference is { } reference)
        {
            historyProductions = await jobOnList.ListProductionsOfReferenceAsync(reference, historyMachineId, cancellationToken);
            // A produção pedida: o nº e, quando o dropdown junta várias
            // referências, a referência dela. Um link antigo só com o nº
            // escolhe a mais recente com esse nº, nunca as duas juntas. Uma
            // produção que já não é desta referência (a referência mudou) não filtra.
            var (wanted, wantedReference) = ParseHistoryProduction(Sanitize(producao));
            var escolhida = wanted is null
                ? null
                : historyProductions.FirstOrDefault(p => p.ProductionNumber == wanted
                    && (wantedReference is null || p.Reference == wantedReference));
            // O dropdown só tem as últimas produções; uma mais antiga pedida
            // por link continua a filtrar e entra no dropdown.
            if (wanted is not null && escolhida is null)
            {
                escolhida = await paginaQuery.ProducaoDoHistoricoAsync(
                    reference, wanted, wantedReference, historyMachineId, cancellationToken);
                if (escolhida is not null)
                {
                    historyProductions = [.. historyProductions, escolhida];
                }
            }
            historyProduction = escolhida?.ProductionNumber;
            historyProductionReference = escolhida?.Reference;
        }

        var filter = new JobOnHistoryFilter(
            historyReference, historyMachineId, historyProduction, historyDesde, historyAte, historyProductionReference);
        var searchApplied = !filter.IsEmpty;
        var page = PagedList<ProductionContextReadModel>.Empty;
        if (searchApplied)
        {
            page = await jobOnList.SearchHistoryPageAsync(
                filter, pagina, cancellationToken: cancellationToken);
        }

        return new JobOnHistoricoResult(
            historyReference, historyMachineId, historyDesde, historyAte, warnings,
            historyProductions, historyProduction, historyProductionReference, searchApplied, page);
    }

    /// <summary>
    /// O valor pedido do dropdown das produções: «nº» (os links antigos e o
    /// dropdown de uma só referência) ou «nº|referência».
    /// </summary>
    private static (string? Number, string? Reference) ParseHistoryProduction(string? value)
    {
        if (value is null)
        {
            return (null, null);
        }
        var separator = value.IndexOf(JobOnHistorico.Separador);
        if (separator < 0)
        {
            return (value, null);
        }
        var number = value[..separator].Trim();
        var reference = value[(separator + 1)..].Trim().ToUpperInvariant();
        return (number.Length == 0 ? null : number, reference.Length == 0 ? null : reference);
    }

    private static DateOnly? ParseHistoryDate(string? value, string label, List<string> warnings)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (DateOnly.TryParseExact(text, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }
        warnings.Add($"«{label}» ignorada: «{text}» não é uma data válida.");
        return null;
    }

    // The same trimming the Job On page applies to every free-text value.
    private static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
