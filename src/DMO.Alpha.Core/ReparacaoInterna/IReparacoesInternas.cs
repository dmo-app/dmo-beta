using System.Globalization;
using DMO.Alpha.Core.Paging;

namespace DMO.Alpha.Core.ReparacaoInterna;

/// <summary>
/// A CM ou o MF de uma produção: o contexto (<c>cm_id</c> ou <c>mf_id</c>) e a
/// referência e o lote da ferramenta (nunca o nome técnico). Sem referência:
/// a ferramenta foi eliminada (o contexto passou para a REMOVED).
/// </summary>
public sealed record RiFerramenta(int ContextoId, string? Referencia, string? Lote);

/// <summary>Uma produção cujas datas cobrem o dia operacional da máquina.</summary>
public sealed record RiProducao(
    int JobOnId,
    string Producao,
    string Referencia,
    DateOnly InicioEm,
    DateOnly? FimEm,
    RiFerramenta? Cm,
    RiFerramenta? Mf,
    string? CmToolId = null,
    string? MfToolId = null)
{
    public RiFerramenta? Ferramenta(ReparacaoTipo tipo) => tipo == ReparacaoTipo.Cm ? Cm : Mf;

    /// <summary>O <c>tool_id</c> da CM ou do MF (para o link da ficha); null se foi eliminada.</summary>
    public string? ToolId(ReparacaoTipo tipo) => tipo == ReparacaoTipo.Cm ? CmToolId : MfToolId;
}

/// <summary>
/// A produção do dia de uma máquina (Diogo, 17/10/2026, RI-03): as produções
/// cujas datas cobrem o dia operacional, da de maior <c>jobon_id</c> para a
/// de menor. Uma produção sem datas não conta. Com zero não há a que ligar;
/// com duas ou mais a pessoa escolhe, e a de maior <c>jobon_id</c> vem
/// escolhida.
/// </summary>
public sealed record RiProducaoDoDia(DateOnly Dia, IReadOnlyList<RiProducao> Candidatas)
{
    public bool SemProducao => Candidatas.Count == 0;

    public bool VariasProducoes => Candidatas.Count > 1;

    /// <summary>A produção pedida, se for uma das candidatas; senão a de maior <c>jobon_id</c>.</summary>
    public RiProducao? Escolhida(int? jobOnId) =>
        Candidatas.FirstOrDefault(c => c.JobOnId == jobOnId) ?? Candidatas.FirstOrDefault();
}

/// <summary>
/// Registar uma reparação: a máquina, a produção e o contexto que a página
/// mostrou (para ver se mudaram entretanto), o tipo e o número escrito. Com
/// <paramref name="Outra"/> (a CM ou o MF na máquina não é a do Job On) ou
/// sem produção para o dia, a ferramenta vai escrita: a referência e o lote,
/// sem contexto, por associar (peça 4). <paramref name="Dia"/>: o dia
/// operacional escolhido na página (as datas são livres, Diogo, 22/10/2026);
/// sem ele, o de agora. <paramref name="Hora"/>: a hora da reparação
/// escrita na página (vem com a de agora); sem ela, a hora local de agora.
/// </summary>
public sealed record PedidoDeReparacao(
    Guid OperacaoId,
    int? MachineId,
    int? JobOnId,
    ReparacaoTipo? Tipo,
    int? ContextoId,
    string? Numero,
    bool Outra = false,
    string? Referencia = null,
    string? Lote = null,
    DateOnly? Dia = null,
    TimeOnly? Hora = null,
    ReparacaoTrabalho Trabalho = ReparacaoTrabalho.Reparacao);

/// <summary>Como acabou um registo. Só se recusa o impossível ou o que falta.</summary>
public enum ReparacaoResultado
{
    Gravado,
    JaRegistado,
    SemNumero,
    NumeroLongo,
    SemTipo,
    MaquinaNaoExiste,
    ProducaoMudou,
    SemFerramenta,
    FerramentaMudou,
    /// <summary>Outra CM/MF (ou sem produção) sem a referência escrita.</summary>
    SemReferencia,
    ReferenciaLonga,
    /// <summary>Outra CM/MF (ou sem produção) sem o lote escrito.</summary>
    SemLote,
    LoteLongo
}

/// <summary>
/// O resultado de um registo, com o número tal como ficou (sem espaços nas
/// pontas) e, com a ferramenta escrita, a referência e o lote gravados e se
/// ficou sem Job On: a página avisa que falta associar.
/// </summary>
public sealed record ReparacaoSaida(
    ReparacaoResultado Resultado,
    int? ReparacaoId = null,
    string? Numero = null,
    string? Referencia = null,
    string? Lote = null,
    bool SemJobOn = false)
{
    public bool Gravou => Resultado is ReparacaoResultado.Gravado or ReparacaoResultado.JaRegistado;

    /// <summary>Gravou com a ferramenta escrita: falta associar.</summary>
    public bool PorAssociar => Gravou && Referencia is not null;
}

// Máquina, tipo ou produção errados: anula-se e regista-se outra vez.
/// <summary>
/// Corrigir o número e a hora de uma reparação; <paramref name="Hora"/> NULL
/// deixa a hora como está. <paramref name="Chefia"/>: quem pede tem a Consulta
/// da Reparação Interna e pode corrigir as de todos; os outros só as suas.
/// </summary>
public sealed record PedidoDeCorrecao(Guid OperacaoId, int ReparacaoId, string? Numero, string? Motivo = null, TimeOnly? Hora = null, bool Chefia = false);

/// <summary>Anular uma reparação: sai da lista e das contagens e fica na base com o rasto. <paramref name="Chefia"/> como em <see cref="PedidoDeCorrecao"/>.</summary>
public sealed record PedidoDeAnulacao(Guid OperacaoId, int ReparacaoId, string? Motivo = null, bool Chefia = false);

/// <summary>
/// Como acabou uma correção ou uma anulação. Só se recusa o impossível (a
/// reparação já não existe, já foi anulada ou outra pessoa mudou-a
/// entretanto), o que o registo também recusa e a reparação de outra pessoa
/// a quem não é chefia (DS-PERM-01, Diogo 26/10/2026).
/// </summary>
public enum CorrecaoResultado
{
    Corrigido,
    Anulado,
    /// <summary>O mesmo envio outra vez: já estava feito.</summary>
    JaFeito,
    /// <summary>O número e a hora já eram esses: nada mudou.</summary>
    Igual,
    NaoExiste,
    JaAnulada,
    Mudou,
    SemNumero,
    NumeroLongo,
    MotivoLongo,
    /// <summary>A reparação é de outra pessoa: só quem a registou e a chefia a corrigem ou anulam.</summary>
    SoQuemRegistou
}

/// <summary>O resultado, com o número de que a mensagem fala (o corrigido ou o anulado).</summary>
public sealed record CorrecaoSaida(CorrecaoResultado Resultado, string? Numero = null)
{
    public bool Gravou => Resultado is CorrecaoResultado.Corrigido or CorrecaoResultado.Anulado or CorrecaoResultado.JaFeito;
}

/// <summary>
/// Associar a ferramenta a uma reparação com a ferramenta escrita (peça 4):
/// o <c>tool_id</c> escolhido na janela do responsável.
/// </summary>
public sealed record PedidoDeAssociacao(Guid OperacaoId, int ReparacaoId, string? ToolId);

/// <summary>Como acabou uma associação. Só se recusa o impossível.</summary>
public enum AssociacaoResultado
{
    Associado,
    /// <summary>O mesmo envio outra vez: já estava feito.</summary>
    JaFeito,
    NaoExiste,
    JaAnulada,
    /// <summary>Outra pessoa já a associou (ou entretanto).</summary>
    JaAssociada,
    /// <summary>A reparação tem a CM ou o MF do Job On: não há nada a associar.</summary>
    NaoEscrita,
    /// <summary>Nenhuma ferramenta escolhida.</summary>
    SemFerramenta,
    /// <summary>A ferramenta não existe, foi eliminada ou não é do tipo da reparação.</summary>
    FerramentaNaoServe,
    Mudou
}

/// <summary>O resultado, com o número e a ferramenta associada («5447T173 · lote 4») de que a mensagem fala.</summary>
public sealed record AssociacaoSaida(AssociacaoResultado Resultado, string? Numero = null, string? Ferramenta = null)
{
    public bool Gravou => Resultado is AssociacaoResultado.Associado or AssociacaoResultado.JaFeito;
}

/// <summary>
/// Uma reparação na tabela por baixo do registo: pouco e curto. Com a
/// ferramenta escrita, a referência e o lote («Outra: 5447T173 · lote 4»).
/// </summary>
public sealed record RiLinha(
    int Id,
    string Numero,
    ReparacaoTipo Tipo,
    string? Quem,
    DateTime RegistadoEmUtc,
    string? Referencia = null,
    string? Lote = null,
    TimeOnly? Hora = null,
    ReparacaoTrabalho Trabalho = ReparacaoTrabalho.Reparacao);

/// <summary>A Reparação Interna: o dia operacional, a produção do dia, registar, corrigir, anular e a lista.</summary>
public interface IReparacoesInternas
{
    /// <summary>O dia operacional agora, pela hora de viragem da Reparação Interna (Canal Jobon; 8 por omissão).</summary>
    Task<DateOnly> DiaOperacionalAsync(CancellationToken cancellationToken = default);

    /// <summary>As produções da máquina cujas datas cobrem o dia operacional.</summary>
    Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, CancellationToken cancellationToken = default);

    /// <summary>As produções da máquina cujas datas cobrem este dia (a data escolhida na página; as datas são livres).</summary>
    Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, DateOnly dia, CancellationToken cancellationToken = default);

    /// <summary>
    /// A produção do dia de várias máquinas numa só leitura (o painel), com a
    /// mesma regra de <see cref="ProducaoDoDiaAsync(int, DateOnly, CancellationToken)"/>.
    /// Todas as máquinas pedidas vêm no resultado; sem produção, vazia.
    /// </summary>
    Task<IReadOnlyDictionary<int, RiProducaoDoDia>> ProducoesDoDiaAsync(IReadOnlyCollection<int> machineIds, DateOnly dia, CancellationToken cancellationToken = default);

    Task<ReparacaoSaida> RegistarAsync(PedidoDeReparacao pedido, CancellationToken cancellationToken = default);

    /// <summary>Corrige o número (as mesmas regras do registo), com rasto. Só quem a registou e a chefia (DS-PERM-01).</summary>
    Task<CorrecaoSaida> CorrigirAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken = default);

    /// <summary>Anula a reparação (fica na base), com rasto. Só quem a registou e a chefia (DS-PERM-01).</summary>
    Task<CorrecaoSaida> AnularAsync(PedidoDeAnulacao pedido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Associa a ferramenta escrita a uma ferramenta registada do mesmo tipo,
    /// com rasto. Quem tem o módulo pode associar (como corrigir).
    /// </summary>
    Task<AssociacaoSaida> AssociarAsync(PedidoDeAssociacao pedido, CancellationToken cancellationToken = default);

    /// <summary>As reparações desta máquina e produção que contam (sem as anuladas), da mais recente para a mais antiga, em páginas de 25.</summary>
    Task<PagedList<RiLinha>> ListarAsync(int machineId, int jobOnId, int? pagina, CancellationToken cancellationToken = default);

    /// <summary>As reparações sem Job On desta máquina neste dia operacional que contam, como em <see cref="ListarAsync"/>.</summary>
    Task<PagedList<RiLinha>> ListarSemProducaoAsync(int machineId, DateOnly dia, int? pagina, CancellationToken cancellationToken = default);
}

/// <summary>Os nomes que a pessoa lê.</summary>
public static class RiTextos
{
    public static string Tipo(ReparacaoTipo tipo) => tipo == ReparacaoTipo.Cm ? "CM" : "MF";

    /// <summary>O tipo no endereço e no formulário: cm ou mf.</summary>
    public static string Valor(ReparacaoTipo tipo) => tipo == ReparacaoTipo.Cm ? "cm" : "mf";

    public static ReparacaoTipo? TipoDe(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "cm" => ReparacaoTipo.Cm,
        "mf" => ReparacaoTipo.Mf,
        _ => null
    };

    public static string Trabalho(ReparacaoTrabalho trabalho) => trabalho switch
    {
        ReparacaoTrabalho.Soldar => "Soldar",
        ReparacaoTrabalho.Polir => "Polir",
        _ => "Reparação"
    };

    /// <summary>O trabalho no formulário: reparacao, soldar ou polir.</summary>
    public static string TrabalhoValor(ReparacaoTrabalho trabalho) => trabalho switch
    {
        ReparacaoTrabalho.Soldar => "soldar",
        ReparacaoTrabalho.Polir => "polir",
        _ => "reparacao"
    };

    /// <summary>Sem texto, ou com um que não se conhece, é Reparação.</summary>
    public static ReparacaoTrabalho TrabalhoDe(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "soldar" => ReparacaoTrabalho.Soldar,
        "polir" => ReparacaoTrabalho.Polir,
        _ => ReparacaoTrabalho.Reparacao
    };

    /// <summary>A ferramenta como a pessoa a reconhece: «CM 5538T324 · lote 2».</summary>
    public static string Ferramenta(ReparacaoTipo tipo, RiFerramenta ferramenta) =>
        ferramenta.Referencia is null
            ? $"{Tipo(tipo)} eliminada"
            : $"{Tipo(tipo)} {ferramenta.Referencia} · lote {ferramenta.Lote}";

    /// <summary>A ferramenta escrita: «5447T173 · lote 4».</summary>
    public static string Escrita(string referencia, string? lote) =>
        string.IsNullOrEmpty(lote) ? referencia : $"{referencia} · lote {lote}";

    /// <summary>A produção numa lista de candidatas: «202501 · 5538T324 · 21/10 a 25/10».</summary>
    public static string Producao(RiProducao producao) =>
        $"{producao.Producao} · {producao.Referencia} · {Dia(producao.InicioEm)}"
        + (producao.FimEm is { } fim ? $" a {Dia(fim)}" : " sem fim");

    private static string Dia(DateOnly dia) => dia.ToString("dd/MM", CultureInfo.InvariantCulture);
}
