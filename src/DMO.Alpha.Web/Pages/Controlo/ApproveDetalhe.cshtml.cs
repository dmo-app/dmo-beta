using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

[Authorize]
[ModuleEntry(DmoCapabilities.ControloApprove)]
public sealed class ApproveDetalheModel : PageModel
{
    private readonly ShellStateFactory _shell;
    private readonly IProductionContextQuery _production;
    private readonly IPesoQuery _peso;
    private readonly IPegamentosQuery _pegamentos;

    public ApproveDetalheModel(
        ShellStateFactory shell,
        IProductionContextQuery production, IPesoQuery peso,
        IPegamentosQuery pegamentos)
    {
        _shell = shell;
        _production = production;
        _peso = peso;
        _pegamentos = pegamentos;
    }

    public ProductionContextReadModel? Production { get; private set; }
    public PesoReadModel? Peso { get; private set; }
    public PegamentosSheetReadModel? Pegamentos { get; private set; }
    public string Tipo { get; private set; } = "peso";
    public int? ControloId { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "tipo")] string? tipo)
    {
        Tipo = tipo == "pegamentos" ? "pegamentos" : "peso";
        ControloId = controloId;
        ViewData["Title"] = Tipo == "peso" ? "Revisão de Peso" : "Revisão de Pegamentos";
        ViewData["Shell"] = (await _shell.ForControloReviewAsync());

        if (controloId is not int id)
            return Page();

        Production = await _production.GetByControloAsync(id, HttpContext.RequestAborted);
        if (Production is null)
            return Page();

        ViewData["Shell"] = (await _shell.ForControloReviewAsync(Production.JobOnId));

        if (Tipo == "peso")
            Peso = await _peso.FindByControloAsync(id, HttpContext.RequestAborted);
        else
            Pegamentos = await _pegamentos.FindSheetByControloAsync(id, HttpContext.RequestAborted);

        return Page();
    }
}
