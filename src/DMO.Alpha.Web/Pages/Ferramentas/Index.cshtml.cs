using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

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
/// tool_id é SEMPRE mintado pelo backend; a referência é a toda, como no
/// desenho (1075C142, 5447T173 ou ST100), e o formato nunca é recusado;
/// as recusas de negócio são tipadas por enum
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
    private readonly IEditToolTechnicalValuesHandler _editTechnical;
    private readonly DMO.Alpha.Core.Armazem.IArmazemLotesNovos _lotesNovos;

    /// <summary>Os campos técnicos opcionais do registo (os mesmos nomes da ficha).</summary>
    internal static readonly string[] TechnicalFields =
    {
        "process_token", "nominal_weight", "volume_puncao", "volume_marisa",
        "diametro_gargalo", "tp_diametro", "tp_sagitta"
    };

    private readonly IFerramentasUsoSap _usoSap;

    /// <summary>O % uso da última leitura das ferramentas desta página (para a cor do uso cheio).</summary>
    public IReadOnlyDictionary<string, decimal> Usos { get; private set; } = new Dictionary<string, decimal>();

    // Onde está e o estado de cada ferramenta da página, da mesma fonte da
    // ficha e do Job On (Diogo, 27/10/2026: «mostrar o estado, posição no
    // armazém, reparado, por reparar»).
    private readonly IJobOnToolFactsQuery _factos;
    public IReadOnlyDictionary<string, JobOnToolFacts> Factos { get; private set; } = new Dictionary<string, JobOnToolFacts>();
    public JobOnToolFacts? FactosDe(string toolId) => Factos.TryGetValue(toolId, out var f) ? f : null;

    public decimal? UsoDe(string toolId) => Usos.TryGetValue(toolId, out var uso) ? uso : null;

    /// <summary>O % uso escrito, de volta numa recusa.</summary>
    [BindProperty(SupportsGet = true, Name = "uso_percentagem")]
    public string? RequestedUsoPercentagem { get; set; }

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
        IToolLotQuantitySettings lotQuantities,
        IEditToolTechnicalValuesHandler editTechnical,
        DMO.Alpha.Core.Armazem.IArmazemLotesNovos lotesNovos,
        IFerramentasUsoSap usoSap,
        IJobOnToolFactsQuery factos)
    {
        _factos = factos;
        _usoSap = usoSap;
        _lotesNovos = lotesNovos;
        _editTechnical = editTechnical;
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

    /// <summary>Um valor técnico do registo devolvido por uma recusa (PRG); vazio quando não há.</summary>
    public string RequestedTechnical(string field) => Request.Query[field].ToString().Trim();

    /// <summary>A informação técnica do registo abre quando a recusa devolve valores escritos.</summary>
    public bool RequestedTechnicalOpen => TechnicalFields.Any(name => RequestedTechnical(name).Length > 0);

    /// <summary>Template de return do fluxo contextual (?return=…{tool_id}…), local-only.</summary>
    public string? ReturnTemplate { get; private set; }

    /// <summary>Recusa tipada do último POST de criação, transportada por query (PRG).</summary>
    public CreateToolResult? CreateRefusal { get; private set; }

    /// <summary>Na recusa ToolAlreadyExists: a ferramenta que já existe (o aviso oferece abri-la).</summary>
    public string? ExistingToolId { get; private set; }

    /// <summary>
    /// Quem está a gravar: o nome do utilizador, senão o nº de operador,
    /// senão o email (fica no aviso de ferramenta repetida).
    /// </summary>
    internal static string? ActorName(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(DmoClaimTypes.DisplayName)?.Value is { Length: > 0 } name
            ? name
            : user.FindFirst(DmoClaimTypes.OperatorId)?.Value
                ?? user.FindFirst(DmoClaimTypes.Email)?.Value;

    /// <summary>Nota de ferramenta eliminada (PRG do delete da ficha) — o histórico vive nos registos congelados.</summary>
    public bool DeletedNote { get; private set; }

    /// <summary>
    /// O lote novo registado no Armazém que este registo resolve (?lote_novo=,
    /// do aviso do Sumário); null quando não há ou já foi resolvido.
    /// </summary>
    public DMO.Alpha.Core.Armazem.ArmazemLoteNovoItem? LoteNovo { get; private set; }

    /// <summary>A ferramenta que já existe com o tipo, a referência e o lote do lote novo (ligar em vez de criar).</summary>
    public string? LoteNovoExistente { get; private set; }

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
        // Só na BQ (Diogo, 22/10/2026); na CM e na MF o lote vazio grava «s/lote» com aviso.
        CreateToolResult.MissingLot =>
            "Indique o lote da boquilha: é preciso para identificar a ferramenta. Nenhuma ferramenta foi registada.",
        CreateToolResult.MachineNotFound =>
            "Uma das máquinas indicadas não existe no registo. Nenhuma ferramenta foi registada.",
        CreateToolResult.ToolAlreadyExists =>
            "Já existe uma ferramenta com este tipo, referência e lote numa das mesmas máquinas. Nenhuma ferramenta foi registada; " +
            "a que já existia ficou com um aviso para confirmar que está certa.",
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
        [FromQuery(Name = "existing_tool_id")] string? existingToolId,
        [FromQuery(Name = "deleted")] string? deleted,
        [FromQuery(Name = "criar")] string? criar,
        [FromQuery(Name = "technical_name")] string? technicalName,
        [FromQuery(Name = "pagina")] int? pageNumber,
        [FromQuery(Name = "por_pagina")] int? pageSize,
        [FromQuery(Name = "lote_novo")] int? loteNovo = null)
    {
        if (loteNovo is { } loteNovoId && await _lotesNovos.ObterAsync(loteNovoId, HttpContext.RequestAborted) is { PorCriar: true } pendente)
        {
            LoteNovo = pendente;
            LoteNovoExistente = await _lotesNovos.FerramentaExistenteAsync(loteNovoId, HttpContext.RequestAborted);
        }

        // AS TABS VER · CRIAR (o Owner 2026-10-13: «deveria ser um tab para
        // criar e outro para ver o que existe»): o ?criar=1 abre o registo,
        // o default é o VER (a procura + a lista — nunca tudo misturado).
        ShowCreate = criar == "1" || createRefusal is not null;
        ViewData["Title"] = ShowCreate ? "Criar ferramenta" : "Ferramentas";
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

        ExistingToolId = CreateRefusal == CreateToolResult.ToolAlreadyExists ? Sanitize(existingToolId) : null;
        DeletedNote = deleted == "1";

        RequestedTechnicalName = Sanitize(technicalName);
        await LoadCandidatesAsync(type, reference, lot, mfReference, machineId, pageNumber, pageSize);

        Machines = await _machineListQuery.ListAsync(HttpContext.RequestAborted);

        // Template de return do fluxo contextual: validação LOCAL-ONLY —
        // começa por "/" e não por "//" (nenhum destino externo). O template
        // inválido é simplesmente ignorado (nunca recusa: função antes de
        // proteção) e o pós-criação segue para a ficha.
        ReturnTemplate = IsSafeLocalReturnTemplate(returnUrl) ? returnUrl : null;

        return Page();
    }

    /// <summary>Pesquisa/paginação sem compor shell, filtros ou máquinas.</summary>
    public async Task<IActionResult> OnGetResultadosAsync(
        [FromQuery(Name = "type")] string? type,
        [FromQuery(Name = "reference")] string? reference,
        [FromQuery(Name = "lot")] string? lot,
        [FromQuery(Name = "mf_reference")] string? mfReference,
        [FromQuery(Name = "machine_id")] int? machineId,
        [FromQuery(Name = "return")] string? returnUrl,
        [FromQuery(Name = "pagina")] int? pageNumber,
        [FromQuery(Name = "por_pagina")] int? pageSize)
    {
        await LoadCandidatesAsync(type, reference, lot, mfReference, machineId, pageNumber, pageSize);
        ReturnTemplate = IsSafeLocalReturnTemplate(returnUrl) ? returnUrl : null;
        Response.Headers["X-Dmo-Region"] = "ferrResultados";
        return Partial("_FerramentasResultados", this);
    }

    private async Task LoadCandidatesAsync(string? type, string? reference, string? lot,
        string? mfReference, int? machineId, int? pageNumber, int? pageSize)
    {
        // Filtros: dimensões fechadas e explícitas, cada uma apenas REDUZ os
        // candidatos (a seleção é sempre humana explícita sobre o tool_id).
        RequestedTypeToken = NormalizeTypeToken(type);
        RequestedReference = Sanitize(reference);
        RequestedLot = Sanitize(lot);
        RequestedMfReference = Sanitize(mfReference);
        RequestedMachineId = machineId;

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
            ReferenceContains: searchReference);

        // A lista só aparece no separador Ver, e só depois de procurar
        // (Diogo, 17/10/2026: «não deve existir lista até procurar, evitar
        // peso nos carregamentos»): sem filtros, a página abre só com eles.
        // O tipo e a máquina só reduzem: a lista aparece quando se começa a
        // escrever a referência ou o lote (Diogo, 18/10/2026: escolher BQ
        // carregava as BQ todas).
        Searched = searchReference is not null || RequestedLot is not null;
        if (!ShowCreate && Searched)
        {
            var page = await _candidateQuery.FindPageAsync(filter, pageNumber ?? 1, pageSize ?? 10, HttpContext.RequestAborted);
            Candidates = page.Items;
            Usos = await _usoSap.UltimasPercentagensAsync(page.Items.Select(c => c.ToolId).ToList(), HttpContext.RequestAborted);
            Factos = (await _factos.FindAsync(page.Items.Select(c => c.ToolId).ToList(), null, null, null, null, HttpContext.RequestAborted))
                .ToDictionary(f => f.ToolId, StringComparer.Ordinal);
            TotalCandidates = page.TotalCount;
            PageNumber = page.PageNumber;
            PageSize = page.PageSize;
        }

    }

    /// <summary>
    /// O nome técnico lido pelo servidor, com a regra da ficha
    /// (<see cref="TechnicalNameParser"/>; sweep de 19/10/2026, 3.14): o
    /// formulário propõe o tipo com ela e mostra o que a ficha vai mostrar.
    /// A referência nunca vem do nome técnico (Diogo, 19/10/2026: «sempre o
    /// nome da referência, e não do nome técnico»; o nome técnico serve para
    /// ler os detalhes técnicos do desenho).
    /// </summary>
    public IActionResult OnGetNomeTecnico([FromQuery(Name = "nome")] string? nome)
    {
        var decoded = TechnicalNameParser.Decode(nome ?? string.Empty);
        if (decoded.InferredType is null && decoded.Family == "desconhecido")
        {
            return new JsonResult(new { tipo = (string?)null, leitura = (string?)null });
        }

        var partes = new List<string> { decoded.Family };
        if (decoded.InferredType is { } tipo) partes.Add($"tipo {TokenTypeFor(tipo)}");
        if (decoded.Dimension is { } dimension) partes.Add(dimension);
        if (decoded.Process is { } process) partes.Add(process);
        if (decoded.Ventilation is { } ventilation) partes.Add($"ventilação {ventilation}");
        return new JsonResult(new
        {
            tipo = decoded.InferredType is { } inferred ? TokenTypeFor(inferred) : null,
            leitura = string.Join(" · ", partes)
        });
    }

    /// <summary>
    /// A quantidade total do lote sugerida para o tipo e as máquinas
    /// escolhidas no «Registar ferramenta real» (tool-lot-quantity.js).
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
        [FromForm(Name = "quantity_manual")] string? quantityManual,
        [FromForm(Name = "lote_novo")] int? loteNovo = null,
        [FromForm(Name = "uso_percentagem")] string? usoPercentagem = null)
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
                UseLotQuantityDefault: quantityManual != "1" && !unreadableQuantity,
                RequestedBy: ActorName(User)),
            HttpContext.RequestAborted);

        var technical = TechnicalFields.ToDictionary(field => field, field => Sanitize(Request.Form[field].ToString()));

        if (outcome.Result == CreateToolResult.Success && outcome.Tool is { } created)
        {
            // A informação técnica opcional grava na ferramenta acabada de
            // registar; a que já existia não é alterada (abre a sua ficha).
            var unreadableTechnical = created.AlreadyExisted
                ? new Dictionary<string, string>()
                : await SaveTechnicalAsync(created.ToolId, created.Type, technical);
            // O lote novo do Armazém (o aviso do Sumário): a entrada fica na
            // posição guardada e o aviso desaparece.
            DMO.Alpha.Core.Armazem.ArmazemLoteNovoResolucaoSaida? resolucao = null;
            string? posicaoArmazem = null;
            if (loteNovo is { } loteNovoId && !created.AlreadyExisted)
            {
                posicaoArmazem = (await _lotesNovos.ObterAsync(loteNovoId, CancellationToken.None))?.Posicao;
                resolucao = await _lotesNovos.ResolverAsync(loteNovoId, created.ToolId, CancellationToken.None);
            }

            // O % uso (opcional): nunca recusa. Grava como a primeira leitura
            // (origem manual); 100 ou mais grava sem aviso nenhum (Diogo,
            // 23/10/2026); ilegível ou negativo não grava e avisa.
            string? usoAviso = null;
            var usoEscrito = (usoPercentagem ?? string.Empty).Trim().TrimEnd('%').Trim();
            if (usoEscrito.Length > 0 && !created.AlreadyExisted)
            {
                if (int.TryParse(usoEscrito, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var uso))
                {
                    await _usoSap.RegistarPercentagemManualAsync(created.ToolId, uso, CancellationToken.None);
                }
                else
                {
                    usoAviso = usoEscrito.Length > 20 ? usoEscrito[..20] : usoEscrito;
                }
            }

            var avisosArmazem = resolucao is { Resolveu: true } ? resolucao.Avisos.ToArray() : [];
            var hasWarnings = usoAviso is not null || created.LotDefaulted || unreadableQuantity || unreadableTechnical.Count > 0 || avisosArmazem.Length > 0;

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

            // A ficha abre com a nota de criação. Desde a ferramenta repetida
            // (18/10/2026) o handler recusa os mesmos (tipo, referência, lote)
            // em vez de devolver a existente; o existed=1 fica para links antigos.
            var fichaValues = new RouteValueDictionary(new
            {
                tool_id = created.ToolId,
                created = created.AlreadyExisted ? null : "1",
                existed = created.AlreadyExisted ? "1" : null,
                sem_lote = created.LotDefaulted ? "1" : null,
                quantidade_ilegivel = unreadableQuantity ? typedQuantity : null,
                continuar = IsSafeLocalReturnTemplate(returnUrl) ? returnUrl.Replace("{tool_id}", created.ToolId) : null,
                armazem_entrada = resolucao is { Resolveu: true } ? posicaoArmazem : null,
                aviso_armazem = avisosArmazem.Length > 0 ? avisosArmazem : null,
                uso_aviso = usoAviso
            });
            foreach (var field in unreadableTechnical)
                fichaValues[field.Key] = field.Value;
            if (unreadableTechnical.Count > 0)
                fichaValues["tecnica"] = "1";
            return RedirectToPage("/Ferramentas/Ficha", fichaValues);
        }

        // Recusa tipada por query (PRG): os factos submetidos e o return do
        // fluxo contextual sobrevivem à volta (o form volta preenchido).
        var refusalValues = new RouteValueDictionary(new
        {
            type = NormalizeTypeToken(typeToken),
            reference = Sanitize(reference),
            lot = Sanitize(lot),
            mf_reference = Sanitize(mfReference),
            technical_name = Sanitize(technicalName),
            create_refusal = outcome.Result.ToString(),
            existing_tool_id = outcome.ExistingToolId,
            bq_quantity = bqQuantity,
            quantity_manual = quantityManual,
            machine_ids = machineIds,
            @return = returnUrl,
            lote_novo = loteNovo,
            uso_percentagem = Sanitize(usoPercentagem)
        });
        foreach (var field in technical.Where(field => field.Value is not null))
            refusalValues[field.Key] = field.Value;
        return RedirectToPage("/Ferramentas/Index", refusalValues);
    }

    /// <summary>
    /// Ligar o lote novo do Armazém à ferramenta que já existe com o mesmo
    /// tipo, referência e lote (em vez de criar outra): a entrada fica na
    /// posição guardada e a ficha abre.
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostLigarLoteNovoAsync([FromForm(Name = "lote_novo")] int loteNovo)
    {
        var pendente = await _lotesNovos.ObterAsync(loteNovo, HttpContext.RequestAborted);
        var existente = pendente is { PorCriar: true } ? await _lotesNovos.FerramentaExistenteAsync(loteNovo, HttpContext.RequestAborted) : null;
        if (pendente is null || existente is null)
        {
            return RedirectToPage("/Ferramentas/Index", new { criar = 1, lote_novo = loteNovo });
        }

        var resolucao = await _lotesNovos.ResolverAsync(loteNovo, existente, CancellationToken.None);
        return RedirectToPage("/Ferramentas/Ficha", new
        {
            tool_id = existente,
            armazem_entrada = resolucao.Resolveu ? pendente.Posicao : null,
            aviso_armazem = resolucao.Avisos.Count > 0 ? resolucao.Avisos.ToArray() : null
        });
    }

    /// <summary>
    /// Grava a informação técnica escrita no registo (a mesma regra da
    /// ficha: o vazio fica em falta, cada tipo só grava os seus campos). Um
    /// campo ilegível ou impossível (0 ou menos) não impede os outros; volta
    /// para o aviso da ficha, já com o prefixo do aviso de cada campo.
    /// </summary>
    private async Task<Dictionary<string, string>> SaveTechnicalAsync(
        string toolId, ToolType type, IReadOnlyDictionary<string, string?> technical)
    {
        if (technical.Values.All(value => value is null))
            return new Dictionary<string, string>();

        var unreadable = technical
            .Where(field => field.Key != "process_token" && field.Value is not null && MeasurementDecimal.Parse(field.Value) is null)
            .ToDictionary(field => field.Key, field => field.Value!);
        var impossible = technical
            .Where(field => field.Key != "process_token"
                && MeasurementDecimal.Parse(field.Value) is { } value && FichaModel.IsImpossible(field.Key, value))
            .ToDictionary(field => field.Key, field => field.Value!);
        decimal? Value(string field) => unreadable.ContainsKey(field) || impossible.ContainsKey(field)
            ? null
            : MeasurementDecimal.Parse(technical[field]);

        // A ferramenta já está gravada: a informação técnica grava mesmo que
        // o browser tenha abortado o pedido (no duplo clique aborta o
        // primeiro, e o segundo encontra a ferramenta e não a grava).
        var outcome = await _editTechnical.HandleAsync(
            new EditToolTechnicalValues(
                toolId,
                technical["process_token"],
                Value("nominal_weight"),
                Value("volume_puncao"),
                Value("volume_marisa"),
                Value("diametro_gargalo"),
                Value("tp_diametro"),
                null, // o raio do tampão já não é pedido
                Value("tp_sagitta")),
            CancellationToken.None);

        // Só se avisa dos campos que o tipo grava (os outros são ignorados).
        return outcome.Result == EditToolTechnicalValuesResult.Success
            ? unreadable.Select(field => (Key: FichaModel.UnreadablePrefix + field.Key, Field: field.Key, field.Value))
                .Concat(impossible.Select(field => (Key: FichaModel.ImpossiblePrefix + field.Key, Field: field.Key, field.Value)))
                .Where(field => FichaModel.AppliesTo(type, field.Field))
                .ToDictionary(field => field.Key, field => field.Value)
            : new Dictionary<string, string>();
    }

    /// <summary>
    /// Template de regresso local com {tool_id}, pela regra única de caminho
    /// local (<see cref="LocalUrl"/>). Um template inválido é ignorado e a
    /// criação abre a ficha da ferramenta.
    /// </summary>
    internal static bool IsSafeLocalReturnTemplate([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? template) =>
        LocalUrl.IsSafe(template)
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
