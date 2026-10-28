using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// O HISTÓRICO (Diogo, 22/10/2026): com <c>?tool_id=…</c>, uma linha por Job
/// On em que a ferramenta trabalhou (produção, datas, % de uso e as OUTRAS
/// duas ferramentas desse Job On, com o lote); sem ferramenta, o «Histórico
/// das Ferramentas»: abre vazio e procura pela referência e pelos filtros.
/// Clicar numa linha abre o menu Controlo · Reparação · Folha do Job On.
/// Só leitura; a mesma chave da ficha.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class HistoricoModel(
    IFerramentaHistorico historico,
    CurrentUserCapabilities capabilities,
    ShellStateFactory shellState) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "tool_id")]
    public string? ToolId { get; set; }

    [BindProperty(SupportsGet = true, Name = "referencia")]
    public string? ReferenciaTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "lote")]
    public string? LoteEscrito { get; set; }

    public string? Lote => string.IsNullOrWhiteSpace(LoteEscrito) ? null : LoteEscrito.Trim();

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "linha")]
    public string? Linha { get; set; }

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    /// <summary>A ferramenta escolhida (null no Histórico das Ferramentas ou quando não existe).</summary>
    public HistoricoFerramenta? Ferramenta { get; private set; }

    /// <summary>Pediu-se uma ferramenta que não existe.</summary>
    public bool FerramentaNaoExiste { get; private set; }

    public IReadOnlyList<HistoricoLinha> Linhas { get; private set; } = [];

    public bool Cortada { get; private set; }

    public bool ComFiltro { get; private set; }

    public IReadOnlyList<MachineOption> Maquinas { get; private set; } = [];

    public bool AbreControlo { get; private set; }

    public bool AbreReparacao { get; private set; }

    public bool AbreJobOn { get; private set; }

    public ToolType? Tipo => TipoDe(TipoTexto);

    public string? Referencia => string.IsNullOrWhiteSpace(ReferenciaTexto) ? null : ReferenciaTexto.Trim().ToUpperInvariant();

    public static ToolType? TipoDe(string? texto) => texto?.Trim().ToUpperInvariant() switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => null
    };

    /// <summary>As colunas das ferramentas, sempre as três e por esta ordem (Diogo, 22/10/2026).</summary>
    public static readonly IReadOnlyList<ToolType> Colunas = [ToolType.Cm, ToolType.Bq, ToolType.Mf];

    public static string Token(ToolType tipo) => tipo switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        _ => "BQ"
    };

    /// <summary>«CM 5447 · Lote 3».</summary>
    public static string Nome(HistoricoFerramenta f) => $"{Token(f.Tipo)} {f.Referencia} · Lote {LoteTexto.Mostrar(f.Lote)}";

    public static string Dia(DateOnly? dia) => dia?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";

    /// <summary>«01/10/2026 – 08/10/2026»; sem fim, «desde 01/10/2026».</summary>
    public static string Datas(DateOnly? inicio, DateOnly? fim) => (inicio, fim) switch
    {
        (null, null) => "—",
        ({ } de, null) => $"desde {Dia(de)}",
        _ => $"{Dia(inicio)} – {Dia(fim)}"
    };

    public static string Uso(decimal? uso) => uso is { } u ? $"{u.ToString("0.##", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture)} %" : "—";

    public static string Data(DateOnly? dia) => dia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    public static string ControloUrl(HistoricoLinha l) => $"/Controlo/Resumo?jobon_id={l.JobOnId}";

    // A Consulta da RI só conta a produção escolhida com a referência dela escrita.
    public static string ReparacaoUrl(HistoricoLinha l) =>
        $"/ReparacaoInterna/Consultar?referencia={Uri.EscapeDataString(l.Referencia)}&producao={l.JobOnId}";

    public static string JobOnUrl(HistoricoLinha l) => $"/JobOn?jobon_id={l.JobOnId}";

    public async Task<IActionResult> OnGetAsync()
    {
        var cancelar = HttpContext.RequestAborted;
        var podes = await capabilities.GetAsync(cancelar);
        AbreControlo = podes.Contains(DmoCapabilities.ControloCreate, StringComparer.Ordinal);
        AbreReparacao = podes.Contains(DmoCapabilities.ReparacaoInterna, StringComparer.Ordinal);
        AbreJobOn = podes.Contains(DmoCapabilities.JobOnCreate, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(ToolId))
        {
            Ferramenta = await historico.FerramentaAsync(ToolId, cancelar);
            FerramentaNaoExiste = Ferramenta is null;
            ViewData["Title"] = Ferramenta is null ? "Histórico — Ferramentas" : $"Histórico {Nome(Ferramenta)} — Ferramentas";
            ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
            {
                ActiveFunctionKey = "ver",
                Functions = ShellStateFactory.FerramentasFunctions("ver")
            };
            if (Ferramenta is not null)
            {
                Linhas = await historico.DaFerramentaAsync(Ferramenta.ToolId, cancelar);
            }

            return Page();
        }

        // Sem ferramenta, vai para o Pesquisar (Diogo, 28/10/2026: «historico e pesquisar é redundante»).
        return RedirectToPage("/Ferramentas/Index");
    }
}
