using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

[Authorize]
public class IndexModel(CurrentUserCapabilities capabilities) : PageModel
{
    // A HOME MORREU (o Owner 2026-10-12: «o problema dessa pagina é que
    // causa user a carregar todas as vezes que entra em continuar» —
    // a home com o [Continuar] roubava um clique a cada entrada): a raiz
    // entra direto no primeiro módulo do template.
    public async Task<IActionResult> OnGetAsync() =>
        Redirect(ModuleEntryAttribute.HomeFor(User, await capabilities.GetAsync(HttpContext.RequestAborted)));
}
