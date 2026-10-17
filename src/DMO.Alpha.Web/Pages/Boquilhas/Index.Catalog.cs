using DMO.Alpha.Core.Tools;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

public sealed partial class IndexModel
{
    private readonly ICreateToolHandler _createTool;

    [BindProperty] public BqToolDraft Draft { get; set; } = new();
    [BindProperty(SupportsGet = true, Name = "q")] public string? CatalogSearch { get; set; }
    [BindProperty(SupportsGet = true, Name = "machine_id")] public int? CatalogMachineId { get; set; }
    [BindProperty(SupportsGet = true, Name = "pagina")] public int CatalogPageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "por_pagina")] public int CatalogPageSize { get; set; } = 10;
    [BindProperty(SupportsGet = true, Name = "saved")] public string? CatalogSaved { get; set; }
    public IReadOnlyList<ToolCandidate> CatalogTools { get; private set; } = [];
    public IReadOnlyList<MachineOption> CatalogMachines { get; private set; } = [];
    public int CatalogCount { get; private set; }
    public int CatalogPages => Math.Max(1, (int)Math.Ceiling(CatalogCount / (double)CatalogPageSize));
    public string? ToolCreationError { get; private set; }
    /// <summary>A lista só é consultada depois de procurar (Diogo, 17/10/2026: sem lista até procurar).</summary>
    public bool CatalogSearched => !string.IsNullOrWhiteSpace(CatalogSearch) || CatalogMachineId is not null;

    public string? CatalogLink(int page, string? toolId = null, string? saved = null) => Url.Page("/Boquilhas/Index", new
    {
        ficheiros = 1, q = CatalogSearch, machine_id = CatalogMachineId,
        pagina = page, por_pagina = CatalogPageSize, ferramenta = toolId, saved
    });

    private async Task LoadCatalogAsync()
    {
        CatalogMachines = await _db.Machines.AsNoTracking().OrderBy(m => m.Code)
            .Select(m => new MachineOption(m.Id, m.Code)).ToListAsync(HttpContext.RequestAborted);
        if (!CatalogSearched) return;
        var page = await _toolCandidates.FindPageAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, MachineId: CatalogMachineId, Search: CatalogSearch),
            CatalogPageNumber, CatalogPageSize, HttpContext.RequestAborted);
        CatalogTools = page.Items;
        CatalogCount = page.TotalCount;
        CatalogPageNumber = page.PageNumber;
        CatalogPageSize = page.PageSize;
    }

    public async Task<IActionResult> OnPostCreateToolAsync()
    {
        // The catalog is another entry to the canonical Tool handler, not a second store.
        int? quantity = null;
        if (!string.IsNullOrWhiteSpace(Draft.Quantity))
        {
            if (int.TryParse(Draft.Quantity, out var parsed) && parsed >= 0) quantity = parsed;
            else ToolCreationError = "Use uma quantidade inteira igual ou superior a 0. O rascunho foi mantido.";
        }
        if (!ModelState.IsValid)
            ToolCreationError ??= "Confirme os valores e as máquinas selecionadas. O rascunho foi mantido.";
        // beta.63: o handler canónico grava o lote vazio como «s/lote» (o
        // Ferramentas avisa na ficha). Este catálogo ainda não tem esse aviso,
        // por isso mantém o pedido do lote que já fazia.
        if (string.IsNullOrWhiteSpace(Draft.Lot))
            ToolCreationError ??= "Indique o lote da boquilha. O rascunho foi mantido.";

        if (ToolCreationError is null)
        {
            var outcome = await _createTool.HandleAsync(new CreateTool("BQ", Draft.Reference ?? "",
                Draft.Lot ?? "", BqAccountedQuantity: quantity, MachineIds: Draft.MachineIds,
                UseLotQuantityDefault: Draft.QuantityManual != "1"), HttpContext.RequestAborted);
            if (outcome.Result == CreateToolResult.Success && outcome.Tool is { } tool)
                return Redirect(CatalogLink(CatalogPageNumber, tool.ToolId, tool.AlreadyExisted ? "existing" : "created") + "#bq-tool-sheet");
            ToolCreationError = outcome.Result switch
            {
                CreateToolResult.MissingReference => "Indique a referência da boquilha. O rascunho foi mantido.",
                CreateToolResult.MissingLot => "Indique o lote da boquilha. O rascunho foi mantido.",
                CreateToolResult.MachineNotFound => "Uma das máquinas já não existe. Reveja as compatibilidades; o rascunho foi mantido.",
                _ => "Não foi possível guardar a boquilha. Reveja os dados; o rascunho foi mantido."
            };
        }

        ShowFiles = true;
        ViewData["Title"] = "Boquilhas";
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new("boquilhas", "Boquilhas"))) with
        {
            ActiveFunctionKey = "ficheiros", Functions = ShellStateFactory.BoquilhasFunctions("ficheiros")
        };
        Currents = await _currentQuery.ListAsync(HttpContext.RequestAborted);
        await LoadCatalogAsync();
        return Page();
    }
}

public sealed class BqToolDraft
{
    public string? Reference { get; set; }
    public string? Lot { get; set; }
    public string? Quantity { get; set; }
    public string? QuantityManual { get; set; }
    public List<int> MachineIds { get; set; } = [];
}
