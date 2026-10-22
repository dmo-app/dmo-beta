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
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate)]
public sealed class SumarioModel(
    ISumarioQuery sumario,
    IJobOnListQuery jobOnList,
    CurrentUserCapabilities capabilities,
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

    /// <summary>O tipo escolhido no filtro; null = todos os que a conta vê.</summary>
    public SumarioTipo? Tipo { get; private set; }

    /// <summary>Os tipos que os módulos da conta deixam ver (as opções do filtro).</summary>
    public IReadOnlyList<SumarioTipo> TiposVisiveis { get; private set; } = [];

    /// <summary>As linhas do período escolhido, por tipo, pela ordem da lista.</summary>
    public IReadOnlyList<(SumarioTipo Tipo, IReadOnlyList<SumarioItem> Items)> Grupos { get; private set; } = [];

    public DmoCalendarPresentation Calendar { get; private set; } = null!;

    /// <summary>O painel lateral: as próximas produções desta semana, como no Job On.</summary>
    public IReadOnlyList<ProductionContextReadModel> ProximasEntradas { get; private set; } = [];

    private IReadOnlyList<string> _modulos = [];

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "ano")] int? ano,
        [FromQuery(Name = "mes")] int? mes,
        [FromQuery(Name = "dia")] int? dia,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "dias")] int? dias)
    {
        ViewData["Title"] = "Sumário";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("planeamento", "Planeamento"))) with
        {
            ActiveFunctionKey = "sumario",
            Functions = ShellStateFactory.PlanningFunctions("sumario")
        };

        _modulos = await capabilities.GetAsync(HttpContext.RequestAborted);
        TiposVisiveis = SumarioTipos.Visiveis(_modulos);
        Tipo = SumarioTipos.DaChave(tipo) is { } escolhido && TiposVisiveis.Contains(escolhido) ? escolhido : null;
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
        IReadOnlyList<SumarioTipo> tipos = Tipo is { } t ? [t] : TiposVisiveis;
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

        var domingo = hoje.AddDays((7 - (int)hoje.DayOfWeek) % 7);
        ProximasEntradas = JobOn.IndexModel.ProximasEntradasDaSemana(
            await jobOnList.ListWindowAsync(hoje, domingo, HttpContext.RequestAborted), hoje);

        var porDia = items.GroupBy(i => i.Dia).ToDictionary(g => g.Key, g => g.Count());
        Calendar = DmoCalendarPresentation.ForMonth(
            Year, Month,
            countForDay: data => porDia.GetValueOrDefault(data),
            selected: Day,
            navigationPage: PagePath,
            preservedQuery: FiltrosQuery()) with { LegendLabel = "Com informação" };
        return Page();
    }

    /// <summary>Os filtros que o calendário e os links levam (o tipo e os dias).</summary>
    public IReadOnlyDictionary<string, string> FiltrosQuery()
    {
        var query = new Dictionary<string, string>();
        if (Tipo is { } t)
        {
            query["tipo"] = SumarioTipos.Chave(t);
        }

        if (Dias != 1)
        {
            query["dias"] = Dias.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return query;
    }

    /// <summary>
    /// A página onde a linha se vê. As linhas do Controlo abrem o Resumo a quem
    /// tem o Controlo e a Revisão a quem só aprova; null quando não há página.
    /// </summary>
    public string? Href(SumarioItem item)
    {
        var temControlo = _modulos.Contains(DmoCapabilities.ControloCreate, StringComparer.Ordinal);
        return item.Tipo switch
        {
            SumarioTipo.JobOnCriado or SumarioTipo.ProducaoComeca or SumarioTipo.JobOnAlterado when item.JobOnId is { } id =>
                $"/JobOn?jobon_id={id}",
            SumarioTipo.JobOnPorVer or SumarioTipo.ControloNaoAprovado when item.JobOnId is { } id =>
                temControlo ? $"/Controlo/Resumo?jobon_id={id}" : $"/Controlo/Approve?jobon_id={id}",
            SumarioTipo.ControloPorAprovar when item.JobOnId is { } id =>
                $"/Controlo/Approve?jobon_id={id}&ano={item.Dia.Year}&mes={item.Dia.Month}&dia={item.Dia.Day}",
            SumarioTipo.BqMovimentoApagado =>
                item.BqContextId is { } bq ? $"/Boquilhas?bq_id={bq}" : "/Boquilhas?historico=1",
            SumarioTipo.FerramentaDadosEmFalta when item.ToolId is { } toolId =>
                DmoAvisoLink.FichaFerramenta(toolId, item.Titulo, tecnica: true).Href,
            _ => null
        };
    }
}
