using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// Os Movimentos do Armazém (Diogo, 22/10/2026), logo a seguir ao Registo e
/// no lugar do Histórico: à esquerda o que está fora, em reparação, e em que
/// reparador (só CM e MF; as BQ seguem-se nas Boquilhas); à direita as
/// últimas entradas e saídas do Armazém (todas, BQ incluídas), com o
/// reparador e a posição onde ficou. São «últimas»: abre com os mais
/// recentes. Os filtros aplicam ao escolher (o texto com Enter) e trocam só
/// o cartão da direita; com ?ferramenta= mostra só uma ferramenta (o link
/// da ficha).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class UltimosModel(
    IArmazemReparacao reparacao, IArmazemHistorico historico, IArmazemMovimentos movimentos, CurrentUserCapabilities capabilities, ShellStateFactory shellState) : PageModel
{
    public IReadOnlyList<ArmazemForaEmReparacao> Fora { get; private set; } = [];

    /// <summary>As Linhas e o % de uso das ferramentas das duas listas.</summary>
    public IReadOnlyDictionary<string, ArmazemLinhasEUso> LinhasEUso { get; private set; } = new Dictionary<string, ArmazemLinhasEUso>();

    public PagedList<ArmazemHistoricoLinha> Lista { get; private set; } = PagedList<ArmazemHistoricoLinha>.Empty;

    public ListPager Pager { get; private set; } = new(1, 1, 0);

    public IReadOnlyList<string> Reparadores { get; private set; } = [];

    public IReadOnlyList<string> Linhas { get; private set; } = [];

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "referencia")]
    public string? Referencia { get; set; }

    [BindProperty(SupportsGet = true, Name = "lote")]
    public string? Lote { get; set; }

    [BindProperty(SupportsGet = true, Name = "linha")]
    public string? Linha { get; set; }

    [BindProperty(SupportsGet = true, Name = "reparador")]
    public string? Reparador { get; set; }

    [BindProperty(SupportsGet = true, Name = "ferramenta")]
    public string? ToolId { get; set; }

    public ToolType? Tipo => TipoTexto switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => null
    };

    /// <summary>A ferramenta do filtro, quando se veio da ficha.</summary>
    public string? FerramentaDoFiltro { get; private set; }

    /// <summary>Só quem tem as Ferramentas abre a ficha a partir da linha.</summary>
    public bool AbreFicha { get; private set; }

    public bool ComFiltro => De is not null || Ate is not null || Tipo is not null
        || !string.IsNullOrWhiteSpace(Referencia) || !string.IsNullOrWhiteSpace(Lote)
        || !string.IsNullOrWhiteSpace(Linha) || !string.IsNullOrWhiteSpace(Reparador) || !string.IsNullOrWhiteSpace(ToolId);

    public async Task<IActionResult> OnGetAsync(int? pagina)
    {
        ViewData["Title"] = "Movimentos — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "ultimos",
            Functions = ShellStateFactory.ArmazemFunctions("ultimos")
        };

        var ct = HttpContext.RequestAborted;
        AbreFicha = (await capabilities.GetAsync(ct)).Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
        Fora = await reparacao.ForaAsync(ct);
        Reparadores = await historico.ReparadoresAsync(ct);
        Linhas = await historico.LinhasAsync(ct);
        Lista = await historico.ListarAsync(
            new ArmazemHistoricoFiltro(
                ToolId: string.IsNullOrWhiteSpace(ToolId) ? null : ToolId,
                De: De,
                Ate: Ate,
                TipoFerramenta: Tipo,
                Referencia: Referencia,
                Lote: Lote,
                Linha: Linha,
                Reparador: Reparador),
            pagina ?? 1, ct);
        Pager = ListPager.For(Lista);
        LinhasEUso = await movimentos.LinhasEUsoAsync(
            Fora.Select(f => f.ToolId).Concat(Lista.Items.Select(m => m.ToolId)).OfType<string>().ToList(), ct);
        if (!string.IsNullOrWhiteSpace(ToolId) && Lista.Items.FirstOrDefault() is { } primeira)
        {
            FerramentaDoFiltro = Ferramenta(primeira);
        }

        return Page();
    }

    public static string Ferramenta(ArmazemHistoricoLinha m) =>
        m.Tipo is { } tipo ? ArmazemTextos.Ferramenta(tipo, m.Referencia, m.Lote) : $"{m.Referencia} · lote {m.Lote}";

    /// <summary>O movimento, curto: Entrada, Saída · destino, Corrigir localização.</summary>
    public static string Movimento(ArmazemHistoricoLinha m) => m.Movimento == ArmazemMovimentoTipo.Saida && m.Destino is { } destino
        ? $"Saída · {ArmazemTextos.Destino(destino)}"
        : ArmazemTextos.Movimento(m.Movimento);

    public static string Data(DateOnly? dia) => dia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
