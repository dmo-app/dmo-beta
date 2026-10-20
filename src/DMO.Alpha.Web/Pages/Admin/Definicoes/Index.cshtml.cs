using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Definicoes;

/// <summary>
/// O SETUP da Administração (Diogo, 17/10/2026: «o associar email entra numa
/// página de setup, vamos usar mais tarde para outras coisas»): o email do
/// ADMIN. Não é um wizard de instalação: é a página onde se muda o email.
/// As definições dos módulos vivem em cada módulo (Controlo, Boquilhas,
/// Ferramentas) e a hora de viragem no Canal Jobon. Os links antigos
/// (?definition=…) abrem esta página.
/// </summary>
public class IndexModel(IAdminEmailSettings adminEmailSettings) : PageModel
{
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
    public bool SaveSucceeded => SaveResult == "ok-admin";

    public async Task<IActionResult> OnGetAsync(string? save, string? email)
    {
        SaveResult = save;
        if (save == "admin-email-sem-conta")
        {
            var comEmail = email?.Trim() is { Length: > 0 and <= 256 } semConta
                ? $"?email={Uri.EscapeDataString(semConta)}"
                : null;
            NoAccountLink = new DmoAvisoLink("Criar o utilizador", $"/Admin/Users{comEmail}#dmuCriar");
        }
        AdminEmail = await adminEmailSettings.GetAdminEmailAsync(HttpContext.RequestAborted);
        return Page();
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
            return RedirectToPage("/Admin/Definicoes/Index", new { save = "admin-email-diferente" });
        }

        var outcome = await adminEmailSettings.SaveAdminEmailAsync(
            adminEmail ?? string.Empty,
            HttpContext.RequestAborted);

        return outcome switch
        {
            SaveAdminEmailResult.Success => RedirectToPage("/Admin/Definicoes/Index", new { save = "ok-admin" }),
            // Um email sem conta não se grava: com ele, ninguém voltava a
            // entrar no Admin (sweep de 19/10/2026, AD-1).
            SaveAdminEmailResult.NoAccount => RedirectToPage("/Admin/Definicoes/Index",
                new { save = "admin-email-sem-conta", email = adminEmail?.Trim() }),
            _ => RedirectToPage("/Admin/Definicoes/Index", new { save = "admin-email-invalida" })
        };
    }
}
