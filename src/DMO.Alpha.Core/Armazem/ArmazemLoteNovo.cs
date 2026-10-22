using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// UM LOTE NOVO REGISTADO NO ARMAZÉM (Diogo, 22/10/2026: «a pessoa do
/// armazem as vezes da entrada de lotes novos que nao tem tool_id entao
/// poderiamos usar isso como um registo que depois pede permissao a quem tem
/// modulo ferramentas»). O Armazém nunca cria nem edita ferramentas: guarda o
/// tipo, a referência, o lote e a posição de entrada como «por criar». Quem
/// tem o módulo Ferramentas vê o aviso no Sumário do Planeamento, cria a
/// ferramenta (ou liga a uma que já existe) e, ao gravar, a entrada fica
/// registada na posição guardada.
/// </summary>
public sealed class ArmazemLoteNovo
{
    public const int ReferenciaMaxLength = 64;

    public const int LoteMaxLength = 32;

    public int Id { get; set; }

    public ToolType Tipo { get; set; }

    public string Referencia { get; set; } = string.Empty;

    public string Lote { get; set; } = string.Empty;

    /// <summary>A posição da entrada, quatro algarismos.</summary>
    public string Posicao { get; set; } = string.Empty;

    public ArmazemLoteNovoEstado Estado { get; set; } = ArmazemLoteNovoEstado.PorCriar;

    public DateTime RegistadoEmUtc { get; set; }

    public string? RegistadoPor { get; set; }

    public Guid? RegistadoPorUserId { get; set; }

    /// <summary>A ferramenta criada (ou ligada) que resolveu o registo.</summary>
    public string? ResolvedToolId { get; set; }

    /// <summary>A entrada que ficou registada ao resolver.</summary>
    public int? MovimentoId { get; set; }

    public DateTime? ResolvidoEmUtc { get; set; }

    public string? ResolvidoPor { get; set; }

    public Guid? ResolvidoPorUserId { get; set; }
}

public enum ArmazemLoteNovoEstado
{
    PorCriar = 0,
    Resolvido = 1
}

/// <summary>Como acabou o registo de um lote novo. Só se recusa o que falta e a posição que não tem 4 algarismos.</summary>
public enum ArmazemLoteNovoResultado
{
    Gravado,
    SemReferencia,
    SemLote,
    PosicaoInvalida,
    ReferenciaLonga,
    LoteLongo
}

public sealed record ArmazemLoteNovoSaida(ArmazemLoteNovoResultado Resultado, int? Id = null, IReadOnlyList<string>? Avisos = null)
{
    public bool Gravou => Resultado == ArmazemLoteNovoResultado.Gravado;
}

/// <summary>Um lote novo por criar (ou já resolvido), como as páginas o mostram.</summary>
public sealed record ArmazemLoteNovoItem(
    int Id,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string Posicao,
    DateTime RegistadoEmUtc,
    string? RegistadoPor,
    ArmazemLoteNovoEstado Estado,
    string? ResolvedToolId)
{
    public bool PorCriar => Estado == ArmazemLoteNovoEstado.PorCriar;
}

/// <summary>Como acabou a resolução de um lote novo.</summary>
public enum ArmazemLoteNovoResolucao
{
    Resolvido,
    NaoExiste,
    JaResolvido,
    FerramentaNaoExiste
}

public sealed record ArmazemLoteNovoResolucaoSaida(ArmazemLoteNovoResolucao Resultado, IReadOnlyList<string> Avisos)
{
    public bool Resolveu => Resultado == ArmazemLoteNovoResolucao.Resolvido;
}

/// <summary>Os lotes novos registados no Armazém, à espera de quem tem o módulo Ferramentas.</summary>
public interface IArmazemLotesNovos
{
    /// <summary>Guarda o lote novo como «por criar». Nunca cria a ferramenta.</summary>
    Task<ArmazemLoteNovoSaida> RegistarAsync(ToolType tipo, string? referencia, string? lote, string? posicao, CancellationToken cancellationToken = default);

    /// <summary>Os lotes por criar, do mais antigo para o mais recente; com <paramref name="tipo"/>, só desse tipo.</summary>
    Task<IReadOnlyList<ArmazemLoteNovoItem>> PorCriarAsync(ToolType? tipo = null, CancellationToken cancellationToken = default);

    Task<ArmazemLoteNovoItem?> ObterAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>A ferramenta que já existe com o mesmo tipo, referência e lote (para ligar em vez de criar).</summary>
    Task<string?> FerramentaExistenteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o lote com a ferramenta (criada ou existente): dá a entrada no
    /// Armazém na posição guardada, à data do registo, e o aviso desaparece.
    /// </summary>
    Task<ArmazemLoteNovoResolucaoSaida> ResolverAsync(int id, string toolId, CancellationToken cancellationToken = default);
}
