using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

/// <summary>
/// Sair = sair. The GET terminates the session and returns to the login
/// immediately — there is no intermediate page (2026-10-11, Owner: «quando
/// tento sair ou voltar leva me para uma pagina intermedia, que nao precisa
/// de existir»). The POST handler remains for form-based callers.
/// </summary>
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect("/login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect("/login");
    }
}
