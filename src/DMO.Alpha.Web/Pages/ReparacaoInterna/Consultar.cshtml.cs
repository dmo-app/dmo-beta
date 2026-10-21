using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.ReparacaoInterna;

/// <summary>
/// Consultar (blueprint reparacao-interna/OVERVIEW §1 «Consultar», RI-05):
/// os chefes veem quem reparou o quê e em que produção. Como os outros
/// históricos (Diogo, «Procura e filtros»), abre vazia, só com os filtros
/// numa linha, e só lê depois de um filtro: a máquina, os dias (de / até,
/// livres), quem (os utilizadores que já registaram), CM ou MF e a produção
/// em dois passos (a referência, depois uma das últimas 5 produções dela,
/// como no Resumo do controlo). Os dropdowns e as datas aplicam ao escolher;
/// a referência com Enter ou a sugestão. Só a tabela e o dropdown das
/// produções trocam (DmoTroca). A mesma chave do módulo (ri).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ReparacaoInterna)]
public sealed class ConsultarModel(
    IReparacoesInternasConsulta consulta,
    IMachineListQuery maquinas,
    IProductionPickerQuery producoes,
    CurrentUserCapabilities capabilities,
    ShellStateFactory shellState) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "maquina")]
    public string? MaquinaCodigo { get; set; }

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    [BindProperty(SupportsGet = true, Name = "pessoa")]
    public Guid? PessoaId { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "referencia")]
    public string? ReferenciaTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "producao")]
    public int? ProducaoId { get; set; }

    public IReadOnlyList<MachineOption> Maquinas { get; private set; } = [];

    public MachineOption? Maquina { get; private set; }

    public IReadOnlyList<RiPessoa> Pessoas { get; private set; } = [];

    public ReparacaoTipo? Tipo => RiTextos.TipoDe(TipoTexto);

    /// <summary>A referência escrita, sem espaços nas pontas e em maiúsculas (null vazia).</summary>
    public string? Referencia => string.IsNullOrWhiteSpace(ReferenciaTexto) ? null : ReferenciaTexto.Trim().ToUpperInvariant();

    /// <summary>O segundo passo: as últimas 5 produções da referência escrita.</summary>
    public IReadOnlyList<ProductionPickItem> Producoes { get; private set; } = [];

    public PagedList<RiConsultaLinha> Lista { get; private set; } = PagedList<RiConsultaLinha>.Empty;

    public ListPager Pager { get; private set; } = new(1, 1, 0);

    /// <summary>Há um filtro: a tabela lê (sem filtro abre vazia, com o convite).</summary>
    public bool ComFiltro { get; private set; }

    /// <summary>Quem tem o Job On abre a ficha da produção pelo link; os outros veem só o texto.</summary>
    public bool AbreJobOn { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? pagina)
    {
        ViewData["Title"] = "Consultar — Reparação Interna";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("reparacao-interna", "Reparação Interna"))) with
        {
            ActiveFunctionKey = "consultar",
            Functions = ShellStateFactory.ReparacaoInternaFunctions("consultar")
        };

        var cancelar = HttpContext.RequestAborted;
        Maquinas = await maquinas.ListAsync(cancelar);
        Maquina = Maquinas.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        Pessoas = await consulta.PessoasAsync(cancelar);
        // Uma pessoa que já não está na lista (o utilizador foi eliminado) não filtra.
        var pessoa = Pessoas.FirstOrDefault(p => p.Id == PessoaId)?.Id;

        // O segundo passo só com a referência; a produção escolhida fica no
        // dropdown mesmo fora das últimas 5 (um link guardado).
        if (Referencia is { } referencia)
        {
            Producoes = await producoes.ListByReferenceAsync(referencia, cancelar);
            if (ProducaoId is { } pedida && Producoes.All(p => p.JobOnId != pedida)
                && await producoes.FindAsync(pedida, cancelar) is { } guardada)
            {
                Producoes = [.. Producoes, guardada];
            }
        }

        // A produção só conta escolhida no dropdown da referência escrita.
        var producao = Producoes.Any(p => p.JobOnId == ProducaoId) ? ProducaoId : null;
        var filtro = new RiConsultaFiltro(Maquina?.MachineId, De, Ate, pessoa, Tipo, Referencia, producao);
        ComFiltro = filtro.ComFiltro;
        if (!ComFiltro)
        {
            return Page();
        }

        Lista = await consulta.ConsultarAsync(filtro, pagina, cancelar);
        Pager = ListPager.For(Lista);
        AbreJobOn = (await capabilities.GetAsync(cancelar)).Contains(DmoCapabilities.JobOnCreate, StringComparer.Ordinal);
        return Page();
    }

    /// <summary>A produção escolhida no dropdown (só uma das listadas).</summary>
    public bool ProducaoEscolhida(ProductionPickItem item) => item.JobOnId == ProducaoId;

    public static string Data(DateOnly? dia) => dia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>A hora do registo, em hora local da fábrica: «08:42».</summary>
    public static string Hora(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A ferramenta como a pessoa a reconhece: «5538T324 · lote 2» (nunca o nome técnico).</summary>
    public static string Ferramenta(RiFerramenta? ferramenta) => ferramenta switch
    {
        null => "—",
        { Referencia: null } => "Eliminada",
        { Lote: null or "" } => ferramenta.Referencia,
        _ => $"{ferramenta.Referencia} · lote {ferramenta.Lote}"
    };
}
