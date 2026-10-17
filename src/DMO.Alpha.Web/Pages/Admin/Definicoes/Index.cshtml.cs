using DMO.Alpha.Core.Admin;
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
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(string? save)
    {
        SaveResult = save;
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

        var save = outcome == SaveAdminEmailResult.Success ? "ok-admin" : "admin-email-invalida";
        return RedirectToPage("/Admin/Definicoes/Index", new { save });
    }
}
