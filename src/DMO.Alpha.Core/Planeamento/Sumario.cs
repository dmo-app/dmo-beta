using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Core.Planeamento;

/// <summary>
/// Os tipos de informação do Sumário do Planeamento (Diogo, 19/10/2026: «uma
/// pagina que fica no planeamento ao lado do jobon. ela mostra com base nos
/// modulos das contas sumarios do que fazer, alteraçoes feitas»). A ordem é
/// a da lista.
/// </summary>
public enum SumarioTipo
{
    JobOnCriado = 1,
    ProducaoComeca = 2,
    JobOnAlterado = 3,
    JobOnPorVer = 4,
    ControloPorAprovar = 5,
    ControloNaoAprovado = 6,
    BqMovimentoApagado = 7,
    FerramentaDadosEmFalta = 8
}

/// <summary>
/// Uma linha do Sumário: o dia (hora de Lisboa) em que conta, o que aconteceu
/// e as chaves da página onde se vê. <see cref="Titulo"/> leva a referência,
/// nunca o nome técnico.
/// </summary>
public sealed record SumarioItem(
    SumarioTipo Tipo,
    DateOnly Dia,
    DateTime? QuandoUtc,
    string Titulo,
    string? Detalhe,
    string? Por)
{
    public int? JobOnId { get; init; }

    public string? ToolId { get; init; }

    public int? BqContextId { get; init; }

    /// <summary>A máquina da linha (B1…C3), mostrada à parte com a cor dela; null sem máquina.</summary>
    public string? Maquina { get; init; }
}

/// <summary>
/// Quem vê cada tipo, num só sítio: os módulos são as permissões. A
/// informação do Controlo é de quem tem um dos dois módulos do Controlo
/// (Diogo, 19/10/2026: «o controlo create tambem deve poder ver os controlos
/// no jobon. basicamente a regra fica aparece a quem tem modulos do controlo»).
/// </summary>
public static class SumarioTipos
{
    public static IReadOnlyList<SumarioTipo> Todos { get; } = Enum.GetValues<SumarioTipo>();

    public static string Nome(SumarioTipo tipo) => tipo switch
    {
        SumarioTipo.JobOnCriado => "Job Ons criados",
        SumarioTipo.ProducaoComeca => "Mudanças", // Diogo, 19/10/2026: «mudar de produções que começam para Mudanças»
        SumarioTipo.JobOnPorVer => "Job Ons por ver no Controlo",
        SumarioTipo.JobOnAlterado => "Alterações a Job Ons",
        SumarioTipo.ControloPorAprovar => "Controlos por aprovar",
        SumarioTipo.ControloNaoAprovado => "Controlos não aprovados",
        SumarioTipo.BqMovimentoApagado => "Movimentos de boquilhas apagados",
        SumarioTipo.FerramentaDadosEmFalta => "Ferramentas com dados técnicos em falta",
        _ => tipo.ToString()
    };

    /// <summary>A chave do tipo no endereço (o filtro).</summary>
    public static string Chave(SumarioTipo tipo) => tipo switch
    {
        SumarioTipo.JobOnCriado => "criados",
        SumarioTipo.ProducaoComeca => "comecam",
        SumarioTipo.JobOnPorVer => "por-ver",
        SumarioTipo.JobOnAlterado => "alteracoes",
        SumarioTipo.ControloPorAprovar => "por-aprovar",
        SumarioTipo.ControloNaoAprovado => "nao-aprovados",
        SumarioTipo.BqMovimentoApagado => "bq-apagados",
        SumarioTipo.FerramentaDadosEmFalta => "dados-em-falta",
        _ => tipo.ToString()
    };

    public static SumarioTipo? DaChave(string? chave) =>
        Todos.Cast<SumarioTipo?>().FirstOrDefault(t => Chave(t!.Value) == chave);

    public static bool PodeVer(SumarioTipo tipo, IReadOnlyCollection<string> modulos)
    {
        bool Tem(string modulo) => modulos.Contains(modulo, StringComparer.Ordinal);
        return tipo switch
        {
            SumarioTipo.JobOnCriado or SumarioTipo.ProducaoComeca or SumarioTipo.JobOnAlterado => Tem(DmoCapabilities.JobOnCreate),
            SumarioTipo.JobOnPorVer or SumarioTipo.ControloPorAprovar or SumarioTipo.ControloNaoAprovado =>
                Tem(DmoCapabilities.ControloCreate) || Tem(DmoCapabilities.ControloApprove),
            SumarioTipo.BqMovimentoApagado => Tem(DmoCapabilities.Boquilhas),
            SumarioTipo.FerramentaDadosEmFalta => Tem(DmoCapabilities.Ferramentas),
            _ => false
        };
    }

    /// <summary>Os tipos que os módulos da conta deixam ver, pela ordem da lista.</summary>
    public static IReadOnlyList<SumarioTipo> Visiveis(IReadOnlyCollection<string> modulos) =>
        Todos.Where(t => PodeVer(t, modulos)).ToArray();
}

/// <summary>A leitura do Sumário: só os tipos pedidos, num período de dias (hora de Lisboa).</summary>
public interface ISumarioQuery
{
    /// <summary>
    /// As linhas dos tipos pedidos com dia entre <paramref name="from"/>
    /// (incluído) e <paramref name="toExclusive"/>, as mais recentes primeiro.
    /// </summary>
    Task<IReadOnlyList<SumarioItem>> PeriodAsync(
        DateOnly from,
        DateOnly toExclusive,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken = default);
}
