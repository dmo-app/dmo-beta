using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// As Definições das Ferramentas (Diogo, 17/10/2026: «ferramentas nao deve
/// ficar no admin»): os TOTAIS DE LOTE POR MÁQUINA. Um lote de BQ da B1 tem
/// quase sempre 120: o total preenche a quantidade ao criar a ferramenta e
/// pode ser mudado nela. O quadro mostra as máquinas e os componentes em
/// letra legível e grava com um único «Guardar» por baixo. Gravar não altera
/// ferramentas já registadas.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class DefinicoesModel(IToolLotQuantitySettings toolLotQuantities, ShellStateFactory shellState) : PageModel
{
    public IReadOnlyList<ToolLotQuantityDefault> ToolQuantities { get; private set; } = Array.Empty<ToolLotQuantityDefault>();

    /// <summary>O aviso depois de gravar: as quantidades ilegíveis ou negativas que ficaram de fora.</summary>
    public string? ToolQuantityWarning { get; private set; }

    /// <summary>Quantos totais ainda estão por definir (máquina × tipo sem valor).</summary>
    public int MissingCount => ToolQuantities.Count(q => q.Quantity is null);

    public string MissingSummary => MissingCount == 0
        ? "Todos os totais estão definidos."
        : $"{MissingCount} de {ToolQuantities.Count} totais por definir.";

    public async Task<IActionResult> OnGetAsync(int? guardado)
    {
        Saved = guardado == 1;
        await LoadAsync();
        return Page();
    }

    /// <summary>TRUE depois de gravar sem avisos (PRG ?guardado=1).</summary>
    public bool Saved { get; private set; }

    /// <summary>O quadro submetido, devolvido quando a gravação é recusada.</summary>
    public IReadOnlyDictionary<string, string?>? ToolQuantityDraft { get; private set; }

    /// <summary>
    /// POST: gravar o quadro dos totais com o botão «Guardar» (Diogo,
    /// 17/10/2026: «botão pode ser apenas guardar»). Uma quantidade má não
    /// recusa as outras (decisão do proprietário, 16/10/2026): as válidas
    /// ficam gravadas e o aviso diz o que ficou de fora.
    /// </summary>
    public async Task<IActionResult> OnPostSaveToolLotQuantitiesAsync([FromForm] Dictionary<string, string?> quantities)
    {
        var outcome = await toolLotQuantities.SaveAsync(quantities, HttpContext.RequestAborted);
        if (outcome.Result == SaveToolLotQuantitiesResult.Success && outcome.Ignored.Count == 0)
            return RedirectToPage("/Ferramentas/Definicoes", new { guardado = 1 });

        await LoadAsync();
        if (outcome.Result == SaveToolLotQuantitiesResult.Success)
        {
            var ignored = string.Join(", ", outcome.Ignored.Select(i => $"{i.Type.ToString().ToUpperInvariant()} na {i.MachineCode} («{i.Submitted}»)"));
            ToolQuantityWarning = $"Os outros totais foram guardados, exceto: {ignored}. Estes campos mantêm o valor anterior; use quantidades inteiras iguais ou superiores a 0.";
            return Page();
        }

        ToolQuantityDraft = quantities;
        ToolQuantityWarning = "Uma das definições já não corresponde a uma máquina registada. Nenhuma definição foi alterada.";
        return Page();
    }

    private async Task LoadAsync()
    {
        ViewData["Title"] = "Definições — Ferramentas";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = "definicoes",
            Functions = ShellStateFactory.FerramentasFunctions("definicoes")
        };
        ToolQuantities = await toolLotQuantities.ListAsync(HttpContext.RequestAborted);
    }
}
