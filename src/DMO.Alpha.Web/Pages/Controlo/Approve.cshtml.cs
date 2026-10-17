using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Shared;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// A Revisão do Controlo (decisão do proprietário, 17/10/2026, a partir da
/// versão HTML): o calendário marca os dias com controlos por aprovar, a
/// lista mostra os controlos do dia escolhido (cor por máquina, como no Job
/// On) e o controlo aberto mostra-se ao lado para o chefe aprovar ou não
/// aprovar com nota. Nada carrega sem um dia escolhido. Aprovar ou não
/// aprovar é só registo: nunca bloqueia nada.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloApprove)]
public sealed class ApproveModel : PageModel
{
    private readonly IControloRevisaoQuery _revisao;
    private readonly IProductionContextQuery _production;
    private readonly IEnsureControloForJobOnHandler _ensure;
    private readonly IPesoQuery _peso;
    private readonly IPegamentosQuery _pegamentos;
    private readonly ControloApprovalHandler _approval;
    private readonly ShellStateFactory _shell;
    private readonly DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks _mcaliper;

    public ApproveModel(
        IControloRevisaoQuery revisao,
        IProductionContextQuery production,
        IEnsureControloForJobOnHandler ensure,
        IPesoQuery peso,
        IPegamentosQuery pegamentos,
        ControloApprovalHandler approval,
        ShellStateFactory shell,
        DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks mcaliper)
    {
        _mcaliper = mcaliper;
        _revisao = revisao;
        _production = production;
        _ensure = ensure;
        _peso = peso;
        _pegamentos = pegamentos;
        _approval = approval;
        _shell = shell;
    }

    public DmoCalendarPresentation Calendar { get; private set; } = null!;
    public int Year { get; private set; }
    public int Month { get; private set; }

    /// <summary>O dia escolhido; null enquanto nenhum dia foi escolhido.</summary>
    public DateOnly? Day { get; private set; }

    /// <summary>A lista dos controlos sem data de registo (só a pedido).</summary>
    public bool Undated { get; private set; }
    public int UndatedPendingCount { get; private set; }
    public string? Procura { get; private set; }
    public ControloRevisaoEstado? Estado { get; private set; }
    public IReadOnlyList<ControloRevisaoItem> Items { get; private set; } = [];

    public ProductionContextReadModel? Selected { get; private set; }
    public ControloRevisaoItem? SelectedItem { get; private set; }
    public int? ControloId { get; private set; }
    public PesoReadModel? Peso { get; private set; }
    public PegamentosSheetReadModel? Pegamentos { get; private set; }
    public ControloApprovalReadModel? Approval { get; private set; }

    /// <summary>O nome de quem decidiu (aprovou ou não aprovou), quando o operador existe.</summary>
    public string? DecidedByName { get; private set; }
    public ControloRevisaoItem? Previous { get; private set; }
    public PesoReadModel? PreviousPeso { get; private set; }
    public bool MissingProduction { get; private set; }

    /// <summary>
    /// Os links do mcaliper das ferramentas da produção (CM, MF, BQ), por
    /// ordem; só os associados. Um clique no ícone copia (Diogo, 17/10/2026).
    /// </summary>
    public IReadOnlyList<(string Area, string Url)> McaliperLinks { get; private set; } = [];

    /// <summary>O nome de quem está a rever (o «Responsável» da decisão).</summary>
    public string Reviewer { get; private set; } = "";

    /// <summary>A aprovação foi recusada por falta de identidade na sessão (PRG).</summary>
    public bool ApprovalRefusedNoIdentity { get; private set; }

    /// <summary>O «Não aprovar» chegou sem nota (PRG): nada foi gravado.</summary>
    public bool NoteMissing { get; private set; }

    /// <summary>Os parâmetros da lista a manter nos links (dia, procura, estado).</summary>
    public Dictionary<string, string> ListQuery { get; } = new();

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "ano")] int? year,
        [FromQuery(Name = "mes")] int? month,
        [FromQuery(Name = "dia")] int? day,
        [FromQuery(Name = "sem_data")] string? undated,
        [FromQuery(Name = "procura")] string? procura,
        [FromQuery(Name = "estado")] string? estado,
        [FromQuery(Name = "aprovacao_recusada")] string? approvalRefusal = null,
        [FromQuery(Name = "nota_em_falta")] string? noteMissing = null)
    {
        ViewData["Title"] = "Controlo";
        ApprovalRefusedNoIdentity = approvalRefusal == "sem-identidade";
        NoteMissing = noteMissing == "1";
        ViewData["Shell"] = await _shell.ForControloReviewAsync();
        Reviewer = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "";

        Procura = string.IsNullOrWhiteSpace(procura) ? null : procura.Trim();
        Estado = estado switch
        {
            "pendente" => ControloRevisaoEstado.Pendente,
            "aprovado" => ControloRevisaoEstado.Aprovado,
            "nao-aprovado" => ControloRevisaoEstado.NaoAprovado,
            _ => null
        };
        Undated = undated == "1";

        // O controlo aberto leva o calendário para o dia em que foi registado.
        if (jobOnId is int requested)
        {
            SelectedItem = await _revisao.FindByJobOnAsync(requested, HttpContext.RequestAborted);
            if (day is null && !Undated && SelectedItem is not null)
            {
                if (SelectedItem.RegistadoEmUtc is { } em)
                {
                    var local = DmoBusinessTime.ToLocal(em);
                    year = local.Year;
                    month = local.Month;
                    day = local.Day;
                }
                else
                {
                    Undated = true;
                }
            }
        }

        var today = DmoBusinessTime.Now;
        Year = year is >= 2000 and <= 2100 ? year.Value : today.Year;
        Month = month is >= 1 and <= 12 ? month.Value : today.Month;
        if (!Undated && day is int d && d >= 1 && d <= DateTime.DaysInMonth(Year, Month))
        {
            Day = new DateOnly(Year, Month, d);
        }

        if (Procura is not null) ListQuery["procura"] = Procura;
        if (estado is not null && Estado is not null) ListQuery["estado"] = estado;

        var days = await _revisao.MonthAsync(Year, Month, HttpContext.RequestAborted);
        Calendar = BuildCalendar(days);
        UndatedPendingCount = await _revisao.CountUndatedPendingAsync(HttpContext.RequestAborted);

        if (Day is { } chosen)
        {
            Items = await _revisao.DayAsync(chosen, Procura, Estado, HttpContext.RequestAborted);
        }
        else if (Undated)
        {
            Items = await _revisao.UndatedAsync(Procura, Estado, HttpContext.RequestAborted);
        }

        if (jobOnId is not int id)
            return Page();

        Selected = await _production.GetAsync(id, HttpContext.RequestAborted);
        if (Selected is null)
        {
            MissingProduction = true;
            return Page();
        }

        ViewData["Shell"] = await _shell.ForControloReviewAsync(Selected.JobOnId);

        var links = await _mcaliper.GetAsync(
            new[] { Selected.CmToolId, Selected.MfToolId, Selected.BqToolId }, HttpContext.RequestAborted);
        McaliperLinks = new[] { ("CM", Selected.CmToolId), ("MF", Selected.MfToolId), ("BQ", Selected.BqToolId) }
            .Where(slot => slot.Item2 is { } toolId && links.ContainsKey(toolId))
            .Select(slot => (slot.Item1, links[slot.Item2!]))
            .ToList();

        // Mesmo backfill idempotente que o Resumo operacional; não cria
        // um novo Controlo nem nenhum documento de medição.
        var ensured = await _ensure.HandleAsync(
            new EnsureControloForJobOn(id), HttpContext.RequestAborted);
        ControloId = ensured.ControloId;
        if (ControloId is int controloId)
        {
            Peso = await _peso.FindByControloAsync(controloId, HttpContext.RequestAborted);
            Pegamentos = await _pegamentos.FindSheetByControloAsync(controloId, HttpContext.RequestAborted);
            Approval = await _approval.GetAsync(controloId, HttpContext.RequestAborted);
            if ((Approval?.ApprovedByOperatorId ?? Approval?.NotApprovedByOperatorId) is { } decidedBy)
            {
                DecidedByName = await _revisao.OperatorNameAsync(decidedBy, HttpContext.RequestAborted) ?? decidedBy;
            }
        }

        Previous = await _revisao.FindPreviousWithPesoAsync(id, HttpContext.RequestAborted);
        if (Previous is not null)
        {
            PreviousPeso = await _peso.FindByControloAsync(Previous.ControloId, HttpContext.RequestAborted);
        }

        return Page();
    }

    public Task<IActionResult> OnPostApproveAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "nota")] string? note = null) =>
        DecideAsync(jobOnId, note, approve: true);

    public Task<IActionResult> OnPostNaoAprovarAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "nota")] string? note = null) =>
        DecideAsync(jobOnId, note, approve: false);

    private async Task<IActionResult> DecideAsync(int? jobOnId, string? note, bool approve)
    {
        if (jobOnId is not int id)
            return RedirectToPage("/Controlo/Approve");

        var production = await _production.GetAsync(id, HttpContext.RequestAborted);
        if (production is null)
            return RedirectToPage("/Controlo/Approve");

        // Sem identidade na sessão não há registo de quem decidiu: a decisão
        // é recusada e a página di-lo.
        var actor = new[] { DmoClaimTypes.OperatorId, DmoClaimTypes.Email, DmoClaimTypes.DisplayName }
            .Select(type => User.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (actor is null)
            return RedirectToPage("/Controlo/Approve", new { jobon_id = id, aprovacao_recusada = "sem-identidade" });

        if (!approve && string.IsNullOrWhiteSpace(note))
            return RedirectToPage("/Controlo/Approve", new { jobon_id = id, nota_em_falta = "1" });

        note = note?.Trim();
        if (note is { Length: > 2000 })
            note = note[..2000];

        var ensured = await _ensure.HandleAsync(
            new EnsureControloForJobOn(id), HttpContext.RequestAborted);
        if (ensured.ControloId is int controloId)
        {
            if (approve)
                await _approval.ApproveAndGetAsync(controloId, actor, note, HttpContext.RequestAborted);
            else
                await _approval.NotApproveAndGetAsync(controloId, actor, note, HttpContext.RequestAborted);
        }
        return RedirectToPage("/Controlo/Approve", new { jobon_id = id });
    }

    /// <summary>O link de um controlo da lista, mantendo o dia e os filtros.</summary>
    public string ItemLink(ControloRevisaoItem item)
    {
        var query = new Dictionary<string, string?>(ListQuery.ToDictionary(p => p.Key, p => (string?)p.Value))
        {
            ["jobon_id"] = item.JobOnId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (Day is { } chosen)
        {
            query["ano"] = chosen.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            query["mes"] = chosen.Month.ToString(System.Globalization.CultureInfo.InvariantCulture);
            query["dia"] = chosen.Day.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (Undated)
        {
            query["sem_data"] = "1";
        }

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Controlo/Approve", query);
    }

    private DmoCalendarPresentation BuildCalendar(IReadOnlyList<ControloRevisaoDia> days) =>
        // O ponto acende só nos dias com controlos por aprovar; a contagem é de todos.
        DmoCalendarPresentation.ForMonth(
            Year, Month,
            countForDay: data => days.FirstOrDefault(x => x.Day == data.Day)?.Total ?? 0,
            selected: Day,
            navigationPage: "/Controlo/Approve",
            preservedQuery: ListQuery,
            hasForDay: data => days.FirstOrDefault(x => x.Day == data.Day) is { Pendentes: > 0 })
        with { LegendLabel = "Por aprovar" };
}
