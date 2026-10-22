using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>De onde vem uma reparação da história do Job On.</summary>
public enum QuemReparouOrigem
{
    /// <summary>Uma Reparação Interna (na Linha): o reparador é quem a registou.</summary>
    ReparacaoInterna = 1,

    /// <summary>Uma ida à Reparação pelo Armazém: o reparador da Saída.</summary>
    Armazem = 2
}

/// <summary>
/// Quem reparou uma CM ou um MF de um Job On, e quando. Na RI vem o número
/// reparado; no Armazém, a Saída para a Reparação e o regresso (se já voltou).
/// <paramref name="PelaData"/>: o movimento é de antes de o Job On se guardar
/// no movimento e ligou-se pela ferramenta e pelas datas do Job On.
/// </summary>
public sealed record QuemReparou(
    QuemReparouOrigem Origem,
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string? Reparador,
    DateTime EmUtc,
    DateTime? RegressoEmUtc = null,
    string? Numero = null,
    bool PelaData = false);

/// <summary>
/// A história das reparações: a partir de um Job On, quem reparou cada CM e
/// MF (as RI e as idas à Reparação do Armazém).
/// </summary>
public interface IHistoriaDasReparacoes
{
    /// <summary>Quem reparou cada CM e MF do Job On, pela data. Vazio quando o Job On não existe.</summary>
    Task<IReadOnlyList<QuemReparou>> DoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);
}
