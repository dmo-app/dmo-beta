using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Services;
using DMO.Alpha.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace DMO.Alpha.Web.Pages.Admin.Definicoes;

/// <summary>
/// O SETUP da Administração (Diogo, 17/10/2026: «o associar email entra numa
/// página de setup, vamos usar mais tarde para outras coisas»): o email do
/// ADMIN. Não é um wizard de instalação: é a página onde se muda o email.
/// As definições dos módulos vivem em cada módulo (Controlo, Boquilhas,
/// Ferramentas) e a hora de viragem no Canal Jobon. Os links antigos
/// (?definition=…) abrem esta página.
/// </summary>
public class IndexModel(
    IAdminEmailSettings adminEmailSettings,
    IPinDasDefinicoes pinDasDefinicoes,
    ISapDefinicoes sapDefinicoes,
    SapScripts sapScripts,
    IFerramentasUsoSap usoSap,
    ISapProcuraRegras sapProcura,
    DMO.Alpha.Core.Production.IMachineListQuery maquinas) : PageModel
{
    /// <summary>A tabela dos códigos de procura no SAP (DMO-585).</summary>
    public IReadOnlyList<SapProcuraRegra> SapProcura { get; private set; } = [];

    /// <summary>As Linhas para a tabela (os códigos das máquinas).</summary>
    public IReadOnlyList<string> SapLinhas { get; private set; } = [];

    /// <summary>POST: grava uma linha da tabela dos códigos de procura (ou tira-a, com «Tirar»).</summary>
    public async Task<IActionResult> OnPostSaveSapProcuraAsync(
        [FromForm(Name = "procura_tipo")] string? tipo,
        [FromForm(Name = "procura_linha")] string? linha,
        [FromForm(Name = "procura_antes")] string? antes,
        [FromForm(Name = "procura_meio")] string? meio,
        [FromForm(Name = "procura_depois")] string? depois,
        [FromForm(Name = "procura_lote")] bool lote,
        [FromForm(Name = "procura_tirar")] string? tirar)
    {
        ToolType? tipoPedido = tipo?.ToLowerInvariant() switch
        {
            "cm" => ToolType.Cm,
            "mf" => ToolType.Mf,
            "bq" => ToolType.Bq,
            _ => null
        };
        if (tipoPedido is not { } t)
        {
            return VoltarSap("procura-invalida");
        }

        if (!string.IsNullOrEmpty(tirar))
        {
            await sapProcura.ApagarAsync(t, linha ?? string.Empty, HttpContext.RequestAborted);
            return VoltarSap("ok-procura-tirada");
        }

        var regra = new SapProcuraRegra(t, linha ?? string.Empty, antes ?? string.Empty,
            meio?.ToLowerInvariant() switch
            {
                "referencia" => SapProcuraMeio.Referencia,
                "processo" => SapProcuraMeio.NomeTecnicoAteProcesso,
                _ => SapProcuraMeio.NomeTecnico
            }, depois ?? string.Empty, lote);
        var resultado = await sapProcura.GravarAsync(regra, HttpContext.RequestAborted);
        return VoltarSap(resultado == SapProcuraGravacao.Gravado ? "ok-procura" : "procura-invalida");
    }

    /// <summary>GET: os scripts do SAP para o «Atualizar do SAP» do Admin › SAP (escritos na pasta pelo browser).</summary>
    public async Task<IActionResult> OnGetScriptsSapAsync()
    {
        // Diogo, 29/10/2026: sem senha o script prendeu o PC na janela da senha (DMO-661).
        if (!await sapScripts.TemSenhaAsync(HttpContext.RequestAborted))
        {
            return new ConflictObjectResult(new { erro = "Falta a senha do SAP: escreva-a em «Senha do SAP», grave e carregue outra vez." });
        }

        var ficheiros = await sapScripts.FicheirosAsync($"{Request.Scheme}://{Request.Host}{Request.PathBase}", HttpContext.RequestAborted);
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(ficheiros.Select(f => new { nome = f.Nome, base64 = Convert.ToBase64String(f.Conteudo) }));
    }

    /// <summary>GET: os códigos de uma referência (o «Por referência» do Admin › SAP).</summary>
    public async Task<IActionResult> OnGetCodigosSapAsync(string? referencia, string? tipo)
    {
        ToolType? tipoPedido = tipo?.ToLowerInvariant() switch
        {
            "cm" => ToolType.Cm,
            "mf" => ToolType.Mf,
            "bq" => ToolType.Bq,
            _ => null
        };
        var codigos = await usoSap.CodigosDaReferenciaAsync(referencia ?? string.Empty, tipoPedido, HttpContext.RequestAborted);
        return new JsonResult(new { codigos });
    }

    /// <summary>O SAP nas Definições do ADMIN (Diogo, 28/10/2026: «nas definições do admin cria uma página SAP»).</summary>
    public SapDefinicoesDados? Sap { get; private set; }

    public IReadOnlyList<SapLeituraFeita> SapLeituras { get; private set; } = [];

    /// <summary>O texto de cada script (o mudado no Admin ou o da app) e se foi mudado.</summary>
    public IReadOnlyList<(string Nome, string Texto, bool Mudado)> SapScriptsTextos { get; private set; } = [];

    public string? SapResult { get; private set; }

    public bool SapSucceeded => SapResult is "ok" or "ok-script" or "ok-repor" or "ok-procura" or "ok-procura-tirada";

    public string SapResultMessage => SapResult switch
    {
        "ok" => "Definições do SAP guardadas. Os scripts da pasta mudam no próximo «Atualizar do SAP».",
        "ok-script" => "Script guardado.",
        "ok-repor" => "Script reposto como o da app.",
        "ok-procura" => "Código de procura guardado. Vale no próximo «Atualizar do SAP».",
        "ok-procura-tirada" => "Linha tirada da tabela dos códigos de procura.",
        "procura-invalida" => "O código de procura só pode ter letras, algarismos, «-», «_» e «*» (até 20).",
        nameof(SapGravacao.UtilizadorInvalido) => "O utilizador do SAP não pode ficar vazio nem levar aspas.",
        nameof(SapGravacao.SenhaInvalida) => $"A senha tem no máximo {SapDefinicoesRegras.SenhaMaxima} letras.",
        nameof(SapGravacao.HoraInvalida) => "A hora é de 0 a 23.",
        nameof(SapGravacao.DiasInvalidos) => $"Os dias são de {SapDefinicoesRegras.DiasMinimo} a {SapDefinicoesRegras.DiasMaximo}.",
        nameof(SapGravacao.ScriptDesconhecido) => "Esse script não existe.",
        nameof(SapGravacao.ScriptGrande) => "O script é grande demais.",
        _ => string.Empty
    };

    /// <summary>Carrega o Admin › SAP (?sap=…).</summary>
    public async Task CarregarSapAsync(string? resultado)
    {
        SapResult = resultado;
        Sap = await sapDefinicoes.LerAsync(HttpContext.RequestAborted);
        SapLeituras = await sapDefinicoes.LeiturasAsync(20, HttpContext.RequestAborted);
        SapProcura = await sapProcura.LerAsync(HttpContext.RequestAborted);
        SapLinhas = (await maquinas.ListAsync(HttpContext.RequestAborted)).Select(m => m.Code).ToList();
        var textos = new List<(string, string, bool)>();
        // O INSTALAR.cmd não se edita: só instala (Diogo, 28/10/2026 18:31).
        foreach (var nome in SapScriptsRegras.Nomes.Where(n => n != SapScriptsRegras.Instalar))
        {
            var editado = await sapDefinicoes.ScriptEditadoAsync(nome, HttpContext.RequestAborted);
            textos.Add((nome, editado ?? SapScripts.Original(nome), editado is not null));
        }

        SapScriptsTextos = textos;
    }

    /// <summary>POST: o utilizador, a senha (vazia mantém), a hora da leitura diária e os dias de produções.</summary>
    public async Task<IActionResult> OnPostSaveSapAsync(
        [FromForm(Name = "sap_utilizador")] string? utilizador,
        [FromForm(Name = "sap_senha")] string? senha,
        [FromForm(Name = "sap_hora")] int hora,
        [FromForm(Name = "sap_dias")] int dias)
    {
        var resultado = await sapDefinicoes.GravarAsync(utilizador, senha, hora, dias, HttpContext.RequestAborted);
        return VoltarSap(resultado == SapGravacao.Gravado ? "ok" : resultado.ToString());
    }

    /// <summary>POST: o texto de um script mudado à mão; «Repor» volta ao da app.</summary>
    public async Task<IActionResult> OnPostSaveSapScriptAsync(
        [FromForm(Name = "sap_script")] string? nome,
        [FromForm(Name = "sap_texto")] string? texto,
        [FromForm(Name = "sap_repor")] string? repor)
    {
        var reposto = !string.IsNullOrEmpty(repor);
        if (!reposto && texto is not null && SapScriptsRegras.Conhecido(nome) && texto.Replace("\r\n", "\n") == SapScripts.Original(nome!))
        {
            reposto = true;
        }

        var resultado = await sapDefinicoes.GravarScriptAsync(nome ?? string.Empty, reposto ? null : texto, HttpContext.RequestAborted);
        return VoltarSap(resultado != SapGravacao.Gravado ? resultado.ToString() : reposto ? "ok-repor" : "ok-script");
    }

    private RedirectToPageResult VoltarSap(string resultado) =>
        RedirectToPage("/Definicoes/Index", null, new RouteValueDictionary { ["secao"] = "admin", ["sub"] = "sap", ["sap"] = resultado }, null);

    /// <summary>TRUE quando o PIN das Definições já foi criado.</summary>
    public bool TemPin { get; private set; }

    public string? PinResult { get; private set; }

    public bool PinSucceeded => PinResult == "ok";

    public string PinResultMessage => PinResult switch
    {
        "ok" => "PIN das Definições guardado.",
        "invalido" => $"O PIN tem de ter de {PinDasDefinicoesRegras.Minimo} a {PinDasDefinicoesRegras.Maximo} algarismos.",
        "diferente" => "Os dois PINs não coincidem. O PIN não foi alterado.",
        _ => string.Empty
    };

    /// <summary>
    /// POST: criar ou mudar o PIN DAS DEFINIÇÕES (Diogo, 22/10/2026): abre o
    /// cadeado da parte Geral das Definições. Guarda-se só o hash; mudar o PIN
    /// fecha os cadeados já abertos. ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostSavePinAsync(
        [FromForm(Name = "pin")] string? pin,
        [FromForm(Name = "pin_confirmar")] string? pinConfirmar)
    {
        if (!string.Equals(pin, pinConfirmar, StringComparison.Ordinal))
        {
            return Voltar(new { pinapp = "diferente" });
        }

        var resultado = await pinDasDefinicoes.DefinirAsync(pin, HttpContext.RequestAborted);
        return Voltar(new { pinapp = resultado == PinGravacao.Gravado ? "ok" : "invalido" });
    }

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    /// <summary>
    /// O EMAIL DO ADMIN (o Owner 2026-10-16: o ADMIN é apenas um email):
    /// o login por email compara com este valor; null num ambiente novo (o
    /// primeiro login por email com sucesso assume-o).
    /// </summary>
    public string? AdminEmail { get; private set; }

    public string SaveResultMessage => SaveResult switch
    {
        "ok-admin" => "Email do ADMIN guardado.",
        "admin-email-invalida" => "O email indicado não é utilizável.",
        "admin-email-diferente" => "Os dois emails não coincidem. O email do ADMIN não foi alterado.",
        "admin-email-sem-conta" => "Este email ainda não tem conta. Crie primeiro o utilizador em Admin → Utilizadores.",
        _ => string.Empty
    };

    /// <summary>
    /// O link da recusa de um email sem conta: abre o «Criar utilizador» em
    /// Admin → Utilizadores, já com o email escrito. Null nas outras mensagens.
    /// </summary>
    public DmoAvisoLink? NoAccountLink { get; private set; }

    /// <summary>TRUE quando o save transportado é uma gravação feita (a mensagem fecha sozinha).</summary>
    public bool SaveSucceeded => SaveResult is "ok-admin";

    /// <summary>GET: a App vive nas Definições do ADMIN (Diogo, 22/10/2026).</summary>
    public IActionResult OnGet() => Redirect(Destino);

    public const string Destino = "/Definicoes?secao=admin&sub=app";

    /// <summary>Carrega os cartões para as Definições → Admin → App (?app=…, ?pinapp=…).</summary>
    public async Task CarregarAsync(string? save, string? email, string? pin)
    {
        PinResult = pin;
        TemPin = await pinDasDefinicoes.HashAsync(HttpContext.RequestAborted) is not null;
        SaveResult = save;
        if (save == "admin-email-sem-conta")
        {
            var comEmail = email?.Trim() is { Length: > 0 and <= 256 } semConta
                ? $"?email={Uri.EscapeDataString(semConta)}"
                : null;
            NoAccountLink = new DmoAvisoLink("Criar o utilizador", $"/Admin/Users{comEmail}#dmuCriar");
        }
        AdminEmail = await adminEmailSettings.GetAdminEmailAsync(HttpContext.RequestAborted);
    }

    private RedirectToPageResult Voltar(object valores, string? fragmento = null)
    {
        var rota = new RouteValueDictionary { ["secao"] = "admin", ["sub"] = "app" };
        foreach (var (chave, valor) in new RouteValueDictionary(valores))
        {
            rota[chave] = valor;
        }

        return RedirectToPage("/Definicoes/Index", null, rota, fragmento);
    }

    /// <summary>
    /// POST: guardar O EMAIL DO ADMIN: quem autentica com este email entra
    /// como ADMIN; a sessão do ADMIN anterior morre no pedido seguinte. Num
    /// ambiente novo, o campo vazio é assumido pelo primeiro login por email
    /// com sucesso. ADMIN apenas.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAdminEmailAsync(
        [FromForm(Name = "admin_email")] string? adminEmail,
        [FromForm(Name = "admin_email_confirm")] string? adminEmailConfirm)
    {
        // Um erro de escrita tranca o ADMIN fora da app (a sessão cai no
        // pedido seguinte): o email é escrito duas vezes antes de gravar.
        if (!string.Equals(adminEmail?.Trim(), adminEmailConfirm?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Voltar(new { app = "admin-email-diferente" });
        }

        var outcome = await adminEmailSettings.SaveAdminEmailAsync(
            adminEmail ?? string.Empty,
            HttpContext.RequestAborted);

        return outcome switch
        {
            SaveAdminEmailResult.Success => Voltar(new { app = "ok-admin" }),
            // Um email sem conta não se grava: com ele, ninguém voltava a
            // entrar no Admin (sweep de 19/10/2026, AD-1).
            SaveAdminEmailResult.NoAccount => Voltar(
                new { app = "admin-email-sem-conta", email = adminEmail?.Trim() }),
            _ => Voltar(new { app = "admin-email-invalida" })
        };
    }
}
