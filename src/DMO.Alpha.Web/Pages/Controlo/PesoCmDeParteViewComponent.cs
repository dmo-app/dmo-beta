using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O «Manter / Colocar de parte» de cada CM de uma comparação guardada (D10,
/// 19/10/2026), na Comparação do Peso e no detalhe do Histórico. Um
/// componente com os seus dados, para a página do Peso (que já passa das
/// 1500 linhas) só o chamar.
/// </summary>
public sealed class PesoCmDeParteViewComponent(
    PesoCmDeParteHandler handler,
    CurrentUserCapabilities capabilities) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int comparacaoId)
    {
        var linhas = await handler.DaComparacaoAsync(comparacaoId, HttpContext.RequestAborted);
        var granted = await capabilities.GetAsync(HttpContext.RequestAborted);
        var request = HttpContext.Request;
        // Volta a esta página depois de gravar; numa resposta a um POST não
        // há endereço a repetir, e a gravação volta ao detalhe da comparação.
        var voltar = HttpMethods.IsGet(request.Method)
            ? $"{request.Path}{request.QueryString}"
            : $"/Controlo/Peso?historico=1&comparacao_id={comparacaoId}";
        return View(new PesoCmDeParteVista(
            comparacaoId,
            linhas,
            voltar,
            granted.Contains(DmoCapabilities.ControloApprove, StringComparer.Ordinal),
            CmDeParte.Mensagem(request.Query[CmDeParte.Parametro])));
    }
}

/// <summary>As CM de uma comparação e a escolha de cada uma.</summary>
public sealed record PesoCmDeParteVista(
    int ComparacaoId,
    IReadOnlyList<CmDecisaoLinha> Linhas,
    string Voltar,
    bool PodeAprovar,
    (string Tipo, string Texto)? Mensagem);
