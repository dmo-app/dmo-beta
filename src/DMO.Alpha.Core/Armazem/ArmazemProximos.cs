using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>O que falta fazer a uma ferramenta de uma produção que vai começar.</summary>
public enum ArmazemProximoAcao
{
    /// <summary>Já está fora do armazém, no Fabrico: nada a fazer.</summary>
    Pronta,

    /// <summary>Está no armazém: falta tirá-la (Saída para Produção).</summary>
    Tirar,

    /// <summary>Está a reparar, emprestada ou na Sucata: não está disponível.</summary>
    Indisponivel
}

/// <summary>Uma CM, MF ou BQ de uma produção que vai começar, e onde está.</summary>
public sealed record ArmazemProximaFerramenta(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string OndeEsta,
    ArmazemProximoAcao Acao);

/// <summary>Uma produção que vai começar, com as suas ferramentas.</summary>
public sealed record ArmazemProximoJobOn(
    int JobOnId,
    DateOnly InicioEm,
    string Maquina,
    string Referencia,
    string Producao,
    IReadOnlyList<ArmazemProximaFerramenta> Ferramentas)
{
    public int PorTirar => Ferramentas.Count(f => f.Acao == ArmazemProximoAcao.Tirar);

    public int Indisponiveis => Ferramentas.Count(f => f.Acao == ArmazemProximoAcao.Indisponivel);
}

/// <summary>
/// Os próximos Job On, vistos do Armazém: as produções que começam entre hoje
/// e daqui a <c>dias</c>, com onde está cada CM, MF e BQ e o que falta tirar.
/// </summary>
public interface IArmazemProximos
{
    Task<IReadOnlyList<ArmazemProximoJobOn>> ListarAsync(DateOnly hoje, int dias, CancellationToken cancellationToken = default);
}
