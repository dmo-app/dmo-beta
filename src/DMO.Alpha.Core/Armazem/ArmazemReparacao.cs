using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// Uma CM ou um MF que está fora, em reparação: o último movimento do
/// Armazém é uma Saída para Reparação. O reparador é o nome guardado no
/// movimento; <paramref name="SaiuEmUtc"/> é a data do movimento.
/// </summary>
public sealed record ArmazemForaEmReparacao(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string? Reparador,
    DateTime SaiuEmUtc);

/// <summary>
/// O cartão «Fora, em reparação» da função Movimentos do Armazém (Diogo,
/// 22/10/2026): o que está fora a reparar e onde. Só CM e MF: a reparação
/// das BQ segue-se nas Boquilhas.
/// </summary>
public interface IArmazemReparacao
{
    Task<IReadOnlyList<ArmazemForaEmReparacao>> ForaAsync(CancellationToken cancellationToken = default);
}
