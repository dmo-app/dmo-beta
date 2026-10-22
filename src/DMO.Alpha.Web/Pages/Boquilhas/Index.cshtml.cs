using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Core.Admin;
using System.Globalization;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// A FOLHA NATIVA das Boquilhas (o corte F, a decisão do Owner
/// 2026-10-12: «vamos continuar ainda falta muita coisa, o modulo
/// boquilhas por inteiro»). A folha mostra AS MÁQUINAS com a sua
/// PRODUÇÃO CORRENTE (a última produção de cada máquina enquanto o Job On
/// não tem datas — o corte B; a regra F4-4: a corrente segue as datas do
/// jobon com o dia operacional da hora de viragem) e o REGISTO DE
/// MOVIMENTOS de uma boquilha: a saída para reparação, a entrada de
/// volta — o get-or-create do trace é idempotente (uma ferramenta tem no
/// máximo um trace pré-produção não resolvido). Nada é escrito no Job On
/// nem no BqContext; mutação nativa exige sessão autenticada.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Boquilhas)]
public sealed partial class IndexModel : PageModel
{
    private readonly IBoquilhasRegistoQuery _registoQuery;
    private readonly IRegisterBqMovementHandler _movementHandler;
    private readonly IBoquilhasCurrentQuery _currentQuery;
    private readonly ICreateBqRepairTraceHandler _createTrace;
    private readonly IToolLookupQuery _toolLookup;
    private readonly ShellStateFactory _shellState;
    private readonly IBqToolHistoryQuery _history;
    private readonly IAssociateBqRepairTraceToContextHandler _associate;
    private readonly IToolCandidateQuery _toolCandidates;
    private readonly DmoDbContext _db;
    private readonly DMO.Alpha.Infrastructure.Production.JobOnChangeQuery _changes;
    private readonly DMO.Alpha.Core.Production.IProductionPickerQuery _productionPicker;

    public IndexModel(
        IBoquilhasRegistoQuery registoQuery,
        IRegisterBqMovementHandler movementHandler,
        IBoquilhasCurrentQuery currentQuery,
        ICreateBqRepairTraceHandler createTrace,
        IToolLookupQuery toolLookup,
        ShellStateFactory shellState,
        IBqToolHistoryQuery history,
        IAssociateBqRepairTraceToContextHandler associate,
        IToolCandidateQuery toolCandidates,
        DmoDbContext db,
        DMO.Alpha.Infrastructure.Production.JobOnChangeQuery changes,
        DMO.Alpha.Core.Production.IProductionPickerQuery productionPicker)
    {
        _registoQuery = registoQuery;
        _movementHandler = movementHandler;
        _currentQuery = currentQuery;
        _createTrace = createTrace;
        _toolLookup = toolLookup;
        _shellState = shellState;
        _history = history;
        _associate = associate;
        _toolCandidates = toolCandidates;
        _db = db;
        _changes = changes;
        _productionPicker = productionPicker;
    }

    /// <summary>As máquinas com a sua produção corrente (a sidebar viva).</summary>
    public IReadOnlyList<MachineCurrentBq> Currents { get; private set; } =
        Array.Empty<MachineCurrentBq>();

    /// <summary>O contexto da produção a que o registo pertence (o bq_id — os fatos humanos).</summary>
    public BoquilhasRegistoProductionContext? ProductionContext { get; private set; }

    /// <summary>O aviso quando o Job On mudou depois do primeiro movimento do registo; null sem alterações.</summary>
    public JobOnChangeNoticeView? JobOnNotice { get; private set; }

    /// <summary>O HISTÓRICO da ferramenta: os jobons em que foi usada (via os bq_ids — a cascata).</summary>
    public PagedList<BqToolJobHistoryItem> JobHistory { get; private set; } =
        PagedList<BqToolJobHistoryItem>.Empty;

    // AS PÁGINAS DAS LISTAS EM TABELA desta folha (o Owner 2026-10-16:
    // «tudo que sejam listas em tabelas limitar numero e criar
    // paginaçao»): 25 linhas por página, um parâmetro por tabela (o
    // «pagina» já é do catálogo das boquilhas).
    [BindProperty(SupportsGet = true, Name = "usos_pagina")] public int? JobHistoryPage { get; set; }
    [BindProperty(SupportsGet = true, Name = "usos")] public int? JobHistoryFlag { get; set; }

    /// <summary>«Onde esta boquilha foi usada» aberta numa janela (?usos=1), a partir de «Opções».</summary>
    public bool JobHistoryOpen => JobHistoryFlag == 1;

    /// <summary>
    /// A página atual (o mesmo bq_id ou trace_id) com a janela aberta ou
    /// fechada; fechar esquece a página da lista.
    /// </summary>
    public string JobHistoryHref(bool open)
    {
        var query = Request.Query
            .Where(pair => pair.Key is not ("usos" or "usos_pagina" or "handler"))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value)))
            .ToList();
        if (open)
        {
            query.Add(new("usos", "1"));
        }

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(Request.PathBase + Request.Path, query);
    }
    [BindProperty(SupportsGet = true, Name = "registos_pagina")] public int? RegistersPage { get; set; }
    [BindProperty(SupportsGet = true, Name = "mov_pagina")] public int? TraceMovementsPage { get; set; }
    [BindProperty(SupportsGet = true, Name = "hist_pagina")] public int? HistoryListPage { get; set; }

    /// <summary>As BQ da janela «Registar sem Job On» (referência e lote).</summary>
    public IReadOnlyList<SemJobOnTool> SemJobOnTools { get; private set; } = Array.Empty<SemJobOnTool>();

    /// <summary>As linhas da janela «Registar sem Job On».</summary>
    public IReadOnlyList<DMO.Alpha.Core.Production.MachineOption> SemJobOnLines { get; private set; } = Array.Empty<DMO.Alpha.Core.Production.MachineOption>();

    /// <summary>O resultado do último «Registar sem Job On» (ok ou o nome da recusa).</summary>
    public string? SemJobOnResult { get; private set; }

    /// <summary>Os movimentos sem Job On da BQ da ficha, do mais recente para o mais antigo.</summary>
    public IReadOnlyList<SemJobOnMovement> SemJobOnMovements { get; private set; } = Array.Empty<SemJobOnMovement>();

    /// <summary>Read model nativo do trace canónico pedido; null fora desse contexto.</summary>
    public BoquilhasRegistoTrace? RegistoTrace { get; private set; }

    /// <summary>True quando foi pedido um trace_id que não existe: recusa limpa.</summary>
    public bool TraceRequestedNotFound { get; private set; }

    /// <summary>Factos descritivos da BQ pedida (a procura pela referência).</summary>
    public ToolLookupResult? RequestedBq { get; private set; }

    /// <summary>Recusa tipada do último POST de movimento, transportada por query (PRG).</summary>
    [BindProperty(SupportsGet = true, Name = "movement_machine_id")]
    public int? MovementMachineId { get; set; }

    public IReadOnlyList<DMO.Alpha.Core.Production.MachineOption> MovementMachines { get; private set; } = Array.Empty<DMO.Alpha.Core.Production.MachineOption>();

    public RegisterBqMovementResult? MovementRefusal { get; private set; }

    public string MovementRefusalMessage => MovementRefusal switch
    {
        RegisterBqMovementResult.MachineNotFound => "A máquina indicada não existe. Selecione a máquina em uso e volte a guardar.",
        RegisterBqMovementResult.TraceNotFound =>
            "O registo indicado não existe. Nenhum movimento foi registado.",
        RegisterBqMovementResult.TraceToolNotRegistered =>
            "A ferramenta do registo não está registada no Ferramentas. Nenhum movimento foi registado.",
        RegisterBqMovementResult.UnknownMovementType =>
            "Tipo de movimento desconhecido. Use saída, entrada ou entrada sem reparação. Nenhum movimento foi registado.",
        RegisterBqMovementResult.InvalidQuantity =>
            "Indique a quantidade como um número inteiro igual ou superior a 1. Nenhum movimento foi registado.",
        RegisterBqMovementResult.InvalidOccurrenceTime =>
            "A hora indicada não existe em Portugal continental devido à mudança de hora. " +
            "Confirme a data e hora reais do movimento. Nenhum movimento foi registado.",
        _ => string.Empty
    };

    /// <summary>
    /// Recusa tipada do último POST «Associar Job On a este registo»,
    /// transportada por query (PRG). Abrange as recusas da RESOLUÇÃO pelo
    /// nº de produção (os ids nunca visíveis na folha) e as recusas
    /// canónicas do handler de associação.
    /// </summary>
    public AssociateBqRepairTraceToContextResult? AssociationRefusal { get; private set; }

    public string AssociationRefusalMessage => AssociationRefusal switch
    {
        AssociateBqRepairTraceToContextResult.TraceNotFound =>
            "O registo indicado não existe. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.BqContextNotFound =>
            "O contexto de produção indicado não existe. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.TraceAlreadyAssociated =>
            "Este registo já está associado a uma produção. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.BqContextAlreadyTraced =>
            "Essa produção já tem um registo de reparação. Abre o registo existente pela produção — para não misturar nem duplicar movimentos, a associação foi interrompida.",
        AssociateBqRepairTraceToContextResult.TraceToolDoesNotMatchBqContextTool =>
            "A boquilha deste registo não é a boquilha do contexto de produção indicado. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.MissingFacts =>
            "Indica o número de produção (ex.: 202601) para associar o registo. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.ProductionNotFound =>
            "Não encontrámos nenhuma produção com esse número. Cria o Job On (com as datas reais) e volta a associar. Nenhuma associação foi feita.",
        AssociateBqRepairTraceToContextResult.BqNotInProduction =>
            "A boquilha deste registo não é uma das boquilhas dessa produção. Confirma o número de produção. Nenhuma associação foi feita.",
        _ => string.Empty
    };

    /// <summary>
    /// A referência escrita no «Associar Job On» quando não era a da produção
    /// encontrada pelo número (o nº é único): o registo foi associado na mesma
    /// e a folha avisa (decisão do proprietário de 16/10/2026: avisar em vez
    /// de bloquear, desde que não parta nada).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "referencia_indicada")]
    public string? AssociationTypedReference { get; set; }

    /// <summary>
    /// O 1.º passo do «Associar Job On» (procura em dois passos, 16/10/2026):
    /// a referência; só com ela se leem as produções do select.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "associar_ref")]
    public string? AssociateReference { get; set; }

    /// <summary>As produções da referência indicada (o 2.º passo: o select do nº de produção).</summary>
    public IReadOnlyList<DMO.Alpha.Core.Production.ProductionPickItem> AssociateProductions { get; private set; } =
        Array.Empty<DMO.Alpha.Core.Production.ProductionPickItem>();

    /// <summary>A referência indicada não tem produções: aviso âmbar, sem bloqueio.</summary>
    public bool AssociateReferenceWithoutProductions { get; private set; }

    public string? AssociationWarningMessage =>
        AssociationTypedReference is { Length: > 0 } typed && ProductionContext is { } production
            && !string.Equals(typed.Trim(), production.Reference, StringComparison.OrdinalIgnoreCase)
            ? $"A referência indicada ({typed.Trim()}) não é a referência da produção {production.ProductionNumber} ({production.Reference}). " +
              "O registo foi associado pelo número de produção. Se a produção não é esta, contacte o ADMIN."
            : null;

    /// <summary>
    /// A hora indicada e a hora gravada quando a hora do movimento caía na
    /// mudança de hora (março) e passou para a hora seguinte; o movimento foi
    /// gravado e a folha avisa (transportadas por query, PRG).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "hora_indicada")]
    public string? ShiftedFromLocal { get; set; }

    [BindProperty(SupportsGet = true, Name = "hora_gravada")]
    public string? ShiftedToLocal { get; set; }

    public string? OccurrenceShiftWarningMessage
    {
        get
        {
            if (!TryParseLocal(ShiftedFromLocal, out var from) || !TryParseLocal(ShiftedToLocal, out var to))
            {
                return null;
            }

            var pt = CultureInfo.GetCultureInfo("pt-PT");
            return $"A hora indicada ({from.ToString("dd/MM/yyyy HH:mm", pt)}) não existe em Portugal continental por causa da mudança de hora. " +
                   $"O movimento foi gravado às {to.ToString("HH:mm", pt)} de {to.ToString("dd/MM/yyyy", pt)}. Corrija o movimento se a hora real for outra.";
        }
    }

    /// <summary>
    /// A data e a hora gravadas quando ficaram no futuro (para lá da folga de
    /// <see cref="BqMovementOccurrence.FutureToleranceMinutes"/> minutos): o
    /// movimento foi gravado e a folha avisa, para se corrigir se a data
    /// estiver errada (Diogo, 19/10/2026: «as datas têm de ser livres»;
    /// transportada por query, PRG).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "data_no_futuro")]
    public string? FutureOccurrenceLocal { get; set; }

    public string? FutureOccurrenceWarningMessage
    {
        get
        {
            if (!TryParseLocal(FutureOccurrenceLocal, out var future))
            {
                return null;
            }

            var pt = CultureInfo.GetCultureInfo("pt-PT");
            return $"A data e a hora do movimento ({future.ToString("dd/MM/yyyy HH:mm", pt)}) estão no futuro. " +
                   "O movimento foi gravado. Corrija o movimento se a data real for outra.";
        }
    }

    private static bool TryParseLocal(string? value, out DateTime local) =>
        DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    /// <summary>O formato dos campos datetime-local (e dos avisos transportados por query).</summary>
    internal static string LocalFieldValue(DateTime local) =>
        local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// O valor inicial do campo «Data e hora reais» do form do movimento:
    /// o AGORA do servidor em hora local (o equivalente ao dateLocal() do
    /// boquilhas.html — o uso diário submete o campo tal como veio; o
    /// backfill dos ~2 meses de registos passados escreve a data/hora
    /// REAL do movimento, o Owner 2026-10-14). O «+ Registo com data
    /// antiga» da ficha da ferramenta abre o registo com o ?data_antiga
    /// — o primeiro movimento já nasce com a data real dele.
    /// </summary>
    public string MovementFormOccurredAt { get; private set; } =
        DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Depois de uma recusa, o formulário volta com o que se escreveu (o tipo,
    /// a quantidade e, pelo ?data_antiga, a hora): nunca se perde sem aviso
    /// (teste do agente de 19/10/2026, Tc95ce11).
    /// </summary>
    public string MovementFormType => MovementRefusal is not null
        && Request.Query["movement_type"].ToString() is { Length: > 0 } tipo ? tipo : "saida";

    public string MovementFormQuantity => MovementRefusal is not null
        ? Request.Query["quantity"].ToString() : string.Empty;

    /// <summary>
    /// A FICHA DA FERRAMENTA (o toolsTab do dmo-html — a ficha única por
    /// BQ/lote, reutilizável em vários Job Ons; ?ferramenta=&lt;tool_id&gt;
    /// dentro da tab Ficheiros): os fatos da Tool (referência, lote, a
    /// quantidade contabilística, as máquinas compatíveis) e os REGISTOS
    /// dela — os traces ancorados diretamente (pré-produção/sem Job On) ou
    /// associados (via bq_id), com o estado de produção pela janela do
    /// Job On. Os ids vivem no URL/hidden — nunca no texto da folha.
    /// </summary>
    public BqToolSheet? ToolSheet { get; private set; }

    /// <summary>True quando foi pedida uma ficha de ferramenta inexistente: recusa limpa.</summary>
    public bool ToolSheetNotFound { get; private set; }

    /// <summary>
    /// A QUANTIDADE CONTABILÍSTICA do lote da BQ em registo (o fato da
    /// Tool — o lote que não cresce): alimenta o painel das quantidades
    /// calculadas do dmo-html («Quantidade do lote — Fabricação — Em
    /// reparação — Discrepância acumulada»); null quando o lote ainda não
    /// tem quantidade contabilística («—», truthful).
    /// </summary>
    public int? RegisterLotQuantity { get; private set; }

    /// <summary>As máquinas compatíveis da BQ em registo (o facto humano do html).</summary>
    public IReadOnlyList<string> RegisterMachineCodes { get; private set; } =
        Array.Empty<string>();

    /// <summary>A VISTA do Histórico (?historico=1) — o calendário e os movimentos do dia.</summary>
    public bool ShowHistory { get; private set; }

    /// <summary>O mês do calendário (o corrente por omissão).</summary>
    public int HistoryYear { get; private set; }

    public int HistoryMonth { get; private set; }

    /// <summary>Os dias do mês com movimentos (os pontos do calendário).</summary>
    public IReadOnlyList<BqHistoryDayCount> MonthCounts { get; private set; } =
        Array.Empty<BqHistoryDayCount>();

    /// <summary>
    /// O dia escolhido no calendário (os movimentos mostrados em baixo).
    /// </summary>
    public DateOnly? HistoryDay { get; private set; }

    /// <summary>
    /// P2-2: TRUE quando NÃO houve ?dia explícito — o dia em folha é o
    /// DIA OPERACIONAL DEFAULT (o hoje: os movimentos de hoje e o hoje
    /// VESTE o selecionado no calendário do mês corrente). O estado
    /// lógico é o estado visual; quando o operador navega para outro
    /// mês sem escolher dia, o rótulo «Movimentos de hoje» diz o estado
    /// abertamente.
    /// </summary>
    public bool HistoryDayIsDefault { get; private set; }

    /// <summary>O SHARED ELEMENT do calendário (o Owner 2026-10-14) — o viewmodel comum servido pelo partial _DmoCalendar.</summary>
    public DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation? HistoryCalendar { get; private set; }

    /// <summary>Os movimentos detalhados do histórico (o período efetivo com os filtros — a TABELA da casa).</summary>
    public IReadOnlyList<BqHistoryMovementItem> Movements { get; private set; } =
        Array.Empty<BqHistoryMovementItem>();

    /// <summary>Os filtros do html (persistem no formulário): a máquina, o
    /// tipo, desde/até, o reparador e a pesquisa de texto — sobre a data
    /// real do movimento.</summary>
    public string? FilterOperator { get; private set; }
    public string? FilterMachine { get; private set; }
    public string? FilterType { get; private set; }
    public DateOnly? FilterDesde { get; private set; }
    public DateOnly? FilterAte { get; private set; }
    public string? FilterText { get; private set; }

    /// <summary>O «Histórico BQ» das Opções do registo (?bq_ferramenta=tool_id):
    /// só os movimentos desta ferramenta (a BQ e o lote); null sem filtro ou
    /// quando a ferramenta já não existe.</summary>
    public string? FilterToolId { get; private set; }

    /// <summary>A BQ e o lote do filtro, para o mostrar no card dos filtros.</summary>
    public string? FilterToolLabel { get; private set; }

    /// <summary>Os filtros atuais sem o da BQ (o «Tirar» do card dos filtros).</summary>
    public string HistoryWithoutToolHref { get; private set; } = "/Boquilhas?historico=1";

    /// <summary>O mês escolhido no seletor (yyyy-MM), só quando o utilizador o
    /// escolheu: sem desde/até nem dia, é o período da lista.</summary>
    public string? FilterMonth { get; private set; }

    /// <summary>A VISTA ativa do histórico (o report-switch do html): a
    /// grelha do resumo diário ou os movimentos detalhados (o default).</summary>
    public bool ShowMatrix { get; private set; }
    public bool ShowGrouped { get; private set; }
    public bool ShowMovements { get; private set; }
    // Três vistas: «Por máquina / Job On» (a predefinida), «Resumo diário» e
    // «Movimentos detalhados», um movimento por linha. A última saiu na
    // beta.102 e voltou a pedido do Diogo (19/10): as três são diferentes.
    public string HistoryView => ShowMatrix ? "matriz" : ShowMovements ? "movimentos" : "grupos";

    /// <summary>Os dias da grelha do resumo diário, todos, mesmo sem movimentos.</summary>
    public IReadOnlyList<DateOnly> MatrixDays { get; private set; } = [];

    /// <summary>As fichas de BQ usadas no dia escolhido (uma por máquina e registo).</summary>
    public IReadOnlyList<BqHistoryMovementGroup> DayGroups { get; private set; } = [];
    public string HistoryPeriodLabel => ShowMatrix ? MatrixPeriodLabel
        : HistoryDay is { } day ? day.ToString("dd/MM/yyyy")
        : FilterDesde is not null || FilterAte is not null
            ? $"{FilterDesde?.ToString("dd/MM/yyyy") ?? "Início"} — {FilterAte?.ToString("dd/MM/yyyy") ?? "Todos os dias"}"
        : FilterMonth is not null
            ? $"{new DateOnly(HistoryYear, HistoryMonth, 1):dd/MM/yyyy} — {new DateOnly(HistoryYear, HistoryMonth, DateTime.DaysInMonth(HistoryYear, HistoryMonth)):dd/MM/yyyy}"
            : "Todos os dias";
    public string HistoryAllDaysHref { get; private set; } = "/Boquilhas?historico=1";
    public string HistoryTodayHref { get; private set; } = "/Boquilhas?historico=1";
    public IReadOnlyList<BqHistoryMovementGroup> GroupedMovements { get; private set; } = [];
    public IReadOnlyList<BqHistoryMovementItem> DayMovements { get; private set; } = [];


    /// <summary>As entradas do RESUMO DIÁRIO · 6 MÁQUINAS (uma por dia ×
    /// máquina × registo, com E/S/SR — o read model que o gerador PDF da
    /// A5 consome; o gerador só formata).</summary>
    public IReadOnlyList<BqDailySummaryEntry> MatrixEntries { get; private set; } =
        Array.Empty<BqDailySummaryEntry>();

    /// <summary>O período da grelha (o matrixPeriod do html).</summary>
    public string MatrixPeriodLabel { get; private set; } = string.Empty;

    /// <summary>O limite da grelha e dos PDFs (366 dias, por desempenho).</summary>
    public const int MaxMatrixDays = 366;

    /// <summary>
    /// O período pedido passava do limite: a grelha e o PDF mostram os
    /// primeiros 366 dias e avisam (decisão do proprietário de 16/10/2026:
    /// avisar em vez de recusar); null quando o período cabe.
    /// </summary>
    public string? MatrixPeriodWarning { get; private set; }

    /// <summary>
    /// Os movimentos da grelha sem máquina: não têm coluna, mas o cartão dos
    /// totais conta-os, e os dois totais não batiam sem nada o dizer (sweep
    /// de 19/10/2026, B-2). Zero fora da grelha.
    /// </summary>
    public int MatrixWithoutMachine { get; private set; }

    /// <summary>O aviso da grelha e do PDF sobre esses movimentos; null quando não há.</summary>
    public string? MatrixWithoutMachineWarning => MatrixWithoutMachine switch
    {
        0 => null,
        1 => "1 movimento sem máquina não aparece na grelha, mas conta nos totais.",
        var n => $"{n} movimentos sem máquina não aparecem na grelha, mas contam nos totais."
    };

    /// <summary>
    /// A vista «Por máquina / Job On» com o período da grelha e os mesmos
    /// filtros, onde esses movimentos aparecem em «Máquina Por indicar».
    /// </summary>
    public string? MatrixWithoutMachineHref { get; private set; }

    /// <summary>O limite da lista quando há filtros mas nenhum período.</summary>
    public const int MaxListMovements = 500;

    /// <summary>
    /// A lista sem período passou do limite: mostra os mais recentes e
    /// avisa; null quando cabe.
    /// </summary>
    public string? ListLimitWarning { get; private set; }

    /// <summary>
    /// Corta um período ao limite da grelha (os primeiros 366 dias, a contar
    /// do início) e devolve o aviso em PT; sem corte, o aviso é null.
    /// </summary>
    public static (DateOnly Start, DateOnly End, string? Warning) ClampMatrixPeriod(DateOnly start, DateOnly end)
    {
        if (end.DayNumber - start.DayNumber + 1 <= MaxMatrixDays)
        {
            return (start, end, null);
        }

        var cutEnd = start.AddDays(MaxMatrixDays - 1);
        var pt = CultureInfo.GetCultureInfo("pt-PT");
        return (start, cutEnd,
            $"O resumo diário mostra no máximo {MaxMatrixDays} dias. O período pedido ia até {end.ToString("dd/MM/yyyy", pt)}; " +
            $"mostramos de {start.ToString("dd/MM/yyyy", pt)} a {cutEnd.ToString("dd/MM/yyyy", pt)}. Para ver o resto, escolha um intervalo a partir de {cutEnd.AddDays(1).ToString("dd/MM/yyyy", pt)}.");
    }

    /// <summary>OS CONTADORES do filtro (as somas de quantidade por tipo —
    /// «totais de movimentos do filtro, não saldos de stock»).</summary>
    public int FilteredSaidas { get; private set; }
    public int FilteredEntradas { get; private set; }
    public int FilteredSemReparacao { get; private set; }

    /// <summary>O contador de movimentos do filtro (o historyCount do html).</summary>
    public int HistoryMovementCount { get; private set; }

    /// <summary>True quando há pesquisa, período, filtro ou dia escolhido; sem isso a lista fica em branco.</summary>
    public bool HistorySearchApplied { get; private set; }

    public const string HistoryEmptyStateMessage = "Escolha um período ou pesquise para ver os movimentos.";

    /// <summary>O HREF do PDF CONTEXTUAL (o único botão «Imprimir / PDF
    /// (filtros atuais)» — o que exporta é a vista e os filtros atuais:
    /// a grelha na vista Grelha; os movimentos na vista detalhada; a
    /// máquina quando escolhida; o mês pelo seletor). Os 3 geradores da
    /// A5 continuam por trás.</summary>
    public string PdfContextualHref { get; private set; } = "/Boquilhas?historico=1";

    /// <summary>A tab FICHEIROS (?ficheiros=1 — os registos das BQ com os estados).</summary>
    public bool ShowFiles { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "trace_id")] int? traceId,
        [FromQuery(Name = "bq_id")] int? bqContextId,
        [FromQuery(Name = "movement_refusal")] string? movementRefusal,
        [FromQuery(Name = "association_refusal")] string? associationRefusal,
        [FromQuery(Name = "historico")] string? historico,
        [FromQuery(Name = "ficheiros")] string? ficheiros,
        [FromQuery(Name = "ano")] int? ano,
        [FromQuery(Name = "mes")] int? mes,
        [FromQuery(Name = "dia")] int? dia,
        [FromQuery(Name = "reparador")] string? reparador,
        [FromQuery(Name = "maquina")] string? maquina,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "desde")] DateOnly? desde,
        [FromQuery(Name = "ate")] DateOnly? ate,
        [FromQuery(Name = "pesquisa")] string? pesquisa,
        [FromQuery(Name = "vista")] string? vista,
        [FromQuery(Name = "mes_ano")] string? mesAno,
        [FromQuery(Name = "ferramenta")] string? ferramenta,
        [FromQuery(Name = "data_antiga")] string? dataAntiga,
        [FromQuery(Name = "bq_ferramenta")] string? bqFerramenta)
    {
        ViewData["Title"] = "Boquilhas";
        // O «+ Registo com data antiga» da ficha da ferramenta: o registo
        // abre com o campo «Data e hora reais» JÁ VESTIDO com a data/hora
        // real do primeiro movimento (o fluxo do boquilhas.html). Ilegível
        // ou ausente: o agora do servidor — o uso diário, nunca um
        // bloqueio.
        if (!string.IsNullOrWhiteSpace(dataAntiga)
            && DateTime.TryParse(dataAntiga, CultureInfo.InvariantCulture, DateTimeStyles.None, out var antiga))
        {
            MovementFormOccurredAt = antiga.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        }
        // AS FUNÇÕES NA BARRA (o Owner 2026-10-13: «registo e historico sao
        // botoes quando deveriam estar no tab»): o padrão do
        // Controlo/Planeamento — a barra de funções do shell, nunca botões
        // no page-head; as máquinas ficam no SIDEPANEL da folha.
        var activeKey = historico == "1" ? "historico" : ficheiros == "1" ? "ficheiros" : "registo";
        ShowHistory = historico == "1";
        ShowFiles = ficheiros == "1";
        if (ShowFiles && User?.Identity?.IsAuthenticated == true)
        {
            // «Registar sem Job On» (Diogo, 22/10/2026): as BQ registadas,
            // para a janela da página Boquilhas.
            SemJobOnTools = await _db.Tools.AsNoTracking()
                .Where(t => t.Type == ToolType.Bq && t.ToolId != ToolRegistryConstants.RemovedToolId)
                .OrderBy(t => t.Reference).ThenBy(t => t.Lot)
                .Select(t => new SemJobOnTool(t.ToolId, t.Reference, t.Lot))
                .ToListAsync(HttpContext.RequestAborted);
            SemJobOnResult = Request.Query["sem_jobon"].FirstOrDefault();
            // A linha do movimento (Diogo, 22/10/2026: ajuda a distinguir a BQ).
            SemJobOnLines = await _db.Machines.AsNoTracking()
                .OrderBy(m => m.Code)
                .Select(m => new DMO.Alpha.Core.Production.MachineOption(m.Id, m.Code))
                .ToListAsync(HttpContext.RequestAborted);
        }
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new("boquilhas", "Boquilhas"))) with
        {
            ActiveFunctionKey = activeKey,
            Functions = ShellStateFactory.BoquilhasFunctions(activeKey)
        };

        var authenticated = User?.Identity?.IsAuthenticated == true;

        if (movementRefusal is not null
            && Enum.TryParse(movementRefusal, out RegisterBqMovementResult parsed)
            && parsed != RegisterBqMovementResult.Success)
        {
            MovementRefusal = parsed;
        }

        if (associationRefusal is not null
            && Enum.TryParse(associationRefusal, out AssociateBqRepairTraceToContextResult parsedAssociation)
            && parsedAssociation != AssociateBqRepairTraceToContextResult.Success)
        {
            AssociationRefusal = parsedAssociation;
        }

        // O SIDEPANEL É FIXO (o Owner 2026-10-14, verbatim: «repara que
        // quando mexes nos tabs desaparece a informaçao do sidepanel»): as
        // correntes carregam-se EM TODAS as vistas (o painel lateral
        // persiste entre o Registo, os Ficheiros, a ficha e o Histórico —
        // no dmo.html o sidepanel é fixo e os tabs só trocam o conteúdo
        // central). Era carregado só nas vistas de registo/ficheiros; no
        // Histórico o painel ficava sem informação.
        Currents = await _currentQuery.ListAsync(HttpContext.RequestAborted);

        // A VISTA DO HISTÓRICO (o Owner: «o calendario pertence ao
        // historico das boquilhas, para conseguirmos carregar num dia e ver
        // os movimentos desse dia. depois poder filtrar por reparador
        // maquina referencia») — o calendário do mês, o dia escolhido, os
        // movimentos com os filtros. O REGISTO fica limpo (a vista default).
        if (historico == "1")
        {
            await ComposeHistoryAsync(ano, mes, dia, reparador, maquina, tipo, desde, ate, pesquisa, vista, mesAno, bqFerramenta);

            return Page();
        }

        // Canonical BQ catalog: server-side search/pagination and the selected tool sheet.
        if (ficheiros == "1")
        {
            ShowFiles = true;

            // A FICHA DA FERRAMENTA (o toolsTab do dmo-html): ?ferramenta=
            // dentro da tab Ficheiros abre a ficha única da BQ/lote — os
            // fatos, os registos dela e o «+ Registo com data antiga». O
            // tool_id vive no URL (a regra da casa); a folha nunca o
            // mostra como texto.
            if (!string.IsNullOrWhiteSpace(ferramenta))
            {
                if (!authenticated)
                {
                    return Challenge();
                }

                await ComposeToolSheetAsync(ferramenta.Trim(), HttpContext.RequestAborted);
            }

            await LoadCatalogAsync();
            return Page();
        }

        // A FATIA 2 (a cascata: o módulo ancora NO CONTEXT ID — o bq_id da
        // produção, nunca o tool_id): o bq_id → a ferramenta do contexto →
        // o trace já associado. Decisão do proprietário de 16/10/2026: abrir
        // a máquina NÃO cria o registo; sem registo, a folha mostra o mesmo
        // estado vazio e o primeiro movimento cria-o (o POST com o bq_id).
        if (bqContextId is int canonicalBqId)
        {
            if (!authenticated)
            {
                return Challenge();
            }

            // O bq_id, a produção, o trace JÁ ASSOCIADO (um bq_id tem um
            // único trace) e a ferramenta numa consulta.
            var context = await _registoQuery.FindByContextAsync(canonicalBqId, TraceMovementsPage, HttpContext.RequestAborted);
            if (context is null)
            {
                TraceRequestedNotFound = true;
                MovementRefusal ??= RegisterBqMovementResult.TraceNotFound;
            }
            else
            {
                RegistoTrace = context.Trace;

                if (RegistoTrace is null)
                {
                    await ComposePendingRegisterAsync(context, HttpContext.RequestAborted);
                }

                if (RegistoTrace is not null)
                {
                    // Os fatos da produção (o contexto do jobon) e o
                    // HISTÓRICO da ferramenta (os jobons em que foi usada).
                    ProductionContext = context.Production;
                    JobOnNotice = JobOnChangeNoticeView.From(
                        "O Job On mudou depois deste registo.",
                        await _changes.ForBqContextAsync(canonicalBqId, HttpContext.RequestAborted));
                    JobHistory = await _history.ListJobHistoryAsync(
                        context.ToolId, JobHistoryPage, cancellationToken: HttpContext.RequestAborted);
                    RequestedBq = context.Tool;
                }
            }

            await LoadRegisterFactsAsync(HttpContext.RequestAborted);
            return Page();
        }

        if (traceId is int canonicalId)
        {
            if (!authenticated)
            {
                return Challenge();
            }

            RegistoTrace = await _registoQuery.FindTraceAsync(canonicalId, TraceMovementsPage, HttpContext.RequestAborted);
            if (RegistoTrace is null)
            {
                TraceRequestedNotFound = true;
                MovementRefusal ??= RegisterBqMovementResult.TraceNotFound;
            }
            else
            {
                // O «Associar Job On» em dois passos: as produções só depois
                // da referência, e só as dela.
                AssociateReference = string.IsNullOrWhiteSpace(AssociateReference) ? null : AssociateReference.Trim();
                if (RegistoTrace.BqContextId is null && AssociateReference is not null)
                {
                    AssociateProductions = await _productionPicker.ListByReferenceAsync(AssociateReference, HttpContext.RequestAborted);
                    AssociateReferenceWithoutProductions = AssociateProductions.Count == 0;
                }

                // O card da produção (o bq_id do trace — os fatos humanos),
                // já lido com o trace.
                ProductionContext = RegistoTrace.Production;
            }
        }

        await LoadRegisterFactsAsync(HttpContext.RequestAborted);

        return Page();
    }

    private async Task ComposeHistoryAsync(int? ano, int? mes, int? dia, string? reparador,
        string? maquina, string? tipo, DateOnly? desde, DateOnly? ate, string? pesquisa,
        string? vista, string? mesAno, string? bqFerramenta = null)
    {
        ShowHistory = true;
        // O «Histórico BQ» (Diogo, 19/10/2026): a ferramenta do registo, com
        // a BQ e o lote à vista no card dos filtros.
        if (bqFerramenta?.Trim() is { Length: > 0 } wantedTool)
        {
            var filterTool = await _db.Tools
                .AsNoTracking()
                .Where(t => t.ToolId == wantedTool)
                .Select(t => new { t.ToolId, t.Reference, t.Lot })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (filterTool is not null)
            {
                FilterToolId = filterTool.ToolId;
                FilterToolLabel = $"BQ {filterTool.Reference}" + (filterTool.Lot is { Length: > 0 } lot ? $" · Lote {lot}" : "");
            }
        }
        var today = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        // O SELETOR DE MÊS (o histMonthQuick do html — yyyy-MM): a
        // âncora do ano/mês quando presente.
        if (mesAno is { Length: 7 } quickMonth
            && int.TryParse(quickMonth[..4], out var quickYear)
            && int.TryParse(quickMonth[5..], out var quickMonthNumber)
            && quickYear is > 1999 and < 3000
            && quickMonthNumber is >= 1 and <= 12)
        {
            HistoryYear = quickYear;
            HistoryMonth = quickMonthNumber;
            FilterMonth = quickMonth;
        }
        else
        {
            HistoryYear = ano is int y && y is > 1999 and < 3000 ? y : today.Year;
            HistoryMonth = mes is int mm && mm is >= 1 and <= 12 ? mm : today.Month;
        }
        MonthCounts = await _history.ListMonthCountsAsync(
            HistoryYear, HistoryMonth, HttpContext.RequestAborted);

        // OS FILTROS DO HTML: a máquina (B1..C3), o tipo (Saída /
        // Entrada / Entrada sem reparação), desde/até, o reparador e a
        // pesquisa de texto — todos sobre a data real (a A1).
        ShowMatrix = vista == "matriz";
        ShowMovements = vista == "movimentos";
        ShowGrouped = !ShowMatrix && !ShowMovements;
        FilterMachine = maquina?.Trim();
        FilterType = tipo?.Trim();
        FilterOperator = reparador?.Trim();
        FilterText = pesquisa?.Trim();
        FilterDesde = desde;
        FilterAte = ate;

        // O PERÍODO EFETIVO: desde/até quando dados; o dia do
        // calendário quando clicado (?dia — o «clica num dia para
        // filtrar»); SEM período = TODOS os movimentos (o uso default
        // do html). A grelha (a matriz) precisa de um período fechado:
        // as pontas em falta vestem o mês selecionado.
        var monthStart = new DateOnly(HistoryYear, HistoryMonth, 1);
        var monthEnd = new DateOnly(HistoryYear, HistoryMonth, DateTime.DaysInMonth(HistoryYear, HistoryMonth));
        DateOnly? listStart = null, listEnd = null;
        DateOnly matrixStart, matrixEnd;
        if (desde is not null || ate is not null)
        {
            listStart = desde;
            listEnd = ate;
            matrixStart = desde ?? monthStart;
            matrixEnd = ate ?? monthEnd;
        }
        else if (dia is int dd && dd >= 1 && dd <= DateTime.DaysInMonth(HistoryYear, HistoryMonth))
        {
            HistoryDay = new DateOnly(HistoryYear, HistoryMonth, dd);
            listStart = HistoryDay;
            listEnd = HistoryDay;
            matrixStart = HistoryDay.Value;
            matrixEnd = HistoryDay.Value;
        }
        else if (FilterMonth is not null)
        {
            // O mês escolhido no seletor é um período: «Filtrar» só com o mês
            // mostra os movimentos desse mês (antes a lista ficava vazia).
            HistoryDayIsDefault = true;
            HistoryDay = null;
            listStart = monthStart;
            listEnd = monthEnd;
            matrixStart = monthStart;
            matrixEnd = monthEnd;
        }
        else
        {
            HistoryDayIsDefault = true;
            HistoryDay = null;
            matrixStart = monthStart;
            matrixEnd = monthEnd;
        }

        // A GRELHA tem um limite de 366 dias (desempenho): um período maior é
        // cortado nos primeiros 366 dias e a folha avisa — nunca recusa.
        if (ShowMatrix)
        {
            (matrixStart, matrixEnd, MatrixPeriodWarning) = ClampMatrixPeriod(matrixStart, matrixEnd);
        }

        // A lista abre em branco: só carrega depois de uma pesquisa, um período,
        // outro filtro ou um dia do calendário. A grelha e o PDF mantêm-se.
        HistorySearchApplied = listStart is not null || listEnd is not null
            || !string.IsNullOrEmpty(FilterMachine) || !string.IsNullOrEmpty(FilterType)
            || !string.IsNullOrEmpty(FilterOperator) || !string.IsNullOrEmpty(FilterText)
            || FilterToolId is not null;

        // A LISTA dos movimentos detalhados (TABELA da casa — nunca os
        // cards espremidos) + OS CONTADORES do filtro (as SOMAS DE
        // QUANTIDADE por tipo — «totais de movimentos do filtro, não
        // saldos de stock»).
        // Só filtros, sem período (ex.: «Máquina B1»): a lista mostra os
        // movimentos mais recentes até ao limite e avisa, em vez de ler
        // todos os movimentos de sempre (desempenho, 18/10/2026).
        var semPeriodo = !ShowMatrix && listStart is null && listEnd is null;
        Movements = ShowMatrix || HistorySearchApplied
            ? await _history.ListMovementsAsync(
                ShowMatrix ? matrixStart : listStart,
                ShowMatrix ? matrixEnd : listEnd,
                FilterMachine,
                FilterType,
                FilterOperator,
                FilterText,
                HttpContext.RequestAborted,
                semPeriodo ? MaxListMovements + 1 : null,
                FilterToolId)
            : [];
        if (semPeriodo && Movements.Count > MaxListMovements)
        {
            Movements = Movements.Take(MaxListMovements).ToList();
            ListLimitWarning = $"Mostra os {MaxListMovements} movimentos mais recentes. Escolha um período para ver os anteriores.";
        }
        HistoryMovementCount = Movements.Count;
        FilteredSaidas = Movements.Where(m => m.MovementType == BqMovementType.Saida.ToString()).Sum(m => m.Quantity);
        FilteredEntradas = Movements.Where(m => m.MovementType == BqMovementType.Entrada.ToString()).Sum(m => m.Quantity);
        FilteredSemReparacao = Movements.Where(m => m.MovementType == BqMovementType.EntradaSemReparacao.ToString()).Sum(m => m.Quantity);

        var query = new Dictionary<string, string?>
        {
            ["historico"] = "1", ["vista"] = HistoryView,
            ["ano"] = HistoryYear.ToString(CultureInfo.InvariantCulture),
            ["mes"] = HistoryMonth.ToString(CultureInfo.InvariantCulture),
            ["maquina"] = FilterMachine, ["tipo"] = FilterType,
            ["reparador"] = FilterOperator, ["pesquisa"] = FilterText,
            ["bq_ferramenta"] = FilterToolId,
            ["desde"] = FilterDesde?.ToString("yyyy-MM-dd"),
            ["ate"] = FilterAte?.ToString("yyyy-MM-dd"),
            ["mes_ano"] = FilterMonth,
            ["dia"] = HistoryDay?.Day.ToString(CultureInfo.InvariantCulture)
        };
        HistoryAllDaysHref = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Boquilhas",
            query.Where(p => p.Key is not ("desde" or "ate" or "dia" or "mes_ano") && !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Key, p => p.Value));
        HistoryWithoutToolHref = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Boquilhas",
            query.Where(p => p.Key != "bq_ferramenta" && !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Key, p => p.Value));
        var todayQuery = new Dictionary<string, string?>(query)
        {
            ["ano"] = today.Year.ToString(CultureInfo.InvariantCulture),
            ["mes"] = today.Month.ToString(CultureInfo.InvariantCulture),
            ["dia"] = today.Day.ToString(CultureInfo.InvariantCulture)
        };
        todayQuery.Remove("desde"); todayQuery.Remove("ate"); todayQuery.Remove("mes_ano");
        HistoryTodayHref = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Boquilhas",
            todayQuery.Where(p => !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Key, p => p.Value));
        query["handler"] = "PdfHistorico";
        PdfContextualHref = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Boquilhas",
            query.Where(p => !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Key, p => p.Value));
        GroupedMovements = BqHistoryMovementGroup.From(Movements);
        DayMovements = HistoryDay is { } selectedDay ? Movements.Where(m =>
            DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(m.OccurredAtUtc)) == selectedDay).ToList() : [];
        DayGroups = BqHistoryMovementGroup.From(DayMovements);

        // A GRELHA do resumo diário (o read model pronto para o PDF da
        // A5 — o gerador só formata), já cortada ao limite acima: agrupa os
        // movimentos acabados de ler (o mesmo período e os mesmos filtros),
        // sem voltar a consultar a base.
        if (ShowMatrix)
        {
            MatrixPeriodLabel =
                $"{matrixStart.ToString("dd/MM/yyyy")} — {matrixEnd.ToString("dd/MM/yyyy")}";
            MatrixEntries = BqDailySummary.FromMovements(Movements);
            MatrixDays = Enumerable.Range(0, matrixEnd.DayNumber - matrixStart.DayNumber + 1)
                .Select(matrixStart.AddDays).ToList();
            MatrixWithoutMachine = Movements.Count(m => m.MachineCode is null);
            if (MatrixWithoutMachine > 0)
            {
                var lista = query
                    .Where(p => p.Key is not ("vista" or "desde" or "ate" or "dia" or "mes_ano" or "handler") && !string.IsNullOrEmpty(p.Value))
                    .ToDictionary(p => p.Key, p => p.Value);
                lista["vista"] = "grupos";
                lista["desde"] = matrixStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                lista["ate"] = matrixEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                MatrixWithoutMachineHref = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Boquilhas", lista);
            }
        }

        // O CALENDÁRIO COMPOEM-SE EM TODOS OS DIAS (o fix do achado do
        // Owner: com ?dia explícito o calendário nem se compunha — a
        // coluna dele colapsava e a página ficava espremida). O dia
        // escolhido veste-se no calendário, default ou explícito; os
        // filtros ativos preservam-se nos links dos dias (o «clica num
        // dia para filtrar» do html).
        var calendarPreserved = new Dictionary<string, string>
        {
            ["vista"] = HistoryView
        };
        if (FilterMachine is { Length: > 0 } calendarMachine)
        {
            calendarPreserved["maquina"] = calendarMachine;
        }
        if (FilterType is { Length: > 0 } calendarType)
        {
            calendarPreserved["tipo"] = calendarType;
        }
        if (FilterOperator is { Length: > 0 } calendarOperator)
        {
            calendarPreserved["reparador"] = calendarOperator;
        }
        if (FilterText is { Length: > 0 } calendarText)
        {
            calendarPreserved["pesquisa"] = calendarText;
        }
        if (FilterToolId is { } calendarTool)
        {
            calendarPreserved["bq_ferramenta"] = calendarTool;
        }
        // Escolher um dia substitui desde/até; os restantes filtros mantêm-se.
        HistoryCalendar = ComporCalendar(HistoryYear, HistoryMonth, MonthCounts, HistoryDay, calendarPreserved);

    }

    public async Task<IActionResult> OnGetPdfHistoricoAsync(
        int? ano, int? mes, int? dia, string? reparador, string? maquina, string? tipo,
        DateOnly? desde, DateOnly? ate, string? pesquisa, string? vista,
        [FromQuery(Name = "mes_ano")] string? mesAno,
        [FromQuery(Name = "bq_ferramenta")] string? bqFerramenta)
    {
        // O PDF segue a lista. Sem período nem filtro (a lista ainda em
        // branco), sai o mês do calendário: exportar todos os movimentos de
        // sempre dava centenas de páginas (teste de carga de 17/10/2026).
        if (desde is null && ate is null && dia is null && string.IsNullOrWhiteSpace(mesAno)
            && string.IsNullOrWhiteSpace(maquina) && string.IsNullOrWhiteSpace(tipo)
            && string.IsNullOrWhiteSpace(reparador) && string.IsNullOrWhiteSpace(pesquisa)
            && string.IsNullOrWhiteSpace(bqFerramenta))
        {
            var today = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            var year = ano is int y && y is > 1999 and < 3000 ? y : today.Year;
            var month = mes is int m && m is >= 1 and <= 12 ? m : today.Month;
            mesAno = $"{year:D4}-{month:D2}";
        }
        await ComposeHistoryAsync(ano, mes, dia, reparador, maquina, tipo, desde, ate, pesquisa, vista, mesAno, bqFerramenta);
        var generator = new DMO.Alpha.Infrastructure.Boquilhas.BqHistoryPdfGenerator();
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value ?? User.FindFirst(DmoClaimTypes.Email)?.Value;
        var bytes = ShowMatrix
            ? generator.GenerateDailyMatrix(new(MatrixPeriodLabel, MatrixEntries,
                DMO.Alpha.Infrastructure.Boquilhas.BqHistoryPdfGenerator.HouseMachines, generatedBy,
                Warning: MatrixPeriodWarning,
                WithoutMachineWarning: MatrixWithoutMachineWarning is { } semMaquina
                    ? $"{semMaquina} Estão na vista «Por máquina / Job On»."
                    : null))
            : generator.GenerateMovementList(new(
                ShowMovements ? "Boquilhas · Movimentos detalhados" : "Boquilhas · Por máquina / Job On",
                (ListLimitWarning is not null ? $"Os {HistoryMovementCount} movimentos mais recentes" : $"{HistoryMovementCount} movimento(s)")
                    + $" · {HistoryPeriodLabel}"
                    + (FilterMachine is { Length: > 0 } ? $" · Máquina {FilterMachine}" : "")
                    + (FilterType is { Length: > 0 } ? $" · {BqMovementLabels.Of(FilterType)}" : "")
                    + (FilterOperator is { Length: > 0 } ? $" · Reparador {FilterOperator}" : "")
                    + (FilterText is { Length: > 0 } ? $" · Pesquisa {FilterText}" : "")
                    + (FilterToolLabel is { Length: > 0 } ? $" · {FilterToolLabel}" : ""),
                Movements, generatedBy, GroupByMachineAndTrace: ShowGrouped));
        return File(bytes, "application/pdf", $"BOQUILHAS-{HistoryView}.pdf");
    }

    // Ambos os destinos (?bq_id e ?trace_id) compõem a mesma ficha.
    private async Task LoadRegisterFactsAsync(CancellationToken cancellationToken)
    {
        // Os fatos do lote para o painel das quantidades calculadas
        // (o dmo-html: «Quantidade do lote — Fabricação — Em
        // reparação — Discrepância acumulada»): a quantidade
        // contabilística da Tool (o lote que não cresce) e as
        // máquinas compatíveis (o facto humano).
        var linkedMachines = new List<DMO.Alpha.Core.Production.MachineOption>();
        if (RegistoTrace?.ToolId is { } registerToolId)
        {
            RegisterLotQuantity = RegistoTrace.ToolAccountedQuantity;
            linkedMachines = await _db.ToolMachineLinks
                .AsNoTracking()
                .Where(link => link.ToolId == registerToolId)
                .OrderBy(link => link.MachineId)
                .Select(link => new DMO.Alpha.Core.Production.MachineOption(link.MachineId, link.Machine.Code))
                .ToListAsync(cancellationToken);
            RegisterMachineCodes = linkedMachines.Select(m => m.Code).ToList();
        }
        // As máquinas do formulário: as do painel lateral, já lidas.
        var allMachines = Currents.Count > 0
            ? Currents.DistinctBy(m => m.MachineId).Select(m => new DMO.Alpha.Core.Production.MachineOption(m.MachineId, m.MachineCode)).ToList()
            : await _db.Machines.AsNoTracking().OrderBy(m => m.Code)
                .Select(m => new DMO.Alpha.Core.Production.MachineOption(m.Id, m.Code))
                .ToListAsync(cancellationToken);
        // SÓ AS MÁQUINAS DA FERRAMENTA (Diogo, 18/10/2026: «se ela só
        // trabalha na C2 como está na ficha da ferramenta não deveria
        // conseguir escolher B2»): com máquinas na ficha, a lista mostra só
        // essas (e a da produção associada, se for outra). Sem máquinas na
        // ficha, mostra todas.
        if (linkedMachines.Count > 0)
        {
            var allowed = linkedMachines.Select(m => m.MachineId).ToHashSet();
            var productionMachine = ProductionContext?.MachineCode;
            allMachines = allMachines
                .Where(m => allowed.Contains(m.MachineId) || (productionMachine is not null && m.Code == productionMachine))
                .ToList();
        }
        MovementMachines = allMachines;
        await ComposeMovementCorrectionAsync();
    }

    /// <summary>
    /// A FICHA DA FERRAMENTA (o toolsTab do dmo-html): os fatos da BQ/lote
    /// (referência, lote, a quantidade contabilística, as máquinas
    /// compatíveis) e os registos dela — os traces ancorados diretamente
    /// (pré-produção/sem Job On) ou associados (via bq_id), com o estado
    /// pela janela do Job On. Composta toda no servidor (a lei «tudo
    /// razor»); o tool_id só vive no URL/hidden.
    /// </summary>
    private async Task ComposeToolSheetAsync(string toolId, CancellationToken cancellationToken)
    {
        var tool = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new { t.Reference, t.Lot, t.BqAccountedQuantity })
            .FirstOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            ToolSheetNotFound = true;
            return;
        }

        var machines = await _db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => link.ToolId == toolId)
            .OrderBy(link => link.MachineId)
            .Select(link => link.Machine.Code)
            .ToListAsync(cancellationToken);

        ToolSheet = new BqToolSheet(
            toolId,
            tool.Reference,
            tool.Lot,
            tool.BqAccountedQuantity,
            machines,
            await ListRegisterCandidatesAsync(toolId, RegistersPage, cancellationToken));

        // Os movimentos sem Job On desta BQ, aqui na página Boquilhas: o
        // Registo é só para os registos de um Job On (Diogo, 22/10/2026).
        var semJobOn = await _db.BqMovements.AsNoTracking()
            .Where(m => m.Trace.ToolId == toolId && m.Trace.BqContextId == null)
            .OrderByDescending(m => m.OccurredAtUtc).ThenByDescending(m => m.Id)
            .Take(SemJobOnMovementLimit)
            .Select(m => new { m.Type, m.Quantity, m.MachineCodeFrozen, m.OccurredAtUtc })
            .ToListAsync(cancellationToken);
        SemJobOnMovements = semJobOn
            .Select(m => new SemJobOnMovement(
                DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(m.OccurredAtUtc), m.Type, m.Quantity, m.MachineCodeFrozen))
            .ToList();
    }

    /// <summary>Quantos movimentos sem Job On a ficha mostra.</summary>
    public const int SemJobOnMovementLimit = 20;

    /// <summary>
    /// «Registar sem Job On» (Diogo, 22/10/2026): a janela da página
    /// Boquilhas grava um movimento na BQ e lote escolhidos, sem Job On. Abre
    /// (ou reutiliza) o registo sem Job On da BQ e grava o movimento pelos
    /// mesmos handlers do Registo; volta à página Boquilhas com a ficha dessa
    /// BQ e os movimentos dela.
    /// </summary>
    public async Task<IActionResult> OnPostRegistarSemJobOnAsync(
        [FromForm(Name = "tool_id")] string? toolId,
        [FromForm(Name = "movement_type")] string? movementType,
        [FromForm(Name = "quantity")] int? quantity,
        [FromForm(Name = "occurred_at")] string? occurredAt,
        [FromForm(Name = "movement_machine_id")] int? lineId)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var id = toolId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("/Boquilhas/Index", new { ficheiros = "1", sem_jobon = "SemBq" });
        }

        object Volta(string resultado) => new { ficheiros = "1", ferramenta = id, sem_jobon = resultado };
        if (quantity is null or < 1)
        {
            return RedirectToPage("/Boquilhas/Index", Volta(nameof(RegisterBqMovementResult.InvalidQuantity)));
        }

        var trace = await _createTrace.HandleAsync(new CreateBqRepairTrace(id), HttpContext.RequestAborted);
        if (trace.Trace is null)
        {
            return RedirectToPage("/Boquilhas/Index", new { ficheiros = "1", sem_jobon = "SemBq" });
        }

        DateTime? occurredAtUtc = null;
        if (!string.IsNullOrWhiteSpace(occurredAt)
            && DateTime.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredLocal))
        {
            occurredAtUtc = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToUtc(
                DMO.Alpha.Core.Runtime.DmoBusinessTime.NextValidLocal(occurredLocal, out _));
        }

        var outcome = await _movementHandler.HandleAsync(
            new RegisterBqMovement(
                trace.Trace.BqRepairTraceId,
                movementType ?? string.Empty,
                quantity.Value,
                User.FindFirst(DmoClaimTypes.OperatorId)?.Value,
                occurredAtUtc,
                lineId),
            HttpContext.RequestAborted);
        return RedirectToPage("/Boquilhas/Index", Volta(
            outcome.Result == RegisterBqMovementResult.Success ? "ok" : outcome.Result.ToString()));
    }

    /// <summary>A frase do resultado do «Registar sem Job On».</summary>
    public string? SemJobOnMessage => SemJobOnResult switch
    {
        null or "" => null,
        "ok" => "Movimento registado sem Job On.",
        "SemBq" => "Escolha a BQ e o lote. Nenhum movimento foi registado.",
        _ when Enum.TryParse(SemJobOnResult, out RegisterBqMovementResult r) =>
            r switch
            {
                RegisterBqMovementResult.InvalidQuantity => "Indique a quantidade como um número inteiro igual ou superior a 1. Nenhum movimento foi registado.",
                RegisterBqMovementResult.MachineNotFound => "A linha indicada não existe. Nenhum movimento foi registado.",
                RegisterBqMovementResult.UnknownMovementType => "Escolha Saída, Entrada ou Entrada sem reparação. Nenhum movimento foi registado.",
                _ => "O movimento não foi registado."
            },
        _ => null
    };

    /// <summary>
    /// Os registos de uma BQ (o painel de escolha do dmo-html): o trace
    /// âncora direta (pré-produção/sem Job On) OU associado (a Tool
    /// alcança-se via bq_id → BqContext.ToolId), do mais recente para o
    /// mais antigo, com o estado de produção pela janela do Job On e o
    /// número de movimentos de cada registo.
    /// </summary>
    private async Task<PagedList<BqRegisterCandidate>> ListRegisterCandidatesAsync(
        string toolId,
        int? pageNumber,
        CancellationToken cancellationToken)
    {
        var traces = await _db.BqRepairTraces
            .AsNoTracking()
            .Where(t => t.ToolId == toolId
                || (t.BqContext != null && t.BqContext.ToolId == toolId))
            .OrderByDescending(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.BqContextId,
                // A BQ deste registo foi trocada no Job On (18/10/2026): a
                // produção pode estar em máquina, mas já com outra BQ.
                Trocada = t.BqContext != null && t.BqContext.JobOnId == null,
                // Um bq_id de uma BQ trocada continua a mostrar a produção.
                ProductionNumber = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.ProductionNumber : t.BqContext.ReplacedJobOn!.ProductionNumber,
                ProductionReference = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.Reference : t.BqContext.ReplacedJobOn!.Reference,
                MachineCode = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.Machine.Code : t.BqContext.ReplacedJobOn!.Machine.Code,
                InicioEm = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.InicioEm : t.BqContext.ReplacedJobOn!.InicioEm,
                FimEm = t.BqContext == null ? null : t.BqContext.JobOn != null ? t.BqContext.JobOn.FimEm : t.BqContext.ReplacedJobOn!.FimEm
            })
            .ToPagedListAsync(pageNumber, cancellationToken: cancellationToken);

        // As contagens só dos registos da página.
        var traceIds = traces.Items.Select(t => t.Id).ToList();
        var movementCounts = await _db.BqMovements
            .AsNoTracking()
            .Where(m => traceIds.Contains(m.BqRepairTraceId))
            .GroupBy(m => m.BqRepairTraceId)
            .Select(g => new { TraceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TraceId, g => g.Count, cancellationToken);

        // O estado pelo dia operacional, como o painel e a procura.
        var hoje = await _currentQuery.OperationalDayAsync(cancellationToken);
        return new PagedList<BqRegisterCandidate>(traces.Items.Select(t => new BqRegisterCandidate(
            t.Id,
            t.ProductionNumber,
            t.ProductionReference,
            t.MachineCode,
            t.BqContextId is null
                ? "Sem Job On"
                : t.Trocada
                ? "BQ trocada nesta produção"
                : ProductionStateLabel(t.InicioEm, t.FimEm, hoje),
            movementCounts.TryGetValue(t.Id, out var count) ? count : 0)).ToList(),
            traces.TotalCount, traces.PageNumber, traces.PageSize);
    }

    /// <summary>
    /// O estado de produção pela JANELA do Job On (o productionState do
    /// dmo-html): em máquina quando o hoje está dentro da janela, 
    /// programada quando ainda vem, anterior quando fechada, sem janela
    /// quando o Job On não tem datas.
    /// </summary>
    private static string ProductionStateLabel(DateOnly? inicioEm, DateOnly? fimEm, DateOnly hoje)
    {
        if (inicioEm is not { } inicio)
        {
            return "Produção sem janela";
        }

        if (inicio > hoje)
        {
            return "Produção programada";
        }

        return fimEm is not { } fim || hoje <= fim ? "Produção em máquina" : "Produção anterior";
    }

    /// <summary>
    /// POST nativo do Registo: o browser submete o trace_id canónico, o
    /// token canónico, a quantidade observada e a DATA/HORA REAL do
    /// movimento (o campo «Data e hora reais», pre-preenchido com o agora
    /// — o uso diário não o toca; o backfill dos ~2 meses escreve a data
    /// antiga, o Owner 2026-10-14). A discrepância nunca é enviada pelo
    /// cliente — o backend é a fonte autoritativa.
    /// </summary>
    // O mesmo envio outra vez (a ligação caiu depois de gravar) não grava
    // duas vezes (deep sweep, 18/10/2026, B-boquilhas-2).
    [EnvioUnico]
    public async Task<IActionResult> OnPostRegisterMovementAsync(
        [FromForm(Name = "trace_id")] int traceId,
        [FromForm(Name = "movement_type")] string? movementType,
        [FromForm(Name = "quantity")] int? quantity,
        [FromForm(Name = "occurred_at")] string? occurredAt,
        [FromForm(Name = "movement_machine_id")] int? movementMachineId,
        [FromForm(Name = "bq_id")] int? bqContextId = null)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        // A máquina aberta sem registo (decisão do proprietário de
        // 16/10/2026): o form envia o bq_id e o primeiro movimento cria o
        // registo. As recusas voltam à mesma máquina (?bq_id).
        int? pendingBqId = traceId <= 0 && bqContextId is int formBqId ? formBqId : null;

        // A QUANTIDADE OBSERVADA (o stresser das Boquilhas 2026-10-12):
        // ilegível (abc) ou não-positiva (-5, 0) NÃO é facto — recusa tipada,
        // nunca o 0 silencioso que reescrevia o observado nem o negativo que
        // deformava o saldo derivado do trace.
        if (quantity is null or < 1)
        {
            return RedirectToPage("/Boquilhas/Index", RegisterRoute(traceId, pendingBqId,
                ("movement_refusal", "InvalidQuantity")));
        }

        // A DATA/HORA REAL do movimento: o valor do datetime-local chega em
        // hora local da fábrica (Europe/Lisbon) e persiste em UTC. SEM data (ausente ou
        // ilegível) o AGORA DO BACKEND manda — nunca uma recusa extra:
        // registar não pode bloquear por causa da data (a recusa tipada
        // continua a informar apenas as causas de negócio de sempre).
        // A hora que cai no salto da mudança de hora (março: 01:00–01:59 não
        // existe) passa para a hora seguinte e a folha avisa — nunca recusa
        // (decisão do proprietário de 16/10/2026).
        DateTime? occurredAtUtc = null;
        DateTime? occurredValidLocal = null;
        (string From, string To)? shiftedTime = null;
        if (!string.IsNullOrWhiteSpace(occurredAt)
            && DateTime.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredLocal))
        {
            var validLocal = DMO.Alpha.Core.Runtime.DmoBusinessTime.NextValidLocal(occurredLocal, out var shifted);
            if (shifted)
            {
                shiftedTime = (LocalFieldValue(occurredLocal), LocalFieldValue(validLocal));
            }
            occurredAtUtc = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToUtc(validLocal);
            occurredValidLocal = validLocal;
        }

        var operatorId = User.FindFirst(DmoClaimTypes.OperatorId)?.Value;
        var outcome = pendingBqId is int bqId
            ? await _movementHandler.HandleForContextAsync(
                new RegisterBqMovementForContext(
                    bqId,
                    movementType ?? string.Empty,
                    quantity ?? 0,
                    operatorId,
                    occurredAtUtc,
                    movementMachineId),
                HttpContext.RequestAborted)
            : await _movementHandler.HandleAsync(
                new RegisterBqMovement(
                    traceId,
                    movementType ?? string.Empty,
                    quantity ?? 0,
                    operatorId,
                    occurredAtUtc,
                    movementMachineId),
                HttpContext.RequestAborted);

        if (outcome.Result == RegisterBqMovementResult.Success)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = outcome.Movement?.BqRepairTraceId ?? traceId,
                data_antiga = KeptOccurrenceField(occurredAtUtc, occurredValidLocal),
                hora_indicada = shiftedTime?.From,
                hora_gravada = shiftedTime?.To,
                data_no_futuro = outcome.OccurredInTheFuture && occurredValidLocal is DateTime futureLocal
                    ? LocalFieldValue(futureLocal) : null
            });
        }

        return RedirectToPage("/Boquilhas/Index", RegisterRoute(traceId, pendingBqId,
            ("movement_refusal", outcome.Result.ToString()),
            ("movement_machine_id", movementMachineId),
            ("movement_type", movementType),
            ("quantity", quantity),
            ("data_antiga", occurredValidLocal is DateTime escrita ? LocalFieldValue(escrita) : null)));
    }

    /// <summary>
    /// A data e hora que o form volta a mostrar depois de gravar (Diogo,
    /// 17/10/2026: lançar muitas guias seguidas): a indicada, quando não era
    /// o agora (mais de <see cref="KeptOccurrenceMinutes"/> minutos de
    /// diferença). No uso diário, com o agora pré-preenchido, o form volta ao
    /// agora, para nunca gravar uma hora antiga sem querer.
    /// </summary>
    private static string? KeptOccurrenceField(DateTime? occurredAtUtc, DateTime? occurredLocal) =>
        occurredAtUtc is DateTime utc && occurredLocal is DateTime local
            && Math.Abs((utc - DateTime.UtcNow).TotalMinutes) > KeptOccurrenceMinutes
            ? LocalFieldValue(local)
            : null;

    internal const int KeptOccurrenceMinutes = 10;

    /// <summary>
    /// POST nativo «Associar Job On a este registo» (o fim do ciclo do
    /// backfill — o Owner 2026-10-14, verbatim: «criar um jobon, e poder
    /// associar um registo ao mesmo»): o browser submete APENAS FATOS
    /// HUMANOS (o nº de produção, ex.: 202601, e a referência completa,
    /// ex.: 5447T173) e o trace_id canónico no input hidden — a página
    /// resolve o bq_id (os ids nunca visíveis na folha): a produção pelo
    /// nº (único; a referência só confirma e, diferente, dá um aviso depois
    /// de associar) → o contexto BQ da produção que partilha o tool_id
    /// canónico do trace (a correlação do handler) → O HANDLER EXISTENTE
    /// faz a associação (define o BqContextId no trace, limpa a âncora
    /// direta; os movimentos e as datas mantêm-se; um trace nunca é
    /// reassociado e um bq_id nunca recebe um segundo trace). O Job On
    /// retroativo cria-se pelo fluxo existente do JobOn (as datas do
    /// passado gravam como vêm) — esta página não cria produções.
    /// Recusas tipadas com a causa em PT; nada bloqueia além da recusa.
    /// </summary>
    public async Task<IActionResult> OnPostAssociateJobOnAsync(
        [FromForm(Name = "trace_id")] int traceId,
        [FromForm(Name = "job_number")] string? jobNumber,
        [FromForm(Name = "job_reference")] string? jobReference,
        [FromForm(Name = "job_on_id")] int? jobOnId = null)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        // 1. O registo tem de existir (o POST transporta o trace_id
        //    canónico no hidden — nunca na folha visível).
        var trace = await _registoQuery.FindTraceAsync(traceId, cancellationToken: HttpContext.RequestAborted);
        if (trace is null)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                association_refusal =
                    AssociateBqRepairTraceToContextResult.TraceNotFound.ToString()
            });
        }

        // 2. Um trace já associado nunca é reassociado (uma produção
        //    posterior nunca toma posse de um trace anterior) — o handler
        //    recusa na mesma; a resolução antecipa a causa.
        if (trace.BqContextId is not null)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                association_refusal =
                    AssociateBqRepairTraceToContextResult.TraceAlreadyAssociated.ToString()
            });
        }

        // 3. O nº de produção (ex.: 202601) é o que encontra a produção; sem
        //    ele é recusa tipada informada, nunca um POST silencioso. A
        //    referência é opcional: só confirma (decisão do proprietário de
        //    16/10/2026 — encontrar pelo nº e avisar se a referência diferir).
        var number = jobNumber?.Trim();
        var reference = jobReference?.Trim();
        if (jobOnId is null && string.IsNullOrEmpty(number))
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                association_refusal =
                    AssociateBqRepairTraceToContextResult.MissingFacts.ToString()
            });
        }

        // 4. A produção resolve pelo nº de produção. O mesmo nº existe em
        //    várias referências (17/10/2026), por isso entre as produções
        //    com esse nº ganha a da referência indicada e, sem ela, a que
        //    tem a BQ deste registo. Nenhuma produção com esse
        //    nº → o Job On retroativo ainda não existe. Uma referência
        //    diferente não recusa: a folha avisa depois de associar.
        //    A produção escolhida na lista da referência (referência + nº,
        //    únicos juntos) ganha sempre, tenha ou não a BQ deste registo.
        //    A lista de produções da página manda o próprio Job On
        //    (job_on_id): é essa a produção associada, e não outra com o
        //    mesmo nº. Com o nº e o texto escrito, «2847» associava a
        //    4526Y171 · 2026999 em vez da 2847Y171 · 2026999 (deep sweep 2,
        //    F-1, 18/10/2026).
        DMO.Alpha.Core.Production.JobOn? jobOn;
        if (jobOnId is { } chosenId)
        {
            jobOn = await _db.JobOns
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == chosenId, HttpContext.RequestAborted);
            reference = null;
        }
        else
        {
            var candidates = await _db.JobOns
                .AsNoTracking()
                .Where(j => j.ProductionNumber == number)
                .OrderByDescending(j => j.Id)
                .Select(j => new
                {
                    JobOn = j,
                    HasBq = _db.BqContexts.Any(c => c.JobOnId == j.Id && c.ToolId == trace.ToolId)
                })
                .ToListAsync(HttpContext.RequestAborted);
            jobOn = candidates
                .OrderByDescending(c => !string.IsNullOrEmpty(reference)
                    && string.Equals(reference, c.JobOn.Reference, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.HasBq)
                .Select(c => c.JobOn)
                .FirstOrDefault();
        }
        if (jobOn is null)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                association_refusal =
                    AssociateBqRepairTraceToContextResult.ProductionNotFound.ToString()
            });
        }

        // 5. A BQ do registo tem de ser a BQ deste Job On: o contexto BQ
        //    da produção que partilha o tool_id canónico do trace (a
        //    correlação do handler, AssociateBqRepairTraceToContextHandler
        //    L68-76 — a página apenas a antecipa com a causa em PT).
        var bqContext = await _db.BqContexts
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOn.Id && c.ToolId == trace.ToolId)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (bqContext is null)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                association_refusal =
                    AssociateBqRepairTraceToContextResult.BqNotInProduction.ToString()
            });
        }

        // 6. O HANDLER EXISTENTE faz a associação: preserva o
        //    bq_repair_trace_id e todos os movimentos (nunca recria nem
        //    copia o trace), limpa a âncora direta e respeita
        //    um-trace-por-bq_id com recusas tipadas próprias.
        var outcome = await _associate.HandleAsync(
            new AssociateBqRepairTraceToContext(traceId, bqContext.Id),
            HttpContext.RequestAborted);

        if (outcome.Result == AssociateBqRepairTraceToContextResult.Success)
        {
            var referenceDiffers = !string.IsNullOrEmpty(reference)
                && !string.Equals(reference, jobOn.Reference, StringComparison.OrdinalIgnoreCase);
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                referencia_indicada = referenceDiffers ? reference : null
            });
        }

        return RedirectToPage("/Boquilhas/Index", new
        {
            trace_id = traceId,
            association_refusal = outcome.Result.ToString()
        });
    }

    private static DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation ComporCalendar(
        int year,
        int month,
        IReadOnlyList<BqHistoryDayCount> counts,
        DateOnly? selected,
        IReadOnlyDictionary<string, string> preservedFilters)
    {
        // O CLIQUE NUM DIA PRESERVA OS FILTROS (o html: «clica num dia
        // para filtrar» — os filtros ativos continuam a aplicar ao dia).
        // O calendário envia mês e ano; manter apenas o contexto operacional.
        var preserved = new Dictionary<string, string>
        {
            ["historico"] = "1"
        };
        foreach (var (key, value) in preservedFilters)
        {
            if (key is not ("ano" or "mes" or "dia"))
            {
                preserved[key] = value;
            }
        }
        return DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation.ForMonth(
            year, month,
            data => counts.FirstOrDefault(c => c.Day == data)?.MovementCount ?? 0,
            selected, "/Boquilhas/Index", preserved);
    }
/// <summary>
/// Um registo de uma BQ no painel de escolha (o registerContext do
/// dmo-html): os FATOS HUMANOS do registo — o nº e a referência da
/// produção quando associado, a máquina, o estado pela janela do Job On
/// («Sem Job On» para o registo avulso) e o número de movimentos. O
/// bq_repair_trace_id vive só no URL/hidden — nunca no texto.
/// </summary>
public sealed record BqRegisterCandidate(
    int BqRepairTraceId,
    string? ProductionNumber,
    string? ProductionReference,
    string? MachineCode,
    string State,
    int MovementCount);

/// <summary>
/// A ficha da ferramenta (o toolsTab do dmo-html — a ficha única por
/// BQ/lote, reutilizável em vários Job Ons): os fatos da Tool (a
/// referência, o lote, a quantidade contabilística, as máquinas
/// compatíveis) e os registos dela. O tool_id vive só no URL/hidden.
/// </summary>
public sealed record BqToolSheet(
    string ToolId,
    string Reference,
    string? Lot,
    int? AccountedQuantity,
    IReadOnlyList<string> Machines,
    PagedList<BqRegisterCandidate> Registers);}

/// <summary>Uma BQ na janela «Registar sem Job On».</summary>
public sealed record SemJobOnTool(string ToolId, string Reference, string Lot);

/// <summary>Um movimento sem Job On, na ficha da BQ da página Boquilhas.</summary>
public sealed record SemJobOnMovement(DateTime OccurredAtLocal, BqMovementType Type, int Quantity, string? MachineCode);
