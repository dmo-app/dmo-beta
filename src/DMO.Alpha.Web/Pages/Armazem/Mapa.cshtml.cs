using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// O mapa do Armazém (como o ecrã principal do Access antigo): as posições de
/// um armário, livres e ocupadas, com a ocupação em percentagem. O armário e o
/// filtro aplicam-se ao escolher; cada ferramenta abre os seus movimentos.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class MapaModel(IArmazemMapa mapa, ShellStateFactory shellState) : PageModel
{
    public ArmazemMapaModelo Mapa { get; private set; } = new([], null, 0, 0, []);

    [BindProperty(SupportsGet = true, Name = "armario")]
    public string? Armario { get; set; }

    [BindProperty(SupportsGet = true, Name = "mostrar")]
    public string? Mostrar { get; set; }

    public ArmazemMapaFiltro Filtro => Mostrar switch
    {
        "livres" => ArmazemMapaFiltro.Livres,
        "ocupadas" => ArmazemMapaFiltro.Ocupadas,
        "por-reparar" => ArmazemMapaFiltro.PorReparar,
        _ => ArmazemMapaFiltro.Todas
    };

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "Mapa — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "mapa",
            Functions = ShellStateFactory.ArmazemFunctions("mapa")
        };

        Mapa = await mapa.MapaAsync(Armario, Filtro, HttpContext.RequestAborted);
        return Page();
    }

    /// <summary>O nome do armário na lista (ex.: «Armário 40 · 12 de 50 ocupadas · 24%»).</summary>
    public static string Rotulo(ArmazemArmario armario) =>
        $"Armário {armario.Chave} · {armario.Ocupadas} de {armario.Posicoes} ocupadas · {armario.Percentagem}%";

    /// <summary>O texto das ferramentas de uma posição; a CM e o MF da mesma referência e lote ficam numa linha só.</summary>
    public static IReadOnlyList<(string ToolId, string Texto)> Linhas(ArmazemMapaPosicao posicao)
    {
        if (posicao.Par)
        {
            var cm = posicao.Ferramentas.First(f => f.Tipo == DMO.Alpha.Core.Tools.ToolType.Cm);
            return [(cm.ToolId, $"CM+MF {cm.Referencia} · lote {cm.Lote}")];
        }

        return posicao.Ferramentas
            .Select(f => (f.ToolId, ArmazemTextos.Ferramenta(f.Tipo, f.Referencia, f.Lote)))
            .ToList();
    }
}
