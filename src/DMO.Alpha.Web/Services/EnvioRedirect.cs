using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DMO.Alpha.Web.Services;

internal sealed record EnvioRedirect(string Url, bool Permanente, bool PreservarMetodo)
{
    public static string? Guardar(IActionResult? result, IUrlHelper urls)
    {
        var url = result switch
        {
            RedirectToPageResult r => urls.Page(r.PageName, r.PageHandler, r.RouteValues, r.Protocol, r.Host, r.Fragment),
            RedirectToActionResult r => urls.Action(r.ActionName, r.ControllerName, r.RouteValues, null, null, r.Fragment),
            RedirectToRouteResult r => urls.RouteUrl(r.RouteName, r.RouteValues, null, null, r.Fragment),
            RedirectResult r => r.Url,
            LocalRedirectResult r => r.Url,
            _ => null
        };
        if (url is null || !urls.IsLocalUrl(url)) return null;
        var (permanent, preserve) = result switch
        {
            RedirectToPageResult r => (r.Permanent, r.PreserveMethod),
            RedirectToActionResult r => (r.Permanent, r.PreserveMethod),
            RedirectToRouteResult r => (r.Permanent, r.PreserveMethod),
            RedirectResult r => (r.Permanent, r.PreserveMethod),
            LocalRedirectResult r => (r.Permanent, r.PreserveMethod),
            _ => (false, false)
        };
        var json = JsonSerializer.Serialize(new EnvioRedirect(url, permanent, preserve));
        return json.Length <= 16384 ? json : null;
    }

    public static IActionResult Ler(string json)
    {
        var r = JsonSerializer.Deserialize<EnvioRedirect>(json) ?? throw new InvalidOperationException("Resposta do envio em falta.");
        return new LocalRedirectResult(r.Url, r.Permanente, r.PreservarMetodo);
    }
}
