using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin;

// The Admin landing destination is the Definições page.
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Definicoes/Index");
}
