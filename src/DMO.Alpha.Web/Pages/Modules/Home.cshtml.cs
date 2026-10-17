using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Modules;

[Authorize]
public class HomeModel : PageModel
{
    private readonly ICurrentAccountContext _accountContext;
    private readonly ShellStateFactory _shellState;
    private readonly CurrentUserCapabilities _capabilities;

    public HomeModel(ICurrentAccountContext accountContext, ShellStateFactory shellState, CurrentUserCapabilities capabilities)
    {
        _accountContext = accountContext;
        _shellState = shellState;
        _capabilities = capabilities;
    }

    public ActorContext Actor => _accountContext.Current;

    /// <summary>Os módulos que o template dá, pela ordem da barra.</summary>
    public IReadOnlyList<DmoModule> Modules { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var granted = await _capabilities.GetAsync(HttpContext.RequestAborted);
        Modules = DmoCapabilities.MenuModules(granted);

        // The Home module surface renders inside the shared shell. Its module
        // functions and side panel arrive with the module implementation.
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("Home", new ModulePresentation("home", "Início")));
    }
}
