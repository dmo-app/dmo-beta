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
        await SairDosDoisEsquemasAsync();
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect(posto == "1" ? "/Posto" : "/login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await SairDosDoisEsquemasAsync();
        DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes.Fechar(HttpContext);
        return Redirect("/login");
    }

    // AUTH-B05 (DMO-657): desde que o posto passou a ter o seu próprio
    // esquema de cookie, sair tem de acabar os dois — nunca se sabe por
    // qual dos dois esta sessão entrou sem ler o cookie.
    private async Task SairDosDoisEsquemasAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(DMO.Alpha.Web.Services.PostoAuthentication.Scheme);
    }
}
