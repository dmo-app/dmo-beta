using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

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
[DefinicoesEntry("boquilhas", DmoCapabilities.Boquilhas)]
// O limite do Excel vale para a página toda: num handler o ASP.NET
// ignora-o (aviso MVC1001).
[RequestSizeLimit(DefinicoesModel.ExcelTamanhoMaximo + 1024 * 1024)]
public sealed class DefinicoesModel(
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting,
    IBqRepairerQuery repairerQuery,
    ICreateBqRepairerHandler createRepairer,
    IEditBqRepairerHandler editRepairer,
    IDeleteBqRepairerHandler deleteRepairer,
    IBqExcelBoquilhas excel,
    [FromKeyedServices(DefinicoesModel.RelogioDoExcel)] TimeProvider? relogio = null) : PageModel
{
    /// <summary>A chave do relógio do «hoje» do Excel das Boquilhas (os testes fixam-no).</summary>
    public const string RelogioDoExcel = "bq-excel-relogio";

    private DateOnly HojeDoExcel => DateOnly.FromDateTime(
        DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal((relogio ?? TimeProvider.System).GetUtcNow()));

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
        "ok-removido" => "Reparador removido.",
        "vazio" => "O valor não pode ficar em branco.",
        "reparador-vazio" => "O nome do reparador não pode ficar em branco.",
        "reparador-existente" => "Já existe um reparador com esse nome.",
        "reparador-inexistente" => "O reparador indicado já não existe. Crie-o primeiro e volte a escolher.",
        "rota-maquina" => "O código de máquina não é um dos 6 do encaminhamento (B1, B2, B3, C1, C2, C3).",
        _ => string.Empty
    };

    /// <summary>
    /// TRUE quando o save transportado é uma gravação feita (a mensagem fecha
    /// sozinha); as recusas ficam no canto até se fecharem. Uma só mensagem
    /// por gravação, na pilha do canto: não empurra os cartões.
    /// </summary>
    public bool SaveSucceeded => SaveResult is "ok-reparador" or "ok-rota" or "ok-removido";

    /// <summary>GET: as definições vivem na página única /Definicoes (Diogo, 22/10/2026).</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "boquilhas"));

    /// <summary>Carrega o que a secção deste módulo nas Definições mostra.</summary>
    public async Task CarregarAsync(string? save)
    {

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
    }

    /// <summary>
    /// POST: criar uma ENTIDADE REPARADORA. Recusa tipada no nome vazio e no
    /// nome repetido; a gravação nunca bloqueia a folha.
    /// </summary>
    [DefinicaoGeral]
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
        return RedirectToPage("/Definicoes/Index", new { save });
    }

    /// <summary>
    /// POST: editar O NOME de um reparador, no próprio valor da tabela (pedido
    /// JSON) ou por PRG. Não reescreve os movimentos passados: cada saída
    /// guarda o nome com que foi feita.
    /// </summary>
    [DefinicaoGeral]
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

        return RedirectToPage("/Definicoes/Index", new { save });
    }

    /// <summary>
    /// POST: remover um reparador (Diogo, 21/10/2026). As saídas já feitas
    /// guardam o nome; as rotas que o usavam ficam sem reparador.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostDeleteRepairerAsync(
        [FromForm(Name = "repairer_id")] int? repairerId)
    {
        var outcome = await deleteRepairer.HandleAsync(
            new DeleteBqRepairer(repairerId ?? 0), HttpContext.RequestAborted);
        var save = outcome.Result == BqRepairerWriteResult.Success ? "ok-removido" : "reparador-inexistente";
        return RedirectToPage("/Definicoes/Index", new { save });
    }

    /// <summary>
    /// POST: gravar a rota das máquinas (B1..C3). Cada «machine_code» chega
    /// como «B1|3» (máquina|reparador) ou «B1|» (limpa a rota: a saída
    /// grava na mesma, sem reparador). Escolher no dropdown grava logo; a
    /// página envia só essa máquina (um formulário por máquina, deep sweep
    /// 18/10/2026, A1-M1).
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSaveMachineRoutesAsync(
        [FromForm(Name = "machine_code")] List<string>? machineRoutes)
    {
        if (machineRoutes is null || machineRoutes.Count == 0)
        {
            return RedirectToPage("/Definicoes/Index", new { save = "rota-maquina" });
        }

        foreach (var raw in machineRoutes)
        {
            var code = raw?.Split('|')[0].Trim() ?? string.Empty;
            var idPart = raw?.Contains('|') == true ? raw.Split('|')[1] : string.Empty;
            int? repairerId = int.TryParse(idPart, out var parsed) ? parsed : null;

            if (!BqRepairerRouting.TryGetMachineCode(BqRepairerRouting.SettingKeyFor(code), out _))
            {
                return RedirectToPage("/Definicoes/Index", new { save = "rota-maquina" });
            }

            var outcome = await saveModuleSetting.HandleAsync(
                new SaveModuleSetting(BqRepairerRouting.SettingKeyFor(code), repairerId?.ToString() ?? string.Empty),
                HttpContext.RequestAborted);

            if (outcome.Result != SaveModuleSettingResult.Success
                && outcome.Result != SaveModuleSettingResult.BoquilhasInvalidRepairerId)
            {
                return RedirectToPage("/Definicoes/Index", new { save = "vazio" });
            }
        }

        return RedirectToPage("/Definicoes/Index", new { save = "ok-rota" });
    }

    /// <summary>O tamanho máximo do Excel das Boquilhas que o navegador envia.</summary>
    public const long ExcelTamanhoMaximo = 20 * 1024 * 1024;

    /// <summary>
    /// GET: a versão dos movimentos das Boquilhas. O navegador onde o Excel
    /// do chefe está ligado compara-a com a da última escrita e só envia o
    /// ficheiro quando mudou.
    /// </summary>
    public async Task<IActionResult> OnGetExcelVersaoAsync() =>
        new JsonResult(new { versao = await excel.VersaoAsync(HttpContext.RequestAborted) });

    /// <summary>
    /// POST: o EXCEL DAS BOQUILHAS DO CHEFE (Diogo, 21/10/2026: «cada registo
    /// que faço passa guardar a informação nesse excel que está numa pasta do
    /// pc»). O navegador envia o ficheiro da pasta e a data desde a qual a app
    /// escreve; a resposta é o mesmo ficheiro com esses dias escritos, que o
    /// navegador grava por cima. O ficheiro nunca fica no servidor.
    /// </summary>
    public async Task<IActionResult> OnPostExcelAsync(
        [FromForm(Name = "livro")] IFormFile? livro,
        [FromForm(Name = "desde")] string? desde,
        [FromForm(Name = "escritas")] string? escritas)
    {
        if (livro is null || livro.Length == 0 || livro.Length > ExcelTamanhoMaximo)
        {
            return ExcelRecusa("Escolha o Excel das Boquilhas (.xlsx) nas Definições das Boquilhas.");
        }

        var hoje = HojeDoExcel;
        if (!DateOnly.TryParseExact(desde, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var inicio))
        {
            inicio = hoje;
        }

        var anteriores = LerEscritas(escritas);
        using var memoria = new MemoryStream();
        await livro.CopyToAsync(memoria, HttpContext.RequestAborted);
        BqExcelResultado resultado;
        try
        {
            resultado = await excel.EscreverAsync(memoria.ToArray(), inicio > hoje ? hoje : inicio, hoje, anteriores, HttpContext.RequestAborted);
        }
        catch (BqExcelInvalidoException recusa)
        {
            return ExcelRecusa(recusa.Message);
        }

        return ExcelFicheiro(resultado);
    }

    /// <summary>
    /// POST: «ATUALIZAR» o Excel das Boquilhas (Diogo, 22/10/2026: «dia
    /// anterior está igual ao que a app tem? Se sim não atualizamos, acabou.
    /// Se não, atualiza e pergunta se o dia antes do anterior está igual …»).
    /// Escreve hoje e recua a partir de ontem até ao primeiro dia igual; a
    /// mensagem diz que dias foram atualizados. O ficheiro nunca fica no servidor.
    /// </summary>
    public async Task<IActionResult> OnPostExcelAtualizarAsync(
        [FromForm(Name = "livro")] IFormFile? livro,
        [FromForm(Name = "escritas")] string? escritas)
    {
        if (livro is null || livro.Length == 0 || livro.Length > ExcelTamanhoMaximo)
        {
            return ExcelRecusa("Escolha o Excel das Boquilhas (.xlsx) nas Definições das Boquilhas.");
        }

        var hoje = HojeDoExcel;
        using var memoria = new MemoryStream();
        await livro.CopyToAsync(memoria, HttpContext.RequestAborted);
        BqExcelAtualizacao atualizacao;
        try
        {
            atualizacao = await excel.AtualizarAsync(memoria.ToArray(), hoje, LerEscritas(escritas), HttpContext.RequestAborted);
        }
        catch (BqExcelInvalidoException recusa)
        {
            return ExcelRecusa(recusa.Message);
        }

        Response.Headers["X-Dmo-Excel-Mensagem"] = Uri.EscapeDataString(atualizacao.Mensagem);
        Response.Headers["X-Dmo-Excel-Atualizados"] = string.Join(",", atualizacao.Atualizados.Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        return ExcelFicheiro(atualizacao.Resultado);
    }

    private static IReadOnlyList<BqExcelEscrita> LerEscritas(string? escritas)
    {
        try
        {
            return string.IsNullOrWhiteSpace(escritas) ? [] :
                System.Text.Json.JsonSerializer.Deserialize<List<BqExcelEscrita>>(escritas, ExcelJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private FileContentResult ExcelFicheiro(BqExcelResultado resultado)
    {
        Response.Headers["X-Dmo-Excel-Versao"] = resultado.Versao;
        if (resultado.Avisos.Count > 0)
        {
            Response.Headers["X-Dmo-Excel-Avisos"] = Uri.EscapeDataString(string.Join("\n", resultado.Avisos));
        }

        Response.Headers["X-Dmo-Excel-Escritas"] = Uri.EscapeDataString(
            System.Text.Json.JsonSerializer.Serialize(resultado.Escritas ?? [], ExcelJson));
        return File(resultado.Livro, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    /// <summary>
    /// GET: um Excel novo, vazio, no formato do Excel das Boquilhas, para o
    /// navegador gravar na pasta escolhida (Diogo, 21/10/2026: «escolher dir
    /// do excel que vamos criar»). O ficheiro nunca fica no servidor.
    /// </summary>
    public IActionResult OnGetExcelNovo([FromQuery] DateOnly? dia) =>
        File(excel.Novo(dia ?? DateOnly.FromDateTime(DateTime.Today)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    private static readonly System.Text.Json.JsonSerializerOptions ExcelJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    private ContentResult ExcelRecusa(string mensagem) => new()
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity,
        ContentType = "text/plain; charset=utf-8",
        Content = mensagem
    };
}
