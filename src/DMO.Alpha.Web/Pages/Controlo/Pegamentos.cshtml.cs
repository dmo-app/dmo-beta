using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Surface nativo da folha de Pegamentos (Controlo Create, corte
/// production-scoped): a medição dimensional da combinação CM+BQ+MF do
/// contexto Job On ativo, ancorada ao controlo_id (?controlo_id=N — a LEI
/// da app: «a partir do jobon nasce o controlo_id e com controlo_id ganhas
/// o contexto todo para pegamentos e peso»). O contexto é resolvido pela
/// query canónica composta no momento da leitura (query purpose-specific
/// IPegamentosQuery): o operador NUNCA seleciona ferramentas na folha
/// (spec: «It does not ask the user to reselect CM, BQ or MF») e um
/// componente ausente do contexto (o MF do J10 real) simplesmente não tem
/// card. A medição é o gesto em cruz (costura 0° + contra costura 90°
/// quando a geometria real a permite) registada por POST como facto
/// histórico append-only; todos os cálculos (ovalização com sinal
/// preservado, média, corredor nominal da tool ± tolerância congelada no
/// registo) são do backend. O gargalo em falta na tool é completado no
/// fluxo e grava no tool_id owner (regra Ferramentas), nunca no registo de
/// Pegamentos. Alertas do corredor são informativos: a app é de registo,
/// nunca bloqueia. O mapa de limites (o «gráfico todo» do mockup, autoridade
/// visual) é projetado no servidor como apresentação do modelo dimensional
/// — não é a autoridade de validação (manual §7 L356-360). Mutação nativa
/// exige sessão autenticada ([Authorize]: o anónimo é desafiado ANTES de
/// qualquer leitura/escrita); recusas tipadas voltam por query (PRG) com o
/// vocabulário fechado dos enums, nunca texto livre.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PegamentosModel : PageModel
{
    private readonly IRecordPegamentosMeasurementHandler _recordMeasurement;
    private readonly IPegamentosQuery _pegamentosQuery;
    private readonly ShellStateFactory _shellState;
    private readonly DMO.Alpha.Core.Tools.IToolLookupQuery _toolLookup;
    private readonly DMO.Alpha.Infrastructure.Controlo.PegamentosPdfGenerator _pdfGenerator;
    private readonly DMO.Alpha.Core.Controlo.IModuleSettingsQuery _moduleSettings;
    private readonly DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler _approvalHandler;
    private readonly ICreateToolScopedPegamentosHandler _createToolScoped;
    private readonly IAssociatePegamentosToControloHandler _associate;
    private readonly IEnsureControloForJobOnHandler _ensureControlo;
    private readonly DMO.Alpha.Core.Production.IJobOnListQuery _jobOnList;
    private readonly DMO.Alpha.Infrastructure.Data.DmoDbContext _db;

    private readonly DMO.Alpha.Infrastructure.Production.JobOnChangeQuery _changes;

    public PegamentosModel(
        IRecordPegamentosMeasurementHandler recordMeasurement,
        IPegamentosQuery pegamentosQuery,
        ShellStateFactory shellState,
        DMO.Alpha.Core.Tools.IToolLookupQuery toolLookup,
        DMO.Alpha.Infrastructure.Controlo.PegamentosPdfGenerator pdfGenerator,
        DMO.Alpha.Core.Controlo.IModuleSettingsQuery moduleSettings,
        DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler approvalHandler,
        DMO.Alpha.Infrastructure.Production.JobOnChangeQuery changes,
        ICreateToolScopedPegamentosHandler createToolScoped,
        IAssociatePegamentosToControloHandler associate,
        IEnsureControloForJobOnHandler ensureControlo,
        DMO.Alpha.Core.Production.IJobOnListQuery jobOnList,
        DMO.Alpha.Infrastructure.Data.DmoDbContext db)
    {
        _createToolScoped = createToolScoped;
        _associate = associate;
        _ensureControlo = ensureControlo;
        _jobOnList = jobOnList;
        _db = db;
        _recordMeasurement = recordMeasurement;
        _changes = changes;
        _pegamentosQuery = pegamentosQuery;
        _shellState = shellState;
        _toolLookup = toolLookup;
        _pdfGenerator = pdfGenerator;
        _moduleSettings = moduleSettings;
        _approvalHandler = approvalHandler;
    }

    /// <summary>
    /// OS CARDS DOS PEGAMENTOS (o Owner 2026-10-13: «pegamentos podem ter
    /// os cards com o que é relevante para os pegamentos: cm bq mf e os
    /// lotes»): as REFERÊNCIAS e os LOTES dos componentes medidos — nunca
    /// os tool_id (os fatos humanos).
    /// </summary>
    public DMO.Alpha.Core.Tools.ToolLookupResult? CmTool { get; private set; }
    public DMO.Alpha.Core.Tools.ToolLookupResult? BqTool { get; private set; }
    public DMO.Alpha.Core.Tools.ToolLookupResult? MfTool { get; private set; }

    /// <summary>O valor de ?modo que abre a escolha do Job On nos Pegamentos novos.</summary>
    public const string ModoJobOnValor = "jobon";

    /// <summary>True nos Pegamentos novos associados a um Job On (?modo=jobon).</summary>
    public bool ModoJobOn { get; private set; }

    /// <summary>pegamentos_id pedido (?pegamentos_id): os Pegamentos sem Job On abrem por ele.</summary>
    public int? RequestedPegamentosId { get; private set; }

    /// <summary>As referências escritas na escolha das ferramentas sem Job On.</summary>
    public string? RequestedCmReference { get; private set; }
    public string? RequestedBqReference { get; private set; }
    public string? RequestedMfReference { get; private set; }

    /// <summary>True quando uma referência escrita não resolve no Ferramentas para o seu tipo.</summary>
    public bool CmToolRequestedNotFound { get; private set; }
    public bool BqToolRequestedNotFound { get; private set; }
    public bool MfToolRequestedNotFound { get; private set; }

    /// <summary>A referência e a máquina procuradas na escolha do Job On (primeiro passo).</summary>
    public string? JobOnReferencia { get; private set; }
    public string? JobOnMaquina { get; private set; }

    /// <summary>As máquinas do filtro da escolha do Job On.</summary>
    public IReadOnlyList<string> Maquinas { get; private set; } = Array.Empty<string>();

    /// <summary>As produções da referência procurada (segundo passo).</summary>
    public IReadOnlyList<DMO.Alpha.Core.Production.ProductionContextReadModel> Producoes { get; private set; }
        = Array.Empty<DMO.Alpha.Core.Production.ProductionContextReadModel>();

    /// <summary>True quando o «Associar Job On» da folha sem Job On está aberto.</summary>
    public bool AssociarAberto { get; private set; }

    /// <summary>True depois de associar os Pegamentos a um Job On (PRG).</summary>
    public bool Associado { get; private set; }

    /// <summary>A mensagem da recusa (criar, escolher ou associar), apenas apresentação.</summary>
    public string? EscolhaRefusalMessage { get; private set; }

    /// <summary>A folha do controlo aberto (?controlo_id); null fora desse contexto.</summary>
    public PegamentosSheetReadModel? Sheet { get; private set; }

    /// <summary>O aviso quando o Job On mudou depois do registo dos Pegamentos; null sem alterações.</summary>
    public JobOnChangeNoticeView? JobOnNotice { get; private set; }

    /// <summary>controlo_id submetido (estado de apresentação preservado).</summary>
    public int? RequestedControloId { get; private set; }

    /// <summary>True quando foi pedido um controlo_id que não existe: aviso limpo, nada inventado.</summary>
    public bool ControloRequestedNotFound { get; private set; }

    /// <summary>O resumo por baixo do mapa de limites (null sem folha).</summary>
    public PegamentosMapSummary? Map { get; private set; }

    /// <summary>Recusa tipada do último POST de medição, transportada por query (PRG).</summary>
    public RecordPegamentosMeasurementResult? MeasurementRefusal { get; private set; }

    [BindProperty(SupportsGet = true, Name = "draft_component")]
    public string? DraftComponent { get; set; }

    [BindProperty(SupportsGet = true, Name = "draft_costura")]
    public string? DraftCostura { get; set; }

    [BindProperty(SupportsGet = true, Name = "draft_contra")]
    public string? DraftContra { get; set; }

    [BindProperty(SupportsGet = true, Name = "draft_gargalo")]
    public string? DraftGargalo { get; set; }

    /// <summary>A leitura aberta para corrigir ou apagar (Diogo, 17/10/2026).</summary>
    [BindProperty(SupportsGet = true, Name = "editar_medicao")]
    public int? EditarMedicaoId { get; set; }

    /// <summary>O valor carregado para corrigir: «costura» ou «contra» (recebe o foco).</summary>
    [BindProperty(SupportsGet = true, Name = "campo")]
    public string? EditarCampo { get; set; }

    [BindProperty(SupportsGet = true, Name = "correcao")]
    public string? Correcao { get; set; }

    public string? CorrecaoMessage => Correcao switch
    {
        "nao-encontrada" => "Essa leitura já não existe nesta folha. Nada foi alterado.",
        "valor-invalido" => "Indique a costura com um valor numérico (aceita vírgula ou ponto decimal). Nada foi alterado.",
        _ => null
    };

    public string? DraftValue(string component, string? value) =>
        (MeasurementRefusal is RecordPegamentosMeasurementResult.MissingGargalo
            or RecordPegamentosMeasurementResult.InvalidMeasurementValue) && DraftComponent == component
            ? value : null;

    public string DraftInputType(string component, string? value) =>
        DraftValue(component, value) is { Length: > 0 } draft && MeasurementDecimal.Parse(draft) is null
            ? "text" : "number";

    /// <summary>
    /// Avisos da última medição GRAVADA, transportados por query (PRG) —
    /// a regra do Owner 16/10/2026: «tentar sempre warning, desde que não
    /// parta nada». A medição ficou registada; o aviso explica o que ficou
    /// de fora.
    /// </summary>
    public PegamentosMeasurementNotice MeasurementNotice { get; private set; }

    /// <summary>O componente da medição gravada com aviso (CM/BQ/MF).</summary>
    public string? NoticeComponent { get; private set; }

    /// <summary>Comprimento máximo do valor ignorado ecoado no aviso (a query não cresce sem limite).</summary>
    private const int MaxEchoedLength = 40;

    /// <summary>O gargalo escrito mas ilegível que foi ignorado (eco para o aviso).</summary>
    public string? IgnoredGargalo { get; private set; }

    /// <summary>As mensagens dos avisos da última medição gravada, apenas apresentação.</summary>
    public IReadOnlyList<string> MeasurementNoticeMessages
    {
        get
        {
            var label = NoticeComponent is "CM" or "BQ" or "MF" ? NoticeComponent : "do componente";
            var messages = new List<string>();
            var notEvaluable = MeasurementNotice.HasFlag(PegamentosMeasurementNotice.NotEvaluable);
            if (MeasurementNotice.HasFlag(PegamentosMeasurementNotice.GargaloIgnored))
            {
                messages.Add($"“{IgnoredGargalo}” não é um diâmetro legível e foi ignorado; " +
                    (notEvaluable
                        ? $"a medição ficou «não avaliável» porque a ferramenta {label} não tem gargalo. " +
                          "Preencha o gargalo na ferramenta (no campo «Gargalo» da próxima medição ou em Ferramentas), " +
                          "com vírgula ou ponto decimal, por exemplo 70,00 ou 70.00."
                        : $"a medição foi avaliada com o gargalo já registado na ferramenta {label}."));
            }
            else if (notEvaluable)
            {
                messages.Add($"Medição registada como «não avaliável»: a ferramenta {label} não tem gargalo. " +
                    "As medições ficam «não avaliáveis» até o gargalo ser preenchido na ferramenta " +
                    "(no campo «Gargalo» da próxima medição ou em Ferramentas).");
            }
            if (MeasurementNotice.HasFlag(PegamentosMeasurementNotice.ContraCosturaIgnored))
            {
                messages.Add("Medição registada sem contra costura: o valor escrito não é um número legível " +
                    "(aceita vírgula ou ponto decimal, por exemplo 69,90 ou 69.90). A costura foi registada.");
            }
            return messages;
        }
    }

    /// <summary>Recusa tipada da geração do PDF, transportada por query (PRG).</summary>
    public string? PdfRefusal { get; private set; }

    public string PdfRefusalMessage => PdfRefusal switch
    {
        "sem-controlo" => "Abra uma folha de Pegamentos (de uma produção ou sem Job On) para gerar o PDF.",
        _ => string.Empty
    };

    /// <summary>Mensagem da recusa de medição, apenas apresentação.</summary>
    public string MeasurementRefusalMessage => MeasurementRefusal switch
    {
        RecordPegamentosMeasurementResult.ControloNotFound =>
            "O controlo indicado não existe. Nenhuma medição foi registada.",
        RecordPegamentosMeasurementResult.UnknownComponent =>
            "Componente desconhecido. Use CM, BQ ou MF. Nenhuma medição foi registada.",
        RecordPegamentosMeasurementResult.ComponentNotInProduction =>
            "O componente indicado não existe no contexto desta produção. " +
            "Nenhuma medição foi registada e nenhuma ferramenta foi inventada.",
        RecordPegamentosMeasurementResult.MissingGargalo =>
            "Indique o nominal do gargalo da ferramenta (aceita vírgula ou ponto decimal, por exemplo 70,00 ou 70.00): " +
            "sem nominal não se registam Pegamentos. " +
            "As leituras introduzidas foram mantidas; nenhuma medição foi registada.",
        RecordPegamentosMeasurementResult.PegamentosNotFound =>
            "Os Pegamentos indicados não existem. Nenhuma medição foi registada.",
        RecordPegamentosMeasurementResult.InvalidMeasurementValue =>
            "Indique a costura com um valor numérico (aceita vírgula ou ponto decimal, por exemplo 70,10 ou 70.10). " +
            "A costura é obrigatória; a contra costura pode ficar vazia quando não foi medida. " +
            "Os valores introduzidos foram mantidos; nenhuma medição foi registada.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "measurement_refusal")] string? measurementRefusal,
        [FromQuery(Name = "pdf_refusal")] string? pdfRefusal,
        [FromQuery(Name = "measurement_notice")] string? measurementNotice = null,
        [FromQuery(Name = "notice_component")] string? noticeComponent = null,
        [FromQuery(Name = "ignored_gargalo")] string? ignoredGargalo = null,
        [FromQuery(Name = "pegamentos_id")] int? pegamentosId = null,
        [FromQuery(Name = "modo")] string? modo = null,
        [FromQuery(Name = "cm_ref")] string? cmRef = null,
        [FromQuery(Name = "bq_ref")] string? bqRef = null,
        [FromQuery(Name = "mf_ref")] string? mfRef = null,
        [FromQuery(Name = "referencia")] string? referencia = null,
        [FromQuery(Name = "maquina")] string? maquina = null,
        [FromQuery(Name = "associar")] string? associar = null,
        [FromQuery(Name = "associado")] string? associado = null,
        [FromQuery(Name = "recusa")] string? recusa = null)
    {
        PdfRefusal = pdfRefusal;
        ViewData["Title"] = "Pegamentos — Controlo";

        // O CORTE H: a barra do módulo é SEMPRE completa (o
        // ShellStateFactory — o mapeamento num só sítio; nada de
        // capability aqui).
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = "pegamentos",
            Functions = ShellStateFactory.ControloFunctions("pegamentos", controloId: controloId)
        };

        // Recusa tipada do POST anterior, transportada por query (PRG) com
        // o vocabulário fechado dos enums.
        if (measurementRefusal is not null
            && Enum.TryParse(measurementRefusal, out RecordPegamentosMeasurementResult parsed)
            && parsed != RecordPegamentosMeasurementResult.Success)
        {
            MeasurementRefusal = parsed;
        }

        // Avisos da medição gravada (vocabulário fechado: só os bits
        // conhecidos; um valor estranho na query não mostra nada).
        if (measurementNotice is not null
            && Enum.TryParse(measurementNotice, out PegamentosMeasurementNotice notice))
        {
            MeasurementNotice = notice & PegamentosMeasurementNoticeValues.Known;
            NoticeComponent = noticeComponent;
            IgnoredGargalo = ignoredGargalo;
        }

        RequestedControloId = controloId;
        RequestedPegamentosId = controloId is null ? pegamentosId : null;
        Associado = associado == "1";
        EscolhaRefusalMessage = RefusalMessage(recusa);

        if (controloId is not null || RequestedPegamentosId is not null)
        {
            // A folha inteira por controlo_id: contexto canónico da produção,
            // gargalos correntes das tools, tolerância efetiva corrente e o
            // registo durável quando existe (a ausência é o estado válido
            // antes da primeira medição — a folha mostra-se pronta a medir).
            // Pelo pegamentos_id: os Pegamentos sem Job On, com as
            // ferramentas guardadas no registo.
            Sheet = controloId is int canonicalControloId
                ? await _pegamentosQuery.FindSheetByControloAsync(canonicalControloId, HttpContext.RequestAborted)
                : await _pegamentosQuery.FindSheetByPegamentosAsync(RequestedPegamentosId!.Value, HttpContext.RequestAborted);

            ControloRequestedNotFound = Sheet is null;

            // O «Associar Job On» dos Pegamentos sem Job On: a mesma escolha
            // em dois passos (referência e máquina, depois a produção).
            if (Sheet is { SemJobOn: true })
            {
                AssociarAberto = associar == "1" || EscolhaRefusalMessage is not null;
                if (AssociarAberto)
                {
                    await LoadJobOnChoiceAsync(referencia, maquina);
                }
            }

            // AS REFERÊNCIAS E OS LOTES dos componentes (os cards da folha —
            // os fatos humanos, nunca os tool_id).
            if (Sheet is not null)
            {
                if (Sheet.CmToolId is { } cmId)
                {
                    CmTool = await _toolLookup.FindByToolIdAsync(cmId, HttpContext.RequestAborted);
                }
                if (Sheet.BqToolId is { } bqId)
                {
                    BqTool = await _toolLookup.FindByToolIdAsync(bqId, HttpContext.RequestAborted);
                }
                if (Sheet.MfToolId is { } mfId)
                {
                    MfTool = await _toolLookup.FindByToolIdAsync(mfId, HttpContext.RequestAborted);
                }
            }

            if (Sheet is not null)
            {
                Map = PegamentosMapSummary.From(Sheet);
                // O aviso de mudança do Job On só existe com produção.
                if (Sheet.ControloId is { } sheetControloId)
                {
                    JobOnNotice = JobOnChangeNoticeView.From(
                        "O Job On mudou depois deste registo de Pegamentos.",
                        await _changes.ForPegamentosAsync(sheetControloId, HttpContext.RequestAborted));
                }
            }
        }

        else
        {
            // Pegamentos novos: sem Job On (as ferramentas à mão) ou
            // associados a um Job On (a produção abre pelo controlo_id).
            ModoJobOn = modo == ModoJobOnValor;
            if (ModoJobOn)
            {
                await LoadJobOnChoiceAsync(referencia, maquina);
            }
            else
            {
                RequestedCmReference = Sanitize(cmRef);
                RequestedBqReference = Sanitize(bqRef);
                RequestedMfReference = Sanitize(mfRef);
                (CmTool, CmToolRequestedNotFound) = await ResolveToolAsync(RequestedCmReference, DMO.Alpha.Core.Tools.ToolType.Cm);
                (BqTool, BqToolRequestedNotFound) = await ResolveToolAsync(RequestedBqReference, DMO.Alpha.Core.Tools.ToolType.Bq);
                (MfTool, MfToolRequestedNotFound) = await ResolveToolAsync(RequestedMfReference, DMO.Alpha.Core.Tools.ToolType.Mf);
            }
        }

        // Pegamentos conhece jobon_id pelo read model: preservar a produção nas tabs.
        if (ViewData["Shell"] is ShellPresentation shell)
        {
            ViewData["Shell"] = shell with
            {
                Functions = ShellStateFactory.ControloFunctions(
                    "pegamentos", Sheet?.JobOnId, Sheet?.ControloId ?? controloId)
            };
        }

        return Page();
    }

    /// <summary>True quando a CM, a BQ e o MF estão escolhidos e nenhuma referência escrita ficou por resolver.</summary>
    public bool FerramentasProntas =>
        CmTool is not null && BqTool is not null && MfTool is not null
        && !CmToolRequestedNotFound && !BqToolRequestedNotFound && !MfToolRequestedNotFound;

    private async Task<(DMO.Alpha.Core.Tools.ToolLookupResult? Tool, bool NotFound)> ResolveToolAsync(
        string? requested, DMO.Alpha.Core.Tools.ToolType type)
    {
        if (requested is null)
        {
            return (null, false);
        }

        var tool = await _toolLookup.FindByReferenceAsync(requested, type, HttpContext.RequestAborted);
        if (tool is null && await _toolLookup.FindByToolIdAsync(requested, HttpContext.RequestAborted) is { } byId && byId.Type == type)
        {
            tool = byId;
        }

        return (tool, tool is null);
    }

    private async Task LoadJobOnChoiceAsync(string? referencia, string? maquina)
    {
        JobOnReferencia = Sanitize(referencia);
        JobOnMaquina = Sanitize(maquina);
        Maquinas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _db.Machines.OrderBy(m => m.Code).Select(m => m.Code), HttpContext.RequestAborted);
        if (JobOnReferencia is { } referenciaProcurada)
        {
            Producoes = await _jobOnList.ListByReferenceAsync(referenciaProcurada, JobOnMaquina, HttpContext.RequestAborted);
        }
    }

    private static string? RefusalMessage(string? recusa) => recusa switch
    {
        "sem-ferramentas" => "Escolha a CM, a BQ e o MF: os Pegamentos medem sempre os três. Nada foi criado.",
        nameof(CreateToolScopedPegamentosResult.CmToolNotFound) => "A CM indicada não está registada no Ferramentas. Nada foi criado.",
        nameof(CreateToolScopedPegamentosResult.BqToolNotFound) => "A boquilha (BQ) indicada não está registada no Ferramentas. Nada foi criado.",
        nameof(CreateToolScopedPegamentosResult.MfToolNotFound) => "O molde final (MF) indicado não está registado no Ferramentas. Nada foi criado.",
        "sem-escolha" => "Escolha um Job On. Nada foi associado.",
        "inexistente" => "O Job On escolhido não existe. Nada foi associado.",
        nameof(AssociatePegamentosToControloResult.PegamentosNotFound) => "Os Pegamentos indicados não existem. Nada foi associado.",
        nameof(AssociatePegamentosToControloResult.PegamentosAlreadyAssociated) => "Estes Pegamentos já pertencem a outra produção. Nada foi alterado.",
        nameof(AssociatePegamentosToControloResult.ControloAlreadyHasPegamentos) => "Esta produção já tem os seus Pegamentos. Nada foi alterado.",
        "ToolMismatch-Cm" => "A CM destes Pegamentos não é a CM do Job On. Nada foi associado.",
        "ToolMismatch-Bq" => "A boquilha (BQ) destes Pegamentos não é a do Job On. Nada foi associado.",
        "ToolMismatch-Mf" => "O molde final (MF) destes Pegamentos não é o do Job On. Nada foi associado.",
        "ToolMismatch" => "As ferramentas destes Pegamentos não são as do Job On. Nada foi associado.",
        _ => null
    };

    /// <summary>
    /// POST: criar uns Pegamentos sem Job On com as ferramentas escolhidas
    /// (tool_id resolvidos pela referência na folha). Sucesso abre a folha
    /// pelo pegamentos_id; a recusa volta à escolha com as referências.
    /// </summary>
    public async Task<IActionResult> OnPostCreateToolScopedAsync(
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "bq_tool_id")] string? bqToolId,
        [FromForm(Name = "mf_tool_id")] string? mfToolId,
        [FromForm(Name = "cm_ref")] string? cmRef,
        [FromForm(Name = "bq_ref")] string? bqRef,
        [FromForm(Name = "mf_ref")] string? mfRef)
    {
        var outcome = await _createToolScoped.HandleAsync(
            new CreateToolScopedPegamentos(cmToolId, bqToolId, mfToolId), HttpContext.RequestAborted);
        if (outcome is { Result: CreateToolScopedPegamentosResult.Success, PegamentosId: int created })
        {
            return RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = created });
        }

        return RedirectToPage("/Controlo/Pegamentos", new
        {
            cm_ref = Sanitize(cmRef),
            bq_ref = Sanitize(bqRef),
            mf_ref = Sanitize(mfRef),
            recusa = outcome.Result == CreateToolScopedPegamentosResult.NoTools ? "sem-ferramentas" : outcome.Result.ToString()
        });
    }

    /// <summary>
    /// POST: os Pegamentos novos associados a um Job On — o jobon_id
    /// escolhido resolve o controlo_id (get-or-create idempotente) e abre a
    /// folha da produção. Nada é criado além do controlo.
    /// </summary>
    public async Task<IActionResult> OnPostAbrirJobOnAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId)
    {
        if (jobOnId is not int escolhido)
        {
            return RedirectToPage("/Controlo/Pegamentos", new { modo = ModoJobOnValor, recusa = "sem-escolha" });
        }

        var ensured = await _ensureControlo.HandleAsync(new EnsureControloForJobOn(escolhido), HttpContext.RequestAborted);
        return ensured.ControloId is int controlo
            ? RedirectToPage("/Controlo/Pegamentos", new { controlo_id = controlo })
            : RedirectToPage("/Controlo/Pegamentos", new { modo = ModoJobOnValor, recusa = "inexistente" });
    }

    /// <summary>
    /// POST: associar os Pegamentos sem Job On à produção escolhida. O
    /// controlo_id resolve-se pelo jobon_id; as ferramentas do registo têm
    /// de ser as do Job On. Sucesso abre a folha da produção.
    /// </summary>
    public async Task<IActionResult> OnPostAssociateAsync(
        [FromForm(Name = "pegamentos_id")] int pegamentosId,
        [FromForm(Name = "jobon_id")] int? jobOnId)
    {
        IActionResult Back(string recusa) =>
            RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = pegamentosId, associar = "1", recusa });

        if (jobOnId is not int escolhido)
        {
            return Back("sem-escolha");
        }

        var ensured = await _ensureControlo.HandleAsync(new EnsureControloForJobOn(escolhido), HttpContext.RequestAborted);
        if (ensured.ControloId is not int controlo)
        {
            return Back("inexistente");
        }

        var outcome = await _associate.HandleAsync(
            new AssociatePegamentosToControlo(controlo, pegamentosId), HttpContext.RequestAborted);
        if (outcome.Result == AssociatePegamentosToControloResult.Success)
        {
            return RedirectToPage("/Controlo/Pegamentos", new { controlo_id = controlo, associado = "1" });
        }

        return Back(outcome is { Result: AssociatePegamentosToControloResult.ToolMismatch, MismatchedComponents: [var only] }
            ? $"ToolMismatch-{only}"
            : outcome.Result.ToString());
    }

    /// <summary>
    /// POST nativo: registar uma medição real (o gesto em cruz) num
    /// componente da combinação herdada do contexto Job On ativo. O browser
    /// submete apenas os factos observados (costura obrigatória, contra
    /// costura opcional — n/a quando a geometria não permite) e, quando a
    /// tool do componente não tem gargalo, o valor completado no fluxo
    /// (gravado no tool_id owner). A posição da linha, o nominal congelado e
    /// todos os valores derivados são do backend. Sucesso volta à folha por
    /// redirect (PRG); a recusa é tipada e volta por query.
    /// </summary>
    public async Task<IActionResult> OnPostRegisterMeasurementAsync(
        [FromForm(Name = "controlo_id")] int? controloIdForm,
        [FromForm(Name = "component")] string? component,
        [FromForm(Name = "gargalo")] string? gargalo,
        [FromForm(Name = "pegamentos_id")] int? pegamentosIdForm = null)
    {
        // Várias linhas de uma vez («+ Linha», Diogo 17/10/2026): cada par
        // costura/contra costura é uma medição; as linhas vazias ignoram-se.
        // Uma costura ilegível recusa tudo antes de gravar a primeira.
        var costuras = Request.Form["costura"].ToList();
        var contraCosturas = Request.Form["contra_costura"].ToList();
        var linhas = Enumerable.Range(0, Math.Max(costuras.Count, contraCosturas.Count))
            .Select(i => (Costura: costuras.ElementAtOrDefault(i), Contra: contraCosturas.ElementAtOrDefault(i)))
            .Where(l => Sanitize(l.Costura) is not null || Sanitize(l.Contra) is not null)
            .ToList();
        if (linhas.Count == 0)
        {
            linhas.Add((null, null));
        }

        var costura = linhas[0].Costura;
        var contraCostura = linhas[0].Contra;
        var ilegivel = linhas.FindIndex(l => MeasurementDecimal.Parse(l.Costura) is null);
        if (ilegivel > 0)
        {
            costura = linhas[ilegivel].Costura;
            contraCostura = linhas[ilegivel].Contra;
        }

        // A folha volta por onde abriu: o controlo_id da produção ou, nos
        // Pegamentos sem Job On, o pegamentos_id.
        var controloId = controloIdForm ?? 0;
        var pegamentosId = controloIdForm is null ? pegamentosIdForm : null;
        RouteValueDictionary Anchor(object? extra = null)
        {
            var values = extra is null ? new RouteValueDictionary() : new RouteValueDictionary(extra);
            if (pegamentosId is int registo)
            {
                values["pegamentos_id"] = registo;
            }
            else
            {
                values["controlo_id"] = controloId;
            }
            return values;
        }

        // A costura é o facto base da leitura (NOT NULL): sem costura
        // legível não há medição. A contra costura é opcional: escrita mas
        // ilegível, a medição grava sem ela e o aviso di-lo (Owner
        // 16/10/2026: «tentar sempre warning, desde que não parta nada»).
        var componentToken = Sanitize(component) ?? string.Empty;
        if (MeasurementDecimal.Parse(costura) is not { } costuraMm)
        {
            return MeasurementRefused(Anchor(), componentToken, costura, contraCostura, gargalo,
                RecordPegamentosMeasurementResult.InvalidMeasurementValue);
        }

        var notice = PegamentosMeasurementNotice.None;
        var contraCosturaMm = ParseOptionalDecimal(contraCostura);
        if (Sanitize(contraCostura) is not null && contraCosturaMm is null)
        {
            notice |= PegamentosMeasurementNotice.ContraCosturaIgnored;
        }

        // Gargalo escrito mas ilegível: é ignorado (nunca chega à
        // ferramenta) e o aviso nomeia o valor. Gargalo vazio ou ignorado
        // numa ferramenta sem gargalo: a medição grava sem nominal («não
        // avaliável») e avisa.
        var gargaloMm = ParseOptionalDecimal(gargalo);
        var ignoredGargalo = Sanitize(gargalo) is { } writtenGargalo && gargaloMm is null
            ? writtenGargalo
            : null;
        if (ignoredGargalo is not null)
        {
            notice |= PegamentosMeasurementNotice.GargaloIgnored;
        }

        // O gargalo fornecido completa apenas a ferramenta que ainda não
        // o tem. O valor existente não volta a ser pedido nem substituído.
        var outcome = await _recordMeasurement.HandleAsync(
            new RecordPegamentosMeasurement(
                controloId,
                componentToken,
                costuraMm,
                contraCosturaMm,
                gargaloMm,
                pegamentosId),
            HttpContext.RequestAborted);

        if (outcome.Result == RecordPegamentosMeasurementResult.Success)
        {
            foreach (var linha in linhas.Skip(1))
            {
                var outraContra = ParseOptionalDecimal(linha.Contra);
                if (Sanitize(linha.Contra) is not null && outraContra is null)
                {
                    notice |= PegamentosMeasurementNotice.ContraCosturaIgnored;
                }

                var outra = await _recordMeasurement.HandleAsync(
                    new RecordPegamentosMeasurement(
                        controloId,
                        componentToken,
                        MeasurementDecimal.Parse(linha.Costura)!.Value,
                        outraContra,
                        null,
                        outcome.Measurement?.PegamentosId ?? pegamentosId),
                    HttpContext.RequestAborted);
                if (outra.Result != RecordPegamentosMeasurementResult.Success)
                {
                    return RedirectToPage("/Controlo/Pegamentos", Anchor(new
                    {
                        measurement_refusal = outra.Result.ToString()
                    }));
                }
            }

            if (outcome.Measurement is { NominalGargaloMm: null })
            {
                notice |= PegamentosMeasurementNotice.NotEvaluable;
            }

            return notice == PegamentosMeasurementNotice.None
                ? RedirectToPage("/Controlo/Pegamentos", Anchor())
                : RedirectToPage("/Controlo/Pegamentos", Anchor(new
                {
                    measurement_notice = notice.ToString(),
                    notice_component = componentToken,
                    ignored_gargalo = ignoredGargalo is { Length: > MaxEchoedLength }
                        ? ignoredGargalo[..MaxEchoedLength] + "…"
                        : ignoredGargalo
                }));
        }

        if (outcome.Result == RecordPegamentosMeasurementResult.MissingGargalo)
        {
            return MeasurementRefused(Anchor(), componentToken, costura, contraCostura, gargalo, outcome.Result);
        }

        return RedirectToPage("/Controlo/Pegamentos", Anchor(new
        {
            measurement_refusal = outcome.Result.ToString()
        }));
    }

    /// <summary>Corrige a costura e a contra costura de uma leitura desta folha.</summary>
    public async Task<IActionResult> OnPostEditMeasurementAsync(
        [FromServices] ICorrectPegamentosMeasurementHandler handler,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "pegamentos_id")] int? pegamentosId,
        [FromForm(Name = "measurement_id")] int measurementId,
        [FromForm(Name = "costura")] string? costura,
        [FromForm(Name = "contra_costura")] string? contraCostura)
    {
        var anchor = CorrectionAnchor(controloId, pegamentosId);
        if (MeasurementDecimal.Parse(costura) is not { } costuraMm)
        {
            anchor["editar_medicao"] = measurementId;
            anchor["correcao"] = "valor-invalido";
            return RedirectToPage("/Controlo/Pegamentos", anchor);
        }

        var result = await handler.EditAsync(
            new EditPegamentosMeasurement(
                new PegamentosMeasurementAnchor(measurementId, controloId, controloId is null ? pegamentosId : null),
                costuraMm,
                ParseOptionalDecimal(contraCostura)),
            HttpContext.RequestAborted);
        if (result != CorrectPegamentosMeasurementResult.Success)
        {
            anchor["correcao"] = "nao-encontrada";
        }

        return RedirectToPage("/Controlo/Pegamentos", anchor);
    }

    /// <summary>Apaga uma leitura desta folha; as restantes são renumeradas.</summary>
    public async Task<IActionResult> OnPostDeleteMeasurementAsync(
        [FromServices] ICorrectPegamentosMeasurementHandler handler,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "pegamentos_id")] int? pegamentosId,
        [FromForm(Name = "measurement_id")] int measurementId)
    {
        var anchor = CorrectionAnchor(controloId, pegamentosId);
        var result = await handler.DeleteAsync(
            new PegamentosMeasurementAnchor(measurementId, controloId, controloId is null ? pegamentosId : null),
            HttpContext.RequestAborted);
        if (result != CorrectPegamentosMeasurementResult.Success)
        {
            anchor["correcao"] = "nao-encontrada";
        }

        return RedirectToPage("/Controlo/Pegamentos", anchor);
    }

    private static RouteValueDictionary CorrectionAnchor(int? controloId, int? pegamentosId)
    {
        var values = new RouteValueDictionary();
        if (controloId is int controlo)
        {
            values["controlo_id"] = controlo;
        }
        else
        {
            values["pegamentos_id"] = pegamentosId;
        }
        return values;
    }

    private IActionResult MeasurementRefused(RouteValueDictionary anchor, string component, string? costura,
        string? contraCostura, string? gargalo, RecordPegamentosMeasurementResult result)
    {
        anchor["measurement_refusal"] = result.ToString();
        anchor["draft_component"] = component;
        anchor["draft_costura"] = CanonicalDraft(costura);
        anchor["draft_contra"] = CanonicalDraft(contraCostura);
        anchor["draft_gargalo"] = CanonicalDraft(gargalo);
        return RedirectToPage("/Controlo/Pegamentos", anchor);
    }

    private static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static decimal? ParseOptionalDecimal(string? value) =>
        MeasurementDecimal.Parse(value);

    private static string? CanonicalDraft(string? value) =>
        MeasurementDecimal.Parse(value)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? value;

    /// <summary>
    /// Descarrega a folha completa de Pegamentos com avaliações históricas do Core, mapa e tabelas por componente.
    /// Sem medições ainda, sai a folha em branco (ferramentas, nominal e tolerância, tabelas vazias
    /// para preencher à mão). Geração em memória: não grava PDFs no servidor nem altera dados persistidos.
    /// </summary>
    public async Task<IActionResult> OnPostGeneratePdfAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "pegamentos_id")] int? pegamentosId = null)
    {
        if (controloId is null && pegamentosId is null)
        {
            return RedirectToPage("/Controlo/Pegamentos", new { pdf_refusal = "sem-controlo" });
        }

        var sheet = controloId is int canonicalControloId
            ? await _pegamentosQuery.FindSheetByControloAsync(canonicalControloId, HttpContext.RequestAborted)
            : await _pegamentosQuery.FindSheetByPegamentosAsync(pegamentosId!.Value, HttpContext.RequestAborted);
        if (sheet is null)
        {
            // Controlo inexistente: a folha mostra o aviso limpo do controlo em falta.
            return controloId is not null
                ? RedirectToPage("/Controlo/Pegamentos", new { controlo_id = controloId })
                : RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = pegamentosId });
        }

        // O SELO DE APROVAÇÃO no papel (o corte D) e QUEM GERA O PAPEL
        // (o rodapé do alvo: «Diogo Oliveira 28/07/2026, 14:52:28»).
        // Sem Job On não há aprovação.
        var approval = sheet.ControloId is int approvalControloId
            ? await _approvalHandler.GetAsync(approvalControloId, HttpContext.RequestAborted)
            : null;
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value;

        var fileName = ControloDocumentPaths.GetPegamentosFileName(sheet.Reference, sheet.ProductionNumber);

        // Sem registo ainda: a folha em branco para preencher à mão.
        var bytes = _pdfGenerator.GenerateBytes(PegamentosPdfGenerator.FromSheet(sheet, approval, generatedBy)
            with { BlankForHandFilling = sheet.Pegamentos is null });
        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(ControloDocumentPaths.GetProductionRelativeFolder(sheet.Reference, sheet.ProductionNumber));
        return File(bytes, "application/pdf", fileName);
    }
}

/// <summary>
/// Avisos de uma medição de Pegamentos que FOI gravada (não são recusas):
/// o que ficou de fora e porquê.
/// </summary>
[Flags]
public enum PegamentosMeasurementNotice
{
    None = 0,

    /// <summary>A ferramenta não tem gargalo e nenhum foi indicado: a linha ficou sem nominal («não avaliável»).</summary>
    NotEvaluable = 1,

    /// <summary>A contra costura escrita era ilegível: a medição gravou-se só com a costura.</summary>
    ContraCosturaIgnored = 2,

    /// <summary>O gargalo escrito era ilegível: foi ignorado e não foi gravado na ferramenta.</summary>
    GargaloIgnored = 4
}

internal static class PegamentosMeasurementNoticeValues
{
    internal const PegamentosMeasurementNotice Known =
        PegamentosMeasurementNotice.NotEvaluable
        | PegamentosMeasurementNotice.ContraCosturaIgnored
        | PegamentosMeasurementNotice.GargaloIgnored;
}

/// <summary>
/// O estado por baixo do mapa de limites: o texto do resumo, se há alertas e
/// quantas medições existem. O desenho do mapa é feito por
/// <c>PegamentosLimitMap.Render</c>, o mesmo do PDF.
/// </summary>
public sealed record PegamentosMapSummary(string SummaryText, bool HasAlerts, int MeasurementCount)
{
    internal static PegamentosMapSummary From(PegamentosSheetReadModel sheet)
    {
        var measurements = sheet.Pegamentos?.Measurements ?? Array.Empty<PegamentosMeasurementReadModel>();
        var hasNominal = sheet.Components.Any(c => c.DiametroGargaloMm is not null);
        var hasAverage = sheet.Pegamentos?.ComponentAverages.Any(a => a.AverageMediaMm is not null) == true;
        if (measurements.Count == 0 && !hasNominal && !hasAverage)
        {
            return new(
                "Ainda não existem nominais de gargalo nem medições para representar. " +
                "Complete o gargalo de uma ferramenta ou registe a primeira medição.",
                false,
                0);
        }

        var alertCount = measurements.Count(m => m.Evaluation.HasAlert);
        var summary = measurements.Count == 0
            ? "Ainda não existem medições para representar."
            : alertCount > 0
                ? (alertCount == 1 ? "1 medição fora do corredor (atingiu ou ultrapassou um limite)." : $"{alertCount} medições fora do corredor (atingiram ou ultrapassaram um limite).")
                : measurements.Any(m => m.Evaluation.IsNotEvaluable)
                    ? "Existem medições não avaliáveis — nominal não registado."
                    : "Todas as medições permanecem dentro do corredor do respetivo componente.";
        return new(summary, alertCount > 0, measurements.Count);
    }
}
