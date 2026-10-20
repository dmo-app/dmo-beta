using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O «Enviar» do Resumo do controlo (Diogo, 19/10/2026, D9 e «essa opção deve
/// existir no resumo do controlo; pode ser acedido pelo resumo ou pelo
/// histórico»): envia o PDF do Peso da produção à lista escolhida, com o
/// modelo escolhido, e volta ao Resumo com o resultado. Não tem ecrã próprio:
/// a janela do envio está no Resumo (componente EnvioPeso).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class EnviarPesoModel(
    EnviarPesoPorEmailHandler enviar,
    IEnsureControloForJobOnHandler controlos) : PageModel
{
    public IActionResult OnGet([FromQuery(Name = "jobon_id")] int? jobOnId) =>
        RedirectToPage("/Controlo/Resumo", new { jobon_id = jobOnId });

    /// <summary>
    /// POST: enviar. O mesmo envio repetido pela ligação (X-Dmo-Envio) não
    /// envia outra vez; um clique novo em «Enviar» é um envio novo.
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "modelo_id")] int? modeloId,
        [FromForm(Name = "lista_id")] int? listaId)
    {
        // O controlo vem da produção, no servidor: nunca do formulário.
        var controloId = jobOnId is int jobOn
            ? await controlos.FindExistingIdAsync(jobOn, HttpContext.RequestAborted)
            : null;
        var resultado = controloId is int controlo
            ? await enviar.EnviarAsync(
                controlo,
                modeloId,
                listaId,
                User.FindFirst(DmoClaimTypes.OperatorId)?.Value ?? User.FindFirst(DmoClaimTypes.Email)?.Value ?? "—",
                User.FindFirst(DmoClaimTypes.DisplayName)?.Value ?? User.FindFirst(DmoClaimTypes.Email)?.Value,
                HttpContext.RequestAborted)
            : EnviarPesoResultado.SemPeso;
        return RedirectToPage("/Controlo/Resumo", null, new { jobon_id = jobOnId, envio = resultado.ToString() }, "resumoEnvio");
    }
}
