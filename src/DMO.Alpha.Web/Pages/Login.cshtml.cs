using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

// O token do formulário verifica-se no OnPost (deep sweep B-admin-4): com
// dois separadores no login, entrar no primeiro deixava o token do segundo
// sem valor e o envio dava «HTTP ERROR 400», que o F5 repetia.
[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    private readonly DmoSignInService _signInService;
    private readonly InstalacaoInicial _instalacao;

    public LoginModel(DmoSignInService signInService, InstalacaoInicial instalacao)
    {
        _signInService = signInService;
        _instalacao = instalacao;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Identifier { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Acabou de se fazer a primeira instalação (/instalar).</summary>
    public bool Instalado { get; set; }

    public async Task<IActionResult> OnGetAsync(bool instalado = false)
    {
        // Base sem ADMIN com o login próprio: ninguém consegue entrar, por
        // isso o login leva à primeira instalação.
        if (await _instalacao.PendenteAsync(HttpContext.RequestAborted))
        {
            return Redirect("/instalar");
        }

        Instalado = instalado;
        // O LOGIN ENTRA LOGO NO PLANEAMENTO (o Owner 2026-10-12: «a pagina
        // que aparece depois do login pode desaparecer e entrar logo com o
        // user no planeamento» — a home com o [Continuar] roubava um clique
        // a cada entrada). O returnUrl explícito mantém-se (os fluxos
        // forçados: a mudança de palavra-passe, os deep-links).
        ReturnUrl ??= "/JobOn";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        [FromServices] IAntiforgery antiforgery,
        string identifier,
        string password,
        string? returnUrl)
    {
        Identifier = identifier;
        ReturnUrl = returnUrl ?? "/JobOn";

        // Com sessão, o token só não serve porque se entrou noutro separador
        // (o cookie da sessão não vem de outro site, por ser SameSite=Lax):
        // a entrada segue. Sem sessão, o formulário volta com um token novo.
        if (User.Identity?.IsAuthenticated != true
            && !await antiforgery.IsRequestValidAsync(HttpContext))
        {
            ErrorMessage = "A página de entrada expirou. Entre de novo.";
            return Page();
        }

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
