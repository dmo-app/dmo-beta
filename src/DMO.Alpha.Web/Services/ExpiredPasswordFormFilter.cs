using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DMO.Alpha.Web.Services;

/// <summary>Reopens an expired password form without replaying the submitted password.</summary>
public sealed class ExpiredPasswordFormFilter : IAsyncAlwaysRunResultFilter
{
    // A segunda submissão do mesmo formulário (duplo clique, ou «Guardar»
    // de novo enquanto a página seguinte carrega) chega com o token antigo
    // e a sessão já renovada pela mudança. A palavra-passe já foi alterada:
    // segue para a aterragem com o aviso de sucesso em vez de reabrir o
    // formulário (antes: HTTP 400; depois da beta.12: o formulário outra vez).
    private static readonly TimeSpan RecentChangeWindow = TimeSpan.FromMinutes(5);

    private readonly DmoSignInService _signInService;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public ExpiredPasswordFormFilter(DmoSignInService signInService, ITempDataDictionaryFactory tempDataFactory)
    {
        _signInService = signInService;
        _tempDataFactory = tempDataFactory;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true
                && !user.HasClaim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString)
                && long.TryParse(user.FindFirst(DmoClaimTypes.PasswordChangedAt)?.Value,
                    NumberStyles.None, CultureInfo.InvariantCulture, out var changedAt)
                && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(changedAt) < RecentChangeWindow)
            {
                _tempDataFactory.GetTempData(context.HttpContext)["PasswordChanged"] = "1";
                context.Result = new RedirectResult(await _signInService.ResolveLandingAsync(user));
            }
            else
            {
                context.Result = new RedirectToPageResult("/change-password", new { form_expired = "1" });
            }
        }

        await next();
    }
}
