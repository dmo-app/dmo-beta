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
/// As Posições do Armazém (módulo do full, peça E; blueprint armazem/OVERVIEW
/// §1): criar uma posição, procurar pelo início do código e arquivar ou
/// reativar no próprio valor da linha (sem botões nas linhas). Uma posição
/// nunca se elimina.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class PosicoesModel(IArmazemPosicoes posicoes, IArmazemMovimentos movimentos, ShellStateFactory shellState) : PageModel
{
    public PagedList<ArmazemPosicaoItem> Lista { get; private set; } = PagedList<ArmazemPosicaoItem>.Empty;

    public ListPager Pager { get; private set; } = new(1, 1, 0);

    /// <summary>As ferramentas em cada posição da página.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<ArmazemOcupante>> Ocupantes { get; private set; } =
        new Dictionary<int, IReadOnlyList<ArmazemOcupante>>();

    [BindProperty(SupportsGet = true, Name = "codigo")]
    public string? ComecaPor { get; set; }

    [BindProperty(SupportsGet = true, Name = "estado")]
    public string? Estado { get; set; }

    /// <summary>O resultado da gravação, pelo redirect (PRG).</summary>
    public string? Gravacao { get; private set; }

    /// <summary>O código da posição de que fala a mensagem.</summary>
    public string? CodigoGravado { get; private set; }

    public bool Gravou => Gravacao is "criada" or "arquivada" or "reativada";

    public string Mensagem => Gravacao switch
    {
        "criada" => $"Posição {CodigoGravado} criada.",
        "arquivada" => $"Posição {CodigoGravado} arquivada.",
        "reativada" => $"Posição {CodigoGravado} reativada.",
        "invalido" => "O código da posição tem só letras e algarismos, até 16 (ex.: 0042, 7001A, A001).",
        "existe" => $"A posição {CodigoGravado} já existe.",
        "nao-existe" => "Essa posição já não existe.",
        _ => string.Empty
    };

    public ArmazemPosicaoFiltro Filtro => Estado switch
    {
        "ativas" => ArmazemPosicaoFiltro.Ativas,
        "arquivadas" => ArmazemPosicaoFiltro.Arquivadas,
        _ => ArmazemPosicaoFiltro.Todas
    };

    public async Task<IActionResult> OnGetAsync(string? gravacao, string? posicao, int? pagina)
    {
        ViewData["Title"] = "Posições — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "posicoes",
            Functions = ShellStateFactory.ArmazemFunctions("posicoes")
        };

        Gravacao = gravacao;
        CodigoGravado = posicao;
        Lista = await posicoes.ListarAsync(ComecaPor, Filtro, pagina ?? 1, HttpContext.RequestAborted);
        Pager = ListPager.For(Lista);
        Ocupantes = await movimentos.OcupantesAsync(Lista.Items.Select(p => p.Id).ToList(), HttpContext.RequestAborted);
        return Page();
    }

    /// <summary>POST: criar uma posição. Recusa o código fora da regra e o que já existe.</summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostCriarAsync([FromForm(Name = "novo_codigo")] string? codigo)
    {
        var (resultado, gravado) = await posicoes.CriarAsync(codigo, HttpContext.RequestAborted);
        var gravacao = resultado switch
        {
            ArmazemPosicaoResultado.Gravado => "criada",
            ArmazemPosicaoResultado.JaExiste => "existe",
            _ => "invalido"
        };
        return RedirectToPage("/Armazem/Posicoes", new { gravacao, posicao = gravado, codigo = ComecaPor, estado = Estado });
    }

    /// <summary>POST: arquivar ou reativar, escolhido no estado da linha.</summary>
    public async Task<IActionResult> OnPostEstadoAsync(
        [FromForm(Name = "posicao_id")] int posicaoId,
        [FromForm(Name = "posicao_codigo")] string? codigo,
        [FromForm(Name = "ativa")] string? ativa,
        [FromForm(Name = "pagina")] int? pagina)
    {
        var ficaAtiva = ativa == "sim";
        var resultado = await posicoes.MudarAtivaAsync(posicaoId, ficaAtiva, HttpContext.RequestAborted);
        var gravacao = resultado == ArmazemPosicaoResultado.Gravado
            ? (ficaAtiva ? "reativada" : "arquivada")
            : "nao-existe";
        return RedirectToPage("/Armazem/Posicoes", new { gravacao, posicao = codigo, codigo = ComecaPor, estado = Estado, pagina });
    }
}
