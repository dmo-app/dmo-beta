using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Programada;

/// <summary>
/// UMA LISTA DA REPARAÇÃO PROGRAMADA (Diogo, 22/10/2026: «Essa função deve
/// criar uma lista de ferramentas. Associar a um reparador a lista fica
/// activa até última referência entrar»). É um separador das Ferramentas:
/// procura-se, associa-se a um reparador e imprime-se para quem vai retirar
/// do Armazém. A lista não tem movimentos próprios: a Saída e a Entrada do
/// Armazém vão atualizando cada ferramenta, e a lista fecha sozinha quando a
/// última ferramenta dá entrada.
/// </summary>
public sealed class ReparacaoProgramadaLista
{
    public int Id { get; set; }

    /// <summary>O reparador de CM e MF (Armazém › Definições).</summary>
    public int? ReparadorId { get; set; }

    /// <summary>O nome do reparador, congelado ao criar, como nos movimentos.</summary>
    public string ReparadorNome { get; set; } = string.Empty;

    public DateTime CriadaEmUtc { get; set; }

    public string? CriadaPor { get; set; }

    public Guid? CriadaPorUserId { get; set; }

    /// <summary>Quando a última ferramenta deu entrada (null: ativa).</summary>
    public DateTime? FechadaEmUtc { get; set; }
}

/// <summary>
/// Uma ferramenta numa lista, com o que se via dela ao associar (o tipo, a
/// referência, o lote, o % de uso e as Linhas) e os movimentos do Armazém que
/// a foram atualizando: a Saída (foi retirada) e a Entrada (voltou).
/// </summary>
public sealed class ReparacaoProgramadaItem
{
    public const int LinhasMaxLength = 200;

    public int Id { get; set; }

    public int ListaId { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public DateTime AdicionadaEmUtc { get; set; }

    public ToolType Tipo { get; set; }

    public string Referencia { get; set; } = string.Empty;

    public string Lote { get; set; } = string.Empty;

    /// <summary>O % de uso da última leitura, ao associar.</summary>
    public decimal? Utilizacao { get; set; }

    /// <summary>As Linhas da ferramenta, ao associar («B1, B2»).</summary>
    public string? Linhas { get; set; }

    /// <summary>A Saída do Armazém depois de associada.</summary>
    public int? SaidaMovimentoId { get; set; }

    /// <summary>A Entrada no Armazém que a devolveu.</summary>
    public int? EntradaMovimentoId { get; set; }
}

/// <summary>Os filtros da procura: tipo (CM ou MF), referência, lote, Linha e estado.</summary>
public sealed record ProgramadaProcura(
    ToolType? Tipo = null,
    string? Referencia = null,
    string? Lote = null,
    string? Linha = null,
    FerramentaEstadoTipo? Estado = null)
{
    public bool Vazia => Tipo is null && string.IsNullOrWhiteSpace(Referencia) && string.IsNullOrWhiteSpace(Lote)
        && string.IsNullOrWhiteSpace(Linha) && Estado is null;
}

/// <summary>Uma ferramenta encontrada: onde está agora e se já está numa lista ativa.</summary>
public sealed record ProgramadaCandidata(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    decimal? Utilizacao,
    string? Linhas,
    string? Posicao,
    string OndeEsta,
    FerramentaEstadoTipo? Estado,
    string? EmLista);

/// <summary>Uma lista, como o seletor a mostra.</summary>
public sealed record ProgramadaListaResumo(int Id, string Reparador, DateTime CriadaEmUtc, string? CriadaPor, DateTime? FechadaEmUtc)
{
    public bool Ativa => FechadaEmUtc is null;
}

/// <summary>Uma linha da lista: a ferramenta, onde está agora, o estado, e a Saída e a Entrada.</summary>
public sealed record ProgramadaLinha(
    int Id,
    string? ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    decimal? Utilizacao,
    string? Linhas,
    string OndeEsta,
    FerramentaEstadoTipo? Estado,
    DateTime? SaidaEmUtc,
    DateTime? EntradaEmUtc,
    string? EntradaPosicao,
    string? EntradaPor)
{
    public bool Entrou => EntradaEmUtc is not null;

    public bool Saiu => SaidaEmUtc is not null;
}

public sealed record ProgramadaListaDetalhe(ProgramadaListaResumo Lista, IReadOnlyList<ProgramadaLinha> Linhas);

public enum ProgramadaAssociacao
{
    Associada,
    SemFerramentas,
    SemReparador,
    ReparadorInvalido,
    ListaNaoExiste,
    ListaFechada
}

/// <summary>Como acabou associar: a lista e os avisos (as que já estavam, as BQ que não entram).</summary>
public sealed record ProgramadaAssociacaoSaida(ProgramadaAssociacao Resultado, int? ListaId, int Associadas, IReadOnlyList<string> Avisos)
{
    public bool Associou => Resultado == ProgramadaAssociacao.Associada;
}

/// <summary>A Reparação Programada: procurar, associar a uma lista e ver as listas.</summary>
public interface IReparacaoProgramada
{
    /// <summary>As CM e os MF com os filtros (vazia sem filtros).</summary>
    Task<IReadOnlyList<ProgramadaCandidata>> ProcurarAsync(ProgramadaProcura procura, CancellationToken cancellationToken = default);

    /// <summary>
    /// Associa as ferramentas a uma lista ativa (<paramref name="listaId"/>)
    /// ou a uma lista nova para o reparador (<paramref name="reparadorId"/>).
    /// </summary>
    Task<ProgramadaAssociacaoSaida> AssociarAsync(IReadOnlyCollection<string> toolIds, int? listaId, int? reparadorId, CancellationToken cancellationToken = default);

    /// <summary>Cria uma lista ativa vazia para o reparador («Nova lista» e «Criar»).</summary>
    Task<ProgramadaAssociacaoSaida> CriarListaAsync(int? reparadorId, CancellationToken cancellationToken = default);

    /// <summary>As listas: as ativas, ou com <paramref name="fechadas"/> só as fechadas; das mais recentes para as mais antigas.</summary>
    Task<IReadOnlyList<ProgramadaListaResumo>> ListasAsync(bool fechadas, CancellationToken cancellationToken = default);

    Task<ProgramadaListaDetalhe?> ListaAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Os reparadores de CM e MF ativos.</summary>
    Task<IReadOnlyList<ArmazemReparadorOpcao>> ReparadoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Os códigos das Linhas, por ordem.</summary>
    Task<IReadOnlyList<string>> LinhasAsync(CancellationToken cancellationToken = default);
}
