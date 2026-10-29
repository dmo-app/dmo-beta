using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// A Pesquisa do Armazém (DMO-641, Diogo 29/10/2026: «armazem nao tem
/// pesquisa [...] um user quiser pesquisar uma ferramenta nao consegue»):
/// procura uma ferramenta (CM, MF ou BQ) pela referência, pelo lote ou pela
/// posição, onde quer que esteja, e cada linha dos resultados abre o registo
/// dessa ferramenta (a Entrada, ou a Saída se está numa posição). Procura ao
/// escrever; só a zona dos resultados troca.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class PesquisarModel(IArmazemMovimentos movimentos, ShellStateFactory shellState) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Procura { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    public ToolType? Tipo => MovimentosModel.TipoDe(TipoTexto);

    public IReadOnlyList<ArmazemProcuraItem> Encontradas { get; private set; } = [];

    /// <summary>Há texto ou tipo: sem nada a página abre vazia.</summary>
    public bool Pedida => !string.IsNullOrWhiteSpace(Procura);

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "Pesquisar — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "pesquisar",
            Functions = ShellStateFactory.ArmazemFunctions("pesquisar")
        };
        await ProcurarAsync();
        return Page();
    }

    /// <summary>A procura ao escrever (?handler=Resultados): só a zona dos resultados.</summary>
    public async Task<IActionResult> OnGetResultadosAsync()
    {
        await ProcurarAsync();
        Response.Headers["X-Dmo-Region"] = "armPesqResultados";
        return Partial("_ArmazemPesquisaResultados", this);
    }

    private async Task ProcurarAsync()
    {
        if (!Pedida)
        {
            return;
        }

        var encontradas = await movimentos.ProcurarAsync(Procura, HttpContext.RequestAborted);
        Encontradas = Tipo is { } tipo ? encontradas.Where(i => i.Tipo == tipo).ToList() : encontradas;
    }
}
