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
    private readonly ControloAreaStateHandler _areas;
    private readonly PuncoesHandler _puncoes;
    private readonly IMachineListQuery _machines;
    private readonly RevisaoControloAnteriorQuery _anteriores;
    private readonly ResumoPdfDocumentBuilder _resumoPdf;
    private readonly CurrentUserCapabilities _capabilities;

    public ApproveModel(
        IControloRevisaoQuery revisao,
        IProductionContextQuery production,
        IEnsureControloForJobOnHandler ensure,
        IPesoQuery peso,
        IPegamentosQuery pegamentos,
        ControloApprovalHandler approval,
        ShellStateFactory shell,
        DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks mcaliper,
        ControloAreaStateHandler areas,
        PuncoesHandler puncoes,
        IMachineListQuery machines,
        RevisaoControloAnteriorQuery anteriores,
        ResumoPdfDocumentBuilder resumoPdf,
        CurrentUserCapabilities capabilities)
    {
        _capabilities = capabilities;
        _anteriores = anteriores;
        _resumoPdf = resumoPdf;
        _machines = machines;
        _puncoes = puncoes;
        _mcaliper = mcaliper;
        _areas = areas;
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

    /// <summary>A máquina escolhida no filtro (o código); null = todas.</summary>
    public string? Maquina { get; private set; }

    /// <summary>
    /// A consulta do que já foi feito (Diogo, 19/10/2026: «para consultar o
    /// que já foi feito (…) com filtros»): com uma data «De» ou «Até», ou
    /// com um filtro sem dia escolhido, a lista é a do período, os mais
    /// recentes primeiro.
    /// </summary>
    public bool Periodo { get; private set; }
    public DateOnly? De { get; private set; }
    public DateOnly? Ate { get; private set; }

    /// <summary>O período que a lista mostra (com as datas por omissão: até hoje, um ano para trás).</summary>
    public DateOnly PeriodoDe { get; private set; }
    public DateOnly PeriodoAte { get; private set; }

    /// <summary>No período, a lista mostra no máximo estes controlos.</summary>
    public const int MaxPeriodo = 200;

    public IReadOnlyList<MachineOption> Machines { get; private set; } = [];
    public IReadOnlyList<ControloRevisaoItem> Items { get; private set; } = [];

    public ProductionContextReadModel? Selected { get; private set; }
    public ControloRevisaoItem? SelectedItem { get; private set; }
    public int? ListSelectedJobOnId => Selected?.JobOnId ?? SelectedItem?.JobOnId;
    public int? ControloId { get; private set; }
    /// <summary>
    /// O Peso da produção para um valor só: o das ferramentas atuais, senão o
    /// mais recente (18/10/2026).
    /// </summary>
    public PesoReadModel? Peso { get; private set; }

    /// <summary>
    /// Todos os Pesos da produção, um por CM+BQ (a CM ou a BQ mudou a meio):
    /// a revisão mostra-os todos; uma aprovação cobre todos (resposta R16-8
    /// do especialista).
    /// </summary>
    public IReadOnlyList<PesoReadModel> Pesos { get; private set; } = [];

    /// <summary>
    /// O controlo anterior de cada Peso, pelo peso_id: o peso do SAP e a
    /// produção escolhida no Peso, senão só a sugestão (C6).
    /// </summary>
    public IReadOnlyDictionary<int, RevisaoControloAnterior> Anteriores { get; private set; } = new Dictionary<int, RevisaoControloAnterior>();

    public PegamentosSheetReadModel? Pegamentos { get; private set; }
    public ControloApprovalReadModel? Approval { get; private set; }

    /// <summary>O nome de quem decidiu (aprovou ou não aprovou), quando o operador existe.</summary>
    public string? DecidedByName { get; private set; }

    /// <summary>
    /// As decisões de cada parte (só o Peso; Diogo, 19/10/2026), com o nome
    /// de quem decidiu.
    /// </summary>
    public IReadOnlyDictionary<ControloParte, (ControloDecisaoReadModel Decisao, string Nome)> Partes { get; private set; }
        = new Dictionary<ControloParte, (ControloDecisaoReadModel, string)>();

    /// <summary>Os últimos registos de cada parte, para os avisos do que chegou depois da aprovação.</summary>
    public IReadOnlyDictionary<ControloParte, ControloParteRegistos> Registos { get; private set; }
        = new Dictionary<ControloParte, ControloParteRegistos>();

    /// <summary>O registo das punções NNPB do controlo (o resumo do PU NNPB, quando existe).</summary>
    public PuncoesSheet? Puncoes { get; private set; }

    /// <summary>O PU NNPB aparece quando a CM é NNPB ou o registo já existe (como no Resumo).</summary>
    public bool ShowPuncoes => Puncoes is { IsNnpb: true } || Puncoes?.Registo is not null;

    /// <summary>O OK/NOK das áreas do Resumo (CM, BQ, MF, PU, CS), para o Resumo das ferramentas.</summary>
    public IReadOnlyList<ControloAreaStateReadModel> Areas { get; private set; } = [];

    /// <summary>A parte em que a nota faltou ao não aprovar (PRG).</summary>
    public ControloParte? NoteMissingParte { get; private set; }
    public bool MissingProduction { get; private set; }

    /// <summary>
    /// Os links do mcaliper das ferramentas da produção (CM, MF, BQ), por
    /// ordem; só os associados. Um clique no ícone copia (Diogo, 17/10/2026).
    /// </summary>
    public IReadOnlyList<(string Area, string Url)> McaliperLinks { get; private set; } = [];

    /// <summary>
    /// O botão «Resumo» do controlo aberto e o link do aviso dos Pegamentos
    /// (Diogo, 19/10/2026): as páginas do Resumo e dos Pegamentos são do
    /// Controlo Criar, por isso só aparecem a quem o tem.
    /// </summary>
    public bool TemControloCriar { get; private set; }

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
        [FromQuery(Name = "maquina")] string? maquina = null,
        [FromQuery(Name = "de")] DateOnly? de = null,
        [FromQuery(Name = "ate")] DateOnly? ate = null,
        [FromQuery(Name = "aprovacao_recusada")] string? approvalRefusal = null,
        [FromQuery(Name = "nota_em_falta")] string? noteMissing = null,
        [FromQuery(Name = "parte")] string? parte = null)
    {
        ViewData["Title"] = "Controlo";
        ApprovalRefusedNoIdentity = approvalRefusal == "sem-identidade";
        NoteMissing = noteMissing == "1";
        NoteMissingParte = NoteMissing ? ParteDe(parte) : null;
        ViewData["Shell"] = await _shell.ForControloReviewAsync();
        Reviewer = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "";

        await LoadListContextAsync(jobOnId, year, month, day, undated, procura, estado, maquina, de, ate);
        Machines = await _machines.ListAsync(HttpContext.RequestAborted);

        var days = await _revisao.MonthAsync(Year, Month, HttpContext.RequestAborted);
        Calendar = BuildCalendar(days);
        UndatedPendingCount = await _revisao.CountUndatedPendingAsync(HttpContext.RequestAborted);

        await LoadListItemsAsync();

        if (jobOnId is not int id)
            return Page();

        Selected = await _production.GetAsync(id, HttpContext.RequestAborted);
        if (Selected is null)
        {
            MissingProduction = true;
            return Page();
        }

        ViewData["Shell"] = await _shell.ForControloReviewAsync(Selected.JobOnId);
        TemControloCriar = (await _capabilities.GetAsync(HttpContext.RequestAborted))
            .Contains(DmoCapabilities.ControloCreate, StringComparer.Ordinal);

        var links = await _mcaliper.GetAsync(
            new[] { Selected.CmToolId, Selected.MfToolId, Selected.BqToolId }, HttpContext.RequestAborted);
        McaliperLinks = new[] { ("CM", Selected.CmToolId), ("MF", Selected.MfToolId), ("BQ", Selected.BqToolId) }
            .Where(slot => slot.Item2 is { } toolId && links.ContainsKey(toolId))
            .Select(slot => (slot.Item1, links[slot.Item2!]))
            .ToList();

        // Abrir para ver não escreve nada: só lê o controlo, se existir.
        // Aprovar (o POST) é que o cria, nas produções antigas sem ele.
        ControloId = await _ensure.FindExistingIdAsync(id, HttpContext.RequestAborted);
        if (ControloId is int controloId)
        {
            Pesos = await _peso.ListByControloAsync(controloId, HttpContext.RequestAborted);
            Peso = Pesos.FirstOrDefault(p => p.IsCurrent) ?? Pesos.LastOrDefault();
            // O controlo anterior de cada Peso: o que o operador escolheu no
            // Peso, senão só a sugestão (Diogo, 19/10/2026, C6).
            Anteriores = await _anteriores.ParaPesosAsync(id, Pesos, HttpContext.RequestAborted);
            Pegamentos = await _pegamentos.FindSheetByControloAsync(controloId, cancellationToken: HttpContext.RequestAborted);
            Approval = await _approval.GetAsync(controloId, HttpContext.RequestAborted);
            // O nome vem com o estado (sweep de 19/10/2026, C-3), na mesma consulta.
            DecidedByName = Approval?.DecididoPorNome ?? Approval?.ApprovedByOperatorId ?? Approval?.NotApprovedByOperatorId;

            Partes = await PartesAsync(_approval, _revisao, controloId, HttpContext.RequestAborted);
            Registos = await _approval.RegistosAsync(controloId, HttpContext.RequestAborted);
            Areas = await _areas.GetAsync(controloId, HttpContext.RequestAborted);
            Puncoes = await _puncoes.GetSheetAsync(controloId, HttpContext.RequestAborted);
        }

        return Page();
    }

    /// <summary>Only the filtered list; no calendar, shell, detail or writes.</summary>
    public async Task<IActionResult> OnGetResultadosAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "ano")] int? year,
        [FromQuery(Name = "mes")] int? month,
        [FromQuery(Name = "dia")] int? day,
        [FromQuery(Name = "sem_data")] string? undated,
        [FromQuery(Name = "procura")] string? procura,
        [FromQuery(Name = "estado")] string? estado,
        [FromQuery(Name = "maquina")] string? maquina = null,
        [FromQuery(Name = "de")] DateOnly? de = null,
        [FromQuery(Name = "ate")] DateOnly? ate = null)
    {
        await LoadListContextAsync(jobOnId, year, month, day, undated, procura, estado, maquina, de, ate);
        await LoadListItemsAsync();
        Response.Headers["X-Dmo-Region"] = "revisaoResultados";
        return Partial("_RevisaoResultados", this);
    }

    private async Task LoadListContextAsync(
        int? jobOnId, int? year, int? month, int? day,
        string? undated, string? procura, string? estado,
        string? maquina, DateOnly? de, DateOnly? ate)
    {
        Procura = string.IsNullOrWhiteSpace(procura) ? null : procura.Trim();
        Maquina = string.IsNullOrWhiteSpace(maquina) ? null : maquina.Trim().ToUpperInvariant();
        De = de;
        Ate = ate;
        Estado = estado switch
        {
            "pendente" => ControloRevisaoEstado.Pendente,
            "aprovado" => ControloRevisaoEstado.Aprovado,
            "nao-aprovado" => ControloRevisaoEstado.NaoAprovado,
            _ => null
        };
        Undated = undated == "1";
        var semDia = day is null && !Undated;
        Periodo = De is not null || Ate is not null
            || (semDia && (Procura is not null || Estado is not null || Maquina is not null));
        if (Periodo)
        {
            Undated = false;
            PeriodoAte = Ate ?? DateOnly.FromDateTime(DmoBusinessTime.Now);
            PeriodoDe = De ?? PeriodoAte.AddYears(-1);
            if (PeriodoDe > PeriodoAte)
                (PeriodoDe, PeriodoAte) = (PeriodoAte, PeriodoDe);
        }

        // O controlo aberto leva o calendário para o dia em que foi registado.
        if (jobOnId is int requested)
        {
            SelectedItem = await _revisao.FindByJobOnAsync(requested, HttpContext.RequestAborted);
            if (day is null && !Undated && !Periodo && SelectedItem is not null)
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
        if (!Undated && !Periodo && day is int d && d >= 1 && d <= DateTime.DaysInMonth(Year, Month))
        {
            Day = new DateOnly(Year, Month, d);
        }

        if (Procura is not null) ListQuery["procura"] = Procura;
        if (estado is not null && Estado is not null) ListQuery["estado"] = estado;
        if (Maquina is not null) ListQuery["maquina"] = Maquina;
    }

    /// <summary>O parâmetro «estado» do endereço, ou null para todos.</summary>
    public string? EstadoParam => Estado switch
    {
        ControloRevisaoEstado.Pendente => "pendente",
        ControloRevisaoEstado.Aprovado => "aprovado",
        ControloRevisaoEstado.NaoAprovado => "nao-aprovado",
        _ => null
    };

    private async Task LoadListItemsAsync()
    {
        if (Periodo)
        {
            Items = await _revisao.PeriodAsync(PeriodoDe, PeriodoAte.AddDays(1), Procura, Estado, Maquina, MaxPeriodo, HttpContext.RequestAborted);
        }
        else if (Day is { } chosen)
        {
            Items = await _revisao.DayAsync(chosen, Procura, Estado, Maquina, HttpContext.RequestAborted);
        }
        else if (Undated)
        {
            Items = await _revisao.UndatedAsync(Procura, Estado, Maquina, HttpContext.RequestAborted);
        }
    }

    public Task<IActionResult> OnPostApproveAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "nota")] string? note = null,
        [FromForm] RevisaoContexto? contexto = null,
        [FromForm(Name = "parte")] string? parte = null) =>
        DecideAsync(jobOnId, note, approve: true, contexto, parte);

    public Task<IActionResult> OnPostNaoAprovarAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "nota")] string? note = null,
        [FromForm] RevisaoContexto? contexto = null,
        [FromForm(Name = "parte")] string? parte = null) =>
        DecideAsync(jobOnId, note, approve: false, contexto, parte);

    /// <summary>
    /// O Resumo do Controlo também na Revisão (Diogo, 19/10/2026: «controlo
    /// aprove também deveria receber resumo»): o PDF do Resumo, ou o PDF
    /// completo, os mesmos do Resumo, para quem só tem o Controlo Aprovar. A
    /// geração é a do Resumo (o mesmo serviço, em memória; nada é gravado).
    /// </summary>
    public async Task<IActionResult> OnPostResumoPdfAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "apenas_resumo")] bool summaryOnly = false)
    {
        var documento = jobOnId is int id
            ? await _resumoPdf.BuildAsync(id, null, null, summaryOnly, ResumoModel.GeradoPor(User), HttpContext.RequestAborted)
            : null;
        if (documento is null)
        {
            // Uma recusa volta à Revisão, não ao Resumo (que este utilizador pode não ter).
            return RedirectToPage("/Controlo/Approve", jobOnId is int jobOn ? new { jobon_id = jobOn } : null);
        }

        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(documento.Folder);
        return File(documento.Bytes, "application/pdf", documento.FileName);
    }

    /// <summary>A parte de uma decisão pelo nome do formulário; null = o controlo inteiro.</summary>
    public static ControloParte? ParteDe(string? parte) => parte switch
    {
        "peso" => ControloParte.Peso,
        _ => null
    };

    /// <summary>O nome de uma parte no formulário e nos endereços.</summary>
    public static string NomeDaParte(ControloParte parte) => "peso";

    /// <summary>O estado e os botões de uma parte, na Revisão e na consulta dessa parte.</summary>
    /// <remarks>
    /// Com <paramref name="registos"/>, a vista sabe o que chegou depois da
    /// aprovação (C7, C9); <paramref name="consultar"/> é o link dos avisos
    /// para a consulta da parte (null na própria consulta).
    /// </remarks>
    public static RevisaoParteVista Vista(
        IReadOnlyDictionary<ControloParte, (ControloDecisaoReadModel Decisao, string Nome)> partes,
        ControloParte parte, bool temRegisto,
        IReadOnlyDictionary<ControloParte, ControloParteRegistos>? registos = null,
        string? consultar = null) =>
        (partes.TryGetValue(parte, out var d)
            ? new RevisaoParteVista(parte, d.Decisao, d.Nome, temRegisto)
            : new RevisaoParteVista(parte, null, null, temRegisto))
        with
        {
            Registos = registos?.GetValueOrDefault(parte),
            Consultar = consultar
        };

    /// <summary>As decisões das partes de um controlo (o Peso), com o nome de quem decidiu.</summary>
    public static async Task<IReadOnlyDictionary<ControloParte, (ControloDecisaoReadModel Decisao, string Nome)>> PartesAsync(
        ControloApprovalHandler approval, IControloRevisaoQuery revisao, int controloId, CancellationToken cancellationToken)
    {
        var partes = new Dictionary<ControloParte, (ControloDecisaoReadModel, string)>();
        var nomes = new Dictionary<string, string>();
        foreach (var decisao in await approval.PartsAsync(controloId, cancellationToken))
        {
            if (!Enum.IsDefined(decisao.Parte))
                continue;
            if (!nomes.TryGetValue(decisao.OperatorId, out var nome))
            {
                nome = await revisao.OperatorNameAsync(decisao.OperatorId, cancellationToken) ?? decisao.OperatorId;
                nomes[decisao.OperatorId] = nome;
            }
            partes[decisao.Parte] = (decisao, nome);
        }
        return partes;
    }

    /// <summary>
    /// O dia e os filtros da lista que vêm com a decisão (E8): o redirect
    /// devolve-os, para a página voltar igual fora das zonas e trocar só o
    /// controlo, a lista, o calendário e a contagem dos sem data.
    /// </summary>
    public sealed class RevisaoContexto
    {
        [BindProperty(Name = "ano")] public int? Ano { get; set; }
        [BindProperty(Name = "mes")] public int? Mes { get; set; }
        [BindProperty(Name = "dia")] public int? Dia { get; set; }
        [BindProperty(Name = "sem_data")] public string? SemData { get; set; }
        [BindProperty(Name = "procura")] public string? Procura { get; set; }
        [BindProperty(Name = "estado")] public string? Estado { get; set; }
        [BindProperty(Name = "maquina")] public string? Maquina { get; set; }
        [BindProperty(Name = "de")] public DateOnly? De { get; set; }
        [BindProperty(Name = "ate")] public DateOnly? Ate { get; set; }

        /// <summary>
        /// A decisão veio da Consulta do Peso (Diogo,
        /// 19/10/2026: «merter aprovar tambem nas paginas dos mesmos»): o
        /// redirect volta a essa consulta.
        /// </summary>
        [BindProperty(Name = "voltar")] public string? Voltar { get; set; }
        [BindProperty(Name = "controlo_id")] public int? ControloId { get; set; }
    }

    private IActionResult Para(Microsoft.AspNetCore.Routing.RouteValueDictionary rota, RevisaoContexto? contexto)
    {
        if (contexto is { Voltar: "peso", ControloId: int controloId })
        {
            var consulta = new Dictionary<string, string?>
            {
                ["controlo_id"] = controloId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["tipo"] = contexto.Voltar
            };
            foreach (var flag in new[] { "aprovacao_recusada", "nota_em_falta" })
            {
                if (rota.TryGetValue(flag, out var valor))
                    consulta[flag] = valor?.ToString();
            }
            return Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Controlo/Approve/Detalhe", consulta));
        }
        return RedirectToPage("/Controlo/Approve", rota);
    }

    private static Microsoft.AspNetCore.Routing.RouteValueDictionary Volta(
        int jobOnId, RevisaoContexto? contexto, string? flag = null, string? valor = null)
    {
        var rota = new Microsoft.AspNetCore.Routing.RouteValueDictionary { ["jobon_id"] = jobOnId };
        if (contexto is not null)
        {
            if (contexto.Ano is int ano && contexto.Mes is int mes && contexto.Dia is int dia)
            {
                rota["ano"] = ano;
                rota["mes"] = mes;
                rota["dia"] = dia;
            }
            else if (contexto.SemData == "1")
            {
                rota["sem_data"] = "1";
            }

            if (!string.IsNullOrWhiteSpace(contexto.Procura))
                rota["procura"] = contexto.Procura.Trim();
            if (contexto.Estado is "pendente" or "aprovado" or "nao-aprovado")
                rota["estado"] = contexto.Estado;
            if (!string.IsNullOrWhiteSpace(contexto.Maquina))
                rota["maquina"] = contexto.Maquina.Trim();
            if (contexto.De is { } de)
                rota["de"] = de.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (contexto.Ate is { } ate)
                rota["ate"] = ate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (flag is not null)
            rota[flag] = valor;
        return rota;
    }

    private async Task<IActionResult> DecideAsync(int? jobOnId, string? note, bool approve, RevisaoContexto? contexto = null, string? parteNome = null)
    {
        var parte = ParteDe(parteNome);
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
            return Para(Volta(id, contexto, "aprovacao_recusada", "sem-identidade"), contexto);

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            var semNota = Volta(id, contexto, "nota_em_falta", "1");
            if (parte is { } emFalta)
                semNota["parte"] = NomeDaParte(emFalta);
            return Para(semNota, contexto);
        }

        note = note?.Trim();
        if (note is { Length: > 2000 })
            note = note[..2000];

        var ensured = await _ensure.HandleAsync(
            new EnsureControloForJobOn(id), HttpContext.RequestAborted);
        if (ensured.ControloId is int controloId)
        {
            if (parte is { } umaParte)
                await _approval.DecidePartAsync(controloId, umaParte, approve, actor, note, HttpContext.RequestAborted);
            else if (approve)
                await _approval.ApproveAndGetAsync(controloId, actor, note, HttpContext.RequestAborted);
            else
                await _approval.NotApproveAndGetAsync(controloId, actor, note, HttpContext.RequestAborted);
        }
        return Para(Volta(id, contexto), contexto);
    }

    /// <summary>O link de um controlo da lista, mantendo o dia e os filtros.</summary>
    public string ItemLink(ControloRevisaoItem item)
    {
        var query = new Dictionary<string, string?>(ListQuery.ToDictionary(p => p.Key, p => (string?)p.Value))
        {
            ["jobon_id"] = item.JobOnId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (Periodo)
        {
            if (De is { } de) query["de"] = de.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (Ate is { } ate) query["ate"] = ate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (Day is { } chosen)
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
        with { LegendLabel = "Por aprovar", Filtros = "revisaoFiltrosForm procura estado maquina" };
}

/// <summary>Uma parte que se aprova (o Peso): a decisão, quem a deu e se a parte tem registo.</summary>
public sealed record RevisaoParteVista(ControloParte Parte, ControloDecisaoReadModel? Decisao, string? Nome, bool TemRegisto)
{
    /// <summary>Os últimos registos da parte, para comparar com a decisão.</summary>
    public ControloParteRegistos? Registos { get; init; }

    /// <summary>O link dos avisos para a consulta da parte; null na própria consulta.</summary>
    public string? Consultar { get; init; }

    private DateTime? AprovadaEm => Decisao is { Aprovado: true } d ? d.EmUtc : null;

    /// <summary>
    /// Um Peso novo depois da aprovação (a CM ou a BQ trocou): o registo novo tem aprovação própria e a parte volta a estar
    /// por aprovar (Diogo, 19/10/2026, C7).
    /// </summary>
    public bool NovoDepois => Registos?.UltimoRegistoNovo > AprovadaEm;

    /// <summary>Uma leitura registada depois da aprovação: a aprovação mantém-se e a Revisão avisa (C9).</summary>
    public bool RegistadoDepois => !NovoDepois && Registos?.UltimaLeitura > AprovadaEm;

    /// <summary>Uma leitura corrigida (mudada ou apagada) depois da aprovação: a aprovação mantém-se e a Revisão avisa (C9).</summary>
    public bool CorrigidoDepois => !NovoDepois && Registos?.UltimaCorrecao > AprovadaEm;

    /// <summary>A parte aprovada e sem registo novo depois da aprovação.</summary>
    public bool Aprovada => AprovadaEm is not null && !NovoDepois;
}
