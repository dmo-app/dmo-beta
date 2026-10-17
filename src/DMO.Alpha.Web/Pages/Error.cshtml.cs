using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel : PageModel
{
    public string RequestId { get; private set; } = string.Empty;

    public void OnGet()
    {
        // ExceptionHandlerMiddleware logs the original exception. Only its
        // correlation identifier belongs in the response, never its details.
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = StatusCodes.Status500InternalServerError;
    }

    // The exception handler preserves the original HTTP method on re-execution.
    // Rendering an error must not require resubmitting an antiforgery token.
    public void OnPost() => OnGet();
}
