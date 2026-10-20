using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Grava «Manter / Colocar de parte» de uma CM da comparação do Peso (D10,
/// 19/10/2026) e volta à comparação. Só POST, numa página própria (o Peso já
/// passa das 1500 linhas: o que é novo nasce fora dele). Quem tem o
/// Controlo Criar escolhe; a aprovação é do Controlo Aprovar (CmDeParteAprovar).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PesoCmEscolhaModel(PesoCmDeParteHandler handler) : PageModel
{
    private const string Padrao = "/Controlo/Peso?comparacao=1";

    public IActionResult OnGet() => LocalRedirect(Padrao);

    [EnvioUnico]
    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "measurement_id")] int measurementId,
        [FromForm(Name = "escolha")] string? escolha,
        [FromForm(Name = "voltar")] string? voltar)
    {
        if (CmDeParte.Quem(User) is not { } quem)
        {
            return LocalRedirect(CmDeParte.Voltar(Url, voltar, Padrao, "sem-identidade"));
        }

        bool? deParte = escolha switch
        {
            "de-parte" => true,
            "manter" => false,
            _ => null
        };
        var resultado = await handler.EscolherAsync(measurementId, deParte, quem, CancellationToken.None);
        string? codigo = resultado switch
        {
            CmDeParteResultado.NaoExiste => "nao-existe",
            CmDeParteResultado.SemAlteracao => null,
            _ => deParte switch { true => "ok-de-parte", false => "ok-manter", null => "ok-sem-escolha" }
        };
        return LocalRedirect(CmDeParte.Voltar(Url, voltar, Padrao, codigo));
    }
}
