using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Armazem;

/// <summary>Uma Entrada: a posição, a origem e o estado escolhidos. Data vazia é agora.</summary>
public sealed record PedidoDeEntrada(
    Guid OperacaoId,
    string ToolId,
    string? Posicao,
    ArmazemOrigem? Origem,
    FerramentaEstadoTipo? Estado,
    DateTime? OcorridoEmUtc,
    string? Observacoes,
    bool CriarPosicao = false);

/// <summary>Uma Saída: o destino e, conforme o destino, o reparador, a fábrica ou o estado.</summary>
public sealed record PedidoDeSaida(
    Guid OperacaoId,
    string ToolId,
    ArmazemDestino? Destino,
    int? ReparadorId,
    string? FabricaDestino,
    FerramentaEstadoTipo? Estado,
    DateTime? OcorridoEmUtc,
    string? Observacoes);

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
    public bool Gravou => Resultado is ArmazemMovimentoResultado.Gravado or ArmazemMovimentoResultado.JaRegistado;

    public static ArmazemMovimentoSaida Recusa(ArmazemMovimentoResultado resultado, string? posicao = null) =>
        new(resultado, [], posicao);
}

/// <summary>Uma ferramenta encontrada na procura do Armazém, com onde está.</summary>
public sealed record ArmazemProcuraItem(
    string ToolId,
    ToolType Tipo,
    string Referencia,
    string Lote,
    string OndeEsta,
    bool NoArmazem);

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
}

/// <summary>Uma posição com as ferramentas que lá estão (a lista das Posições).</summary>
public sealed record ArmazemPosicaoOcupacao(int PosicaoId, IReadOnlyList<ArmazemOcupante> Ferramentas);

/// <summary>Os movimentos do Armazém: procurar, ver onde está, Entrada, Saída e Corrigir localização.</summary>
public interface IArmazemMovimentos
{
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
