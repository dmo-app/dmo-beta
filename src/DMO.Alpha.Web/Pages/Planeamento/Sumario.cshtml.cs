using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Shared;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Planeamento;

/// <summary>
/// Planeamento · Sumário (Diogo, 19/10/2026: «uma pagina que fica no
/// planeamento ao lado do jobon. ela mostra com base nos modulos das contas
/// sumarios do que fazer, alteraçoes feitas»; «o calendario pode ser uma boa
/// forma de abrir o sumario daquele dia»; «um filtro para tipo de informacao
/// que mostra e dias»). Abre no dia de hoje. Cada tipo só aparece a quem tem
/// o módulo dele (<see cref="SumarioTipos.PodeVer"/>) e cada linha abre a
/// página onde se vê. Só leitura.
/// Abre a quem tem o Job On criar ou ver (Diogo, 26/10/2026: «sumario é
/// para quem tem jobon ver tambem»).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate, DmoCapabilities.JobOnView)]
public sealed class SumarioModel(
    ISumarioQuery sumario,
    IJobOnListQuery jobOnList,
    CurrentUserCapabilities capabilities,
    SumarioNovidades novidades,
    ShellStateFactory shellState) : PageModel
{
    private const string PagePath = "/Planeamento/Sumario";

    /// <summary>Os períodos do filtro «Dias»: o dia escolhido e os anteriores.</summary>
    public static IReadOnlyList<int> PeriodosDias { get; } = [1, 7, 30];

    public int Year { get; private set; }

    public int Month { get; private set; }

    /// <summary>O dia escolhido; null quando se mudou de mês sem escolher um dia.</summary>
    public DateOnly? Day { get; private set; }

    /// <summary>Quantos dias a lista mostra, até ao dia escolhido (incluído).</summary>
    public int Dias { get; private set; } = 1;

    /// <summary>
    /// A chave escolhida no filtro, já validada: «importante», uma opção de
    /// <see cref="SumarioTipos.Filtros"/> ou um tipo só (os links antigos);
    /// null = toda a informação que a conta vê.
    /// </summary>
    public string? Escolha { get; private set; }

    /// <summary>Os tipos da escolha que a conta vê; null = todos.</summary>
    public IReadOnlyList<SumarioTipo>? Escolhidos { get; private set; }

    /// <summary>As opções do filtro a seguir a «Importante» que a conta vê (Job On, Armazém, Controlo).</summary>
    public IReadOnlyList<SumarioFiltro> FiltrosVisiveis { get; private set; } = [];

    /// <summary>Os tipos que os módulos da conta deixam ver (as opções do filtro).</summary>
    public IReadOnlyList<SumarioTipo> TiposVisiveis { get; private set; } = [];

    /// <summary>As linhas do período escolhido, por tipo, pela ordem da lista.</summary>
    public IReadOnlyList<(SumarioTipo Tipo, IReadOnlyList<SumarioItem> Items)> Grupos { get; private set; } = [];

    public DmoCalendarPresentation Calendar { get; private set; } = null!;

    /// <summary>O painel lateral: as próximas produções desta semana, como no Job On.</summary>
    public IReadOnlyList<ProductionContextReadModel> ProximasEntradas { get; private set; } = [];

    /// <summary>A chave do filtro «Importante» (só o grupo do topo).</summary>
    public const string ChaveImportante = "importante";

    /// <summary>
    /// O GRUPO «IMPORTANTE» NO TOPO (Diogo, 23/10/2026: «os notices
    /// importantes entram no sumário. Criar uma Informação "Importante" e
    /// ficam sempre no topo até resolver»): os por resolver, seja qual for o
    /// dia escolhido, com os lotes novos por criar e os lembretes da ficha
    /// (antes avisos à parte). Vazio com um filtro de informação.
    /// </summary>
    public IReadOnlyList<SumarioItem> Importantes { get; private set; } = [];

    /// <summary>O filtro «Importante»: só o grupo do topo, sem a informação do período.</summary>
    public bool SoImportante { get; private set; }

    /// <summary>Os tipos de informação que os módulos deixam ver (as opções do filtro a seguir a «Importante»).</summary>
    public IReadOnlyList<SumarioTipo> TiposInformacao { get; private set; } = [];

    /// <summary>A conta vê algum tipo importante (o filtro mostra «Importante»).</summary>
    public bool VeImportantes { get; private set; }

    private IReadOnlyList<string> _modulos = [];

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "ano")] int? ano,
        [FromQuery(Name = "mes")] int? mes,
        [FromQuery(Name = "dia")] int? dia,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "dias")] int? dias)
    {
        ViewData["Title"] = "Sumário";
        // Só com o Job On ver, sem o Histórico (auditoria de 27/10/2026); o «Job On»
        // e o título do painel levam ao Planeamento em leitura (Diogo, 27/10/2026).
        var soVer = !await capabilities.HasAsync(DmoCapabilities.JobOnCreate, HttpContext.RequestAborted);
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("planeamento", "Planeamento"))) with
        {
            ActiveFunctionKey = "sumario",
            Functions = ShellStateFactory.PlanningFunctions("sumario", soVer: soVer)
        };
        // Abrir o Sumário é ver os importantes: o ponto do separador apaga-se
        // (Diogo, 23/10/2026).
        await novidades.MarcarVistoAsync(HttpContext.RequestAborted);

        _modulos = await capabilities.GetAsync(HttpContext.RequestAborted);
        var visiveis = SumarioTipos.Visiveis(_modulos);
        TiposVisiveis = visiveis.Where(SumarioTipos.DoPeriodo).ToArray();
        TiposInformacao = TiposVisiveis.Where(t => !SumarioTipos.Importante(t)).ToArray();
        VeImportantes = visiveis.Any(SumarioTipos.Importante);
        FiltrosVisiveis = SumarioTipos.Filtros.Where(f => f.Tipos.Any(t => visiveis.Contains(t))).ToArray();
        // Uma opção do filtro junta vários tipos; um tipo só no endereço (os
        // links antigos) continua a abrir só esse tipo. Os importantes da
        // escolha (o Controlo) ficam no grupo do topo.
        if (tipo == ChaveImportante && VeImportantes)
        {
            Escolha = tipo;
        }
        else if (SumarioTipos.FiltroDaChave(tipo) is { } filtro && FiltrosVisiveis.Contains(filtro))
        {
            Escolha = filtro.Chave;
            Escolhidos = filtro.Tipos.Where(t => visiveis.Contains(t)).ToArray();
        }
        else if (SumarioTipos.DaChave(tipo) is { } escolhido && visiveis.Contains(escolhido))
        {
            Escolha = tipo;
            Escolhidos = [escolhido];
        }

        SoImportante = VeImportantes && (Escolha == ChaveImportante || Escolhidos is { } e && e.All(SumarioTipos.Importante));
        if (VeImportantes && (Escolhidos is null || Escolhidos.Any(SumarioTipos.Importante)))
        {
            var importantes = await novidades.ImportantesAsync(_modulos, HttpContext.RequestAborted);
            Importantes = Escolhidos is { } so ? importantes.Where(i => so.Contains(i.Tipo)).ToList() : importantes;
        }

        Dias = dias is { } d && PeriodosDias.Contains(d) ? d : 1;

        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var mesValido = ano is >= 2000 and <= 2100 && mes is >= 1 and <= 12;
        Year = mesValido ? ano!.Value : hoje.Year;
        Month = mesValido ? mes!.Value : hoje.Month;
        if (dia is { } diaPedido && diaPedido >= 1 && diaPedido <= DateTime.DaysInMonth(Year, Month))
        {
            Day = new DateOnly(Year, Month, diaPedido);
        }
        // dia=0 é o duplo clique do calendário que tira a seleção (componentes partilhados).
        else if (dia != 0 && Year == hoje.Year && Month == hoje.Month)
        {
            Day = hoje;
        }

        // Uma leitura só: o mês do calendário e o período da lista.
        var inicioMes = new DateOnly(Year, Month, 1);
        var fimMes = inicioMes.AddMonths(1);
        var de = Day is { } escolhidoDia && escolhidoDia.AddDays(1 - Dias) < inicioMes ? escolhidoDia.AddDays(1 - Dias) : inicioMes;
        IReadOnlyList<SumarioTipo> tipos = SoImportante ? [] : Escolhidos is { } t ? t.Where(SumarioTipos.DoPeriodo).Where(x => !SumarioTipos.Importante(x)).ToArray() : TiposInformacao;
        IReadOnlyList<SumarioItem> items = tipos.Count == 0
            ? []
            : await sumario.PeriodAsync(de, fimMes, tipos, HttpContext.RequestAborted);

        if (Day is { } selecionado)
        {
            var desde = selecionado.AddDays(1 - Dias);
            Grupos = items
                .Where(i => i.Dia >= desde && i.Dia <= selecionado)
                .GroupBy(i => i.Tipo)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, (IReadOnlyList<SumarioItem>)g.ToList()))
                .ToList();
        }

        ProximasEntradas = JobOn.IndexModel.ProximasEntradasDaSemana(
            await jobOnList.ListWindowAsync(hoje, JobOn.IndexModel.AteUmaSemana(hoje), HttpContext.RequestAborted), hoje);

        var porDia = items.GroupBy(i => i.Dia).ToDictionary(g => g.Key, g => g.Count());
        Calendar = DmoCalendarPresentation.ForMonth(
            Year, Month,
            countForDay: data => porDia.GetValueOrDefault(data),
            selected: Day,
            navigationPage: PagePath,
            preservedQuery: FiltrosQuery()) with { LegendLabel = "Com informação" };
        return Page();
    }

    /// <summary>
    /// A opção do filtro que aparece escolhida: a da escolha; um tipo só (link
    /// antigo) mostra a opção que o junta ou, sem ela, «Importante».
    /// </summary>
    public string? OpcaoEscolhida =>
        Escolhidos is { Count: 1 } um && SumarioTipos.Chave(um[0]) == Escolha
            ? FiltrosVisiveis.FirstOrDefault(f => f.Tipos.Contains(um[0]))?.Chave ?? (SoImportante ? ChaveImportante : null)
            : Escolha;

    /// <summary>Os filtros que o calendário e os links levam (o tipo e os dias).</summary>
    public IReadOnlyDictionary<string, string> FiltrosQuery()
    {
        var query = new Dictionary<string, string>();
        if (Escolha is { } escolha)
        {
            query["tipo"] = escolha;
        }

        if (Dias != 1)
        {
            query["dias"] = Dias.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return query;
    }

    /// <summary>
    /// A página que resolve a linha (Diogo, 23/10/2026: «no sumário mostrar
    /// sempre os links que levam à página que resolve o problema»), sempre uma
    /// que a conta abre. As linhas do Controlo abrem o Resumo a quem tem o
    /// Controlo e a Revisão a quem só aprova; os por aprovar abrem a Revisão a
    /// quem aprova. Os dados técnicos resolvem-se na ficha; sem o módulo
    /// Ferramentas, no Resumo do controlo da produção que precisa deles.
    /// </summary>
    public string? Href(SumarioItem item) => Href(item, _modulos);

    /// <inheritdoc cref="Href(SumarioItem)"/>
    public static string? Href(SumarioItem item, IReadOnlyCollection<string> modulos)
    {
        var temControlo = modulos.Contains(DmoCapabilities.ControloCreate, StringComparer.Ordinal);
        var aprova = modulos.Contains(DmoCapabilities.ControloApprove, StringComparer.Ordinal);
        var temFerramentas = modulos.Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
        var temArmazem = modulos.Contains(DmoCapabilities.ArmazemGestao, StringComparer.Ordinal);
        if (item.Link is { } link)
        {
            return link;
        }

        return item.Tipo switch
        {
            SumarioTipo.JobOnCriado or SumarioTipo.ProducaoComeca or SumarioTipo.JobOnAlterado when item.JobOnId is { } id =>
                $"/JobOn?jobon_id={id}",
            SumarioTipo.JobOnPorVer or SumarioTipo.ControloNaoAprovado when item.JobOnId is { } id =>
                temControlo ? $"/Controlo/Resumo?jobon_id={id}" : $"/Controlo/Approve?jobon_id={id}",
            SumarioTipo.ControloPorAprovar when item.JobOnId is { } id =>
                aprova ? $"/Controlo/Approve?jobon_id={id}&ano={item.Dia.Year}&mes={item.Dia.Month}&dia={item.Dia.Day}" : $"/Controlo/Resumo?jobon_id={id}",
            SumarioTipo.BqMovimentoApagado =>
                item.BqContextId is { } bq ? $"/Boquilhas?bq_id={bq}" : "/Boquilhas?historico=1",
            SumarioTipo.FerramentaDadosEmFalta when item.ToolId is { } toolId && temFerramentas =>
                DmoAvisoLink.FichaFerramenta(toolId, item.Titulo, tecnica: true).Href,
            SumarioTipo.FerramentaDadosEmFalta when item.JobOnId is { } id =>
                $"/Controlo/Resumo?jobon_id={id}",
            SumarioTipo.UsoAtualizado when item.ToolId is { } toolId =>
                DmoAvisoLink.FichaFerramenta(toolId, item.Titulo).Href,
            // Os movimentos do Armazém abrem os Movimentos a quem tem o
            // Armazém, a ficha a quem tem as Ferramentas e, senão, o Job On
            // do movimento (a folha em leitura a quem só tem o Job On ver).
            SumarioTipo.ArmazemMovimento when item.ToolId is { } toolId && temArmazem =>
                DmoAvisoLink.MovimentosDoArmazem(toolId, item.Titulo).Href,
            SumarioTipo.ArmazemMovimento when item.ToolId is { } toolId && temFerramentas =>
                DmoAvisoLink.FichaFerramenta(toolId, item.Titulo).Href,
            SumarioTipo.ArmazemMovimento when item.JobOnId is { } id && modulos.Contains(DmoCapabilities.JobOnCreate, StringComparer.Ordinal) =>
                $"/JobOn?jobon_id={id}",
            SumarioTipo.ArmazemMovimento when item.JobOnId is { } id =>
                $"/JobOn/Folha?jobon_id={id}",
            _ => null
        };
    }
}
