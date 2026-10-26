using DMO.Alpha.Core.Tools;
using DMO.Alpha.Core.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Boquilhas;

public sealed partial class IndexModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? CatalogSearch { get; set; }
    [BindProperty(SupportsGet = true, Name = "machine_id")] public int? CatalogMachineId { get; set; }
    [BindProperty(SupportsGet = true, Name = "pagina")] public int CatalogPageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "por_pagina")] public int CatalogPageSize { get; set; } = 10;
    [BindProperty(SupportsGet = true, Name = "saved")] public string? CatalogSaved { get; set; }
    public IReadOnlyList<ToolCandidate> CatalogTools { get; private set; } = [];
    public IReadOnlyList<MachineOption> CatalogMachines { get; private set; } = [];
    public int CatalogCount { get; private set; }
    public int CatalogPages => Math.Max(1, (int)Math.Ceiling(CatalogCount / (double)CatalogPageSize));
    /// <summary>
    /// A lista só é consultada depois de procurar (Diogo, 17/10/2026: «não
    /// deve existir lista até procurar, evitar peso nos carregamentos»): ela
    /// aparece quando se começa a escrever a referência ou o lote. A máquina
    /// só reduz, como o tipo e a máquina nas Ferramentas (Diogo, 18/10/2026:
    /// «escolher BQ carregava as BQ todas»); escolher só a máquina carregava
    /// todas as BQ dela (deep sweep 2, 18/10/2026, F-16). «Criar boquilha»
    /// continua a levar a máquina do filtro.
    /// </summary>
    public bool CatalogSearched => !string.IsNullOrWhiteSpace(CatalogSearch);

    public string? CatalogLink(int page, string? toolId = null, string? saved = null) => Url.Page("/Boquilhas/Index", new
    {
        ficheiros = 1, q = CatalogSearch, machine_id = CatalogMachineId,
        pagina = page, por_pagina = CatalogPageSize, ferramenta = toolId, saved
    });

    /// <summary>
    /// «Criar boquilha»: a página única de registar ferramenta (Diogo,
    /// 18/10/2026: «não vamos inventar mais páginas para ferramentas, 1
    /// página e funciona com todas»), com o tipo BQ e a máquina do filtro já
    /// escolhidos. Registada, volta a este catálogo com a ficha dela aberta.
    /// </summary>
    public string CreateToolLink()
    {
        var query = new List<string> { "criar=1", "type=BQ" };
        if (CatalogMachineId is int machineId)
        {
            query.Add("machine_ids=" + machineId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        query.Add("return=" + Uri.EscapeDataString("/Boquilhas?ficheiros=1&ferramenta={tool_id}&saved=created#bq-tool-sheet"));
        return "/Ferramentas?" + string.Join("&", query);
    }

    /// <summary>
    /// O «Guardar ficha» do formulário curto que saiu (beta.234), numa página
    /// aberta antes da atualização: não grava nada e abre o registo das
    /// Ferramentas, em vez de um erro.
    /// </summary>
    public IActionResult OnPostCreateTool() => LocalRedirect(CreateToolLink());

    private async Task LoadCatalogAsync()
    {
        CatalogMachines = await _machineList.ListAsync(HttpContext.RequestAborted);
        if (!CatalogSearched) return;
        var page = await _toolCandidates.FindPageAsync(
            new ToolCandidateFilter(Type: ToolType.Bq, MachineId: CatalogMachineId, Search: CatalogSearch),
            CatalogPageNumber, CatalogPageSize, HttpContext.RequestAborted);
        CatalogTools = page.Items;
        CatalogCount = page.TotalCount;
        CatalogPageNumber = page.PageNumber;
        CatalogPageSize = page.PageSize;
    }
}
