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
/// As reparações com a ferramenta escrita (peça 4, Diogo, 21/10/2026) levam
/// a marca «Por associar» e o filtro «Só por associar» mostra só essas; a
/// marca abre a janela «Associar ferramenta» com as ferramentas do mesmo
/// tipo que servem. Associar grava uma vez por envio, com rasto; quem tem a
/// chave pode associar (como corrigir). Associa-se só a ferramenta: o Job On
/// de uma reparação sem Job On fica por ligar nesta peça.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ReparacaoInterna)]
public sealed class ConsultarModel(
    IReparacoesInternasConsulta consulta,
    IReparacoesInternas reparacoes,
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

    /// <summary>«Só por associar»: as reparações com a ferramenta escrita e ainda sem a associada.</summary>
    [BindProperty(SupportsGet = true, Name = "por_associar")]
    public bool PorAssociar { get; set; }

    /// <summary>Como acabou uma associação, pelo redirect (PRG), com o número e a ferramenta.</summary>
    public string? Associacao { get; private set; }

    public string? Numero { get; private set; }

    public string? FerramentaAssociada { get; private set; }

    public bool Associou => Associacao is "associado";

    public string Mensagem => Associacao switch
    {
        "associado" => $"Reparação do nº {Numero} associada a {FerramentaAssociada}.",
        "ja-associada" => $"A reparação do nº {Numero} já estava associada a {FerramentaAssociada}. Nada mudou.",
        "ja-anulada" => $"A reparação do nº {Numero} foi anulada. Nada mudou.",
        "nao-existe" => "Esta reparação já não existe. Nada mudou.",
        "nao-escrita" => $"A reparação do nº {Numero} já tem a ferramenta do Job On. Nada mudou.",
        "sem-ferramenta" => "Escolha a ferramenta. Nada mudou.",
        "nao-serve" => "Essa ferramenta já não existe ou não é do tipo da reparação. Nada mudou.",
        "mudou" => $"Outra pessoa mudou a reparação do nº {Numero} entretanto. Confira e associe outra vez.",
        _ => string.Empty
    };

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

    /// <summary>Quem tem as Ferramentas abre a ficha da ferramenta (a do Job On ou a associada); a escrita fica texto.</summary>
    public bool AbreFerramentas { get; private set; }

    /// <summary>O <c>tool_id</c> da ferramenta que a linha mostra para o link da ficha: a associada ou a do contexto; null na escrita por associar e na eliminada.</summary>
    public static string? ToolIdDaFicha(RiConsultaLinha linha) => linha.Escrita switch
    {
        null => linha.Ferramenta is { Referencia: not null } ? linha.FerramentaToolId : null,
        { PorAssociar: true } or { AssociadaReferencia: null } => null,
        var escrita => escrita.AssociadaToolId
    };

    public async Task<IActionResult> OnGetAsync(string? associacao, string? numero, string? ferramenta, int? pagina)
    {
        Associacao = associacao;
        Numero = numero;
        FerramentaAssociada = ferramenta;

        ViewData["Title"] = "Consultar — Reparação";
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
        var filtro = new RiConsultaFiltro(Maquina?.MachineId, De, Ate, pessoa, Tipo, Referencia, producao, PorAssociar);
        ComFiltro = filtro.ComFiltro;
        if (!ComFiltro)
        {
            return Page();
        }

        Lista = await consulta.ConsultarAsync(filtro, pagina, cancelar);
        Pager = ListPager.For(Lista);
        var podes = await capabilities.GetAsync(cancelar);
        AbreJobOn = podes.Contains(DmoCapabilities.JobOnCreate, StringComparer.Ordinal);
        AbreFerramentas = podes.Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
        return Page();
    }

    /// <summary>
    /// POST: associar a ferramenta escolhida na janela a uma reparação por
    /// associar. Grava uma vez por envio; volta à mesma consulta e página (PRG).
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostAssociarAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparacao_id")] int reparacaoId,
        [FromForm(Name = "tool_id")] string? toolId,
        [FromQuery(Name = "pagina")] int? pagina)
    {
        var saida = await reparacoes.AssociarAsync(
            new PedidoDeAssociacao(operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId, reparacaoId, toolId),
            HttpContext.RequestAborted);
        var rota = Rota(pagina);
        rota["associacao"] = saida.Resultado switch
        {
            AssociacaoResultado.Associado or AssociacaoResultado.JaFeito => "associado",
            AssociacaoResultado.JaAssociada => "ja-associada",
            AssociacaoResultado.JaAnulada => "ja-anulada",
            AssociacaoResultado.NaoExiste => "nao-existe",
            AssociacaoResultado.NaoEscrita => "nao-escrita",
            AssociacaoResultado.SemFerramenta => "sem-ferramenta",
            AssociacaoResultado.FerramentaNaoServe => "nao-serve",
            _ => "mudou"
        };
        rota["numero"] = saida.Numero;
        rota["ferramenta"] = saida.Ferramenta;
        return RedirectToPage("/ReparacaoInterna/Consultar", rota);
    }

    /// <summary>Os filtros como estão e a página: o formulário de associar volta a eles.</summary>
    public Dictionary<string, string?> Rota(int? pagina) => new(StringComparer.Ordinal)
    {
        ["maquina"] = MaquinaCodigo,
        ["de"] = De is null ? null : Data(De),
        ["ate"] = Ate is null ? null : Data(Ate),
        ["pessoa"] = PessoaId?.ToString(),
        ["tipo"] = TipoTexto,
        ["referencia"] = ReferenciaTexto,
        ["producao"] = ProducaoId?.ToString(CultureInfo.InvariantCulture),
        ["por_associar"] = PorAssociar ? "true" : null,
        ["pagina"] = pagina is > 1 ? pagina.Value.ToString(CultureInfo.InvariantCulture) : null
    };

    /// <summary>A produção escolhida no dropdown (só uma das listadas).</summary>
    public bool ProducaoEscolhida(ProductionPickItem item) => item.JobOnId == ProducaoId;

    public static string Data(DateOnly? dia) => dia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>A hora do registo, em hora local da fábrica: «08:42».</summary>
    public static string Hora(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// A ferramenta de uma linha: a associada; por associar, a escrita
    /// («5447T173 · lote 4 (escrita)»); senão a do contexto.
    /// </summary>
    public static string Ferramenta(RiConsultaLinha linha) => linha.Escrita switch
    {
        null => Ferramenta(linha.Ferramenta),
        { PorAssociar: true } escrita => $"{RiTextos.Escrita(escrita.Referencia, escrita.Lote)} (escrita)",
        { AssociadaReferencia: null } => "Eliminada",
        var escrita => RiTextos.Escrita(escrita.AssociadaReferencia, escrita.AssociadaLote)
    };

    /// <summary>A ferramenta como a pessoa a reconhece: «5538T324 · lote 2» (nunca o nome técnico).</summary>
    public static string Ferramenta(RiFerramenta? ferramenta) => ferramenta switch
    {
        null => "—",
        { Referencia: null } => "Eliminada",
        { Lote: null or "" } => ferramenta.Referencia,
        _ => $"{ferramenta.Referencia} · lote {ferramenta.Lote}"
    };
}
