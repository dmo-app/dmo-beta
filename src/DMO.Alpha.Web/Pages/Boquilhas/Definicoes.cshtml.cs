using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// As Definições das Boquilhas (Diogo, 17/10/2026: «separamos as definições
/// para não ter tudo misturado numa página» e «vai para boquilhas»): as
/// ENTIDADES REPARADORAS (o Owner 2026-10-12: «reparadores sao as entidades
/// para onde enviamos as boquilhas») e O ENCAMINHAMENTO POR MÁQUINA (chaves
/// ModuleSettings Boquilhas:Repairer&lt;código&gt; — a Saída nunca pergunta
/// o reparador ao operador: resolve pela rota). Saíram do Admin →
/// Definições; quem tem o módulo Boquilhas muda-as aqui. O nome de um
/// reparador edita-se no próprio valor da tabela.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Boquilhas)]
public sealed class DefinicoesModel(
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting,
    IBqRepairerQuery repairerQuery,
    ICreateBqRepairerHandler createRepairer,
    IEditBqRepairerHandler editRepairer,
    ShellStateFactory shellState) : PageModel
{
    /// <summary>As ENTIDADES REPARADORAS, por nome; vazia mostra o aviso de criar a primeira.</summary>
    public IReadOnlyList<BqRepairerItem> Repairers { get; private set; } = Array.Empty<BqRepairerItem>();

    /// <summary>O MAPA das 6 máquinas: B1..C3 → o Id do reparador (null = sem rota).</summary>
    public IReadOnlyDictionary<string, int?> MachineRepairerIds { get; private set; } = new Dictionary<string, int?>();

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    public string SaveResultMessage => MessageFor(SaveResult);

    private static string MessageFor(string? save) => save switch
    {
        "ok-reparador" or "ok-rota" => "Definição gravada.",
        "vazio" => "O valor não pode ficar em branco.",
        "reparador-vazio" => "O nome do reparador não pode ficar em branco.",
        "reparador-existente" => "Já existe um reparador com esse nome.",
        "reparador-inexistente" => "O reparador indicado já não existe. Crie-o primeiro e volte a escolher.",
        "rota-maquina" => "O código de máquina não é um dos 6 do encaminhamento (B1, B2, B3, C1, C2, C3).",
        _ => string.Empty
    };

    /// <summary>TRUE quando o save transportado pertence ao card dos reparadores.</summary>
    public bool ShowRepairerSaveMessage =>
        SaveResult is "ok-reparador" or "reparador-vazio" or "reparador-existente" or "reparador-inexistente";

    /// <summary>TRUE quando o save transportado pertence ao card da rota por máquina.</summary>
    public bool ShowRoutingSaveMessage =>
        SaveResult is "ok-rota" or "reparador-inexistente" or "rota-maquina" or "vazio";

    public async Task<IActionResult> OnGetAsync(string? save)
    {
        ViewData["Title"] = "Definições — Boquilhas";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("boquilhas", "Boquilhas"))) with
        {
            ActiveFunctionKey = "definicoes",
            Functions = ShellStateFactory.BoquilhasFunctions("definicoes")
        };

        SaveResult = save;
        Repairers = await repairerQuery.ListAsync(HttpContext.RequestAborted);
        var machineMap = new Dictionary<string, int?>(BqRepairerRouting.MachineCodes.Length);
        foreach (var machineCode in BqRepairerRouting.MachineCodes)
        {
            var configured = await moduleSettings.FindValueAsync(
                BqRepairerRouting.SettingKeyFor(machineCode), HttpContext.RequestAborted);
            machineMap[machineCode] = int.TryParse(configured, out var repairerId) ? repairerId : null;
        }

        MachineRepairerIds = machineMap;
        return Page();
    }

    /// <summary>
    /// POST: criar uma ENTIDADE REPARADORA. Recusa tipada no nome vazio e no
    /// nome repetido; a gravação nunca bloqueia a folha.
    /// </summary>
    public async Task<IActionResult> OnPostCreateRepairerAsync(string? name)
    {
        var outcome = await createRepairer.HandleAsync(
            new CreateBqRepairer(name ?? string.Empty), HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            BqRepairerWriteResult.Success => "ok-reparador",
            BqRepairerWriteResult.EmptyName => "reparador-vazio",
            _ => "reparador-existente"
        };
        return RedirectToPage("/Boquilhas/Definicoes", new { save });
    }

    /// <summary>
    /// POST: editar O NOME de um reparador, no próprio valor da tabela (pedido
    /// JSON) ou por PRG. Não reescreve os movimentos passados: cada saída
    /// guarda o nome com que foi feita.
    /// </summary>
    public async Task<IActionResult> OnPostEditRepairerAsync(
        [FromForm(Name = "repairer_id")] int? repairerId,
        string? name)
    {
        var outcome = await editRepairer.HandleAsync(
            new EditBqRepairer(repairerId ?? 0, name ?? string.Empty), HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            BqRepairerWriteResult.Success => "ok-reparador",
            BqRepairerWriteResult.EmptyName => "reparador-vazio",
            BqRepairerWriteResult.NotFound => "reparador-inexistente",
            _ => "reparador-existente"
        };

        if (Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            var ok = save == "ok-reparador";
            return new JsonResult(new { ok, valor = ok ? name?.Trim() : null, aviso = ok ? null : MessageFor(save) });
        }

        return RedirectToPage("/Boquilhas/Definicoes", new { save });
    }

    /// <summary>
    /// POST: gravar a rota das máquinas (B1..C3). Cada «machine_code» chega
    /// como «B1|3» (máquina|reparador) ou «B1|» (limpa a rota: a saída
    /// grava na mesma, sem reparador). Escolher no dropdown grava logo.
    /// </summary>
    public async Task<IActionResult> OnPostSaveMachineRoutesAsync(
        [FromForm(Name = "machine_code")] List<string>? machineRoutes)
    {
        if (machineRoutes is null || machineRoutes.Count == 0)
        {
            return RedirectToPage("/Boquilhas/Definicoes", new { save = "rota-maquina" });
        }

        foreach (var raw in machineRoutes)
        {
            var code = raw?.Split('|')[0].Trim() ?? string.Empty;
            var idPart = raw?.Contains('|') == true ? raw.Split('|')[1] : string.Empty;
            int? repairerId = int.TryParse(idPart, out var parsed) ? parsed : null;

            if (!BqRepairerRouting.TryGetMachineCode(BqRepairerRouting.SettingKeyFor(code), out _))
            {
                return RedirectToPage("/Boquilhas/Definicoes", new { save = "rota-maquina" });
            }

            var outcome = await saveModuleSetting.HandleAsync(
                new SaveModuleSetting(BqRepairerRouting.SettingKeyFor(code), repairerId?.ToString() ?? string.Empty),
                HttpContext.RequestAborted);

            if (outcome.Result != SaveModuleSettingResult.Success
                && outcome.Result != SaveModuleSettingResult.BoquilhasInvalidRepairerId)
            {
                return RedirectToPage("/Boquilhas/Definicoes", new { save = "vazio" });
            }
        }

        return RedirectToPage("/Boquilhas/Definicoes", new { save = "ok-rota" });
    }
}
