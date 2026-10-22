using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// A lista da Reparação Programada para imprimir e entregar a quem vai
/// retirar do armazém (Diogo, 22/10/2026). É o browser que imprime
/// (window.print e o CSS de impressão): o servidor não gera PDF.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class ReparacaoProgramadaImprimirModel(IReparacaoProgramada programada) : PageModel
{
    public ProgramadaListaDetalhe Lista { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int lista)
    {
        if (await programada.ListaAsync(lista, HttpContext.RequestAborted) is not { } detalhe)
        {
            return NotFound();
        }

        Lista = detalhe;
        return Page();
    }
}
