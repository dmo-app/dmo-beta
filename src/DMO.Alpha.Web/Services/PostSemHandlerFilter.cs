using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// Um POST (ou outro verbo que não GET/HEAD) que não chega a nenhum handler
/// já não desenha a página: sem handler, o shell não é preenchido e o layout
/// rebentava com erro 500 (audit externo, 16/11/2026, G4). Um handler pedido
/// que não existe dá 404; a página sem handler para esse verbo dá 405.
/// </summary>
public sealed class PostSemHandlerFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var metodo = context.HttpContext.Request.Method;
        if (context.HandlerMethod is null && !HttpMethods.IsGet(metodo) && !HttpMethods.IsHead(metodo))
        {
            var pedido = context.HttpContext.Request.Query["handler"].ToString();
            context.Result = string.IsNullOrEmpty(pedido) && context.RouteData.Values["handler"] is null
                ? new StatusCodeResult(StatusCodes.Status405MethodNotAllowed)
                : new NotFoundResult();
            return Task.CompletedTask;
        }

        return next();
    }
}
