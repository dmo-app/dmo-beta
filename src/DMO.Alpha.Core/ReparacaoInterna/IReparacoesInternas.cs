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
    RiFerramenta? Mf)
{
    public RiFerramenta? Ferramenta(ReparacaoTipo tipo) => tipo == ReparacaoTipo.Cm ? Cm : Mf;
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
/// mostrou (para ver se mudaram entretanto), o tipo e o número escrito.
/// </summary>
public sealed record PedidoDeReparacao(
    Guid OperacaoId,
    int? MachineId,
    int? JobOnId,
    ReparacaoTipo? Tipo,
    int? ContextoId,
    string? Numero);

/// <summary>Como acabou um registo. Só se recusa o impossível ou o que falta.</summary>
public enum ReparacaoResultado
{
    Gravado,
    JaRegistado,
    SemNumero,
    NumeroLongo,
    SemTipo,
    MaquinaNaoExiste,
    SemProducao,
    ProducaoMudou,
    SemFerramenta,
    FerramentaMudou
}

/// <summary>O resultado de um registo, com o número tal como ficou (sem espaços nas pontas).</summary>
public sealed record ReparacaoSaida(ReparacaoResultado Resultado, int? ReparacaoId = null, string? Numero = null)
{
    public bool Gravou => Resultado is ReparacaoResultado.Gravado or ReparacaoResultado.JaRegistado;
}

/// <summary>Corrigir o número de uma reparação (máquina, tipo ou produção errados: anula-se e regista-se outra vez).</summary>
public sealed record PedidoDeCorrecao(Guid OperacaoId, int ReparacaoId, string? Numero, string? Motivo = null);

/// <summary>Anular uma reparação: sai da lista e das contagens e fica na base com o rasto.</summary>
public sealed record PedidoDeAnulacao(Guid OperacaoId, int ReparacaoId, string? Motivo = null);

/// <summary>
/// Como acabou uma correção ou uma anulação. Só se recusa o impossível (a
/// reparação já não existe, já foi anulada ou outra pessoa mudou-a
/// entretanto) e o que o registo também recusa.
/// </summary>
public enum CorrecaoResultado
{
    Corrigido,
    Anulado,
    /// <summary>O mesmo envio outra vez: já estava feito.</summary>
    JaFeito,
    /// <summary>O número já era esse: nada mudou.</summary>
    Igual,
    NaoExiste,
    JaAnulada,
    Mudou,
    SemNumero,
    NumeroLongo,
    MotivoLongo
}

/// <summary>O resultado, com o número de que a mensagem fala (o corrigido ou o anulado).</summary>
public sealed record CorrecaoSaida(CorrecaoResultado Resultado, string? Numero = null)
{
    public bool Gravou => Resultado is CorrecaoResultado.Corrigido or CorrecaoResultado.Anulado or CorrecaoResultado.JaFeito;
}

/// <summary>Uma reparação na tabela por baixo do registo: pouco e curto.</summary>
public sealed record RiLinha(int Id, string Numero, ReparacaoTipo Tipo, string? Quem, DateTime RegistadoEmUtc);

/// <summary>A Reparação Interna: o dia operacional, a produção do dia, registar, corrigir, anular e a lista.</summary>
public interface IReparacoesInternas
{
    /// <summary>O dia operacional agora, pela hora de viragem da Reparação Interna (Canal Jobon; 8 por omissão).</summary>
    Task<DateOnly> DiaOperacionalAsync(CancellationToken cancellationToken = default);

    /// <summary>As produções da máquina cujas datas cobrem o dia operacional.</summary>
    Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, CancellationToken cancellationToken = default);

    Task<ReparacaoSaida> RegistarAsync(PedidoDeReparacao pedido, CancellationToken cancellationToken = default);

    /// <summary>Corrige o número (as mesmas regras do registo), com rasto. Quem tem o módulo pode corrigir (RI-04).</summary>
    Task<CorrecaoSaida> CorrigirAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken = default);

    /// <summary>Anula a reparação (fica na base), com rasto. Quem tem o módulo pode anular (RI-04).</summary>
    Task<CorrecaoSaida> AnularAsync(PedidoDeAnulacao pedido, CancellationToken cancellationToken = default);

    /// <summary>As reparações desta máquina e produção que contam (sem as anuladas), da mais recente para a mais antiga, em páginas de 25.</summary>
    Task<PagedList<RiLinha>> ListarAsync(int machineId, int jobOnId, int? pagina, CancellationToken cancellationToken = default);
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

    /// <summary>A ferramenta como a pessoa a reconhece: «CM 5538T324 · lote 2».</summary>
    public static string Ferramenta(ReparacaoTipo tipo, RiFerramenta ferramenta) =>
        ferramenta.Referencia is null
            ? $"{Tipo(tipo)} eliminada"
            : $"{Tipo(tipo)} {ferramenta.Referencia} · lote {ferramenta.Lote}";

    /// <summary>A produção numa lista de candidatas: «202501 · 5538T324 · 21/10 a 25/10».</summary>
    public static string Producao(RiProducao producao) =>
        $"{producao.Producao} · {producao.Referencia} · {Dia(producao.InicioEm)}"
        + (producao.FimEm is { } fim ? $" a {Dia(fim)}" : " sem fim");

    private static string Dia(DateOnly dia) => dia.ToString("dd/MM", CultureInfo.InvariantCulture);
}
