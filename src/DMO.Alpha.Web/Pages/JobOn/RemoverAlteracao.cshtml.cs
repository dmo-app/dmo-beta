using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// «Remover por engano» (Diogo, 23/10/2026: «ter forma de remover o notice
/// de troca em caso de engano»): o POST da janela de uma linha de «O que
/// mudou» na folha, só para quem tem o Job On criar. A alteração fica
/// gravada, marcada como removida (nada se perde); os valores do Job On não
/// mudam. Volta à folha com «O que mudou» aberto.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate)]
public sealed class RemoverAlteracaoModel(RemoveJobOnChangeHandler remove, CurrentUserCapabilities capabilities) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/JobOn/Index");

    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "jobon_id")] string? jobOnId,
        [FromForm(Name = "change_id")] string? changeId)
    {
        if (!int.TryParse(jobOnId?.Trim(), out var id))
        {
            return RedirectToPage("/JobOn/Index");
        }

        // Um POST forjado de quem só vê o Job On não muda nada.
        if (!await capabilities.HasAsync(DmoCapabilities.JobOnCreate, HttpContext.RequestAborted))
        {
            return Redirect($"/JobOn/Folha?jobon_id={id}");
        }

        if (!int.TryParse(changeId?.Trim(), out var alteracao))
        {
            return Redirect($"/JobOn/Folha?jobon_id={id}&vista=alteracoes");
        }

        var result = await remove.HandleAsync(id, alteracao, IndexModel.ActorName(User), HttpContext.RequestAborted);
        return Redirect($"/JobOn/Folha?jobon_id={id}&vista=alteracoes{(result == RemoveJobOnChangeResult.Removed ? "&removida=1" : "")}");
    }
}
