using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A procura do separador Histórico do Peso (ver <see cref="IControloHistoricoProcura"/>):
/// as mesmas regras e as mesmas consultas que estavam na página.
/// </summary>
public sealed class ControloHistoricoProcura(IJobOnListQuery jobOnList, IControloHistorySearchQuery historico)
    : IControloHistoricoProcura
{
    public async Task<ControloHistoricoResultado> ProcurarAsync(
        ControloHistoricoPedido pedido, CancellationToken cancellationToken = default)
    {
        var tipo = Tipo(pedido.Tipo);
        var data = DateOnly.TryParse(pedido.Data, out var dia) ? dia : (DateOnly?)null;

        // A procura em dois passos: com a referência escrita, o nº de
        // produção escolhe-se num dropdown só com as produções dela, pela
        // mesma regra da tabela (o início da referência ou da parte BQ;
        // deep sweep 2, F-6). Sem a referência, ou sem Job On, não há
        // dropdown e a produção não filtra.
        IReadOnlyList<JobOnProductionOption> producoes = [];
        JobOnProductionOption? escolhida = null;
        if (pedido.Referencia is { } referencia && !pedido.SemJobOn)
        {
            producoes = await jobOnList.ListProductionsOfReferenceAsync(referencia, null, cancellationToken);

            // A produção pedida: o nº e a referência dela (o valor das
            // opções). Um link antigo só com o nº escolhe a mais recente
            // com esse nº, nunca as duas juntas.
            var (numeroPedido, referenciaPedida) = Producao(pedido.Producao);
            escolhida = numeroPedido is null
                ? null
                : producoes.FirstOrDefault(p => p.ProductionNumber == numeroPedido
                    && (referenciaPedida is null || p.Reference == referenciaPedida));
            // O dropdown só tem as últimas produções; uma mais antiga
            // pedida por link continua a filtrar e entra no dropdown
            // (deep sweep 2, F-18). Uma produção que já não é destas
            // referências (a referência mudou) não filtra.
            if (numeroPedido is not null && escolhida is null)
            {
                escolhida = await historico.ProducaoDaReferenciaAsync(
                    referencia, numeroPedido, referenciaPedida, cancellationToken);
                if (escolhida is not null)
                {
                    producoes = [.. producoes, escolhida];
                }
            }
        }

        var resultado = new ControloHistoricoResultado(
            pedido.Referencia, escolhida?.ProductionNumber, escolhida?.Reference, pedido.Ferramenta, pedido.Lote,
            tipo, data, pedido.SemJobOn, producoes, null);
        if (!resultado.ComProcura)
        {
            return resultado;
        }

        var pagina = await historico.SearchPageAsync(
            resultado.Referencia, resultado.Producao, resultado.Ferramenta, tipo, data,
            pedido.Pagina, cancellationToken: cancellationToken, semJobOn: pedido.SemJobOn,
            productionReference: resultado.ProducaoReferencia, lote: pedido.Lote);
        // A Referência também aceita a referência da CM (Diogo,
        // 27/10/2026): sem resultados pela referência do produto,
        // procura-a como ferramenta.
        if (pagina.TotalCount == 0 && pedido.Ferramenta is null && pedido.Referencia is { } referenciaCm)
        {
            var comoFerramenta = await historico.SearchPageAsync(
                null, null, referenciaCm, tipo, data,
                pedido.Pagina, cancellationToken: cancellationToken, semJobOn: pedido.SemJobOn, lote: pedido.Lote);
            if (comoFerramenta.TotalCount > 0)
            {
                pagina = comoFerramenta;
            }
        }

        return resultado with { Pagina = pagina };
    }

    /// <summary>
    /// O valor pedido do dropdown das produções do histórico: «nº|referência»
    /// (o valor das opções) ou só «nº» (os links antigos).
    /// </summary>
    internal static (string? Number, string? Reference) Producao(string? value)
    {
        if (value is null)
        {
            return (null, null);
        }
        var separator = value.IndexOf(ControloHistoricoValores.ProducaoSeparador);
        if (separator < 0)
        {
            return (value, null);
        }
        var number = value[..separator].Trim();
        var reference = value[(separator + 1)..].Trim().ToUpperInvariant();
        return (number.Length == 0 ? null : number, reference.Length == 0 ? null : reference);
    }

    /// <summary>O filtro do tipo (o vocabulário fechado do select do histórico).</summary>
    private static ControloHistoryKind? Tipo(string? tipo) => tipo switch
    {
        "Peso" => ControloHistoryKind.Peso,
        "Pegamentos" => ControloHistoryKind.Pegamentos,
        "Comparacao" => ControloHistoryKind.Comparacao,
        "Resumo" => ControloHistoryKind.Resumo,
        _ => null
    };
}
