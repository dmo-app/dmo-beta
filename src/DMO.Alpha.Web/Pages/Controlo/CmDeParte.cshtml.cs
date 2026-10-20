using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Aprova, não aprova ou volta a pôr por aprovar uma CM posta de parte na
/// Comparação do Peso, uma decisão por CM (o Diogo, 19/10/2026, D10), e volta
/// à comparação: a aprovação faz-se na própria tabela da comparação, não numa
/// página à parte (o Diogo, 19/10/2026). Só POST, do Controlo Aprovar. É só
/// registo: aprovar não muda o estado da CM nas Ferramentas (D11).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloApprove)]
public sealed class CmDeParteModel(PesoCmDeParteHandler handler) : PageModel
{
    private const string Padrao = "/Controlo/Peso?comparacao=1";

    public IActionResult OnGet() => LocalRedirect(Padrao);

    /// <summary>
    /// POST: leva a escolha que a comparação mostrou (escolhida_em): se mudou
    /// entretanto, nada se grava.
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "measurement_id")] int measurementId,
        [FromForm(Name = "decisao")] string? decisao,
        [FromForm(Name = "escolhida_em")] long escolhidaEmTicks,
        [FromForm(Name = "voltar")] string? voltar)
    {
        if (CmDeParte.Quem(User) is not { } quem)
        {
            return LocalRedirect(CmDeParte.Voltar(Url, voltar, Padrao, "sem-identidade"));
        }

        bool? aprovado = decisao switch
        {
            "aprovar" => true,
            "nao-aprovar" => false,
            _ => null
        };
        var escolhidaEm = escolhidaEmTicks > 0 && escolhidaEmTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(escolhidaEmTicks, DateTimeKind.Utc)
            : DateTime.MinValue;
        var resultado = await handler.DecidirAsync(measurementId, aprovado, escolhidaEm, quem, CancellationToken.None);
        string? codigo = resultado switch
        {
            CmDeParteResultado.Mudou => "mudou",
            CmDeParteResultado.SemAlteracao => null,
            _ => aprovado switch { true => "ok-aprovado", false => "ok-nao-aprovado", null => "ok-por-aprovar" }
        };
        return LocalRedirect(CmDeParte.Voltar(Url, voltar, Padrao, codigo));
    }
}
