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
    FerramentaDadosEmFalta = 8,

    /// <summary>
    /// O % de uso de uma ferramenta mudou: o Job On onde trabalhou acabou
    /// (Diogo, 23/10/2026: «% de uso atualizada»).
    /// </summary>
    UsoAtualizado = 9,

    /// <summary>
    /// Os lotes novos registados no Armazém por criar nas Ferramentas (Diogo,
    /// 23/10/2026: «o chefe dá approve e passa para as ferramentas»). Não
    /// dependem do dia: mostram-se sempre, nos avisos do topo.
    /// </summary>
    LoteNovoPorCriar = 10
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

    /// <summary>
    /// A página que resolve, quando não sai das chaves acima (o lote novo
    /// abre o registo das Ferramentas já preenchido); null = pelas chaves.
    /// </summary>
    public string? Link { get; init; }

    /// <summary>O registo do lote novo no Armazém (só nos lotes novos por criar).</summary>
    public int? LoteNovoId { get; init; }

    /// <summary>
    /// O lembrete «Utilização por atualizar» da ficha (a ferramenta entrou no
    /// Armazém), mostrado como linha do % de uso.
    /// </summary>
    public bool Lembrete { get; init; }

    /// <summary>
    /// Já resolvida (o % de uso com a leitura do fim): sai do grupo
    /// «Importante». As outras resolvidas já não vêm da consulta.
    /// </summary>
    public bool Resolvido { get; init; }

    /// <summary>O instante da linha para o «visto»: a hora, ou o início do dia sem hora.</summary>
    public DateTime InstanteUtc => QuandoUtc ?? DateTime.SpecifyKind(
        Runtime.DmoBusinessTime.ToUtc(Dia.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc);
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
        SumarioTipo.UsoAtualizado => "% de uso atualizada",
        SumarioTipo.LoteNovoPorCriar => "Lotes novos por criar",
        _ => tipo.ToString()
    };

    /// <summary>
    /// Importante (pede uma ação) ou só informação (Diogo, 23/10/2026: os
    /// controlos por aprovar e não aprovados, os Job Ons por ver, os dados
    /// técnicos em falta, as boquilhas apagadas, o % de uso e os lotes novos
    /// são importantes; os criados, as mudanças e as alterações são
    /// informação). A lista marca-os e o ponto do separador conta só estes.
    /// </summary>
    public static bool Importante(SumarioTipo tipo) => tipo switch
    {
        SumarioTipo.JobOnCriado or SumarioTipo.ProducaoComeca or SumarioTipo.JobOnAlterado => false,
        _ => true
    };

    /// <summary>
    /// Os tipos que vêm do período escolhido (o calendário e o filtro); os
    /// lotes novos por criar mostram-se sempre, fora do período.
    /// </summary>
    public static bool DoPeriodo(SumarioTipo tipo) => tipo != SumarioTipo.LoteNovoPorCriar;

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
        SumarioTipo.UsoAtualizado => "uso-atualizado",
        SumarioTipo.LoteNovoPorCriar => "lotes-novos",
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
            // Diogo, 23/10/2026: os dados técnicos fazem falta ao Peso e aos
            // Pegamentos, por isso são de quem tem o Controlo Criar.
            SumarioTipo.FerramentaDadosEmFalta => Tem(DmoCapabilities.ControloCreate),
            SumarioTipo.UsoAtualizado or SumarioTipo.LoteNovoPorCriar => Tem(DmoCapabilities.Ferramentas),
            _ => false
        };
    }

    /// <summary>Os tipos que os módulos da conta deixam ver, pela ordem da lista.</summary>
    public static IReadOnlyList<SumarioTipo> Visiveis(IReadOnlyCollection<string> modulos) =>
        Todos.Where(t => PodeVer(t, modulos)).ToArray();
}

/// <summary>A leitura do Sumário: só os tipos pedidos, num período de dias (hora de Lisboa).</summary>
public sealed record SumarioPagina(IReadOnlyList<SumarioItem> Items, bool HaMais);

public interface ISumarioQuery
{
    /// <summary>Blocos de pendências por tipo, com continuação explícita para não ocultar as posteriores ao limite.</summary>
    Task<SumarioPagina> PorResolverPaginaAsync(DateOnly hoje, IReadOnlyCollection<SumarioTipo> tipos, int pagina, CancellationToken cancellationToken = default);

    /// <summary>
    /// As linhas dos tipos pedidos com dia entre <paramref name="from"/>
    /// (incluído) e <paramref name="toExclusive"/>, as mais recentes primeiro.
    /// </summary>
    Task<IReadOnlyList<SumarioItem>> PeriodAsync(
        DateOnly from,
        DateOnly toExclusive,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os importantes por resolver dos tipos pedidos, seja qual for o dia
    /// (Diogo, 23/10/2026: «ficam sempre no topo até resolver»), as mais
    /// recentes primeiro. Cada tipo sai quando o estado diz que se resolveu;
    /// os lotes novos e os lembretes da ficha vêm dos serviços deles.
    /// </summary>
    Task<IReadOnlyList<SumarioItem>> PorResolverAsync(
        DateOnly hoje,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken = default);
}
