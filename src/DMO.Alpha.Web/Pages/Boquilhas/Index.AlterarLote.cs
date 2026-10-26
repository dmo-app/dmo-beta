using DMO.Alpha.Core.Production;
using Microsoft.AspNetCore.Mvc;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// «Alterar lote» no «Opções» do Registo (Diogo, 26/10/2026): a BQ da
/// produção passa para outro lote da mesma referência. O lote novo nasce num
/// bq_id novo da mesma produção e o Registo abre nele; os movimentos ficam no
/// lote antigo.
/// </summary>
public sealed partial class IndexModel
{
    private readonly IAlterarLoteBq _alterarLote;

    /// <summary>Os outros lotes da BQ do registo aberto; null quando o registo já não é o da produção.</summary>
    public LotesDaBq? LotesParaAlterar { get; private set; }

    /// <summary>O lote para onde a BQ passou, no regresso do «Alterar lote».</summary>
    [BindProperty(SupportsGet = true, Name = "lote_alterado")]
    public string? LoteAlterado { get; set; }

    /// <summary>A recusa do «Alterar lote» (o lote já tinha sido trocado ou não serve).</summary>
    [BindProperty(SupportsGet = true, Name = "lote_recusa")]
    public string? LoteRecusa { get; set; }

    public string? LoteRecusaMessage => LoteRecusa switch
    {
        nameof(AlterarLoteResultado.JaNaoEDaProducao) => "Este lote já não é o da produção: outra pessoa alterou-o entretanto. Nada mudou.",
        nameof(AlterarLoteResultado.LoteNaoServe) => "O lote escolhido não é desta BQ. Nada mudou.",
        _ => null
    };

    public async Task<IActionResult> OnPostAlterarLoteAsync(
        [FromForm(Name = "alterar_bq_id")] int bqContextId,
        [FromForm(Name = "tool_id")] string? toolId)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var outcome = await _alterarLote.AlterarAsync(
            new AlterarLote(bqContextId, toolId?.Trim() ?? string.Empty, DMO.Alpha.Web.Pages.JobOn.IndexModel.ActorName(User)),
            HttpContext.RequestAborted);
        return outcome.Resultado == AlterarLoteResultado.Alterado
            ? RedirectToPage("/Boquilhas/Index", RegisterRoute(0, outcome.BqContextId!.Value, ("lote_alterado", outcome.Lot)))
            : RedirectToPage("/Boquilhas/Index", RegisterRoute(0, bqContextId, ("lote_recusa", outcome.Resultado.ToString())));
    }
}
