using DMO.Alpha.Core.Paging;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// As leituras que a página Boquilhas faz para listar os registos de uma BQ
/// e para encontrar a produção a que um registo se associa (a gravação da
/// associação é do handler). Só leitura, sem gravar nada.
/// </summary>
public interface IBoquilhasAssociacaoQuery
{
    /// <summary>
    /// Os registos da BQ (o trace âncora direta ou associado), do mais recente
    /// para o mais antigo, uma página de cada vez, com o número de movimentos.
    /// </summary>
    Task<PagedList<BqRegistoDaBq>> RegistosDaBqAsync(string toolId, int? pageNumber, CancellationToken cancellationToken = default);

    /// <summary>O registo da BQ ainda sem Job On, com o número de movimentos; null se não existe.</summary>
    Task<BqRegistoPendente?> RegistoSemJobOnAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>A produção escolhida pelo id; null se não existe.</summary>
    Task<BqProducaoParaAssociar?> ProducaoAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>As produções com este número (a mais recente primeiro), e se cada uma tem a BQ deste registo.</summary>
    Task<IReadOnlyList<BqProducaoComBq>> ProducoesDoNumeroAsync(string productionNumber, string? toolId, CancellationToken cancellationToken = default);

    /// <summary>O contexto BQ da produção que partilha a ferramenta do registo; null se a produção não a tem.</summary>
    Task<int?> ContextoDaBqNaProducaoAsync(int jobOnId, string? toolId, CancellationToken cancellationToken = default);
}

/// <summary>Um registo de uma BQ: a produção a que pertence (se pertence) e quantos movimentos tem.</summary>
public sealed record BqRegistoDaBq(
    int Id,
    int? BqContextId,
    bool Trocada,
    string? ProductionNumber,
    string? ProductionReference,
    string? MachineCode,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    int MovementCount);

/// <summary>O registo da BQ sem Job On e quantos movimentos tem.</summary>
public sealed record BqRegistoPendente(int Id, int Movements);

/// <summary>A produção escolhida para associar um registo.</summary>
public sealed record BqProducaoParaAssociar(int Id, string Reference);

/// <summary>Uma produção com o número pedido e se tem a BQ do registo.</summary>
public sealed record BqProducaoComBq(int Id, string Reference, bool TemBq);
