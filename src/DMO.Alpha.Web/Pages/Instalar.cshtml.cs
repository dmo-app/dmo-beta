using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

/// <summary>
/// A primeira instalação (<see cref="InstalacaoInicial"/>): só com o login
/// próprio e só enquanto a base não tem ADMIN. Feita, manda para o login.
/// </summary>
public class InstalarModel(InstalacaoInicial instalacao) : PageModel
{
    public string? Email { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await instalacao.PendenteAsync(HttpContext.RequestAborted))
        {
            return Redirect("/login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? codigo, string? email, string? palavraPasse, string? confirmar)
    {
        Email = email;
        ErrorMessage = await instalacao.InstalarAsync(codigo, email, palavraPasse, confirmar, HttpContext.RequestAborted);
        if (ErrorMessage == InstalacaoInicial.JaFeita)
        {
            return Redirect("/login");
        }

        return ErrorMessage is null ? Redirect("/login?instalado=true") : Page();
    }
}
