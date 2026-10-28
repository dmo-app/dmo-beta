using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// Uma Entrada: a posição, a origem e o estado escolhidos. Data vazia é agora.
/// <paramref name="ComPar"/>: a CM e o MF da mesma referência e lote entram
/// juntos, na mesma posição e no mesmo envio (como no Access). A Entrada
/// guarda a Linha e o reparador com que a ferramenta vem: com
/// <paramref name="LinhaEReparadorDoFormulario"/>, os que a pessoa deixou no
/// formulário (vazio fica vazio); sem ele, os da última Saída.
/// </summary>
public sealed record PedidoDeEntrada(
    Guid OperacaoId,
    string ToolId,
    string? Posicao,
    ArmazemOrigem? Origem,
    FerramentaEstadoTipo? Estado,
    DateTime? OcorridoEmUtc,
    string? Observacoes,
    bool CriarPosicao = false,
    bool ComPar = false,
    int? LinhaId = null,
    string? Reparador = null,
    bool LinhaEReparadorDoFormulario = false,
    string? Fabrica = null);

/// <summary>Uma Saída: o destino e, conforme o destino, o reparador, a fábrica, o estado ou a Linha (Produção).</summary>
public sealed record PedidoDeSaida(
    Guid OperacaoId,
    string ToolId,
    ArmazemDestino? Destino,
    int? ReparadorId,
    string? FabricaDestino,
    FerramentaEstadoTipo? Estado,
    DateTime? OcorridoEmUtc,
    string? Observacoes,
    int? LinhaId = null);

/// <summary>Corrigir localização: a posição certa, ou null para «Não está no armazém».</summary>
public sealed record PedidoDeCorrecao(
    Guid OperacaoId,
    string ToolId,
    string? Posicao,
    string? Observacoes,
    bool CriarPosicao = false);

/// <summary>Como acabou um movimento. Só se recusa o impossível ou o que falta; o resto grava e avisa.</summary>
public enum ArmazemMovimentoResultado
{
    Gravado,
    JaRegistado,
    FerramentaNaoExiste,
    JaNoArmazem,
    NaoEstaNoArmazem,
    PosicaoInvalida,
    PosicaoNaoExiste,
    SemOrigem,
    SemEstado,
    SemDestino,
    SemReparador,
    ReparadorInvalido,
    ObservacoesLongas,
    FabricaLonga,
    NadaAMudar,
    MudouEntretanto
}

/// <summary>O resultado de um movimento, com os avisos do que gravou mesmo assim.</summary>
public sealed record ArmazemMovimentoSaida(
    ArmazemMovimentoResultado Resultado,
    IReadOnlyList<string> Avisos,
    string? Posicao = null,
    int? MovimentoId = null)
{
    /// <summary>A Entrada levou também o par (a CM ou o MF da mesma referência e lote).</summary>
    public bool ComPar { get; init; }

    public bool Gravou => Resultado is ArmazemMovimentoResultado.Gravado or ArmazemMovimentoResultado.JaRegistado;

    public static ArmazemMovimentoSaida Recusa(ArmazemMovimentoResultado resultado, string? posicao = null) =>
        new(resultado, [], posicao);
}

/// <summary>
/// Uma ferramenta encontrada na procura do Armazém: onde está, o estado, as
/// máquinas (nenhuma: todas) e o % de uso da última leitura (Diogo,
/// 21/10/2026: «posição, referência, lote, estado, máquinas, % uso»).
/// </summary>
public sealed record ArmazemProcuraItem(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string OndeEsta,
    bool NoArmazem,
    FerramentaEstadoTipo? Estado = null,
    IReadOnlyList<string>? Maquinas = null,
    decimal? Utilizacao = null);

/// <summary>Um movimento já registado de uma ferramenta, para a lista dos últimos.</summary>
public sealed record ArmazemMovimentoLinha(
    int Id,
    ArmazemMovimentoTipo Tipo,
    DateTime OcorridoEmUtc,
    string Descricao,
    string? Observacoes,
    string? RegistadoPor);

/// <summary>Um reparador que se pode escolher na Saída para Reparação.</summary>
public sealed record ArmazemReparadorOpcao(int Id, string Nome);

/// <summary>Outra ferramenta que já está numa posição.</summary>
public sealed record ArmazemOcupante(string ToolId, ToolType Tipo, string Referencia, string Lote, DateTime DesdeUtc);

/// <summary>
/// Onde está uma ferramenta agora e o que a Entrada propõe: a origem pela
/// última Saída e o estado (Por reparar depois da Produção, Reparado depois
/// da Reparação; D11), e os últimos movimentos.
/// </summary>
public sealed record ArmazemSituacao(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string? Posicao,
    DateTime? NaPosicaoDesdeUtc,
    FerramentaLocalizacaoTipo Localizacao,
    string? LocalizacaoReparador,
    DateTime? LocalizacaoDesdeUtc,
    FerramentaEstadoTipo? Estado,
    DateTime? EstadoEmUtc,
    ArmazemDestino? UltimaSaida,
    string? UltimaSaidaReparador,
    ArmazemOrigem? OrigemProposta,
    FerramentaEstadoTipo? EstadoProposto,
    IReadOnlyList<ArmazemReparadorOpcao> Reparadores,
    IReadOnlyList<ArmazemMovimentoLinha> UltimosMovimentos)
{
    public bool NoArmazem => Posicao is not null;

    /// <summary>O par desta ferramenta (o MF da CM, ou a CM do MF, da mesma referência e lote), se existir.</summary>
    public ArmazemPar? Par { get; init; }

    /// <summary>
    /// O reparador da lista ativa da Reparação Programada em que a ferramenta
    /// está e ainda não voltou. Na Entrada o estado vem «Reparado»; a pessoa
    /// pode mudar, com aviso (Diogo, 22/10/2026).
    /// </summary>
    public string? ListaProgramada { get; init; }

    /// <summary>As Linhas que se podem escolher (a Saída para Produção e a Entrada).</summary>
    public IReadOnlyList<ArmazemLinhaOpcao> Linhas { get; init; } = [];

    /// <summary>As Linhas da ficha da ferramenta (a Saída para Produção propõe a única).</summary>
    public IReadOnlyList<int> LinhasDaFerramenta { get; init; } = [];

    /// <summary>A Linha com que a ferramenta vem (a da última Saída), para a Entrada.</summary>
    public int? VindaLinhaId { get; init; }

    /// <summary>O reparador com que a ferramenta vem (o da última Saída), para a Entrada.</summary>
    public string? VindaReparador { get; init; }

    /// <summary>A fábrica da BA de onde a ferramenta volta (a da última Saída para Emprestado).</summary>
    public string? VindaFabrica { get; init; }
}

/// <summary>Uma Linha (máquina) que se pode escolher.</summary>
public sealed record ArmazemLinhaOpcao(int Id, string Codigo);

/// <summary>O par de uma CM ou de um MF, e a posição onde está (null: fora do armazém).</summary>
public sealed record ArmazemPar(string ToolId, ToolType Tipo, string? Posicao)
{
    public bool NoArmazem => Posicao is not null;
}

/// <summary>Os movimentos do Armazém: procurar, ver onde está, Entrada, Saída e Corrigir localização.</summary>
/// <summary>As Linhas da ferramenta e o % de uso da última leitura (para distinguir os lotes nas listas).</summary>
public sealed record ArmazemLinhasEUso(IReadOnlyList<string> Linhas, decimal? Uso);

public interface IArmazemMovimentos
{
    /// <summary>As Linhas e o % de uso de cada ferramenta (as que não existem não vêm).</summary>
    Task<IReadOnlyDictionary<string, ArmazemLinhasEUso>> LinhasEUsoAsync(IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken = default);

    /// <summary>As ferramentas cuja referência ou lote correspondem ao texto, ou que estão na posição com esse código.</summary>
    Task<IReadOnlyList<ArmazemProcuraItem>> ProcurarAsync(string? texto, CancellationToken cancellationToken = default);

    /// <summary>Onde está a ferramenta e o que a Entrada propõe; null quando não existe.</summary>
    Task<ArmazemSituacao?> SituacaoAsync(string? toolId, CancellationToken cancellationToken = default);

    /// <summary>As ferramentas em cada uma destas posições.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<ArmazemOcupante>>> OcupantesAsync(
        IReadOnlyCollection<int> posicaoIds, CancellationToken cancellationToken = default);

    Task<ArmazemMovimentoSaida> EntradaAsync(PedidoDeEntrada pedido, CancellationToken cancellationToken = default);

    Task<ArmazemMovimentoSaida> SaidaAsync(PedidoDeSaida pedido, CancellationToken cancellationToken = default);

    Task<ArmazemMovimentoSaida> CorrigirAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken = default);
}

/// <summary>Os nomes que a pessoa lê.</summary>
public static class ArmazemTextos
{
    public static string Tipo(ToolType tipo) => tipo switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        _ => "BQ"
    };

    public static string Movimento(ArmazemMovimentoTipo tipo) => tipo switch
    {
        ArmazemMovimentoTipo.Entrada => "Entrada",
        ArmazemMovimentoTipo.Saida => "Saída",
        _ => "Corrigir localização"
    };

    public static string Ferramenta(ToolType tipo, string referencia, string lote) =>
        $"{Tipo(tipo)} {referencia} · lote {lote}";

    public static string Origem(ArmazemOrigem origem) => origem switch
    {
        ArmazemOrigem.Producao => "Produção",
        ArmazemOrigem.Reparacao => "Reparação",
        ArmazemOrigem.Emprestimo => "Empréstimo",
        ArmazemOrigem.Nova => "Nova",
        _ => "Já estava no armazém"
    };

    public static string Destino(ArmazemDestino destino) => destino switch
    {
        ArmazemDestino.Reparacao => "Reparação",
        ArmazemDestino.Producao => "Produção",
        ArmazemDestino.Emprestado => "Emprestado",
        _ => "Sucata"
    };

    public static string Estado(FerramentaEstadoTipo estado) => estado switch
    {
        FerramentaEstadoTipo.Novo => "Novo",
        FerramentaEstadoTipo.Reparado => "Reparado",
        FerramentaEstadoTipo.PorReparar => "Por reparar",
        _ => "Sucatado"
    };

    /// <summary>Uma ferramenta sem nenhum registo no Armazém (Diogo, 28/10/2026:
    /// «os que não têm registo no armazém mete sem registo»; antes dizia «Fabrico»).</summary>
    public const string SemRegisto = "Sem registo";

    public static string Localizacao(FerramentaLocalizacaoTipo localizacao, string? reparador) => localizacao switch
    {
        FerramentaLocalizacaoTipo.AReparar => string.IsNullOrWhiteSpace(reparador) ? "A reparar" : $"A reparar · {reparador}",
        FerramentaLocalizacaoTipo.Emprestado => "Emprestado",
        FerramentaLocalizacaoTipo.Sucata => "Sucata",
        _ => "Fabrico"
    };

    /// <summary>Os destinos pela ordem da lista (Diogo, 18/10/2026).</summary>
    public static readonly IReadOnlyList<ArmazemDestino> Destinos =
        [ArmazemDestino.Reparacao, ArmazemDestino.Producao, ArmazemDestino.Emprestado, ArmazemDestino.Sucata];

    public static readonly IReadOnlyList<ArmazemOrigem> Origens =
        [ArmazemOrigem.Producao, ArmazemOrigem.Reparacao, ArmazemOrigem.Emprestimo, ArmazemOrigem.Nova, ArmazemOrigem.JaEstava];

    /// <summary>Os estados que a Entrada pede (Diogo, 18/10/2026: Reparado, Por reparar ou Novo).</summary>
    public static readonly IReadOnlyList<FerramentaEstadoTipo> EstadosDaEntrada =
        [FerramentaEstadoTipo.Reparado, FerramentaEstadoTipo.PorReparar, FerramentaEstadoTipo.Novo];
}
