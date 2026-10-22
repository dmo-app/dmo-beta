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

    /// <summary>
    /// Os lotes de uma referência escrita que tem mais de um (sweep de
    /// 19/10/2026, C-4): a escolha mostra-os para o operador escolher o que
    /// está na máquina. Vazio com um só lote, que segue direto.
    /// </summary>
    public IReadOnlyList<DMO.Alpha.Core.Tools.ToolLookupResult> CmLotes { get; private set; } = Array.Empty<DMO.Alpha.Core.Tools.ToolLookupResult>();
    public IReadOnlyList<DMO.Alpha.Core.Tools.ToolLookupResult> BqLotes { get; private set; } = Array.Empty<DMO.Alpha.Core.Tools.ToolLookupResult>();
    public IReadOnlyList<DMO.Alpha.Core.Tools.ToolLookupResult> MfLotes { get; private set; } = Array.Empty<DMO.Alpha.Core.Tools.ToolLookupResult>();

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

    /// <summary>O valor de ?folha depois de uma leitura que começou uma folha nova (a CM, a BQ ou o MF mudou).</summary>
    public const string FolhaNovaValor = "nova";

    /// <summary>O valor de ?folha depois de uma leitura que foi para outra folha que já existia (voltou-se a uma ferramenta).</summary>
    public const string FolhaOutraValor = "outra";

    /// <summary>
    /// A leitura acabada de registar foi para outra folha que não a que a
    /// página mostrava (18/10/2026): a página abre inteira, nessa
    /// folha, e diz porquê. Null sem mudança de folha.
    /// </summary>
    public string? FolhaMudou { get; private set; }

    /// <summary>A mensagem da mudança de folha, apenas apresentação.</summary>
    public string? FolhaMudouMessage => FolhaMudou switch
    {
        FolhaNovaValor => $"{ToolsChangedText()}: esta leitura começou uma folha nova. A folha anterior ficou como estava.",
        FolhaOutraValor => $"{ToolsChangedText()}: esta leitura foi para a folha destas ferramentas, que já existia. A outra folha ficou como estava.",
        _ => null
    };

    /// <summary>
    /// «A BQ mudou», «A CM e o MF mudaram»…: o que difere entre a folha
    /// mostrada e a folha anterior a ela; sem anterior, «As ferramentas mudaram».
    /// </summary>
    private string ToolsChangedText()
    {
        var shown = Sheet?.Pegamentos;
        var previous = shown is null ? null : Sheet!.Sheets.LastOrDefault(s => s.PegamentosId < shown.PegamentosId)
            ?? Sheet!.Sheets.FirstOrDefault(s => s.PegamentosId != shown.PegamentosId);
        if (shown is null || previous is null)
        {
            return "As ferramentas mudaram";
        }

        var changed = new[] { PegamentosComponent.Cm, PegamentosComponent.Bq, PegamentosComponent.Mf }
            .Where(c => shown.ToolIdOf(c) != previous.ToolIdOf(c))
            .Select(c => c == PegamentosComponent.Mf ? "o MF" : $"a {PegamentosVista.Token(c)}")
            .ToList();
        if (changed.Count == 0)
        {
            return "As ferramentas mudaram";
        }

        var list = changed.Count == 1 ? changed[0] : $"{string.Join(", ", changed.Take(changed.Count - 1))} e {changed[^1]}";
        return $"{char.ToUpperInvariant(list[0])}{list[1..]} {(changed.Count == 1 ? "mudou" : "mudaram")}";
    }

    /// <summary>
    /// True quando a produção tem outra folha além da mostrada (a CM, a BQ
    /// ou o MF mudou a meio): o cartão «Folhas desta produção» lista-as. Com
    /// uma só folha a página fica como sempre.
    /// </summary>
    public bool ShowSheets =>
        Sheet is { ControloId: not null } folhas
        && folhas.Sheets.Any(f => f.PegamentosId != folhas.Pegamentos?.PegamentosId);

    /// <summary>
    /// O endereço de uma folha da produção: a folha das ferramentas atuais
    /// abre pelo controlo_id, como sempre; uma folha de antes de uma troca,
    /// pelo controlo_id e o pegamentos_id dela. Sem Job On, pelo pegamentos_id.
    /// </summary>
    public static string SheetHref(PegamentosSheetReadModel sheet, PegamentosReadModel? folha) =>
        sheet.ControloId is not int controlo
            ? $"/Controlo/Pegamentos?pegamentos_id={folha?.PegamentosId}"
            : folha is { IsCurrent: false }
                ? $"/Controlo/Pegamentos?controlo_id={controlo}&pegamentos_id={folha.PegamentosId}"
                : $"/Controlo/Pegamentos?controlo_id={controlo}";

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

    [BindProperty(SupportsGet = true, Name = "draft_numero")]
    public string? DraftNumero { get; set; }

    /// <summary>A leitura aberta para corrigir ou apagar (Diogo, 17/10/2026).</summary>
    [BindProperty(SupportsGet = true, Name = "editar_medicao")]
    public int? EditarMedicaoId { get; set; }

    /// <summary>O valor carregado para corrigir: «costura», «contra» ou «numero» (recebe o foco).</summary>
    [BindProperty(SupportsGet = true, Name = "campo")]
    public string? EditarCampo { get; set; }

    [BindProperty(SupportsGet = true, Name = "correcao")]
    public string? Correcao { get; set; }

    /// <summary>
    /// As linhas do último registo cujo número tinha mais de 20 caracteres e
    /// foi cortado (lista «1,3», por query, PRG). «Só avisos»: o corte nunca
    /// é silencioso (revisão dos testes, 21/10/2026).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "numero_cortado")]
    public string? NumeroCortado { get; set; }

    /// <summary>As linhas do último registo que só tinham o número e não foram registadas.</summary>
    [BindProperty(SupportsGet = true, Name = "numero_sozinho")]
    public string? NumeroSozinho { get; set; }

    /// <summary>A leitura corrigida cujo número tinha mais de 20 caracteres e foi cortado.</summary>
    [BindProperty(SupportsGet = true, Name = "numero_cortado_leitura")]
    public int? NumeroCortadoLeitura { get; set; }

    /// <summary>O comprimento máximo do número da CM/BQ/MF (o resto é cortado, com aviso).</summary>
    public const int NumeroMaxLength = 20;

    private static bool NumeroLongo(string? numero) => Sanitize(numero) is { Length: > NumeroMaxLength };

    private static IEnumerable<int> Linhas(string? lista) =>
        (lista ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => int.TryParse(l, out var n) && n is > 0 and < 1000 ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .Take(50);

    /// <summary>Os avisos do número da CM/BQ/MF (cortado ou sozinho), apenas apresentação.</summary>
    public IReadOnlyList<string> NumeroAvisos
    {
        get
        {
            var avisos = new List<string>();
            foreach (var linha in Linhas(NumeroCortado))
            {
                avisos.Add($"O número da linha {linha} tinha mais de {NumeroMaxLength} caracteres e foi cortado.");
            }
            foreach (var linha in Linhas(NumeroSozinho))
            {
                avisos.Add($"A linha {linha} só tinha o número e não foi registada.");
            }
            if (NumeroCortadoLeitura is int leitura)
            {
                var medicao = Sheet?.Pegamentos?.Measurements.FirstOrDefault(m => m.MeasurementId == leitura);
                var posicao = medicao is null ? 0
                    : Sheet!.Pegamentos!.Measurements.Where(m => m.Component == medicao.Component)
                        .TakeWhile(m => m.MeasurementId != leitura).Count() + 1;
                avisos.Add(posicao > 0
                    ? $"O número da leitura {posicao} da {medicao!.Component.ToString().ToUpperInvariant()} tinha mais de {NumeroMaxLength} caracteres e foi cortado."
                    : $"O número tinha mais de {NumeroMaxLength} caracteres e foi cortado.");
            }
            return avisos;
        }
    }

    public string? CorrecaoMessage => Correcao switch
    {
        "nao-encontrada" => "Essa leitura já não existe nesta folha. Nada foi alterado.",
        "valor-invalido" => "Indique a costura com um valor numérico maior que 0 (aceita vírgula ou ponto decimal). Nada foi alterado.",
        "contra-invalida" => "Indique a contra costura com um valor numérico maior que 0 (aceita vírgula ou ponto decimal) ou deixe-a vazia para a retirar. Nada foi alterado.",
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
        RecordPegamentosMeasurementResult.PegamentosNotCurrent =>
            "Esta folha é de ferramentas que já não estão na produção. A leitura não foi gravada. " +
            "As leituras novas vão para a folha das ferramentas atuais.",
        RecordPegamentosMeasurementResult.InvalidMeasurementValue =>
            "Indique a costura com um valor numérico maior que 0 (aceita vírgula ou ponto decimal, por exemplo 70,10 ou 70.10). " +
            "A costura é obrigatória; a contra costura, quando escrita, também tem de ser maior que 0, e pode ficar vazia quando não foi medida. " +
            "Os valores introduzidos foram mantidos; nenhuma medição foi registada.",
        _ => string.Empty
    };

    /// <summary>A zona do contexto da folha (muda com a primeira medição).</summary>
    public const string ZonaContexto = "pegContexto";

    /// <summary>A zona das mensagens das medições.</summary>
    public const string ZonaAvisos = "pegAvisos";

    /// <summary>A zona do mapa de limites.</summary>
    public const string ZonaMapa = "pegMapa";

    /// <summary>A zona do cartão «Folhas desta produção» (as contagens mudam com cada leitura).</summary>
    public const string ZonaFolhas = "pegFolhas";

    /// <summary>A zona do cartão de um componente (CM, BQ, MF).</summary>
    public static string ZonaCartao(PegamentosComponent component) => $"pegCartao{PegamentosVista.Token(component)}";

    private static bool ZonaConhecida(string zona) =>
        zona is ZonaContexto or ZonaAvisos or ZonaMapa or ZonaFolhas
        || new[] { PegamentosComponent.Cm, PegamentosComponent.Bq, PegamentosComponent.Mf }.Any(c => ZonaCartao(c) == zona);

    /// <summary>As zonas pedidas pelo GET que segue uma gravação (E11); vazio na página inteira.</summary>
    public IReadOnlyList<string> ZonasPedidas { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// A tolerância efetiva exibida: a congelada no registo quando existe, a
    /// corrente (config ?? default) antes da primeira medição — o que a
    /// criação do registo congelará.
    /// </summary>
    public decimal ShownTolerance =>
        Sheet?.Pegamentos?.ToleranceMm ?? Sheet?.CurrentToleranceMm ?? PegamentosCalculation.DefaultToleranceMm;

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
        [FromQuery(Name = "recusa")] string? recusa = null,
        [FromQuery(Name = "folha")] string? folha = null,
        [FromQuery(Name = "cm_tool")] string? cmTool = null,
        [FromQuery(Name = "bq_tool")] string? bqTool = null,
        [FromQuery(Name = "mf_tool")] string? mfTool = null)
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
        FolhaMudou = folha is FolhaNovaValor or FolhaOutraValor ? folha : null;

        // Depois de registar, corrigir ou apagar uma leitura (E11, beta.189):
        // só as zonas pedidas. Sem o contexto, as ferramentas e as alterações
        // do Job On (que só ele mostra) não se leem.
        var zonas = DmoZonasPedidas.Ler(Request, ZonaConhecida);
        Associado = associado == "1";
        EscolhaRefusalMessage = RefusalMessage(recusa);

        if (controloId is not null || RequestedPegamentosId is not null)
        {
            // A folha inteira por controlo_id: contexto canónico da produção,
            // gargalos correntes das tools, tolerância efetiva corrente e o
            // registo durável quando existe (a ausência é o estado válido
            // antes da primeira medição — a folha mostra-se pronta a medir).
            // Pelo pegamentos_id: os Pegamentos sem Job On, com as
            // ferramentas guardadas no registo. Com os dois, essa folha da
            // produção, com as ferramentas dela (uma folha de antes de uma
            // troca da CM, da BQ ou do MF).
            Sheet = controloId is int canonicalControloId
                ? await _pegamentosQuery.FindSheetByControloAsync(canonicalControloId, pegamentosId, HttpContext.RequestAborted)
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

            if (Sheet is not null)
            {
                Map = PegamentosMapSummary.From(Sheet);
            }

            // Só as zonas, se a folha tem todas as pedidas; senão a página
            // inteira, com o contexto. Uma leitura que foi para outra folha
            // (a CM, a BQ ou o MF mudou) abre a página inteira: os cartões
            // todos são dessa folha.
            if (zonas is not null && Sheet is { } zonasSheet && FolhaMudou is null)
            {
                var cartoes = zonasSheet.Components.Select(c => ZonaCartao(c.Component)).ToHashSet(StringComparer.Ordinal);
                if (zonas.All(zona => zona is ZonaContexto or ZonaAvisos or ZonaMapa or ZonaFolhas || cartoes.Contains(zona)))
                {
                    if (zonas.Contains(ZonaContexto))
                    {
                        await LoadContextoAsync(Sheet);
                    }
                    ZonasPedidas = zonas;
                    DmoZonasPedidas.Marcar(Response, zonas);
                    return Partial("_PegamentosZonas", this);
                }
            }

            if (Sheet is not null)
            {
                await LoadContextoAsync(Sheet);
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
                (CmTool, CmToolRequestedNotFound, CmLotes) = await ResolveToolAsync(RequestedCmReference, Sanitize(cmTool), DMO.Alpha.Core.Tools.ToolType.Cm);
                (BqTool, BqToolRequestedNotFound, BqLotes) = await ResolveToolAsync(RequestedBqReference, Sanitize(bqTool), DMO.Alpha.Core.Tools.ToolType.Bq);
                (MfTool, MfToolRequestedNotFound, MfLotes) = await ResolveToolAsync(RequestedMfReference, Sanitize(mfTool), DMO.Alpha.Core.Tools.ToolType.Mf);
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

    /// <summary>
    /// O que só o contexto da folha mostra: AS REFERÊNCIAS E OS LOTES dos
    /// componentes (os fatos humanos, nunca os tool_id) e o aviso de mudança
    /// do Job On (só com produção).
    /// </summary>
    private async Task LoadContextoAsync(PegamentosSheetReadModel sheet)
    {
        if (sheet.CmToolId is { } cmId)
        {
            CmTool = await _toolLookup.FindByToolIdAsync(cmId, HttpContext.RequestAborted);
        }
        if (sheet.BqToolId is { } bqId)
        {
            BqTool = await _toolLookup.FindByToolIdAsync(bqId, HttpContext.RequestAborted);
        }
        if (sheet.MfToolId is { } mfId)
        {
            MfTool = await _toolLookup.FindByToolIdAsync(mfId, HttpContext.RequestAborted);
        }
        // O aviso é da folha mostrada: o que mudou depois de ELA ser gravada.
        if (sheet.ControloId is not null && sheet.Pegamentos is { } folha)
        {
            JobOnNotice = JobOnChangeNoticeView.From(
                "O Job On mudou depois deste registo de Pegamentos.",
                await _changes.ForPegamentosSheetAsync(folha.PegamentosId, HttpContext.RequestAborted));
        }
    }

    /// <summary>
    /// A referência e o lote de um componente no cartão da escolha; «escolha
    /// o lote» enquanto a referência tem vários e nenhum foi escolhido.
    /// </summary>
    public static string FerramentaEscolhida(
        DMO.Alpha.Core.Tools.ToolLookupResult? tool, IReadOnlyList<DMO.Alpha.Core.Tools.ToolLookupResult> lotes) =>
        tool is not null ? $"{tool.Reference} · Lote {tool.Lot}"
        : lotes.Count > 0 ? $"{lotes[0].Reference} · escolha o lote"
        : "—";

    /// <summary>True quando a CM, a BQ e o MF estão escolhidos e nenhuma referência escrita ficou por resolver.</summary>
    public bool FerramentasProntas =>
        CmTool is not null && BqTool is not null && MfTool is not null
        && !CmToolRequestedNotFound && !BqToolRequestedNotFound && !MfToolRequestedNotFound;

    /// <summary>
    /// A ferramenta de uma referência escrita. Com um só lote segue direto;
    /// com mais de um, fica por escolher até o operador escolher o lote que
    /// está na máquina, porque o lote faz parte da ferramenta e associar ao
    /// Job On exige a mesma (sweep de 19/10/2026, C-4). O lote escolhido só
    /// conta se for desta referência: o de outra, que ficou da escolha
    /// anterior, volta à escolha. Um tool_id escrito no lugar da referência
    /// continua a resolver.
    /// </summary>
    private async Task<(DMO.Alpha.Core.Tools.ToolLookupResult? Tool, bool NotFound, IReadOnlyList<DMO.Alpha.Core.Tools.ToolLookupResult> Lotes)> ResolveToolAsync(
        string? requested, string? chosenToolId, DMO.Alpha.Core.Tools.ToolType type)
    {
        var semLotes = Array.Empty<DMO.Alpha.Core.Tools.ToolLookupResult>();
        if (requested is null)
        {
            return (null, false, semLotes);
        }

        var lotes = await DMO.Alpha.Infrastructure.Tools.ToolLookupQuery.ListByReferenceAsync(_db, requested, type, HttpContext.RequestAborted);
        if (lotes.Count > 1)
        {
            return (lotes.FirstOrDefault(t => t.ToolId == chosenToolId), false, lotes);
        }

        var tool = lotes.Count == 1 ? lotes[0] : null;
        if (tool is null && await _toolLookup.FindByToolIdAsync(requested, HttpContext.RequestAborted) is { } byId && byId.Type == type)
        {
            tool = byId;
        }

        return (tool, tool is null, semLotes);
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

        // Os lotes escolhidos voltam com as referências.
        return RedirectToPage("/Controlo/Pegamentos", new
        {
            cm_ref = Sanitize(cmRef),
            bq_ref = Sanitize(bqRef),
            mf_ref = Sanitize(mfRef),
            cm_tool = Sanitize(cmToolId),
            bq_tool = Sanitize(bqToolId),
            mf_tool = Sanitize(mfToolId),
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
    // O mesmo envio outra vez (a ligação caiu depois de gravar) não grava
    // duas vezes (deep sweep, 18/10/2026, B-pegamentos-3).
    [EnvioUnico]
    public async Task<IActionResult> OnPostRegisterMeasurementAsync(
        [FromForm(Name = "controlo_id")] int? controloIdForm,
        [FromForm(Name = "component")] string? component,
        [FromForm(Name = "gargalo")] string? gargalo,
        [FromForm(Name = "pegamentos_id")] int? pegamentosIdForm = null,
        [FromForm(Name = "folha_vista")] int? folhaVista = null)
    {
        // Várias linhas de uma vez («+ Linha», Diogo 17/10/2026): cada par
        // costura/contra costura é uma medição; as linhas vazias ignoram-se.
        // Uma costura ilegível recusa tudo antes de gravar a primeira.
        // O número da CM/BQ/MF lido vai ao lado (opcional, nunca recusa);
        // uma linha só com o número conta como vazia (Diogo, 21/10/2026).
        var costuras = Request.Form["costura"].ToList();
        var contraCosturas = Request.Form["contra_costura"].ToList();
        var numeros = Request.Form["numero"].ToList();
        var enviadas = Enumerable.Range(0, Math.Max(costuras.Count, contraCosturas.Count))
            .Select(i => (Linha: i + 1, Costura: costuras.ElementAtOrDefault(i), Contra: contraCosturas.ElementAtOrDefault(i), Numero: numeros.ElementAtOrDefault(i)))
            .ToList();
        // Nada se perde em silêncio (revisão dos testes, 21/10/2026): a
        // linha só com o número não é registada e o número longo é cortado,
        // mas o aviso di-lo depois de gravar.
        var sozinhos = enviadas
            .Where(l => Sanitize(l.Costura) is null && Sanitize(l.Contra) is null && Sanitize(l.Numero) is not null)
            .Select(l => l.Linha).ToList();
        var linhasComValores = enviadas
            .Where(l => Sanitize(l.Costura) is not null || Sanitize(l.Contra) is not null)
            .ToList();
        var cortados = linhasComValores.Where(l => NumeroLongo(l.Numero)).Select(l => l.Linha).ToList();
        var linhas = linhasComValores.Select(l => (l.Costura, l.Contra, l.Numero)).ToList();
        if (linhas.Count == 0)
        {
            linhas.Add((null, null, null));
        }

        // Uma costura ou contra costura de 0 ou menos é fisicamente
        // impossível e recusa tudo, como a ilegível (Diogo, 18/10/2026).
        var costura = linhas[0].Costura;
        var contraCostura = linhas[0].Contra;
        var numero = linhas[0].Numero;
        var ilegivel = linhas.FindIndex(l => MeasurementDecimal.Parse(l.Costura) is not > 0m
            || ParseOptionalDecimal(l.Contra) is <= 0m);
        if (ilegivel > 0)
        {
            costura = linhas[ilegivel].Costura;
            contraCostura = linhas[ilegivel].Contra;
            numero = linhas[ilegivel].Numero;
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
            // A linha só com o número não é registada, nem quando o envio é
            // recusado: o aviso di-lo sempre.
            if (sozinhos.Count > 0)
            {
                values["numero_sozinho"] = string.Join(',', sozinhos);
            }
            return WithOpenCorrection(values);
        }

        // A costura é o facto base da leitura (NOT NULL): sem costura
        // legível não há medição. A contra costura é opcional: escrita mas
        // ilegível, a medição grava sem ela e o aviso di-lo (Owner
        // 16/10/2026: «tentar sempre warning, desde que não parta nada»).
        var componentToken = Sanitize(component) ?? string.Empty;
        if (MeasurementDecimal.Parse(costura) is not { } costuraMm || ilegivel >= 0)
        {
            return MeasurementRefused(Anchor(), componentToken, costura, contraCostura, gargalo,
                RecordPegamentosMeasurementResult.InvalidMeasurementValue, numero);
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

        // As outras linhas vão no mesmo comando e gravam-se na mesma escrita
        // que a primeira: um pedido cortado a meio (o operador sai da página,
        // a rede cai) não deixa só parte, e reenviar não duplica a 1.ª. O
        // pedido já chegou inteiro: grava até ao fim mesmo que o browser se
        // vá embora (o operador carregou em «Registar» e não perde as linhas).
        var outrasLinhas = new List<PegamentosRow>();
        foreach (var linha in linhas.Skip(1))
        {
            var outraContra = ParseOptionalDecimal(linha.Contra);
            if (Sanitize(linha.Contra) is not null && outraContra is null)
            {
                notice |= PegamentosMeasurementNotice.ContraCosturaIgnored;
            }

            outrasLinhas.Add(new PegamentosRow(MeasurementDecimal.Parse(linha.Costura)!.Value, outraContra, linha.Numero));
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
                pegamentosId,
                outrasLinhas,
                numero),
            CancellationToken.None);

        if (outcome.Result == RecordPegamentosMeasurementResult.Success)
        {
            if (outcome.Measurement is { NominalGargaloMm: null })
            {
                notice |= PegamentosMeasurementNotice.NotEvaluable;
            }

            var gravada = notice == PegamentosMeasurementNotice.None
                ? Anchor()
                : Anchor(new
                {
                    measurement_notice = notice.ToString(),
                    notice_component = componentToken,
                    ignored_gargalo = ignoredGargalo is { Length: > MaxEchoedLength }
                        ? ignoredGargalo[..MaxEchoedLength] + "…"
                        : ignoredGargalo
                });
            if (cortados.Count > 0)
            {
                gravada["numero_cortado"] = string.Join(',', cortados);
            }

            // Depois de trocar a CM, a BQ ou o MF a meio da produção, a
            // leitura vai para a folha das ferramentas atuais (
            // 18/10/2026): uma folha nova, ou a que já existia dessas
            // ferramentas. A página que mostrava outra folha (aberta antes da
            // troca) abre inteira nessa folha e diz o que aconteceu.
            if (outcome.Measurement is { StartedNewSheet: true })
            {
                gravada["folha"] = FolhaNovaValor;
            }
            else if (controloIdForm is not null && folhaVista is int vista && outcome.Measurement?.PegamentosId != vista)
            {
                gravada["folha"] = FolhaOutraValor;
            }

            return RedirectToPage("/Controlo/Pegamentos", gravada);
        }

        if (outcome.Result == RecordPegamentosMeasurementResult.MissingGargalo)
        {
            return MeasurementRefused(Anchor(), componentToken, costura, contraCostura, gargalo, outcome.Result, numero);
        }

        return RedirectToPage("/Controlo/Pegamentos", Anchor(new
        {
            measurement_refusal = outcome.Result.ToString()
        }));
    }

    /// <summary>Corrige a costura, a contra costura e o número da ferramenta de uma leitura desta folha.</summary>
    public async Task<IActionResult> OnPostEditMeasurementAsync(
        [FromServices] ICorrectPegamentosMeasurementHandler handler,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "pegamentos_id")] int? pegamentosId,
        [FromForm(Name = "measurement_id")] int measurementId,
        [FromForm(Name = "costura")] string? costura,
        [FromForm(Name = "contra_costura")] string? contraCostura,
        [FromForm(Name = "numero")] string? numero = null)
    {
        var anchor = CorrectionAnchor(controloId, pegamentosId);
        if (MeasurementDecimal.Parse(costura) is not { } costuraMm || costuraMm <= 0m)
        {
            anchor["editar_medicao"] = measurementId;
            anchor["correcao"] = "valor-invalido";
            return RedirectToPage("/Controlo/Pegamentos", anchor);
        }

        // Só a contra costura vazia a retira. Escrita mas ilegível (70..05)
        // é recusada, como a costura: gravá-la vazia apagava a guardada sem
        // aviso.
        var contraCosturaMm = ParseOptionalDecimal(contraCostura);
        if (Sanitize(contraCostura) is not null && contraCosturaMm is not > 0m)
        {
            anchor["editar_medicao"] = measurementId;
            anchor["campo"] = "contra";
            anchor["correcao"] = "contra-invalida";
            return RedirectToPage("/Controlo/Pegamentos", anchor);
        }

        var result = await handler.EditAsync(
            new EditPegamentosMeasurement(
                new PegamentosMeasurementAnchor(measurementId, controloId, controloId is null ? pegamentosId : null),
                costuraMm,
                contraCosturaMm,
                numero),
            HttpContext.RequestAborted);
        if (result == CorrectPegamentosMeasurementResult.ImpossibleValue)
        {
            anchor["editar_medicao"] = measurementId;
            anchor["correcao"] = "valor-invalido";
        }
        else if (result != CorrectPegamentosMeasurementResult.Success)
        {
            anchor["correcao"] = "nao-encontrada";
        }
        else if (NumeroLongo(numero))
        {
            anchor["numero_cortado_leitura"] = measurementId;
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
        var anchor = WithOpenCorrection(CorrectionAnchor(controloId, pegamentosId));
        var result = await handler.DeleteAsync(
            new PegamentosMeasurementAnchor(measurementId, controloId, controloId is null ? pegamentosId : null),
            HttpContext.RequestAborted);
        if (result != CorrectPegamentosMeasurementResult.Success)
        {
            anchor["correcao"] = "nao-encontrada";
        }

        return RedirectToPage("/Controlo/Pegamentos", anchor);
    }

    // Corrigir ou apagar numa folha de antes de uma troca volta a essa folha
    // (o formulário traz o pegamentos_id dela com o controlo_id).
    private static RouteValueDictionary CorrectionAnchor(int? controloId, int? pegamentosId)
    {
        var values = new RouteValueDictionary();
        if (controloId is int controlo)
        {
            values["controlo_id"] = controlo;
            if (pegamentosId is int folha)
            {
                values["pegamentos_id"] = folha;
            }
        }
        else
        {
            values["pegamentos_id"] = pegamentosId;
        }
        return values;
    }

    /// <summary>
    /// A correção aberta noutro cartão (editar_medicao e campo, enviados pelo
    /// pegamentos-registo.js) volta aberta depois de registar ou apagar uma
    /// leitura: esse cartão não muda e o que lá está escrito fica.
    /// </summary>
    private RouteValueDictionary WithOpenCorrection(RouteValueDictionary values)
    {
        if (EditarMedicaoId is int aberta)
        {
            values["editar_medicao"] = aberta;
            if (EditarCampo is "costura" or "contra" or "numero")
            {
                values["campo"] = EditarCampo;
            }
        }
        return values;
    }

    private IActionResult MeasurementRefused(RouteValueDictionary anchor, string component, string? costura,
        string? contraCostura, string? gargalo, RecordPegamentosMeasurementResult result, string? numero = null)
    {
        anchor["measurement_refusal"] = result.ToString();
        anchor["draft_component"] = component;
        anchor["draft_costura"] = CanonicalDraft(costura);
        anchor["draft_contra"] = CanonicalDraft(contraCostura);
        anchor["draft_gargalo"] = CanonicalDraft(gargalo);
        anchor["draft_numero"] = Sanitize(numero);
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

        // A folha mostrada: a das ferramentas atuais ou, com o pegamentos_id,
        // uma folha de antes de uma troca, com as ferramentas dela.
        var sheet = controloId is int canonicalControloId
            ? await _pegamentosQuery.FindSheetByControloAsync(canonicalControloId, pegamentosId, HttpContext.RequestAborted)
            : await _pegamentosQuery.FindSheetByPegamentosAsync(pegamentosId!.Value, HttpContext.RequestAborted);
        if (sheet is null)
        {
            // Controlo inexistente: a folha mostra o aviso limpo do controlo em falta.
            return controloId is not null
                ? RedirectToPage("/Controlo/Pegamentos", new { controlo_id = controloId })
                : RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = pegamentosId });
        }

        // QUEM GERA O PAPEL (o rodapé do alvo: «Diogo Oliveira 28/07/2026,
        // 14:52:28»). Sem selo de aprovação: os Pegamentos não se aprovam
        // (Diogo, 19/10/2026).
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value;

        // Com mais do que uma folha na produção, «-2», «-3»… no fim do nome
        // (18/10/2026); a primeira fica com o nome de sempre. A folha
        // em branco depois de uma troca é a próxima.
        var fileName = ControloDocumentPaths.GetPegamentosFileName(
            sheet.Reference, sheet.ProductionNumber, sheet.SheetNumber(sheet.Pegamentos) ?? sheet.Sheets.Count + 1);

        // Sem registo ainda: a folha em branco para preencher à mão.
        var bytes = _pdfGenerator.GenerateBytes(PegamentosPdfGenerator.FromSheet(sheet, generatedBy)
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
                ? (alertCount == 1 ? "1 medição fora da tolerância (atingiu ou ultrapassou um limite)." : $"{alertCount} medições fora da tolerância (atingiram ou ultrapassaram um limite).")
                : measurements.Any(m => m.Evaluation.IsNotEvaluable)
                    ? "Existem medições não avaliáveis — nominal não registado."
                    : "Todas as medições permanecem dentro do corredor do respetivo componente.";
        return new(summary, alertCount > 0, measurements.Count);
    }
}
