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
    private readonly IAuthenticationProvider _provider;
    private readonly ICurrentAccountContext _accountContext;
    private readonly DmoDbContext _dbContext;
    private readonly DmoSignInService _signInService;
    private readonly IAdminEmailSettings _adminEmailSettings;

    public ChangePasswordModel(
        IAuthenticationProvider provider,
        ICurrentAccountContext accountContext,
        DmoDbContext dbContext,
        DmoSignInService signInService,
        IAdminEmailSettings adminEmailSettings)
    {
        _provider = provider;
        _accountContext = accountContext;
        _dbContext = dbContext;
        _signInService = signInService;
        _adminEmailSettings = adminEmailSettings;
    }

    public const int MinimumLength = 4;

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

        if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ErrorMessage = "Indique e confirme a nova palavra-passe.";
            return Page();
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "As palavras-passe não coincidem.";
            return Page();
        }

        // Mínimo de 4 caracteres (pedido do Diogo de 17/10/2026: sai o
        // mínimo de 6, um PIN de 4 números serve). O Supabase continua a
        // exigir 6; SupabasePassword trata das curtas.
        if (newPassword.Length < MinimumLength)
        {
            ErrorMessage = $"A nova palavra-passe deve ter pelo menos {MinimumLength} caracteres.";
            return Page();
        }

        var actor = _accountContext.Current;
        if (!actor.IsAuthenticated)
        {
            return Challenge();
        }

        var providerUserId = User.FindFirst(DmoClaimTypes.ProviderUserId)?.Value;
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            ErrorMessage = "Não foi possível identificar o utilizador.";
            return Page();
        }

        var changeResult = await _provider.ChangePasswordAsync(providerUserId, newPassword);
        if (changeResult != ChangePasswordResult.Success)
        {
            ErrorMessage = changeResult switch
            {
                ChangePasswordResult.IdentityNotFound =>
                    "A identidade desta conta já não existe no provedor de autenticação.",
                ChangePasswordResult.PolicyViolation =>
                    "A palavra-passe foi recusada pela política de segurança. Escolhe outra.",
                _ => "O provedor de autenticação não pôde alterar a palavra-passe. Tenta novamente em instantes."
            };
            return Page();
        }

        if (actor.IsAdmin)
        {
            // O ADMIN não tem linha em Users: a versão da sessão dele vive
            // nas definições. As outras sessões do ADMIN terminam; a
            // renovação logo a seguir entra já com a versão nova. A palavra-
            // passe já mudou no provider: a versão sobe mesmo que o browser
            // saia a meio, como no caminho dos Users (deep sweep, 18/10/2026).
            await _adminEmailSettings.BumpAdminSessionVersionAsync(CancellationToken.None);
        }
        else
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.OperatorId == actor.OperatorId);

            if (user is not null)
            {
                user.RequiresPasswordChange = false;
                // As outras sessões deste User terminam; a renovação logo a
                // seguir entra já com a versão nova.
                user.SessionVersion++;
                await _dbContext.SaveChangesAsync();
            }
        }

        // The refresh re-signs in with the NEW password and lands on the
        // shell home. The home page renders the success notice (TempData)
        // so the operator sees the change was applied — never a silent
        // redirect (2026-10-11, pedido do Owner).
        TempData["PasswordChanged"] = "1";

        var identifier = User.FindFirst(DmoClaimTypes.Email)?.Value
            ?? (actor.IsAdmin ? null : actor.OperatorId);

        if (string.IsNullOrWhiteSpace(identifier))
        {
            ErrorMessage = "Não foi possível refazer a autenticação.";
            return Page();
        }

        var signInResult = await _signInService.RefreshSignInAsync(identifier, newPassword, "/JobOn");
        if (!signInResult.Succeeded)
        {
            await HttpContext.SignOutAsync();
            ErrorMessage = "Palavra-passe alterada, mas a sessão não pôde ser renovada. Entre novamente.";
            return Page();
        }

        return Redirect(signInResult.RedirectUrl!);
    }
}
