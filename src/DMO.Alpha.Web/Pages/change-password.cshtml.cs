using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Runtime;
using Microsoft.AspNetCore.Authentication;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages;

[Authorize]
[TypeFilter(typeof(ExpiredPasswordFormFilter))]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ChangePasswordModel : PageModel
{
    private readonly ICurrentAccountContext _accountContext;
    private readonly MudancaDePalavraPasse _mudanca;

    public ChangePasswordModel(ICurrentAccountContext accountContext, MudancaDePalavraPasse mudanca)
    {
        _accountContext = accountContext;
        _mudanca = mudanca;
    }

    public const int MinimumLength = MudancaDePalavraPasse.MinimumLength;

    public bool RequiresPasswordChange { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet([FromQuery(Name = "form_expired")] string? formExpired)
    {
        RequiresPasswordChange = User.HasClaim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString);
        if (formExpired == "1")
        {
            ErrorMessage = "O formulário expirou ou a sessão mudou; introduza de novo a nova palavra-passe e guarde (a anterior não foi alterada).";
        }
    }

    public async Task<IActionResult> OnPostAsync(string newPassword, string confirmPassword)
    {
        RequiresPasswordChange = User.HasClaim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString);
        if (!_accountContext.Current.IsAuthenticated)
        {
            return Challenge();
        }

        // The refresh re-signs in with the NEW password and lands on the
        // shell home. The home page renders the success notice (TempData)
        // so the operator sees the change was applied — never a silent
        // redirect (2026-10-11, pedido do Owner).
        TempData["PasswordChanged"] = "1";
        var resultado = await _mudanca.MudarAsync(User, pedirAtual: false, atual: null, newPassword, confirmPassword, "/JobOn");
        if (resultado.Erro is not null)
        {
            if (!resultado.Mudou)
            {
                TempData.Remove("PasswordChanged");
            }

            ErrorMessage = resultado.Erro;
            return Page();
        }

        return Redirect(resultado.Destino!);
    }
}
