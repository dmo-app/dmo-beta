namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// As leituras de escolha e de filtro que a página Boquilhas faz antes de
/// desenhar o ecrã: as BQ da janela «Registar sem Job On», a ferramenta e a
/// produção por onde o histórico se filtra. Só leitura, sem gravar nada.
/// </summary>
public interface IBoquilhasEscolhasQuery
{
    /// <summary>As BQ registadas (sem a tombstone), por referência e lote, com as máquinas compatíveis e a última linha com movimentos.</summary>
    Task<IReadOnlyList<BqSemJobOnLida>> BqSemJobOnAsync(CancellationToken cancellationToken = default);

    /// <summary>A identidade (referência e lote) de uma ferramenta pelo seu tool_id; null se não existe.</summary>
    Task<BqIdentificada?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>As BQ com esta referência (já em maiúsculas) e algum lote, para escolher a do lote pedido.</summary>
    Task<IReadOnlyList<BqIdentificada>> BqsDaReferenciaAsync(string referenciaEmMaiusculas, CancellationToken cancellationToken = default);

    /// <summary>A produção por onde se filtra o histórico; null se não existe.</summary>
    Task<ProducaoDoFiltro?> ProducaoDoFiltroAsync(int jobOnId, CancellationToken cancellationToken = default);
}

/// <summary>Uma BQ na janela «Registar sem Job On», tal como vem da base.</summary>
public sealed record BqSemJobOnLida(
    string ToolId, string Reference, string Lot, int? Quantity,
    IReadOnlyList<string> Machines, string? MfReference, string? LastLine);

/// <summary>A identidade humana de uma ferramenta: a referência e o lote.</summary>
public sealed record BqIdentificada(string ToolId, string Reference, string Lot);

/// <summary>A produção escolhida como filtro do histórico.</summary>
public sealed record ProducaoDoFiltro(int Id, string ProductionNumber, string Reference, string Line);
