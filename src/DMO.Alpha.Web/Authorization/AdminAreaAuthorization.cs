using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace DMO.Alpha.Web.Authorization;

// BETA: /Admin is never public and has no anonymous exception of any
// kind — only an authenticated ADMIN session (the email of the
// Definições) enters.
public static class AdminAuthorizationPolicies
{
    public const string RequireAdmin = "RequireAdmin";
}

public sealed class AdminAreaRequirement : IAuthorizationRequirement
{
}

public sealed class AdminAreaAuthorizationHandler : AuthorizationHandler<AdminAreaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminAreaRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
