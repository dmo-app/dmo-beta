using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O «Controlo anterior» de cada Peso na Revisão (Diogo, 19/10/2026, C6: «a
/// app não deve escolher automático; pode sugerir»). É a produção que o
/// operador escolheu no Peso («Produção anterior», a mesma do peso do SAP);
/// sem escolha não há comparação, só a sugestão pela regra única de
/// «anterior» (<see cref="IControloRevisaoQuery.FindPreviousWithPesoAsync"/>),
/// com o link para a escolher no Peso. A Revisão e a sua consulta do Peso
/// leem daqui.
/// </summary>
public sealed class RevisaoControloAnteriorQuery
{
    private readonly PesoProducaoAnteriorQuery _producaoAnterior;
    private readonly IPesoQuery _peso;
    private readonly IControloRevisaoQuery _revisao;
    private readonly IPesoComparisonQuery _comparacao;

    public RevisaoControloAnteriorQuery(
        PesoProducaoAnteriorQuery producaoAnterior,
        IPesoQuery peso,
        IControloRevisaoQuery revisao,
        IPesoComparisonQuery comparacao)
    {
        _comparacao = comparacao;
        _producaoAnterior = producaoAnterior;
        _peso = peso;
        _revisao = revisao;
    }

    /// <summary>A comparação de cada Peso da produção, pelo peso_id.</summary>
    public async Task<IReadOnlyDictionary<int, RevisaoControloAnterior>> ParaPesosAsync(
        int jobOnId,
        IReadOnlyList<PesoReadModel> pesos,
        CancellationToken cancellationToken = default)
    {
        var anteriores = new Dictionary<int, RevisaoControloAnterior>();
        ControloRevisaoItem? sugestao = null;
        var sugestaoLida = false;
        foreach (var peso in pesos)
        {
            var escolhida = await _producaoAnterior.FindAsync(peso.PesoId, cancellationToken);
            if (escolhida?.ControloId is int controloEscolhido)
            {
                var pesoEscolhido = await _peso.FindByControloAsync(controloEscolhido, cancellationToken);
                // A tabela CM a CM do Peso, com o Peso dessa produção (Diogo,
                // 19/10/2026: «peso deveria mostrar a tabela que compara com
                // controlo anterior cm com cm»).
                anteriores[peso.PesoId] = new RevisaoControloAnterior(
                    escolhida.PesoGramas,
                    $"Produção {escolhida.ProductionNumber} · {escolhida.Machine}",
                    pesoEscolhido?.Averages,
                    null)
                {
                    Comparacao = pesoEscolhido is { } historico && historico.PesoId != peso.PesoId
                        ? await _comparacao.CompareAsync(peso, historico.PesoId, cancellationToken)
                        : null
                };
                continue;
            }

            if (!sugestaoLida)
            {
                sugestao = await _revisao.FindPreviousWithPesoAsync(jobOnId, cancellationToken);
                sugestaoLida = true;
            }

            anteriores[peso.PesoId] = new RevisaoControloAnterior(
                escolhida?.PesoGramas,
                null,
                null,
                sugestao is { } s ? $"Produção {s.ProductionNumber} · {s.Machine}" : null);
        }

        return anteriores;
    }
}

/// <summary>
/// O controlo anterior de um Peso na Revisão: o peso do SAP da produção
/// anterior, a produção escolhida no Peso e a média do Peso dela; sem
/// escolha, só a <see cref="Sugestao"/> (sem valores).
/// </summary>
public sealed record RevisaoControloAnterior(
    decimal? SapGramas,
    string? Escolhida,
    PesoAveragesReadModel? Media,
    string? Sugestao)
{
    /// <summary>As leituras lado a lado, CM a CM, com o Peso da produção escolhida; null sem escolha.</summary>
    public PesoComparisonView? Comparacao { get; init; }
}
