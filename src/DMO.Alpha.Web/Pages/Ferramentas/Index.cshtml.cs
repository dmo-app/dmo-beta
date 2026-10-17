using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// Surface nativo do registo de Ferramentas (corte A — a RAIZ da cascata
/// tool_id → jobon_id → controlo_id → pegamentos/peso): a folha onde o
/// Owner regista as ferramentas de vidro reais (CM/BQ/MF — BA Glass) e
/// descobre as existentes. A lista mostra FATOS HUMANOS («CM · 5447 ·
/// lote 3 · B1») — o tool_id é a identidade opaca do backend, vive no
/// URL/hidden e nunca é exibido como facto da folha. A criação é
/// CONTEXTUAL (?return=…{tool_id}…): outro fluxo que precisa de uma Tool
/// canónica abre o Ferramentas com contexto, o humano seleciona uma
/// existente ou cria a que falta, e o Ferramentas devolve o tool_id
/// resultante ao chamador (blueprint «Contextual selection and creation»
/// — o Job On nunca minta identidade privada; a validação do return é
/// LOCAL-ONLY: começa por "/" e não por "//", nenhum fluxo externo). O
/// tool_id é SEMPRE mintado pelo backend; os formatos de referência
/// (CM = dígitos; BQ = 1 letra maiúscula + 3 dígitos) são HINT + nota
/// avisadora, nunca recusa; as recusas de negócio são tipadas por enum
/// fechado e voltam por query (PRG). A linha DUMMY (REMOVED) nunca é
/// candidata (excluída por constante na query canónica). Mutação nativa
/// exige sessão autenticada ([Authorize]: o anónimo é desafiado ANTES de
/// qualquer leitura/escrita). O Ferramentas é alcançado contextualmente,
/// não é destino de navegação obrigatório.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class IndexModel : PageModel
{
    private readonly IToolCandidateQuery _candidateQuery;
    private readonly IMachineListQuery _machineListQuery;
    private readonly ICreateToolHandler _createTool;
    private readonly ShellStateFactory _shellState;
    private readonly IToolLotQuantitySettings _lotQuantities;

    [BindProperty(SupportsGet = true, Name = "bq_quantity")]
    public string? RequestedLotQuantity { get; set; }
    [BindProperty(SupportsGet = true, Name = "quantity_manual")]
    public string? RequestedQuantityManual { get; set; }
    [BindProperty(SupportsGet = true, Name = "machine_ids")]
    public List<string>? RequestedMachineIds { get; set; }

    public IndexModel(
        IToolCandidateQuery candidateQuery,
        IMachineListQuery machineListQuery,
        ICreateToolHandler createTool,
        ShellStateFactory shellState,
        IToolLotQuantitySettings lotQuantities)
    {
        _candidateQuery = candidateQuery;
        _machineListQuery = machineListQuery;
        _createTool = createTool;
        _shellState = shellState;
        _lotQuantities = lotQuantities;
    }

    /// <summary>Candidatos (FATOS HUMANOS) com a ordenação canónica Type→Reference→Lot.</summary>
    public int TotalCandidates { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 10;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCandidates / (double)PageSize));

    public string? PageLink(int pageNumber) => Url.Page("/Ferramentas/Index", new
    {
        type = RequestedTypeToken, reference = RequestedReference ?? RequestedMfReference, lot = RequestedLot,
        machine_id = RequestedMachineId,
        pagina = pageNumber, por_pagina = PageSize, @return = ReturnTemplate
    });

    public IReadOnlyList<ToolCandidate> Candidates { get; private set; } = Array.Empty<ToolCandidate>();

    /// <summary>Códigos de máquina por tool_id (uma linha por ligação; sem N+1).</summary>
    /// <summary>Máquinas persistidas (filtro por máquina + criação contextual).</summary>
    public IReadOnlyList<MachineOption> Machines { get; private set; } = Array.Empty<MachineOption>();

    // Estado de apresentação dos filtros (preservado entre pedidos).
    /// <summary>TRUE quando há pelo menos um filtro: só então a lista carrega.</summary>
    public bool Searched { get; private set; }

    public string? RequestedTypeToken { get; private set; }
    public string? RequestedReference { get; private set; }
    public string? RequestedLot { get; private set; }
    public string? RequestedMfReference { get; private set; }

    /// <summary>O nome técnico (o código do desenho) submetido, preservado por PRG.</summary>
    public string? RequestedTechnicalName { get; private set; }
    public int? RequestedMachineId { get; private set; }

    /// <summary>Template de return do fluxo contextual (?return=…{tool_id}…), local-only.</summary>
    public string? ReturnTemplate { get; private set; }

    /// <summary>Recusa tipada do último POST de criação, transportada por query (PRG).</summary>
    public CreateToolResult? CreateRefusal { get; private set; }

    /// <summary>Nota de ferramenta eliminada (PRG do delete da ficha) — o histórico vive nos registos congelados.</summary>
    public bool DeletedNote { get; private set; }

    /// <summary>A tab CRIAR aberta (?criar=1 ou uma recusa do create a voltar).</summary>
    public bool ShowCreate { get; private set; }

    /// <summary>Mensagem da recusa de criação, apenas apresentação.</summary>
    public string CreateRefusalMessage => CreateRefusal switch
    {
        CreateToolResult.InvalidLotQuantity => "Use uma quantidade inteira igual ou superior a 0. O valor introduzido foi mantido.",
        CreateToolResult.UnknownTypeToken =>
            "Tipo desconhecido. Use CM, BQ ou MF. Nenhuma ferramenta foi registada.",
        CreateToolResult.MissingReference =>
            "A referência é a identidade humana do registo («CM · 5447 · lote 3») e não pode ficar em branco. " +
            "Nenhuma ferramenta foi registada.",
        // Já não é devolvido (beta.63: o lote vazio grava «s/lote» com aviso); fica para links antigos.
        CreateToolResult.MissingLot =>
            "O lote é obrigatório — um lote diferente é uma ferramenta diferente. Nenhuma ferramenta foi registada.",
        CreateToolResult.MachineNotFound =>
            "Uma das máquinas indicadas não existe no registo. Nenhuma ferramenta foi registada.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "type")] string? type,
        [FromQuery(Name = "reference")] string? reference,
        [FromQuery(Name = "lot")] string? lot,
        [FromQuery(Name = "mf_reference")] string? mfReference,
        [FromQuery(Name = "machine_id")] int? machineId,
        [FromQuery(Name = "return")] string? returnUrl,
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "deleted")] string? deleted,
        [FromQuery(Name = "criar")] string? criar,
        [FromQuery(Name = "technical_name")] string? technicalName,
        [FromQuery(Name = "pagina")] int? pageNumber,
        [FromQuery(Name = "por_pagina")] int? pageSize)
    {
        // AS TABS VER · CRIAR (o Owner 2026-10-13: «deveria ser um tab para
        // criar e outro para ver o que existe»): o ?criar=1 abre o registo,
        // o default é o VER (a procura + a lista — nunca tudo misturado).
        ShowCreate = criar == "1" || createRefusal is not null;
        ViewData["Title"] = "Ferramentas";
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = ShowCreate ? "criar" : "ver",
            Functions = ShellStateFactory.FerramentasFunctions(ShowCreate ? "criar" : "ver")
        };

        // Recusa tipada do POST anterior, transportada por query (PRG) com o
        // vocabulário fechado do enum.
        if (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateToolResult parsed)
            && parsed != CreateToolResult.Success)
        {
            CreateRefusal = parsed;
        }

        DeletedNote = deleted == "1";

        // Filtros: dimensões fechadas e explícitas, cada uma apenas REDUZ os
        // candidatos (a seleção é sempre humana explícita sobre o tool_id).
        RequestedTypeToken = NormalizeTypeToken(type);
        RequestedReference = Sanitize(reference);
        RequestedLot = Sanitize(lot);
        RequestedMfReference = Sanitize(mfReference);
        RequestedMachineId = machineId;
        RequestedTechnicalName = Sanitize(technicalName);

        // UM SÓ CAMPO «Referência» (Diogo, 17/10/2026: «campos de filtro
        // duplicados referência»): procura a referência da ferramenta OU a
        // referência MF em que encaixa (a regra do antigo «Referência MF /
        // produção», que já incluía a referência). O ?mf_reference= antigo
        // continua a valer como referência.
        var searchReference = RequestedReference ?? RequestedMfReference;
        var filter = new ToolCandidateFilter(
            Type: RequestedTypeToken is null ? null : TypeFor(RequestedTypeToken),
            Lot: RequestedLot,
            MachineId: RequestedMachineId,
            MfReference: searchReference);

        // A lista só aparece no separador Ver, e só depois de procurar
        // (Diogo, 17/10/2026: «não deve existir lista até procurar, evitar
        // peso nos carregamentos»): sem filtros, a página abre só com eles.
        Searched = RequestedTypeToken is not null || searchReference is not null || RequestedLot is not null
            || RequestedMachineId is not null;
        if (!ShowCreate && Searched)
        {
            var page = await _candidateQuery.FindPageAsync(filter, pageNumber ?? 1, pageSize ?? 10, HttpContext.RequestAborted);
            Candidates = page.Items;
            TotalCandidates = page.TotalCount;
            PageNumber = page.PageNumber;
            PageSize = page.PageSize;
        }

        Machines = await _machineListQuery.ListAsync(HttpContext.RequestAborted);

        // Template de return do fluxo contextual: validação LOCAL-ONLY —
        // começa por "/" e não por "//" (nenhum destino externo). O template
        // inválido é simplesmente ignorado (nunca recusa: função antes de
        // proteção) e o pós-criação segue para a ficha.
        ReturnTemplate = IsSafeLocalReturnTemplate(returnUrl) ? returnUrl : null;

        return Page();
    }

    /// <summary>
    /// POST nativo: registar uma ferramenta real (o ato canónico do Owner).
    /// O browser submete apenas os factos humanos (tipo, referência, lote,
    /// referência MF associada opcional, quantidade contabilística do lote
    /// BQ opcional, máquinas aplicáveis); o tool_id é mintado pelo backend,
    /// nunca transportado pelo form. Sucesso com ?return= contextual volta
    /// ao chamador com o tool_id substituído no template (PRG); sem return,
    /// a ficha da nova ferramenta abre com a nota de criação e a nota
    /// avisadora de formato (nunca recusa). Recusa volta por query (PRG).
    /// </summary>
    public async Task<IActionResult> OnGetLotQuantityAsync(
        [FromQuery(Name = "type")] string? type,
        [FromQuery(Name = "machine_ids")] List<int>? machineIds)
    {
        var token = NormalizeTypeToken(type);
        var result = token is null
            ? new ToolLotQuantitySuggestion(null, Array.Empty<ToolLotQuantityDefault>())
            : await _lotQuantities.SuggestAsync(TypeFor(token)!.Value, machineIds ?? new List<int>(), HttpContext.RequestAborted);
        return new JsonResult(new { quantity = result.Quantity, machines = result.Machines.Select(m => new { code = m.MachineCode, quantity = m.Quantity }) });
    }

    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "type_token")] string? typeToken,
        [FromForm(Name = "reference")] string? reference,
        [FromForm(Name = "lot")] string? lot,
        [FromForm(Name = "mf_reference")] string? mfReference,
        [FromForm(Name = "bq_quantity")] string? bqQuantity,
        [FromForm(Name = "machine_ids")] List<string>? machineIds,
        [FromForm(Name = "technical_name")] string? technicalName,
        [FromForm(Name = "return")] string? returnUrl,
        [FromForm(Name = "quantity_manual")] string? quantityManual)
    {
        // beta.63 (A11): a quantidade ilegível não recusa a ferramenta — grava
        // sem quantidade (sem a sugestão automática, que o operador quis
        // substituir) e a ficha avisa. A negativa continua recusada.
        var typedQuantity = Sanitize(bqQuantity);
        var parsedQuantity = ParseOptionalInt(bqQuantity);
        var unreadableQuantity = typedQuantity is not null && parsedQuantity is null;
        var negativeQuantity = parsedQuantity is < 0;
        var outcome = negativeQuantity ? new CreateToolOutcome(CreateToolResult.InvalidLotQuantity) : await _createTool.HandleAsync(
            new CreateTool(
                Sanitize(typeToken) ?? string.Empty,
                (reference ?? string.Empty).ToUpperInvariant(),
                lot ?? string.Empty,
                // A referência MF associada só se aplica à CM (beta.37).
                Sanitize(typeToken) == "CM" ? mfReference?.ToUpperInvariant() : null,
                parsedQuantity,
                machineIds?
                    .Select(Sanitize)
                    .Where(id => id is not null)
                    .Select(id => int.TryParse(id, out var parsed) ? parsed : 0)
                    .Distinct()
                    .ToList(),
                technicalName?.ToUpperInvariant(),
                UseLotQuantityDefault: quantityManual != "1" && !unreadableQuantity),
            HttpContext.RequestAborted);

        if (outcome.Result == CreateToolResult.Success && outcome.Tool is { } created)
        {
            var hasWarnings = created.LotDefaulted || unreadableQuantity;

            // Fluxo contextual: o chamador recebe o tool_id resultante
            // substituído no SEU template local. Sem template (ou inválido),
            // a ficha da nova ferramenta abre com a nota de criação. Com
            // avisos (lote «s/lote», quantidade ilegível), a ficha abre
            // primeiro com o aviso e o link para continuar no fluxo — o
            // aviso nunca se perde no regresso.
            if (IsSafeLocalReturnTemplate(returnUrl) && !hasWarnings)
            {
                return LocalRedirect(returnUrl.Replace("{tool_id}", created.ToolId));
            }

            // GET-OR-CREATE (o Owner: «ferramentas duplicadas sao os lotes
            // diferentes, ou maquinas diferentes»): a mesma (tipo,
            // referência, lote) é a MESMA ferramenta — a ficha abre com a
            // nota honesta (existed=1: já estava registada, as máquinas
            // acrescentaram ao link dela; nada foi duplicado).
            return RedirectToPage("/Ferramentas/Ficha", new
            {
                tool_id = created.ToolId,
                created = created.AlreadyExisted ? null : "1",
                existed = created.AlreadyExisted ? "1" : null,
                advisory = created.ReferenceFormatAdvisory ? "1" : null,
                sem_lote = created.LotDefaulted ? "1" : null,
                quantidade_ilegivel = unreadableQuantity ? typedQuantity : null,
                continuar = IsSafeLocalReturnTemplate(returnUrl) ? returnUrl.Replace("{tool_id}", created.ToolId) : null
            });
        }

        // Recusa tipada por query (PRG): os factos submetidos e o return do
        // fluxo contextual sobrevivem à volta (o form volta preenchido).
        return RedirectToPage("/Ferramentas/Index", new
        {
            type = NormalizeTypeToken(typeToken),
            reference = Sanitize(reference),
            lot = Sanitize(lot),
            mf_reference = Sanitize(mfReference),
            technical_name = Sanitize(technicalName),
            create_refusal = outcome.Result.ToString(),
            bq_quantity = bqQuantity,
            quantity_manual = quantityManual,
            machine_ids = machineIds,
            @return = returnUrl
        });
    }

    /// <summary>
    /// Template de regresso local com {tool_id}: começa por «/», sem «//» nem
    /// «\» (que os browsers tratam como «/»). Um template inválido é ignorado
    /// e a criação abre a ficha da ferramenta.
    /// </summary>
    internal static bool IsSafeLocalReturnTemplate([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? template) =>
        template is not null
        && template.StartsWith("/", StringComparison.Ordinal)
        && !template.StartsWith("//", StringComparison.Ordinal)
        && !template.Contains('\\')
        && template.Contains("{tool_id}", StringComparison.Ordinal);

    /// <summary>Token de tipo canónico (CM/MF/BQ) ou null quando não aplicado.</summary>
    internal static string? NormalizeTypeToken(string? token) => token switch
    {
        "CM" => "CM",
        "MF" => "MF",
        "BQ" => "BQ",
        _ => null
    };

    internal static ToolType? TypeFor(string token) => token switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => null
    };

    internal static string TokenTypeFor(ToolType type) => type switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        ToolType.Bq => "BQ",
        _ => type.ToString()
    };

    internal static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    internal static int? ParseOptionalInt(string? value) =>
        int.TryParse(value?.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
