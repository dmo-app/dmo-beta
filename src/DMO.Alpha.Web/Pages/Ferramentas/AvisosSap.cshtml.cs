using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class AvisosSapModel(IFerramentasAvisosSap avisos) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Ferramentas/Index");

    public async Task<IActionResult> OnPostAsync(string? tool_id, uint? versao, string? campo, string? decisao)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(tool_id) || versao is null || campo is not ("pecas" or "uso") || decisao is not ("sim" or "nao"))
            return BadRequest("Não foi possível ler a decisão. Atualize a ficha e tente outra vez.");
        var resultado = await avisos.DecidirAsync(tool_id, versao.Value, campo, decisao == "sim", HttpContext.RequestAborted);
        if (resultado == DecisaoSapResultado.NaoEncontrado) return NotFound();
        TempData["SapAviso"] = resultado == DecisaoSapResultado.Gravado
            ? decisao == "sim" ? "Valor do SAP guardado." : "Valor da app mantido. Este valor do SAP não volta a avisar."
            : "A ferramenta mudou entretanto. Confirme os valores atuais antes de decidir.";
        return RedirectToPage("/Ferramentas/Ficha", new { tool_id });
    }
}
