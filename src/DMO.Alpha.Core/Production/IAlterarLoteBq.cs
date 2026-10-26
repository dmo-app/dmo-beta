namespace DMO.Alpha.Core.Production;

/// <summary>
/// ALTERAR LOTE (Diogo, 26/10/2026: «tenho de alterar o lote da C2 agora para
/// 167 e não tenho essa opção»): no Registo das Boquilhas, trocar o lote da
/// BQ da produção em curso sem abrir o Job On. É a mesma troca do Editar do
/// Job On (Diogo, 18/10/2026): o lote novo nasce num bq_id novo ligado ao
/// mesmo jobon_id e os movimentos ficam no bq_id do lote antigo.
/// </summary>
public interface IAlterarLoteBq
{
    /// <summary>
    /// Os lotes para onde a BQ deste bq_id pode passar: as outras BQ com a
    /// mesma referência. Null quando o bq_id já não é o da produção (um lote
    /// trocado ou um registo sem Job On): aí não há lote a alterar.
    /// </summary>
    Task<LotesDaBq?> LotesAsync(int bqContextId, CancellationToken cancellationToken = default);

    Task<AlterarLoteOutcome> AlterarAsync(AlterarLote command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os lotes da BQ usados na produção, do primeiro ao atual (Diogo,
    /// 26/10/2026: «ver se depois o jobon_id apresenta todos os lotes que
    /// foram usados durante a fabricação»). Um só lote quando nunca mudou.
    /// </summary>
    Task<IReadOnlyList<string>> LotesUsadosAsync(int jobOnId, CancellationToken cancellationToken = default);
}

/// <summary>A produção do bq_id e os outros lotes da mesma BQ (por lote).</summary>
public sealed record LotesDaBq(int JobOnId, string Reference, string Lot, IReadOnlyList<LoteDaBq> Outros);

public sealed record LoteDaBq(string ToolId, string Lot);

/// <summary>Trocar a BQ do bq_id <paramref name="BqContextId"/> pela ferramenta <paramref name="ToolId"/>.</summary>
public sealed record AlterarLote(int BqContextId, string ToolId, string? ChangedBy);

public enum AlterarLoteResultado
{
    Alterado,
    /// <summary>O bq_id já não é o da produção (outra pessoa trocou o lote entretanto).</summary>
    JaNaoEDaProducao,
    /// <summary>A ferramenta escolhida não é uma BQ com a mesma referência.</summary>
    LoteNaoServe
}

/// <summary>O resultado; <see cref="BqContextId"/> é o bq_id do lote novo.</summary>
public sealed record AlterarLoteOutcome(AlterarLoteResultado Resultado, int? BqContextId = null, string? Lot = null);
