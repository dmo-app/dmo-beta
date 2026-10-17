namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// A leitura das ENTIDADES REPARADORAS para a Admin (Admin → App
/// Definições → Boquilhas): a lista do vocabulário administrado — os
/// FATOS HUMANOS (o nome; o Id é o valor das chaves de rota, nunca
/// mostrado como identidade nas folhas operacionais).
/// </summary>
public interface IBqRepairerQuery
{
    /// <summary>Todos os reparadores, por nome (a ordem humana da lista).</summary>
    Task<IReadOnlyList<BqRepairerItem>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Um reparador da lista da Admin (o fato humano: o nome).</summary>
public sealed record BqRepairerItem(
    int Id,
    string Name);
