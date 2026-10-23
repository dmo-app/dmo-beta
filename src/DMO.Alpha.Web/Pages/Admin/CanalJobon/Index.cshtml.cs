using DMO.Alpha.Core.Controlo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.CanalJobon;

/// <summary>
/// O CANAL JOBON (Diogo, 17/10/2026: «hora viragem precisa de outra página
/// onde depois podemos adicionar mais módulos. Podemos chamar Canal Jobon»):
/// as definições de como o Job On chega aos módulos. Hoje: a hora de viragem
/// do dia das Boquilhas e a da Reparação Interna (uma linha por módulo,
/// DP-15) — a produção corrente de uma máquina muda à hora configurada,
/// nunca à meia-noite nem ao criar o Job On. ADMIN apenas.
/// </summary>
public class IndexModel(IModuleSettingsQuery moduleSettings, ISaveModuleSettingHandler saveModuleSetting) : PageModel
{
    /// <summary>A hora de viragem configurada (null quando ainda não configurada — o consumo usa o padrão 8).</summary>
    public int? BoquilhasDayChangeHour { get; private set; }

    /// <summary>A hora de viragem da Reparação Interna (null: o padrão 8).</summary>
    public int? ReparacaoInternaDayChangeHour { get; private set; }

    /// <summary>De que cartão é o resultado: «ri» para a Reparação Interna, senão as Boquilhas.</summary>
    public string? SaveFrom { get; private set; }

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    public string SaveResultMessage => SaveResult switch
    {
        "ok" => "Hora de viragem gravada.",
        "vazio" => "O valor não pode ficar em branco.",
        "hora-invalida" => "A hora de viragem é um inteiro entre 0 e 23.",
        _ => string.Empty
    };

    /// <summary>GET: o Canal Jobon vive nas Definições do ADMIN (Diogo, 22/10/2026: «canal jobon e app ficam dentro das definições do admin»).</summary>
    public IActionResult OnGet() => Redirect(Destino);

    public const string Destino = "/Definicoes?secao=admin&sub=canal";

    /// <summary>Carrega os cartões para as Definições → Admin → Canal Jobon (?viragem=…&amp;de=ri).</summary>
    public async Task CarregarAsync(string? save, string? de)
    {
        SaveResult = save;
        SaveFrom = de;
        var configured = await moduleSettings.FindValueAsync(
            ModuleSettingKeys.BoquilhasDayChangeHour, HttpContext.RequestAborted);
        BoquilhasDayChangeHour = int.TryParse(configured, out var hour) ? hour : null;
        var ri = await moduleSettings.FindValueAsync(
            ModuleSettingKeys.ReparacaoInternaDayChangeHour, HttpContext.RequestAborted);
        ReparacaoInternaDayChangeHour = int.TryParse(ri, out var riHour) ? riHour : null;
    }

    /// <summary>A mensagem do resultado, só no cartão que gravou.</summary>
    public string MessageFor(string card) => (SaveFrom == "ri" ? "ri" : "boquilhas") == card ? SaveResultMessage : string.Empty;

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
        return RedirectToPage("/Definicoes/Index", new { secao = "admin", sub = "canal", viragem = save });
    }

    /// <summary>POST: gravar a hora de viragem do dia da Reparação Interna (0–23), à parte da das Boquilhas.</summary>
    public async Task<IActionResult> OnPostSaveReparacaoInternaDayChangeHourAsync(int? dayChangeHour)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaDayChangeHour, dayChangeHour?.ToString() ?? string.Empty),
            HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            SaveModuleSettingResult.Success => "ok",
            SaveModuleSettingResult.BoquilhasInvalidHour => "hora-invalida",
            _ => "vazio"
        };
        return RedirectToPage("/Definicoes/Index", new { secao = "admin", sub = "canal", viragem = save, de = "ri" });
    }
}
