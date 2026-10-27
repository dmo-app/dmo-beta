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
/// As Definições da Reparação Interna (Diogo, 25/10/2026: «cada uma no seu»; fica com quem tem «Boquilhas · criar», como as das Boquilhas):
/// a hora de viragem do dia, que estava no Canal Jobon do Admin. A partir
/// desta hora a Reparação liga as reparações à nova produção de cada linha.
/// E os minutos sem uso ao fim dos quais a sessão do posto fecha sozinha.
/// </summary>
[Authorize]
[DefinicoesEntry("reparacao", DmoCapabilities.Boquilhas)]
public sealed class DefinicoesModel(
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting) : PageModel
{
    /// <summary>A hora configurada (null: o padrão 8).</summary>
    public int? DayChangeHour { get; private set; }

    /// <summary>Os minutos sem uso ao fim dos quais a sessão do posto fecha (3 por omissão).</summary>
    public int PostoMinutos { get; private set; } = PostoRiRegras.MinutosPorOmissao;

    public string? Gravacao { get; private set; }

    public string Mensagem => Gravacao switch
    {
        "ok-hora" => "Hora de viragem gravada.",
        "hora-invalida" => "A hora de viragem é um inteiro entre 0 e 23.",
        "vazio" => "O valor não pode ficar em branco.",
        "ok-posto" => "Tempo do posto gravado. Vale a partir da próxima entrada.",
        "posto-invalido" => "Escolha um dos tempos da lista.",
        _ => string.Empty
    };

    public bool Gravou => Gravacao is "ok-hora" or "ok-posto";

    /// <summary>A mensagem é do cartão do posto (e não do da hora de viragem).</summary>
    public bool MensagemDoPosto => Gravacao is "ok-posto" or "posto-invalido";

    /// <summary>GET: as definições vivem na página única /Definicoes.</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "reparacao"));

    public async Task CarregarAsync(string? gravacao)
    {
        Gravacao = gravacao;
        var configured = await moduleSettings.FindValueAsync(
            ModuleSettingKeys.ReparacaoInternaDayChangeHour, HttpContext.RequestAborted);
        DayChangeHour = int.TryParse(configured, out var hour) ? hour : null;
        PostoMinutos = PostoRiRegras.Minutos(await moduleSettings.FindValueAsync(
            ModuleSettingKeys.ReparacaoInternaPostoMinutos, HttpContext.RequestAborted));
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

    /// <summary>
    /// POST: os minutos sem uso ao fim dos quais a sessão aberta pelo posto
    /// fecha sozinha (Diogo, 27/10/2026: «3min auto e opções nas definições
    /// para aumentar»). Escolher no dropdown grava logo.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSavePostoMinutosAsync(int? postoMinutos)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaPostoMinutos, postoMinutos?.ToString() ?? string.Empty),
            HttpContext.RequestAborted);

        var gravacao = outcome.Result == SaveModuleSettingResult.Success ? "ok-posto" : "posto-invalido";
        return RedirectToPage("/Definicoes/Index", new { gravacao, secao = "reparacao" });
    }
}
