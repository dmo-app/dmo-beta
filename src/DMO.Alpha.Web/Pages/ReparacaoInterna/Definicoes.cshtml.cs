using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.ReparacaoInterna;

/// <summary>
/// As Definições da Reparação Interna (Diogo, 25/10/2026: «cada uma no seu»; ficam em «Boquilhas · ver»):
/// a hora de viragem do dia, que estava no Canal Jobon do Admin. A partir
/// desta hora a Reparação liga as reparações à nova produção de cada linha.
/// </summary>
[Authorize]
[DefinicoesEntry("reparacao", DmoCapabilities.BoquilhasView)]
public sealed class DefinicoesModel(
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting) : PageModel
{
    /// <summary>A hora configurada (null: o padrão 8).</summary>
    public int? DayChangeHour { get; private set; }

    public string? Gravacao { get; private set; }

    public string Mensagem => Gravacao switch
    {
        "ok-hora" => "Hora de viragem gravada.",
        "hora-invalida" => "A hora de viragem é um inteiro entre 0 e 23.",
        "vazio" => "O valor não pode ficar em branco.",
        _ => string.Empty
    };

    public bool Gravou => Gravacao == "ok-hora";

    /// <summary>GET: as definições vivem na página única /Definicoes.</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "reparacao"));

    public async Task CarregarAsync(string? gravacao)
    {
        Gravacao = gravacao;
        var configured = await moduleSettings.FindValueAsync(
            ModuleSettingKeys.ReparacaoInternaDayChangeHour, HttpContext.RequestAborted);
        DayChangeHour = int.TryParse(configured, out var hour) ? hour : null;
    }

    /// <summary>POST: gravar a hora de viragem do dia (0–23). Escolher no dropdown grava logo.</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSaveDayChangeHourAsync(int? dayChangeHour)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaDayChangeHour, dayChangeHour?.ToString() ?? string.Empty),
            HttpContext.RequestAborted);

        var gravacao = outcome.Result switch
        {
            SaveModuleSettingResult.Success => "ok-hora",
            SaveModuleSettingResult.BoquilhasInvalidHour => "hora-invalida",
            _ => "vazio"
        };
        return RedirectToPage("/Definicoes/Index", new { gravacao });
    }
}
