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
/// Surface nativo da FICHA de uma ferramenta (Ferramentas, corte A): a
/// ferramenta chega pelo tool_id (?tool_id=… — a identidade opaca do
/// backend, no URL, nunca exibida como facto da folha) e a ficha mostra
/// os FATOS HUMANOS («CM · 5447 · lote 3 · B1»). Daqui mantém-se o
/// registo: editar os factos (full-replace no MESMO tool_id — a correção
/// normal quando a identidade física não mudou), completar/substituir os
/// valores técnicos Tool-owned (incluindo a geometria TP do desenho — os
/// valores reais da folha, gravados tal como vêm: a app NUNCA recalcula
/// derivados), duplicar para um lote novo (novo tool_id, fonte intacta) e
/// eliminar (transação canónica: os registos de consumo são reatribuídos
/// à linha de removidos conservando os factos congelados — a verdade
/// histórica intacta). A eliminação é ação corretiva EXPLÍCITA: o aviso
/// lista as produções afetadas e as contagens dos registos de consumo
/// ANTES da confirmação (POST antiforgery, ?confirm_delete=1) — a
/// existência de histórico é motivo de aviso, nunca recusa automática.
/// Recusas tipadas voltam por query (PRG) com o vocabulário fechado dos
/// enums; formatos de referência são hint + nota avisadora, nunca
/// recusa. Mutação nativa exige sessão autenticada ([Authorize]).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class FichaModel : PageModel
{
    private static readonly string[] TechnicalFieldNames =
    [
        "process_token", "nominal_weight", "volume_puncao", "volume_marisa", "diametro_gargalo",
        "tp_diametro", "tp_raio", "tp_sagitta"
    ];

    /// <summary>Rótulos humanos dos campos numéricos técnicos (os da ficha), para os avisos.</summary>
    private static readonly IReadOnlyDictionary<string, string> TechnicalNumericLabels = new Dictionary<string, string>
    {
        ["nominal_weight"] = "Peso nominal (g)",
        ["volume_puncao"] = "Punção (cm³)",
        ["volume_marisa"] = "Marisa (cm³)",
        ["diametro_gargalo"] = "Diâmetro do gargalo (mm)",
        ["tp_diametro"] = "Tampão — diâmetro (mm)",
        ["tp_raio"] = "Tampão — raio (mm)",
        ["tp_sagitta"] = "Tampão — calote (mm)"
    };

    private const string UnreadablePrefix = "ilegivel_";

    public IReadOnlyDictionary<string, string?>? TechnicalDraft { get; private set; }

    public string TechnicalValue(string name, decimal? stored) => TechnicalDraft is { } draft
        ? ParseOptionalDecimal(draft[name])?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? draft[name] ?? string.Empty
        : stored?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    public string TechnicalProcessValue(CmProcess? stored) => TechnicalDraft is { } draft
        ? draft["process_token"] ?? string.Empty
        : stored is { } process ? process == CmProcess.Nnpb ? "NNPB" : "PS" : string.Empty;

    private readonly IToolSheetQuery _sheetQuery;
    private readonly IMachineListQuery _machineListQuery;
    private readonly IEditToolFactsHandler _editFacts;
    private readonly IEditToolTechnicalValuesHandler _editTechnical;
    private readonly IDuplicateToolHandler _duplicate;
    private readonly IDeleteToolHandler _delete;
    private readonly ShellStateFactory _shellState;

    public FichaModel(
        IToolSheetQuery sheetQuery,
        IMachineListQuery machineListQuery,
        IEditToolFactsHandler editFacts,
        IEditToolTechnicalValuesHandler editTechnical,
        IDuplicateToolHandler duplicate,
        IDeleteToolHandler delete,
        ShellStateFactory shellState)
    {
        _sheetQuery = sheetQuery;
        _machineListQuery = machineListQuery;
        _editFacts = editFacts;
        _editTechnical = editTechnical;
        _duplicate = duplicate;
        _delete = delete;
        _shellState = shellState;
    }

    /// <summary>tool_id pedido (?tool_id) — a identidade canónica no URL.</summary>
    public string? RequestedToolId { get; private set; }

    /// <summary>A ficha da ferramenta (null quando o tool_id não existe ou é a DUMMY).</summary>
    public ToolSheetReadModel? Sheet { get; private set; }

    /// <summary>True quando foi pedido um tool_id que não resolve: aviso limpo, nada inventado.</summary>
    public bool SheetRequestedNotFound { get; private set; }

    /// <summary>Máquinas persistidas (substituição integral de factos).</summary>
    public IReadOnlyList<MachineOption> Machines { get; private set; } = Array.Empty<MachineOption>();

    /// <summary>True quando a ficha abre com o aviso+confirmação de eliminação (?confirm_delete=1).</summary>
    public bool ConfirmDelete { get; private set; }


    /// <summary>Nota de criação (PRG do registo/duplicação): a ferramenta foi registada.</summary>
    /// <summary>Nota de criação do último POST (?created=1): a ficha abre com o facto gravado.</summary>
    public bool CreatedNote { get; private set; }

    /// <summary>
    /// A nota honesta do GET-OR-CREATE (?existed=1): a ferramenta JÁ
    /// existia com os mesmos (tipo, referência, lote) — nada foi
    /// duplicado; as máquinas do form foram acrescentadas às ligações
    /// dela (o Owner: «certas referencias podem trabalhar em mais que
    /// uma maquina»).
    /// </summary>
    public bool ExistedNote { get; private set; }

    /// <summary>Nota avisadora de formato de referência (PRG da criação; recalculada da ficha em visitas seguintes).</summary>
    public bool FormatAdvisoryNote { get; private set; }

    /// <summary>Aviso (?sem_lote=1): o lote chegou vazio e a ferramenta gravou «s/lote» (beta.63).</summary>
    public bool LotDefaultedWarning { get; private set; }

    /// <summary>Aviso (?quantidade_ilegivel=…): a quantidade escrita não foi lida e ficou de fora (beta.63).</summary>
    public string? UnreadableQuantity { get; private set; }

    /// <summary>Aviso (?lote_existente=1): a duplicação abriu a ferramenta que já tinha esse lote (beta.63).</summary>
    public bool DuplicateExistedWarning { get; private set; }

    /// <summary>Campos técnicos não lidos no último POST (rótulo, valor escrito): ficou o valor anterior.</summary>
    public IReadOnlyList<(string Label, string Typed)> UnreadableTechnical { get; private set; } = Array.Empty<(string, string)>();

    /// <summary>
    /// Regresso ao fluxo contextual (?continuar=…) quando a criação tinha
    /// avisos: a ficha mostra o aviso e o link para continuar. Só local.
    /// </summary>
    public string? ContinueUrl { get; private set; }

    /// <summary>Na recusa LotAlreadyExists da edição: a ferramenta que já tem esses factos.</summary>
    public string? ExistingToolId { get; private set; }

    public string LotDefaultedMessage => ExistedNote
        ? $"Não foi indicado lote: usou-se «{ToolRegistryConstants.NoLot}». Já existia uma ferramenta com este tipo, " +
          $"referência e «{ToolRegistryConstants.NoLot}», por isso abriu-se essa em vez de criar outra. " +
          "Se souber o lote real, corrija-o em Editar."
        : $"Não foi indicado lote: a ferramenta ficou gravada com «{ToolRegistryConstants.NoLot}». " +
          "Se souber o lote real, corrija-o em Editar.";

    // Na criação (created/existed) a quantidade ficou de fora; na edição
    // manteve-se a quantidade que já estava gravada.
    public string UnreadableQuantityMessage => CreatedNote || ExistedNote
        ? $"A quantidade «{UnreadableQuantity}» não é um número inteiro e ficou de fora; o resto foi gravado. " +
          "Pode indicá-la em Editar (número inteiro, ex. 120)."
        : $"A quantidade «{UnreadableQuantity}» não é um número inteiro: manteve-se a quantidade anterior; " +
          "os outros factos foram gravados. Use um número inteiro, ex. 120.";

    public string UnreadableTechnicalMessage =>
        "Não foi possível ler: " +
        string.Join(", ", UnreadableTechnical.Select(f => $"{f.Label} «{f.Typed}»")) +
        ". Nesse campo manteve-se o valor anterior; os outros valores técnicos foram gravados. " +
        "Use vírgula ou ponto decimal, sem separador de milhares (ex. 70,10 ou 70.10).";

    /// <summary>Recusa tipada do último POST de factos, transportada por query (PRG).</summary>
    public EditToolFactsResult? FactsRefusal { get; private set; }

    public string FactsRefusalMessage => FactsRefusal switch
    {
        EditToolFactsResult.ToolNotFound =>
            "A ferramenta indicada não existe no registo (ou é a linha de removidos). Nenhum facto foi alterado.",
        EditToolFactsResult.UnknownTypeToken =>
            "Tipo desconhecido. Use CM, BQ ou MF. Nenhum facto foi alterado.",
        EditToolFactsResult.MissingReference =>
            "A referência não pode ficar em branco — é a identidade humana do registo. Nenhum facto foi alterado.",
        // Já não é devolvido (beta.63: o lote vazio grava «s/lote» com aviso); fica para links antigos.
        EditToolFactsResult.MissingLot =>
            "O lote não pode ficar em branco — um lote diferente é uma ferramenta diferente (duplique, não edite). " +
            "Nenhum facto foi alterado.",
        EditToolFactsResult.MachineNotFound =>
            "Uma das máquinas indicadas não existe no registo. Nenhum facto foi alterado.",
        EditToolFactsResult.LotAlreadyExists =>
            "Já existe outra ferramenta com este tipo, referência e lote. Nenhum facto foi alterado.",
        EditToolFactsResult.InvalidLotQuantity =>
            "Use uma quantidade inteira igual ou superior a 0. Nenhum facto foi alterado.",
        _ => string.Empty
    };

    /// <summary>Recusa tipada do último POST de valores técnicos, transportada por query (PRG).</summary>
    public EditToolTechnicalValuesResult? TechnicalRefusal { get; private set; }

    /// <summary>A informação técnica fica fechada até o utilizador a abrir (?tecnica=1 ou depois de gravar).</summary>
    [BindProperty(SupportsGet = true, Name = "tecnica")] public string? ShowTechnicalParam { get; set; }

    public bool ShowTechnical => ShowTechnicalParam == "1" || TechnicalRefusal is not null;

    public string TechnicalRefusalMessage => TechnicalRefusal switch
    {
        EditToolTechnicalValuesResult.ToolNotFound =>
            "A ferramenta indicada não existe no registo. Nenhum valor técnico foi alterado.",
        EditToolTechnicalValuesResult.UnknownProcessToken =>
            "Processo desconhecido. Use NNPB ou PS (ou deixe vazio para limpar). Nenhum valor técnico foi alterado.",
        EditToolTechnicalValuesResult.InvalidNumericValue =>
            "Há um valor técnico que não é um número válido. Use vírgula ou ponto decimal, sem separador de milhares " +
            "(ex. 70,10 ou 70.10). " +
            "Os valores introduzidos foram mantidos para correção. Nenhum valor técnico foi alterado.",
        _ => string.Empty
    };

    /// <summary>Recusa tipada do último POST de duplicação, transportada por query (PRG).</summary>
    public DuplicateToolResult? DuplicateRefusal { get; private set; }

    public string DuplicateRefusalMessage => DuplicateRefusal switch
    {
        DuplicateToolResult.ToolNotFound =>
            "A ferramenta de origem não existe no registo. Nenhuma duplicação foi feita.",
        DuplicateToolResult.MissingNewLot =>
            "O lote novo é obrigatório — a duplicação cria a ferramenta do lote real novo e nunca altera a fonte.",
        // Já não é devolvido (beta.63: abre a ferramenta existente com aviso); fica para links antigos.
        DuplicateToolResult.LotAlreadyExists =>
            "Esse lote já está registado para esta referência. Nenhuma duplicação foi feita.",
        _ => string.Empty
    };

    /// <summary>Recusa tipada do último POST de eliminação, transportada por query (PRG).</summary>
    public DeleteToolResult? DeleteRefusal { get; private set; }

    public string DeleteRefusalMessage => DeleteRefusal switch
    {
        DeleteToolResult.ToolNotFound =>
            "A ferramenta indicada não existe no registo. Nenhuma eliminação foi feita.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "tool_id")] string? toolId,
        [FromQuery(Name = "created")] string? created,
        [FromQuery(Name = "existed")] string? existed,
        [FromQuery(Name = "advisory")] string? advisory,
        [FromQuery(Name = "facts_refusal")] string? factsRefusal,
        [FromQuery(Name = "technical_refusal")] string? technicalRefusal,
        [FromQuery(Name = "duplicate_refusal")] string? duplicateRefusal,
        [FromQuery(Name = "delete_refusal")] string? deleteRefusal,
        [FromQuery(Name = "confirm_delete")] string? confirmDelete,
        [FromQuery(Name = "sem_lote")] string? semLote,
        [FromQuery(Name = "quantidade_ilegivel")] string? quantidadeIlegivel,
        [FromQuery(Name = "lote_existente")] string? loteExistente,
        [FromQuery(Name = "existing_tool_id")] string? existingToolId,
        [FromQuery(Name = "continuar")] string? continuar)
    {
        ViewData["Title"] = "Ficha de ferramenta — Ferramentas";
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = "registo",
            Functions = new[]
            {
                new FunctionPresentation("registo", "Registo", Url.Page("/Ferramentas/Index"), null, false, true, true)
            }
        };

        // Recusas tipadas do POST anterior, transportadas por query (PRG) com
        // o vocabulário fechado dos enums.
        if (factsRefusal is not null
            && Enum.TryParse(factsRefusal, out EditToolFactsResult parsedFacts)
            && parsedFacts != EditToolFactsResult.Success)
        {
            FactsRefusal = parsedFacts;
        }

        if (technicalRefusal is not null
            && Enum.TryParse(technicalRefusal, out EditToolTechnicalValuesResult parsedTechnical)
            && parsedTechnical != EditToolTechnicalValuesResult.Success)
        {
            TechnicalRefusal = parsedTechnical;
            TechnicalDraft = TechnicalFieldNames.ToDictionary(name => name,
                name => (string?)Request.Query[name].ToString());
        }

        if (duplicateRefusal is not null
            && Enum.TryParse(duplicateRefusal, out DuplicateToolResult parsedDuplicate)
            && parsedDuplicate != DuplicateToolResult.Success)
        {
            DuplicateRefusal = parsedDuplicate;
        }

        if (deleteRefusal is not null
            && Enum.TryParse(deleteRefusal, out DeleteToolResult parsedDelete)
            && parsedDelete != DeleteToolResult.Success)
        {
            DeleteRefusal = parsedDelete;
        }

        CreatedNote = created == "1";
        ExistedNote = existed == "1";

        // Avisos do POST anterior (beta.63): o trabalho foi gravado e o aviso
        // diz o que ficou de fora.
        LotDefaultedWarning = semLote == "1";
        UnreadableQuantity = IndexModel.Sanitize(quantidadeIlegivel);
        DuplicateExistedWarning = loteExistente == "1";
        UnreadableTechnical = TechnicalNumericLabels
            .Select(field => (Label: field.Value, Typed: Request.Query[UnreadablePrefix + field.Key].ToString().Trim()))
            .Where(field => field.Typed.Length > 0)
            .ToList();
        ExistingToolId = FactsRefusal == EditToolFactsResult.LotAlreadyExists ? IndexModel.Sanitize(existingToolId) : null;
        ContinueUrl = !string.IsNullOrEmpty(continuar) && Url.IsLocalUrl(continuar) && !continuar.Contains('\\')
            ? continuar
            : null;

        RequestedToolId = IndexModel.Sanitize(toolId);
        Sheet = RequestedToolId is null
            ? null
            : await _sheetQuery.FindByToolIdAsync(RequestedToolId, HttpContext.RequestAborted);

        SheetRequestedNotFound = RequestedToolId is not null && Sheet is null;

        // A nota avisadora de formato: transportada pelo PRG da criação e
        // RECALCULADA da ficha em todas as visitas seguintes (registo
        // truthful — o valor desvia-se do formato canónico mas está
        // registado na mesma).
        FormatAdvisoryNote = Sheet is { } sheet
            ? ReferenceFormatAdvisoryFor(sheet.Type, sheet.Reference)
            : advisory == "1";

        Machines = await _machineListQuery.ListAsync(HttpContext.RequestAborted);

        // ?confirm_delete=1 abre logo a janela «Quer mesmo eliminar…?».
        ConfirmDelete = confirmDelete == "1";

        return Page();
    }

    /// <summary>
    /// POST nativo: substituir integralmente os FACTOS da ferramenta
    /// (full-replace no MESMO tool_id — Type/Reference/Lot/AssociadaMF/
    /// QuantidadeBQ/máquinas). A identidade nunca muda com a edição; um
    /// lote diferente é uma ferramenta diferente (duplicar, não editar).
    /// Sucesso volta à ficha por redirect (PRG); recusa tipada por query.
    /// </summary>
    public async Task<IActionResult> OnPostEditFactsAsync(
        [FromForm(Name = "tool_id")] string? toolId,
        [FromForm(Name = "type_token")] string? typeToken,
        [FromForm(Name = "reference")] string? reference,
        [FromForm(Name = "lot")] string? lot,
        [FromForm(Name = "mf_reference")] string? mfReference,
        [FromForm(Name = "bq_quantity")] string? bqQuantity,
        [FromForm(Name = "machine_ids")] List<string>? machineIds)
    {
        var canonicalToolId = IndexModel.Sanitize(toolId) ?? string.Empty;

        // beta.63: a quantidade ilegível já não recusa a edição — mantém-se a
        // quantidade gravada, o resto grava e a ficha avisa. A negativa
        // continua recusada (pelo handler).
        var typedQuantity = IndexModel.Sanitize(bqQuantity);
        var parsedQuantity = IndexModel.ParseOptionalInt(bqQuantity);
        var unreadableQuantity = typedQuantity is not null && parsedQuantity is null;
        if (unreadableQuantity && !string.IsNullOrEmpty(canonicalToolId))
        {
            parsedQuantity = (await _sheetQuery.FindByToolIdAsync(canonicalToolId, HttpContext.RequestAborted))?.BqAccountedQuantity;
        }

        var outcome = await _editFacts.HandleAsync(
            new EditToolFacts(
                canonicalToolId,
                IndexModel.Sanitize(typeToken) ?? string.Empty,
                (reference ?? string.Empty).ToUpperInvariant(),
                lot ?? string.Empty,
                mfReference?.ToUpperInvariant(),
                parsedQuantity,
                machineIds?
                    .Select(IndexModel.Sanitize)
                    .Where(id => id is not null)
                    .Select(id => int.TryParse(id, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
                    .Distinct()
                    .ToList()),
            HttpContext.RequestAborted);

        if (outcome.Result == EditToolFactsResult.Success)
        {
            return RedirectToPage("/Ferramentas/Ficha", new
            {
                tool_id = canonicalToolId,
                sem_lote = outcome.Tool?.LotDefaulted == true ? "1" : null,
                quantidade_ilegivel = unreadableQuantity ? typedQuantity : null
            });
        }

        return RedirectToPage("/Ferramentas/Ficha", new
        {
            tool_id = canonicalToolId,
            facts_refusal = outcome.Result.ToString(),
            existing_tool_id = outcome.ExistingToolId
        });
    }

    /// <summary>
    /// POST nativo: substituir integralmente os VALORES TÉCNICOS da
    /// ferramenta (todas as colunas do contexto técnico — processo CM,
    /// pesos/volumes/gargalo e a geometria TP do desenho). O valor vazio é
    /// NULL (o estado «em falta» é válido); a linha é criada quando ainda
    /// não existe; grava sempre no tool_id owner. O tampão regista só o
    /// diâmetro, o raio e a calote (beta.37); R base, R esfera e volume já
    /// gravados ficam intactos e o volume é calculado a partir de D e calote.
    /// </summary>
    public async Task<IActionResult> OnPostEditTechnicalAsync(
        [FromForm(Name = "tool_id")] string? toolId,
        [FromForm(Name = "process_token")] string? processToken,
        [FromForm(Name = "nominal_weight")] string? nominalWeight,
        [FromForm(Name = "volume_puncao")] string? volumePuncao,
        [FromForm(Name = "volume_marisa")] string? volumeMarisa,
        [FromForm(Name = "diametro_gargalo")] string? diametroGargalo,
        [FromForm(Name = "tp_diametro")] string? tpDiametro,
        [FromForm(Name = "tp_raio")] string? tpRaio,
        [FromForm(Name = "tp_sagitta")] string? tpSagitta,
        [FromForm(Name = "continuar")] string? continuar = null)
    {
        var canonicalToolId = IndexModel.Sanitize(toolId) ?? string.Empty;

        var submitted = new Dictionary<string, string?>
        {
            ["process_token"] = processToken, ["nominal_weight"] = nominalWeight,
            ["volume_puncao"] = volumePuncao, ["volume_marisa"] = volumeMarisa,
            ["diametro_gargalo"] = diametroGargalo, ["tp_diametro"] = tpDiametro,
            ["tp_raio"] = tpRaio, ["tp_sagitta"] = tpSagitta
        };

        // beta.63 (A9): um campo ilegível já não recusa os outros. Os
        // legíveis gravam; no ilegível fica o valor que já estava gravado
        // (nunca é apagado nem inventado) e a ficha avisa qual foi.
        var unreadable = submitted
            .Where(field => field.Key != "process_token"
                && !string.IsNullOrWhiteSpace(field.Value) && ParseOptionalDecimal(field.Value) is null)
            .ToDictionary(field => field.Key, field => field.Value!.Trim());
        var sheet = unreadable.Count == 0 || string.IsNullOrEmpty(canonicalToolId)
            ? null
            : await _sheetQuery.FindByToolIdAsync(canonicalToolId, HttpContext.RequestAborted);
        var stored = sheet?.Technical;
        decimal? Value(string name, decimal? previous) =>
            unreadable.ContainsKey(name) ? previous : ParseOptionalDecimal(submitted[name]);

        var outcome = await _editTechnical.HandleAsync(
            new EditToolTechnicalValues(
                canonicalToolId,
                IndexModel.Sanitize(processToken),
                Value("nominal_weight", stored?.NominalWeightGrams),
                Value("volume_puncao", stored?.VolumePuncaoCm3),
                Value("volume_marisa", stored?.VolumeMarisaCm3),
                Value("diametro_gargalo", stored?.DiametroGargaloMm),
                Value("tp_diametro", stored?.TpDiametroMm),
                Value("tp_raio", stored?.TpRaioMm),
                Value("tp_sagitta", stored?.TpSagittaMm)),
            HttpContext.RequestAborted);

        if (outcome.Result == EditToolTechnicalValuesResult.Success)
        {
            var routeValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary
            {
                ["tool_id"] = canonicalToolId, ["tecnica"] = "1"
            };
            // Aberta a partir de um fluxo (por exemplo, os Pegamentos sem
            // nominal do gargalo): gravado sem avisos, volta-se a ele.
            var regresso = !string.IsNullOrEmpty(continuar) && Url.IsLocalUrl(continuar) && !continuar.Contains('\\')
                ? continuar
                : null;
            if (regresso is not null && unreadable.Count == 0)
                return LocalRedirect(regresso);
            if (regresso is not null)
                routeValues["continuar"] = regresso;
            // Só se avisa dos campos que este tipo de ferramenta grava (os
            // outros não aparecem na ficha e o handler ignora-os).
            foreach (var field in unreadable.Where(field => AppliesTo(sheet?.Type, field.Key)))
                routeValues[UnreadablePrefix + field.Key] = field.Value;
            return RedirectToPage("/Ferramentas/Ficha", routeValues);
        }

        return TechnicalRefused(canonicalToolId, outcome.Result, submitted);
    }

    /// <summary>
    /// POST nativo: duplicar a ferramenta para um lote novo (criação
    /// assistida de OUTRA ferramenta real — os factos e valores técnicos
    /// copiados são defaults EDITÁVEIS, nunca herança imutável; a fonte
    /// fica intacta). O novo tool_id é mintado pelo backend; a ficha da
    /// nova ferramenta abre com a nota de criação.
    /// </summary>
    public async Task<IActionResult> OnPostDuplicateAsync(
        [FromForm(Name = "tool_id")] string? toolId,
        [FromForm(Name = "new_lot")] string? newLot)
    {
        var canonicalToolId = IndexModel.Sanitize(toolId) ?? string.Empty;

        var outcome = await _duplicate.HandleAsync(
            new DuplicateTool(canonicalToolId, newLot ?? string.Empty),
            HttpContext.RequestAborted);

        // beta.63 (A10): o lote já registado abre a ferramenta existente com aviso.
        if (outcome.Result == DuplicateToolResult.Success && outcome.Tool is { AlreadyExisted: true } existing)
        {
            return RedirectToPage("/Ferramentas/Ficha", new
            {
                tool_id = existing.ToolId,
                lote_existente = "1"
            });
        }

        if (outcome.Result == DuplicateToolResult.Success && outcome.Tool is { } created)
        {
            return RedirectToPage("/Ferramentas/Ficha", new
            {
                tool_id = created.ToolId,
                created = "1",
                advisory = created.ReferenceFormatAdvisory ? "1" : null
            });
        }

        return RedirectToPage("/Ferramentas/Ficha", new
        {
            tool_id = canonicalToolId,
            duplicate_refusal = outcome.Result.ToString()
        });
    }

    /// <summary>
    /// POST nativo: eliminar a ferramenta — a confirmação EXPLÍCITA do
    /// aviso (?confirm_delete=1, POST antiforgery). O handler executa a
    /// transação canónica (backfill congelados → reatribuição dos
    /// registos de consumo à linha de removidos → âncora dos traces não
    /// resolvidos a NULL → eliminação física); nenhum histórico é
    /// destruído nem reescrito. Sucesso volta à lista por redirect (PRG)
    /// com a nota de eliminação; recusa tipada por query.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        [FromForm(Name = "tool_id")] string? toolId)
    {
        var canonicalToolId = IndexModel.Sanitize(toolId) ?? string.Empty;

        var outcome = await _delete.HandleAsync(
            new DeleteTool(canonicalToolId),
            HttpContext.RequestAborted);

        if (outcome.Result == DeleteToolResult.Success)
        {
            return RedirectToPage("/Ferramentas/Index", new { deleted = 1 });
        }

        return RedirectToPage("/Ferramentas/Ficha", new
        {
            tool_id = canonicalToolId,
            delete_refusal = outcome.Result.ToString()
        });
    }

    /// <summary>
    /// Nota avisadora de formato (NUNCA recusa): CM = dígitos (5447);
    /// BQ = 1 letra maiúscula + 3 dígitos (T194/C142/Y232). TRUE quando o
    /// valor registado se desvia do formato canónico — o valor fica
    /// registado na mesma (a mesma regra do handler de criação).
    /// </summary>
    internal static bool ReferenceFormatAdvisoryFor(ToolType type, string reference) => type switch
    {
        ToolType.Cm => !System.Text.RegularExpressions.Regex.IsMatch(reference, @"^\d+$"),
        ToolType.Bq => !System.Text.RegularExpressions.Regex.IsMatch(reference, @"^[A-Z]\d{3}$"),
        _ => false
    };

    /// <summary>Os campos técnicos que cada tipo grava (CM: peso, punção, tampão; BQ: marisa; todos: gargalo).</summary>
    private static bool AppliesTo(ToolType? type, string field) => field switch
    {
        "diametro_gargalo" => true,
        "volume_marisa" => type == ToolType.Bq,
        _ => type == ToolType.Cm
    };

    internal static string TokenTypeFor(ToolType type) => type switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        ToolType.Bq => "BQ",
        _ => type.ToString()
    };

    private IActionResult TechnicalRefused(string toolId, EditToolTechnicalValuesResult refusal,
        IReadOnlyDictionary<string, string?> submitted)
    {
        var routeValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            ["tool_id"] = toolId, ["technical_refusal"] = refusal.ToString()
        };
        foreach (var field in submitted)
            routeValues[field.Key] = field.Value ?? string.Empty;
        return RedirectToPage("/Ferramentas/Ficha", routeValues);
    }

    private static decimal? ParseOptionalDecimal(string? value) => MeasurementDecimal.Parse(value);
}
