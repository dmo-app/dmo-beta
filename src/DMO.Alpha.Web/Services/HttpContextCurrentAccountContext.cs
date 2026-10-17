using DMO.Alpha.Core.Runtime;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DMO.Alpha.Web.Services;

public sealed class HttpContextCurrentAccountContext : ICurrentAccountContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentAccountContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ActorContext Current
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return ActorContext.Anonymous;
            }

            var actorIdValue = user.FindFirst(DmoClaimTypes.ActorId)?.Value;
            var isAdminValue = user.FindFirst(DmoClaimTypes.IsAdmin)?.Value;
            var operatorId = user.FindFirst(DmoClaimTypes.OperatorId)?.Value;

            return new ActorContext
            {
                IsAuthenticated = true,
                ActorId = Guid.TryParse(actorIdValue, out var actorId) ? actorId : null,
                DisplayName = user.FindFirst(DmoClaimTypes.DisplayName)?.Value ?? string.Empty,
                ProfileLabel = user.FindFirst(DmoClaimTypes.ProfileLabel)?.Value,
                IsAdmin = bool.TryParse(isAdminValue, out var isAdmin) && isAdmin,
                OperatorId = operatorId
            };
        }
    }
}
