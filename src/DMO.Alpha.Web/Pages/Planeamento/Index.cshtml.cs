using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Planeamento;

/// <summary>
/// /Planeamento redireciona ao Job On (302 temporário, para os links antigos
/// continuarem a funcionar). Anónimos vão primeiro ao login.
/// </summary>
[Authorize]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/JobOn/Index");
}
