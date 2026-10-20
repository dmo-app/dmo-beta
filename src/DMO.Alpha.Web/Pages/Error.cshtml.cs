using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel(ILogger<ErrorModel> logger) : PageModel
{
    public string RequestId { get; private set; } = string.Empty;

    public void OnGet()
    {
        // ExceptionHandlerMiddleware logs the original exception. Only its
        // correlation identifier belongs in the response, never its details.
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = StatusCodes.Status500InternalServerError;

        // A referência que o utilizador comunica também fica no log, logo a
        // seguir à linha do erro com o stack trace (sweep de 19/10/2026, 5.4):
        // procurar a referência leva ao erro.
        var failure = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (failure is not null)
        {
            logger.LogError("Error reference {ErrorReference}: {ExceptionType} on {Method} {Path}",
                RequestId, failure.Error.GetType().FullName, Request.Method, failure.Path);
        }
    }

    // The exception handler preserves the original HTTP method on re-execution.
    // Rendering an error must not require resubmitting an antiforgery token.
    public void OnPost() => OnGet();
}
