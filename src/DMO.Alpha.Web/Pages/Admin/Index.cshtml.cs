using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin;

// The Admin landing destination is Utilizadores (o Setup está nas Definições → Admin → App).
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Users/Index");
}
