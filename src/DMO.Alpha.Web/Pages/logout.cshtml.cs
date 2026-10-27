using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

/// <summary>
/// Sair = sair. The GET terminates the session and returns to the login
/// immediately — there is no intermediate page (2026-10-11, Owner: «quando
/// tento sair ou voltar leva me para uma pagina intermedia, que nao precisa
/// de existir»). The POST handler remains for form-based callers.
/// </summary>
[AllowAnonymous]
public class LogoutModel : PageModel
{
    /// <param name="posto">«1» quando a sessão do posto fechou por falta de uso
    /// (dmo-posto.js): volta ao posto para a pessoa seguinte, não ao login.</param>
    public async Task<IActionResult> OnGetAsync(string? posto)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect(posto == "1" ? "/Posto" : "/login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect("/login");
    }
}
