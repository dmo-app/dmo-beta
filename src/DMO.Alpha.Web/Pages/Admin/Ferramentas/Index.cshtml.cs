using DMO.Alpha.Core.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Ferramentas;

/// <summary>
/// Os TOTAIS DE LOTE POR MÁQUINA numa página própria da Administração (o
/// proprietário, 17/10/2026: «sim vamos avançar com os totais»). Saem do
/// seletor das Definições. Um lote de BQ da B1 tem quase sempre 120: o total
/// preenche a quantidade ao criar a ferramenta e pode ser mudado nela.
/// Gravar aqui não altera ferramentas já registadas.
/// </summary>
public class IndexModel(IToolLotQuantitySettings toolLotQuantities) : PageModel
{
    public IReadOnlyList<ToolLotQuantityDefault> ToolQuantities { get; private set; } = Array.Empty<ToolLotQuantityDefault>();
    public IReadOnlyDictionary<string, string?>? ToolQuantityDraft { get; private set; }
    public string? ToolQuantityMessage { get; private set; }

    /// <summary>O aviso depois de gravar: as quantidades ilegíveis ou negativas que ficaram de fora.</summary>
    public string? ToolQuantityWarning { get; private set; }

    /// <summary>Quantos totais ainda estão por definir (máquina × tipo sem valor).</summary>
    public int MissingCount => ToolQuantities.Count(q => q.Quantity is null);

    public async Task<IActionResult> OnGetAsync(string? save)
    {
        await LoadAsync();
        if (save == "ok-quantidades") ToolQuantityMessage = "Quantidades por lote guardadas.";
        return Page();
    }

    public async Task<IActionResult> OnPostSaveToolLotQuantitiesAsync([FromForm] Dictionary<string, string?> quantities)
    {
        var outcome = await toolLotQuantities.SaveAsync(quantities, HttpContext.RequestAborted);
        if (outcome.Result == SaveToolLotQuantitiesResult.Success && outcome.Ignored.Count == 0)
            return RedirectToPage("/Admin/Ferramentas/Index", new { save = "ok-quantidades" });
        await LoadAsync();
        if (outcome.Result == SaveToolLotQuantitiesResult.Success)
        {
            // Decisão do proprietário (16/10/2026): uma quantidade má não
            // recusa as outras. As válidas ficaram gravadas; o quadro mostra
            // o que está guardado e o aviso diz o que ficou de fora.
            var ignored = string.Join(", ", outcome.Ignored.Select(i =>
                $"{i.Type.ToString().ToUpperInvariant()} na {i.MachineCode} («{i.Submitted}»)"));
            ToolQuantityWarning = $"Quantidades por lote guardadas, exceto: {ignored}. Estes campos mantêm o valor anterior; use quantidades inteiras iguais ou superiores a 0.";
            return Page();
        }
        ToolQuantityDraft = quantities;
        ToolQuantityMessage = "Uma das definições já não corresponde a uma máquina registada. Nenhuma definição foi alterada.";
        return Page();
    }

    private async Task LoadAsync() =>
        ToolQuantities = await toolLotQuantities.ListAsync(HttpContext.RequestAborted);
}
