using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// As Definições do Controlo (Diogo, 17/10/2026: «remover do main, pertence
/// ao controlo» e «associaçao do dir fica no controlo.create»): a pasta
/// partilhada dos PDFs, associada em cada computador, e a tolerância dos
/// Pegamentos. Saíram do Admin → Definições; quem tem o Controlo Criar
/// muda-as aqui.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class DefinicoesModel(
    IPegamentosToleranceConfiguration pegamentosTolerance,
    ISavePegamentosToleranceHandler savePegamentosTolerance,
    ShellStateFactory shellState) : PageModel
{
    /// <summary>A tolerância efetiva do corredor (a configurada, ou a norma 0,20 mm).</summary>
    public decimal PegamentosToleranceMm { get; private set; } = PegamentosCalculation.DefaultToleranceMm;

    /// <summary>TRUE quando ainda não há tolerância guardada (vale a norma).</summary>
    public bool PegamentosToleranceIsDefault { get; private set; } = true;

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    public string SaveResultMessage => SaveResult switch
    {
        "ok-tolerancia" => "Tolerância guardada. Vale para os próximos registos de Pegamentos.",
        "tolerancia-invalida" => "A tolerância é um valor em mm entre 0,01 e 2,00, com duas casas decimais no máximo.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(string? save)
    {
        ViewData["Title"] = "Definições — Controlo";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = "definicoes",
            Functions = ShellStateFactory.ControloFunctions("definicoes")
        };

        SaveResult = save;
        var configured = await pegamentosTolerance.FindConfiguredAsync(HttpContext.RequestAborted);
        PegamentosToleranceIsDefault = configured is null;
        PegamentosToleranceMm = configured ?? PegamentosCalculation.DefaultToleranceMm;
        return Page();
    }

    /// <summary>
    /// POST: gravar a tolerância do corredor de Pegamentos (mm). Aceita
    /// vírgula ou ponto. Só os registos criados depois usam o novo valor.
    /// </summary>
    public async Task<IActionResult> OnPostSavePegamentosToleranceAsync([FromForm(Name = "tolerance_mm")] string? toleranceMm)
    {
        var save = "tolerancia-invalida";
        var text = toleranceMm?.Trim().Replace(',', '.');
        if (decimal.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
            && await savePegamentosTolerance.SaveAsync(value, HttpContext.RequestAborted)
                == SavePegamentosToleranceResult.Success)
        {
            save = "ok-tolerancia";
        }

        return RedirectToPage("/Controlo/Definicoes", new { save });
    }
}
