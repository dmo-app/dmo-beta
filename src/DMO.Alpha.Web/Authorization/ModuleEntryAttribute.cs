using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMO.Alpha.Web.Authorization;

/// <summary>
/// A entrada no módulo (o Owner 2026-10-16: o template dá módulos e o
/// módulo dá as páginas e as suas funções). A página declara o seu módulo
/// e esta verificação corre uma vez, antes de qualquer handler (GET, POST
/// ou auxiliar). Dentro do módulo, os handlers não voltam a perguntar.
/// Quem não tem o módulo vai, sem aviso, para o seu primeiro módulo (o
/// ADMIN para a Administração); nenhum handler corre. O template é lido uma
/// vez por pedido (<see cref="CurrentUserCapabilities"/>), por isso uma
/// alteração do template vale no pedido seguinte.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleEntryAttribute(string module) : Attribute, IAsyncPageFilter
{
    public string Module { get; } = module;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var capabilities = context.HttpContext.RequestServices.GetRequiredService<CurrentUserCapabilities>();
        var granted = await capabilities.GetAsync(context.HttpContext.RequestAborted);
        if (granted.Contains(Module, StringComparer.Ordinal))
        {
            await next();
            return;
        }

        context.Result = new RedirectResult(
            SameNameModule(granted, context.HttpContext.Request.Query["jobon_id"])
            ?? HomeFor(context.HttpContext.User, granted));
    }

    /// <summary>
    /// Os dois Controlos têm o mesmo nome na interface: quem abre o
    /// Controlo que não tem entra no Controlo que tem, na mesma produção.
    /// </summary>
    private string? SameNameModule(IReadOnlyList<string> granted, string? jobOnId)
    {
        var label = DmoCapabilities.Modules.FirstOrDefault(m => m.Key == Module)?.Label;
        var sibling = DmoCapabilities.Modules.FirstOrDefault(m =>
            m.Key != Module && m.Label == label && granted.Contains(m.Key, StringComparer.Ordinal));
        if (sibling is null)
        {
            return null;
        }

        return int.TryParse(jobOnId, out var id) ? $"{sibling.Home}?jobon_id={id}" : sibling.Home;
    }

    /// <summary>
    /// A página de entrada de quem está autenticado: o primeiro módulo do
    /// template; o ADMIN entra na Administração; sem módulos, a página
    /// inicial explica que falta o template.
    /// </summary>
    public static string HomeFor(System.Security.Claims.ClaimsPrincipal user, IReadOnlyList<string> granted) =>
        user.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString)
            ? "/Admin/Users"
            : DmoCapabilities.HomeFor(granted) ?? "/Modules/Home";
}
