using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// «Opções › Reparação» da folha do Job On (Diogo, 22/10/2026): a Reparação
/// Interna registada neste Job On, só em leitura (quem, o quê, quando). Para
/// quem tem o Planeamento (jobon.create). Sem registo nem edição.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate)]
public sealed class ReparacaoModel(
    IProductionContextQuery productionContext,
    IReparacoesInternas reparacoes,
    DmoDbContext db,
    ShellStateFactory shellState) : PageModel
{
    public ProductionContextReadModel? JobOn { get; private set; }

    public PagedList<RiLinha> Linhas { get; private set; } = PagedList<RiLinha>.Empty;

    public ListPager Pager => new(Linhas.PageNumber, Linhas.TotalPages, Linhas.TotalCount);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "pagina")] int? pagina)
    {
        ViewData["Title"] = "Reparação do Job On";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("planeamento", "Planeamento"))) with
        {
            ActiveFunctionKey = "jobon",
            Functions = await shellState.PlanningFunctionsAsync("jobon")
        };
        var ct = HttpContext.RequestAborted;
        if (jobOnId is int id && await productionContext.GetAsync(id, ct) is { } aberto)
        {
            JobOn = aberto;
            var machineId = await db.JobOns.AsNoTracking().Where(j => j.Id == id).Select(j => j.MachineId).FirstOrDefaultAsync(ct);
            Linhas = await reparacoes.ListarAsync(machineId, id, pagina, ct);
        }

        return Page();
    }
}
