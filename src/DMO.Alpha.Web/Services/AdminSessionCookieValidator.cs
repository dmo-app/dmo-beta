using DMO.Alpha.Core.Admin;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// Validates active user sessions. The ADMIN session lives while the
/// session's email claim is the email kept in the Definições (o ADMIN é
/// o email — o Owner 2026-10-16): changing the field ends the previous
/// ADMIN's session at its next request.
/// </summary>
public static class AdminSessionCookieValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!principal.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString))
        {
            // A cookie is not sufficient: the user must still exist, retain
            // the same Auth identity, and be active. Also revoke cookies
            // issued before an Admin password reset.
            var idValue = principal.FindFirst(DmoClaimTypes.ActorId)?.Value;
            var providerId = principal.FindFirst(DmoClaimTypes.ProviderUserId)?.Value;
            if (!Guid.TryParse(idValue, out var userId) || string.IsNullOrWhiteSpace(providerId))
            {
                context.RejectPrincipal();
                return;
            }

            // Uma só consulta: o utilizador e o seu template. As capabilities
            // ficam guardadas para o resto do pedido.
            var services = context.HttpContext.RequestServices;
            var access = await services.GetRequiredService<IAdminAuthorizationService>()
                .ResolveEffectiveAccessAsync(userId, context.HttpContext.RequestAborted);
            if (access is null || access.IsStandBy
                || !string.Equals(access.ProviderUserId, providerId, StringComparison.Ordinal)
                || (access.RequiresPasswordChange && !principal.HasClaim(
                    DmoClaimTypes.RequiresPasswordChange, bool.TrueString)))
            {
                context.RejectPrincipal();
                return;
            }

            services.GetRequiredService<CurrentUserCapabilities>().Prime(access);
            return;
        }

        // O ADMIN é o email das Definições: a sessão vive enquanto o
        // email do claim for o guardado. Mudar o campo → o cookie do
        // ADMIN anterior morre no pedido seguinte.
        var email = principal.FindFirst(DmoClaimTypes.Email)?.Value;
        var settings = context.HttpContext.RequestServices.GetRequiredService<IAdminEmailSettings>();
        var adminEmail = await settings.GetAdminEmailAsync(context.HttpContext.RequestAborted);

        if (email is null || adminEmail is null
            || !string.Equals(email.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase))
            context.RejectPrincipal();
    }
}
