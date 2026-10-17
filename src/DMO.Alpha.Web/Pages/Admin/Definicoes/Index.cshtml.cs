using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Definicoes;

/// <summary>
/// A superfície de definições EDITÁVEIS POR MÓDULO (a decisão do Owner
/// 2026-10-11: «no admin vamos ter definições para cada modulo»; as do
/// Controlo passaram a 17/10/2026 para /Controlo/Definicoes): o email do
/// ADMIN e as Boquilhas (a hora de
/// viragem do dia operacional — a produção corrente de uma máquina muda à
/// hora configurada, nunca à meia-noite nem ao criar o Job On) e O
/// ENCAMINHAMENTO POR MÁQUINA das Boquilhas (o Owner 2026-10-12:
/// «reparadores sao as entidades para onde enviamos as boquilhas»): as
/// ENTIDADES REPARADORAS (criar/editar o nome — o vocabulário; zero
/// entidades = o aviso) e o MAPA das 6 máquinas (B1..C3) com o
/// reparador de cada uma (chaves ModuleSettings
/// Boquilhas:Repairer&lt;código&gt; — a Saída nunca pergunta o reparador ao
/// operador: resolve pela rota).
/// </summary>
public class IndexModel(
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting,
    IBqRepairerQuery repairerQuery,
    ICreateBqRepairerHandler createRepairer,
    IEditBqRepairerHandler editRepairer,
    IAdminEmailSettings adminEmailSettings) : PageModel
{
    /// <summary>A secção do módulo Boquilhas está aberta (?definition=boquilhas).</summary>
    public bool ShowBoquilhas { get; private set; }

    /// <summary>A hora de viragem configurada (null quando ainda não configurada — o consumo usa o padrão 8).</summary>
    public int? BoquilhasDayChangeHour { get; private set; }

    /// <summary>
    /// As ENTIDADES REPARADORAS (a lista do vocabulário — por nome; vazia
    /// mostra o aviso de criar a primeira).
    /// </summary>
    public IReadOnlyList<BqRepairerItem> Repairers { get; private set; } =
        Array.Empty<BqRepairerItem>();

    /// <summary>
    /// O MAPA das 6 máquinas: o código B1..C3 → o Id do reparador
    /// configurado (null = sem rota — a saída daquela máquina grava sem
    /// reparador, truthful).
    /// </summary>
    public IReadOnlyDictionary<string, int?> MachineRepairerIds { get; private set; } =
        new Dictionary<string, int?>();

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    /// <summary>
    /// O EMAIL DO ADMIN (o Owner 2026-10-16: o ADMIN é apenas um email
    /// guardado nas Definições): o login por email compara com este
    /// valor; null num ambiente novo (o primeiro login por email com
    /// sucesso assume-o).
    /// </summary>
    public string? AdminEmail { get; private set; }

    public string SaveResultMessage => SaveResult switch
    {
        "ok" or "ok-reparador" or "ok-rota" => "Definição gravada.",
        "vazio" => "O valor não pode ficar em branco.",
        "hora-invalida" => "A hora de viragem é um inteiro entre 0 e 23.",
        "reparador-vazio" => "O nome do reparador não pode ficar em branco.",
        "reparador-existente" => "Já existe um reparador com esse nome.",
        "reparador-inexistente" => "O reparador indicado já não existe. Crie-o primeiro e volte a escolher.",
        "rota-maquina" => "O código de máquina não é um dos 6 do encaminhamento (B1, B2, B3, C1, C2, C3).",
        "ok-admin" => "Email do ADMIN guardado.",
        "admin-email-invalida" => "O email indicado não é utilizável.",
        "admin-email-diferente" => "Os dois emails não coincidem. O email do ADMIN não foi alterado.",
        _ => string.Empty
    };

    /// <summary>TRUE quando o save transportado pertence ao card do email do ADMIN.</summary>
    public bool ShowAdminSaveMessage =>
        SaveResult is "ok-admin" or "admin-email-invalida" or "admin-email-diferente";

    /// <summary>TRUE quando o save transportado pertence ao card da hora de viragem.</summary>
    public bool ShowHourSaveMessage =>
        SaveResult is "ok" or "hora-invalida" or "vazio";

    /// <summary>TRUE quando o save transportado pertence ao card das entidades reparadoras.</summary>
    public bool ShowRepairerSaveMessage =>
        SaveResult is "ok-reparador" or "reparador-vazio" or "reparador-existente" or "reparador-inexistente";

    /// <summary>TRUE quando o save transportado pertence ao card do mapa por máquina.</summary>
    public bool ShowRoutingSaveMessage =>
        SaveResult is "ok-rota" or "reparador-inexistente" or "rota-maquina";

    public async Task<IActionResult> OnGetAsync(string? definition, string? save)
    {
        SaveResult = save;
        AdminEmail = await adminEmailSettings.GetAdminEmailAsync(HttpContext.RequestAborted);

        // O DEFAULT DA PÁGINA (o Owner 2026-10-13: a página não abre vazia).
        // As definições do Controlo saíram daqui para o próprio módulo
        // (Diogo, 17/10/2026: «remover do main, pertence ao controlo»): os
        // links antigos abrem a primeira secção que ficou.
        if (string.IsNullOrWhiteSpace(definition) || definition is "controlo" or "pegamentos")
        {
            definition = "boquilhas";
        }

        // Os totais de lote têm página própria (beta.110); o link antigo
        // das Definições continua a levar lá.
        if (definition == "ferramentas")
        {
            return RedirectToPage("/Admin/Ferramentas/Index");
        }

        if (definition == "boquilhas")
        {
            ShowBoquilhas = true;
            var configured = await moduleSettings.FindValueAsync(
                ModuleSettingKeys.BoquilhasDayChangeHour,
                HttpContext.RequestAborted);
            BoquilhasDayChangeHour = int.TryParse(configured, out var hour) ? hour : null;

            // AS ENTIDADES REPARADORAS + O MAPA das 6 máquinas (o Id
            // configurado de cada código; null = sem rota).
            Repairers = await repairerQuery.ListAsync(HttpContext.RequestAborted);
            var machineMap = new Dictionary<string, int?>(BqRepairerRouting.MachineCodes.Length);
            foreach (var machineCode in BqRepairerRouting.MachineCodes)
            {
                var configuredRepairer = await moduleSettings.FindValueAsync(
                    BqRepairerRouting.SettingKeyFor(machineCode),
                    HttpContext.RequestAborted);
                machineMap[machineCode] = int.TryParse(configuredRepairer, out var repairerId)
                    ? repairerId
                    : null;
            }

            MachineRepairerIds = machineMap;
        }

        return Page();
    }

    /// <summary>
    /// POST: gravar a hora de viragem do dia operacional das Boquilhas
    /// (0–23): a produção corrente de uma máquina muda à hora configurada
    /// — a B1 passa a mostrar a nova referência às 8 da manhã (o padrão da
    /// fábrica), nunca à meia-noite. ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostSaveBoquilhasDayChangeHourAsync(int? dayChangeHour)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(
                ModuleSettingKeys.BoquilhasDayChangeHour,
                dayChangeHour?.ToString() ?? string.Empty),
            HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            SaveModuleSettingResult.Success => "ok",
            SaveModuleSettingResult.BoquilhasInvalidHour => "hora-invalida",
            _ => "vazio"
        };
        return RedirectToPage("/Admin/Definicoes/Index", new { definition = "boquilhas", save });
    }

    /// <summary>
    /// POST: criar uma ENTIDADE REPARADORA (o vocabulário do encaminhamento
    /// — o nome é o facto humano). Recusa tipada informativa no nome vazio e
    /// no nome repetido; a gravação nunca bloqueia a folha. ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostCreateRepairerAsync(string? name)
    {
        var outcome = await createRepairer.HandleAsync(
            new CreateBqRepairer(name ?? string.Empty),
            HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            BqRepairerWriteResult.Success => "ok-reparador",
            BqRepairerWriteResult.EmptyName => "reparador-vazio",
            _ => "reparador-existente"
        };
        return RedirectToPage("/Admin/Definicoes/Index", new { definition = "boquilhas", save });
    }

    /// <summary>
    /// POST: editar O NOME de uma entidade reparadora. A edição NÃO
    /// reescreve os movimentos passados — o nome congelado de cada saída é
    /// a verdade histórica dela; as saídas futuras congelam o nome novo.
    /// ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostEditRepairerAsync(
        [FromForm(Name = "repairer_id")] int? repairerId,
        string? name)
    {
        var outcome = await editRepairer.HandleAsync(
            new EditBqRepairer(repairerId ?? 0, name ?? string.Empty),
            HttpContext.RequestAborted);

        var save = outcome.Result switch
        {
            BqRepairerWriteResult.Success => "ok-reparador",
            BqRepairerWriteResult.EmptyName => "reparador-vazio",
            BqRepairerWriteResult.NotFound => "reparador-inexistente",
            _ => "reparador-existente"
        };
        return RedirectToPage("/Admin/Definicoes/Index", new { definition = "boquilhas", save });
    }

    /// <summary>
    /// POST: gravar O REPARADOR DE UMA MÁQUINA do mapa (B1..C3 — a rota da
    /// Saída; nunca um input do form do movimento). O valor vazio LIMPA a
    /// rota da máquina — estado válido: a saída grava na mesma, sem
    /// reparador. ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostSaveMachineRoutesAsync(
        [FromForm(Name = "machine_code")] List<string>? machineRoutes)
    {
        // O FORM ÚNICO DAS ROTAS (o Owner 2026-10-13: «olha para os
        // reparadores» — 6 botões «Guardar B1..C3» empilhados contra a
        // gramática da casa): as 6 máquinas chegam como «machine_code»
        // repetido, cada valor «B1|3» (máquina|reparador) ou vazio (limpa).
        if (machineRoutes is null || machineRoutes.Count == 0)
        {
            return RedirectToPage("/Admin/Definicoes/Index",
                new { definition = "boquilhas", save = "rota-maquina" });
        }

        foreach (var raw in machineRoutes)
        {
            var code = raw?.Split('|')[0].Trim() ?? string.Empty;
            var idPart = raw?.Contains('|') == true ? raw.Split('|')[1] : string.Empty;
            int? repairerId = int.TryParse(idPart, out var parsed) ? parsed : null;

            if (!BqRepairerRouting.TryGetMachineCode(
                    BqRepairerRouting.SettingKeyFor(code), out _))
            {
                return RedirectToPage("/Admin/Definicoes/Index",
                    new { definition = "boquilhas", save = "rota-maquina" });
            }

            var outcome = await saveModuleSetting.HandleAsync(
                new SaveModuleSetting(
                    BqRepairerRouting.SettingKeyFor(code),
                    repairerId?.ToString() ?? string.Empty),
                HttpContext.RequestAborted);

            if (outcome.Result != SaveModuleSettingResult.Success
                && outcome.Result != SaveModuleSettingResult.BoquilhasInvalidRepairerId)
            {
                return RedirectToPage("/Admin/Definicoes/Index",
                    new { definition = "boquilhas", save = "vazio" });
            }
        }

        return RedirectToPage("/Admin/Definicoes/Index",
            new { definition = "boquilhas", save = "ok-rota" });
    }

    /// <summary>
    /// POST: guardar O EMAIL DO ADMIN (o Owner 2026-10-16: o ADMIN é
    /// apenas um email — «usa uma solução apenas para o mail»): quem
    /// autentica com este email entra como ADMIN; a sessão do ADMIN
    /// anterior morre no pedido seguinte. Num ambiente novo, o campo
    /// vazio é assumido pelo primeiro login por email com sucesso.
    /// ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAdminEmailAsync(
        [FromForm(Name = "admin_email")] string? adminEmail,
        [FromForm(Name = "admin_email_confirm")] string? adminEmailConfirm)
    {
        // Um erro de escrita tranca o ADMIN fora da app (a sessão cai no
        // pedido seguinte): o email é escrito duas vezes antes de gravar.
        if (!string.Equals(adminEmail?.Trim(), adminEmailConfirm?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Admin/Definicoes/Index", new { save = "admin-email-diferente" });
        }

        var outcome = await adminEmailSettings.SaveAdminEmailAsync(
            adminEmail ?? string.Empty,
            HttpContext.RequestAborted);

        var save = outcome == SaveAdminEmailResult.Success ? "ok-admin" : "admin-email-invalida";
        return RedirectToPage("/Admin/Definicoes/Index", new { save });
    }

}
