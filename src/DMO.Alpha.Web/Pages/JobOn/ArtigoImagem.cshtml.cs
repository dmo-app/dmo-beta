using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// A imagem do artigo da Folha do Job On (Diogo, 26/10/2026): uma por
/// referência completa, guardada na base e igual em todos os Job On dessa
/// referência. Ver: quem vê o Job On. Associar, trocar ou retirar: só quem
/// tem o Job On criar (um POST forjado de quem só vê não muda nada).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnView, DmoCapabilities.JobOnCreate)]
[RequestSizeLimit(JobOnArtigoImagem.TamanhoMaximo + 64 * 1024)]
public sealed class ArtigoImagemModel(JobOnArtigoImagemHandler imagens, CurrentUserCapabilities capabilities) : PageModel
{
    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "ref")] string? referencia)
    {
        var imagem = await imagens.LerAsync(referencia, HttpContext.RequestAborted);
        if (imagem is null)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(imagem.Dados, imagem.ContentType, imagem.AtualizadoEmUtc, new Microsoft.Net.Http.Headers.EntityTagHeaderValue(
            $"\"{imagem.AtualizadoEmUtc.Ticks:x}\""));
    }

    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "ref")] string? referencia, IFormFile? imagem)
    {
        var ct = HttpContext.RequestAborted;
        if (!await capabilities.HasAsync(DmoCapabilities.JobOnCreate, ct))
        {
            return Forbid();
        }
        if (imagem is null || imagem.Length == 0 || imagem.Length > JobOnArtigoImagem.TamanhoMaximo)
        {
            return BadRequest();
        }
        using var memoria = new MemoryStream();
        await imagem.CopyToAsync(memoria, ct);
        var guardou = await imagens.GuardarAsync(
            referencia, imagem.ContentType, memoria.ToArray(), IndexModel.ActorName(User), ct);
        return guardou ? new NoContentResult() : BadRequest();
    }

    public async Task<IActionResult> OnPostRetirarAsync([FromForm(Name = "ref")] string? referencia)
    {
        var ct = HttpContext.RequestAborted;
        if (!await capabilities.HasAsync(DmoCapabilities.JobOnCreate, ct))
        {
            return Forbid();
        }
        await imagens.RetirarAsync(referencia, ct);
        return new NoContentResult();
    }
}
