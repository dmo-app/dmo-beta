using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Registos;

/// <summary>
/// Os registos por utilizador (Diogo, 2026-10: «logs por utilizador que o
/// Admin poderá ver, com filtro»): o que cada utilizador registou, corrigiu
/// ou anulou nos módulos. A lista só aparece depois de escolher um filtro
/// (as pesquisas abrem vazias); só leitura.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public sealed class IndexModel(
    IRegistosDosUtilizadores registos,
    IUserAdminService users,
    ShellStateFactory shellStateFactory) : PageModel
{
    public IReadOnlyList<UserAdminSummary> Utilizadores { get; private set; } = [];

    public string? RequestedOperatorId { get; private set; }

    public RegistoModulo? RequestedModulo { get; private set; }

    public DateOnly? RequestedDe { get; private set; }

    public DateOnly? RequestedAte { get; private set; }

    /// <summary>Há pelo menos um filtro: a lista aparece.</summary>
    public bool Procurou { get; private set; }

    public PagedList<RegistoDeUtilizador> Pagina { get; private set; } = PagedList<RegistoDeUtilizador>.Empty;

    public ListPager Pager => ListPager.For(Pagina);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "utilizador")] string? operatorId,
        [FromQuery(Name = "modulo")] string? modulo,
        [FromQuery(Name = "de")] DateOnly? de,
        [FromQuery(Name = "ate")] DateOnly? ate,
        [FromQuery(Name = "pagina")] int? pagina)
    {
        ViewData["Title"] = "Registos";
        var shell = await shellStateFactory.ForDestinationAsync("admin", new ModulePresentation("admin", "Administração"));
        ViewData["Shell"] = shell with
        {
            ActiveFunctionKey = AdminAreaNavigation.RegistosFunctionKey,
            Functions = AdminAreaNavigation.Functions(
                AdminAreaNavigation.RegistosFunctionKey,
                Url.Page("/Admin/Users/Index"),
                Url.Page("/Admin/Templates/Index"),
                Url.Page("/Admin/Registos/Index")),
            Planeamento = shell.Planeamento with { Visible = false }
        };

        Utilizadores = (await users.ListUsersAsync(HttpContext.RequestAborted)).OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        RequestedOperatorId = string.IsNullOrWhiteSpace(operatorId) ? null : operatorId.Trim();
        RequestedModulo = Enum.TryParse<RegistoModulo>(modulo, ignoreCase: true, out var m) && Enum.IsDefined(m) ? m : null;
        RequestedDe = de;
        RequestedAte = ate;

        var userId = RequestedOperatorId is null ? null : Utilizadores.FirstOrDefault(u => u.OperatorId == RequestedOperatorId)?.Id;
        Procurou = RequestedOperatorId is not null || RequestedModulo is not null || de is not null || ate is not null;
        if (Procurou && (RequestedOperatorId is null || userId is not null))
        {
            Pagina = await registos.ListarAsync(new RegistosFiltro(userId, RequestedModulo, de, ate), pagina, HttpContext.RequestAborted);
        }

        return Page();
    }
}
