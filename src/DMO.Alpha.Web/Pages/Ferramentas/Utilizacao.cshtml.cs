using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// O histórico do uso de uma ferramenta: todas as produções em que esteve, com
/// o Job On, o produzido e o uso que foi acumulando (Diogo, 27/10/2026). Abre no
/// painel a partir de qualquer «% uso» (Shared/_UsoFerramenta); só leitura.
/// PERM-B01 (DMO-654): o Posto (Reparação Interna) fica confinado à RI — sem
/// a capability Ferramentas, esta página também recusa (não só o menu do
/// Posto que não mostra o link).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class UtilizacaoModel : PageModel
{
    private readonly IToolSheetQuery _sheetQuery;
    private readonly IFerramentasUsoSap _usoSap;
    private readonly IFerramentasFabricoSap _fabricoSap;
    private readonly ShellStateFactory _shellState;

    public UtilizacaoModel(IToolSheetQuery sheetQuery, IFerramentasUsoSap usoSap, IFerramentasFabricoSap fabricoSap, ShellStateFactory shellState)
    {
        _sheetQuery = sheetQuery;
        _usoSap = usoSap;
        _fabricoSap = fabricoSap;
        _shellState = shellState;
    }

    public ToolSheetReadModel? Sheet { get; private set; }

    public FerramentaUtilizacaoAtual? Utilizacao { get; private set; }

    public FerramentaProducoes? Producoes { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "tool_id")] string? toolId)
    {
        ViewData["Title"] = "Utilização da ferramenta — Ferramentas";
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = "ver",
            Functions = ShellStateFactory.FerramentasFunctions("ver")
        };
        var pedido = IndexModel.Sanitize(toolId);
        Sheet = pedido is null ? null : await _sheetQuery.FindByToolIdAsync(pedido, HttpContext.RequestAborted);
        if (Sheet is null)
        {
            return NotFound();
        }

        Utilizacao = await _usoSap.DaFerramentaAsync(Sheet.ToolId, HttpContext.RequestAborted);
        Producoes = await _fabricoSap.ProducoesAsync(Sheet.ToolId, 1, FichaModel.ProducoesPorPagina, HttpContext.RequestAborted);
        return Page();
    }
}
