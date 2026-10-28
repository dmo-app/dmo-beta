using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// As Definições do Armazém: os reparadores de CM e MF (plano do full, peça
/// A; blueprint admin/MAQUINAS-E-REPARADORES). Ficam no módulo que os usa,
/// como os reparadores das Boquilhas nas Definições das Boquilhas, e quem tem
/// o Armazém muda-os (recomendação da MR-02). O nome muda-se no próprio
/// valor; as famílias e o estado escolhem-se na linha e gravam logo.
/// </summary>
[Authorize]
[DefinicoesEntry("armazem", DmoCapabilities.ArmazemGestao)]
public sealed class DefinicoesModel(IReparadores reparadores, IFabricasBa fabricas) : PageModel
{
    public IReadOnlyList<ReparadorItem> Reparadores { get; private set; } = [];

    /// <summary>As fábricas da BA (Diogo, 28/10/2026): começa vazia, só se acrescenta e desativa.</summary>
    public IReadOnlyList<FabricaBaItem> Fabricas { get; private set; } = [];

    public string? Gravacao { get; private set; }

    public bool Gravou => Gravacao == "ok";

    public string Mensagem => MensagemDe(Gravacao);

    private static string MensagemDe(string? gravacao) => gravacao switch
    {
        "ok" => "Definição gravada.",
        "vazio" => "O nome do reparador não pode ficar em branco.",
        "longo" => $"O nome do reparador tem no máximo {Reparador.NomeMaxLength} caracteres.",
        "existe" => "Já existe um reparador de CM e MF com esse nome.",
        "familia" => "Escolha se o reparador recebe CM, MF ou os dois.",
        "nao-existe" => "Esse reparador já não existe.",
        "fab-sigla" => "A sigla da fábrica tem duas letras (por exemplo MG).",
        "fab-vazio" => "O nome da fábrica não pode ficar em branco.",
        "fab-longo" => $"O nome da fábrica tem no máximo {FabricaBa.NomeMaxLength} caracteres.",
        "fab-existe" => "Já existe uma fábrica com essa sigla.",
        "fab-nao-existe" => "Essa fábrica já não existe.",
        _ => string.Empty
    };

    private static string Token(ReparadorResultado resultado) => resultado switch
    {
        ReparadorResultado.Gravado => "ok",
        ReparadorResultado.NomeVazio => "vazio",
        ReparadorResultado.NomeLongo => "longo",
        ReparadorResultado.JaExiste => "existe",
        ReparadorResultado.SemFamilia => "familia",
        _ => "nao-existe"
    };


    private static (bool Cm, bool Mf)? LerFamilias(string? valor) => valor switch
    {
        "ambos" => (true, true),
        "cm" => (true, false),
        "mf" => (false, true),
        _ => null
    };

    /// <summary>GET: as definições vivem na página única /Definicoes (Diogo, 22/10/2026).</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "armazem"));

    /// <summary>Carrega o que a secção deste módulo nas Definições mostra.</summary>
    public async Task CarregarAsync(string? gravacao)
    {

        Gravacao = gravacao;
        Reparadores = await reparadores.ListarAsync(HttpContext.RequestAborted);
        Fabricas = await fabricas.ListarAsync(HttpContext.RequestAborted);
    }

    private static string Token(FabricaBaResultado resultado) => resultado switch
    {
        FabricaBaResultado.Gravada => "ok",
        FabricaBaResultado.SiglaInvalida => "fab-sigla",
        FabricaBaResultado.NomeVazio => "fab-vazio",
        FabricaBaResultado.NomeLongo => "fab-longo",
        FabricaBaResultado.JaExiste => "fab-existe",
        _ => "fab-nao-existe"
    };

    /// <summary>POST: acrescentar uma fábrica da BA (sigla e nome).</summary>
    [EnvioUnico]
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostCriarFabricaAsync(
        [FromForm(Name = "sigla")] string? sigla,
        [FromForm(Name = "nome")] string? nome) =>
        RedirectToPage("/Definicoes/Index", new { gravacao = Token(await fabricas.CriarAsync(sigla, nome, HttpContext.RequestAborted)) });

    /// <summary>POST: mudar o nome (no próprio valor, JSON) ou ativar/desativar uma fábrica (PRG).</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostMudarFabricaAsync(
        [FromForm(Name = "fabrica_id")] int fabricaId,
        [FromForm(Name = "nome")] string? nome,
        [FromForm(Name = "ativa")] string? ativa)
    {
        var gravacao = Token(await fabricas.MudarAsync(fabricaId, nome,
            ativa switch { "sim" => true, "nao" => false, _ => null }, HttpContext.RequestAborted));
        if (Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            var ok = gravacao == "ok";
            return new JsonResult(new { ok, valor = ok ? nome?.Trim() : null, aviso = ok ? null : MensagemDe(gravacao) });
        }

        return RedirectToPage("/Definicoes/Index", new { gravacao });
    }

    /// <summary>POST: criar um reparador de CM e MF.</summary>
    [EnvioUnico]
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostCriarAsync(
        [FromForm(Name = "nome")] string? nome,
        [FromForm(Name = "familias")] string? familias)
    {
        var resultado = LerFamilias(familias) is { } f
            ? await reparadores.CriarAsync(nome, f.Cm, f.Mf, HttpContext.RequestAborted)
            : ReparadorResultado.SemFamilia;
        return RedirectToPage("/Definicoes/Index", new { gravacao = Token(resultado) });
    }

    /// <summary>
    /// POST: mudar um reparador. O nome chega do próprio valor (pedido JSON,
    /// resposta { ok, valor, aviso }); as famílias e o estado chegam das
    /// escolhas da linha (PRG).
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostMudarAsync(
        [FromForm(Name = "reparador_id")] int reparadorId,
        [FromForm(Name = "nome")] string? nome,
        [FromForm(Name = "familias")] string? familias,
        [FromForm(Name = "ativo")] string? ativo)
    {
        var f = LerFamilias(familias);
        var mudanca = new MudancaDeReparador(
            Nome: nome,
            AplicaCm: f?.Cm,
            AplicaMf: f?.Mf,
            Ativo: ativo switch { "sim" => true, "nao" => false, _ => null });
        var gravacao = Token(await reparadores.MudarAsync(reparadorId, mudanca, HttpContext.RequestAborted));

        if (Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            var ok = gravacao == "ok";
            return new JsonResult(new { ok, valor = ok ? nome?.Trim() : null, aviso = ok ? null : MensagemDe(gravacao) });
        }

        return RedirectToPage("/Definicoes/Index", new { gravacao });
    }
}
