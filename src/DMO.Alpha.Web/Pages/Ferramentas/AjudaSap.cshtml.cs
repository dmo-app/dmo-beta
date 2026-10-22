using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// A página de ajuda do SAP: como funciona a leitura da pasta (Diogo,
/// 22/10/2026 06:57). Só texto; as opções ficam nas Definições.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class AjudaSapModel(ShellStateFactory shellState) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "Ajuda do SAP — Ferramentas";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = "definicoes",
            Functions = ShellStateFactory.FerramentasFunctions("definicoes")
        };
        return Page();
    }
}
