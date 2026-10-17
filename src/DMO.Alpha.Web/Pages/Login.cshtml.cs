using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

public class LoginModel : PageModel
{
    private readonly DmoSignInService _signInService;

    public LoginModel(DmoSignInService signInService)
    {
        _signInService = signInService;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Identifier { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // O LOGIN ENTRA LOGO NO PLANEAMENTO (o Owner 2026-10-12: «a pagina
        // que aparece depois do login pode desaparecer e entrar logo com o
        // user no planeamento» — a home com o [Continuar] roubava um clique
        // a cada entrada). O returnUrl explícito mantém-se (os fluxos
        // forçados: a mudança de palavra-passe, os deep-links).
        ReturnUrl ??= "/JobOn";
    }

    public async Task<IActionResult> OnPostAsync(string identifier, string password, string? returnUrl)
    {
        Identifier = identifier;
        ReturnUrl = returnUrl ?? "/JobOn";

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Indique o utilizador e a palavra-passe.";
            return Page();
        }

        var result = await _signInService.SignInAsync(identifier, password, returnUrl);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        return Redirect(result.RedirectUrl!);
    }
}
