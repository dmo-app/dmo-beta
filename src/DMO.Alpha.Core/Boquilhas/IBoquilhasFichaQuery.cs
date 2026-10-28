using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// As leituras da ficha da BQ e do painel das quantidades do Registo que a
/// página Boquilhas fazia ela própria: os factos da ferramenta, as máquinas
/// compatíveis e os movimentos sem Job On. Só leitura, sem gravar nada.
/// </summary>
public interface IBoquilhasFichaQuery
{
    /// <summary>As máquinas ligadas à ferramenta, por ordem do id da máquina; vazio sem ligações.</summary>
    Task<IReadOnlyList<MachineOption>> MaquinasDaFerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>A referência, o lote e a quantidade contabilística da ferramenta; null se não existe.</summary>
    Task<BqFichaFerramenta?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>Os movimentos mais recentes da BQ em registos sem Job On, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<BqMovimentoSemJobOn>> MovimentosSemJobOnAsync(string toolId, int limite, CancellationToken cancellationToken = default);
}

/// <summary>Os factos da ferramenta na ficha da BQ.</summary>
public sealed record BqFichaFerramenta(string Reference, string Lot, int? AccountedQuantity, int? AnQuantity = null);

/// <summary>Um movimento sem Job On, tal como está gravado (a hora em UTC).</summary>
public sealed record BqMovimentoSemJobOn(BqMovementType Type, int Quantity, string? MachineCode, DateTime OccurredAtUtc, int? AnQuantity = null);
