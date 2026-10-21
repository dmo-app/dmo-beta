using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// Os próximos Job On, vistos do Armazém: as produções que começam nos
/// próximos dias, com onde está cada CM, MF e BQ e o que ainda falta tirar do
/// armazém (ou não está disponível: a reparar, emprestada, na Sucata). O
/// período aplica-se ao escolher.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class ProximosModel(IArmazemProximos proximos, ShellStateFactory shellState) : PageModel
{
    /// <summary>Os períodos que se podem escolher, em dias.</summary>
    public static readonly IReadOnlyList<int> Periodos = [7, 14, 30];

    public IReadOnlyList<ArmazemProximoJobOn> Lista { get; private set; } = [];

    [BindProperty(SupportsGet = true, Name = "dias")]
    public int? DiasPedidos { get; set; }

    public int Dias => DiasPedidos is { } d && Periodos.Contains(d) ? d : 14;

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "Próximos Job On — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "proximos",
            Functions = ShellStateFactory.ArmazemFunctions("proximos")
        };

        Lista = await proximos.ListarAsync(DateOnly.FromDateTime(DmoBusinessTime.Now), Dias, HttpContext.RequestAborted);
        return Page();
    }

    private static readonly string[] DiasDaSemana = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    /// <summary>O dia de início com o dia da semana (ex.: «seg 26/10»).</summary>
    public static string Dia(DateOnly dia) => $"{DiasDaSemana[(int)dia.DayOfWeek]} {dia.ToString("dd/MM", CultureInfo.InvariantCulture)}";
}
