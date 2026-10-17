using DMO.Alpha.Core.Controlo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.CanalJobon;

/// <summary>
/// O CANAL JOBON (Diogo, 17/10/2026: «hora viragem precisa de outra página
/// onde depois podemos adicionar mais módulos. Podemos chamar Canal Jobon»):
/// as definições de como o Job On chega aos módulos. Hoje: a hora de viragem
/// do dia das Boquilhas — a produção corrente de uma máquina muda à hora
/// configurada, nunca à meia-noite nem ao criar o Job On. ADMIN apenas.
/// </summary>
public class IndexModel(IModuleSettingsQuery moduleSettings, ISaveModuleSettingHandler saveModuleSetting) : PageModel
{
    /// <summary>A hora de viragem configurada (null quando ainda não configurada — o consumo usa o padrão 8).</summary>
    public int? BoquilhasDayChangeHour { get; private set; }

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    public string SaveResultMessage => SaveResult switch
    {
        "ok" => "Hora de viragem gravada.",
        "vazio" => "O valor não pode ficar em branco.",
        "hora-invalida" => "A hora de viragem é um inteiro entre 0 e 23.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(string? save)
    {
        SaveResult = save;
        var configured = await moduleSettings.FindValueAsync(
            ModuleSettingKeys.BoquilhasDayChangeHour, HttpContext.RequestAborted);
        BoquilhasDayChangeHour = int.TryParse(configured, out var hour) ? hour : null;
        return Page();
    }

    /// <summary>POST: gravar a hora de viragem do dia das Boquilhas (0–23). Escolher no dropdown grava logo.</summary>
    public async Task<IActionResult> OnPostSaveBoquilhasDayChangeHourAsync(int? dayChangeHour)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.BoquilhasDayChangeHour, dayChangeHour?.ToString() ?? string.Empty),
            HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            SaveModuleSettingResult.Success => "ok",
            SaveModuleSettingResult.BoquilhasInvalidHour => "hora-invalida",
            _ => "vazio"
        };
        return RedirectToPage("/Admin/CanalJobon/Index", new { save });
    }
}
