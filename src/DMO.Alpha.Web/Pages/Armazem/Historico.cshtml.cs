using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// O Histórico do Armazém: todos os movimentos, do mais recente para o mais
/// antigo, filtrados pela referência, lote ou posição, pelo movimento, pelo
/// destino da Saída e pelos dias. Como os outros históricos (Diogo, 16 e
/// 17/10, «Procura e filtros»), abre vazio, só com os filtros, e só lista
/// depois de um filtro. Os filtros aplicam-se ao escolher (o texto, com
/// Enter); com ?ferramenta= mostra só uma ferramenta (o link da ficha).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class HistoricoModel(IArmazemHistorico historico, ShellStateFactory shellState) : PageModel
{
    public PagedList<ArmazemHistoricoLinha> Lista { get; private set; } = PagedList<ArmazemHistoricoLinha>.Empty;

    public ListPager Pager { get; private set; } = new(1, 1, 0);

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Texto { get; set; }

    [BindProperty(SupportsGet = true, Name = "ferramenta")]
    public string? ToolId { get; set; }

    [BindProperty(SupportsGet = true, Name = "movimento")]
    public string? MovimentoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "destino")]
    public string? DestinoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    public ArmazemMovimentoTipo? Movimento => Enum.TryParse<ArmazemMovimentoTipo>(MovimentoTexto, out var t) && Enum.IsDefined(t) ? t : null;

    public ArmazemDestino? Destino => Enum.TryParse<ArmazemDestino>(DestinoTexto, out var d) && Enum.IsDefined(d) ? d : null;

    /// <summary>A ferramenta do filtro, quando se veio da ficha (o texto da primeira linha).</summary>
    public string? FerramentaDoFiltro { get; private set; }

    public bool ComFiltro => !string.IsNullOrWhiteSpace(Texto) || !string.IsNullOrWhiteSpace(ToolId)
        || Movimento is not null || Destino is not null || De is not null || Ate is not null;

    public async Task<IActionResult> OnGetAsync(int? pagina)
    {
        ViewData["Title"] = "Histórico — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "historico",
            Functions = ShellStateFactory.ArmazemFunctions("historico")
        };

        if (!ComFiltro)
        {
            return Page();
        }

        // Só a Saída tem destino: escolher um destino mostra as Saídas.
        var movimento = Destino is not null ? ArmazemMovimentoTipo.Saida : Movimento;
        Lista = await historico.ListarAsync(
            new ArmazemHistoricoFiltro(Texto, ToolId, movimento, Destino, De, Ate), pagina ?? 1, HttpContext.RequestAborted);
        Pager = ListPager.For(Lista);
        if (!string.IsNullOrWhiteSpace(ToolId) && Lista.Items.FirstOrDefault() is { } primeira)
        {
            FerramentaDoFiltro = primeira.Tipo is { } tipo
                ? ArmazemTextos.Ferramenta(tipo, primeira.Referencia, primeira.Lote)
                : $"{primeira.Referencia} · lote {primeira.Lote}";
        }

        return Page();
    }

    public static string Data(DateOnly? dia) => dia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
