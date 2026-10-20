using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// «Eliminar Job On» (Diogo, 19/10/2026, D2 / AV-JO-04): o POST da
/// confirmação da ficha, para quem tem o Job On. Fica fora da página do Job On
/// (regra do código, ponto 8). Eliminada, ou já eliminada por um envio
/// anterior, volta ao calendário com a mensagem.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate)]
public sealed class EliminarModel : PageModel
{
    private readonly IDeleteJobOnHandler _delete;

    public EliminarModel(IDeleteJobOnHandler delete)
    {
        _delete = delete;
    }

    public IActionResult OnGet() => RedirectToPage("/JobOn/Index");

    public async Task<IActionResult> OnPostAsync([FromForm(Name = "jobon_id")] string? jobOnId)
    {
        if (!int.TryParse(jobOnId?.Trim(), out var id))
        {
            return RedirectToPage("/JobOn/Index");
        }

        await _delete.HandleAsync(new DeleteJobOn(id, IndexModel.ActorName(User)), HttpContext.RequestAborted);
        return RedirectToPage("/JobOn/Index", new { eliminado = "1" });
    }

}
