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
public sealed class DefinicoesModel(IReparadores reparadores) : PageModel
{
    public IReadOnlyList<ReparadorItem> Reparadores { get; private set; } = [];

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

    /// <summary>O valor da escolha das famílias: «ambos», «cm» ou «mf».</summary>
    public static string Familias(ReparadorItem r) => r.AplicaCm && r.AplicaMf ? "ambos" : r.AplicaCm ? "cm" : "mf";

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
