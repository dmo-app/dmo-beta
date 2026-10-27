using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Registos;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Admin;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Registos;

/// <summary>
/// REGISTOS (Diogo, 27/10/2026: «transformar os logs num módulo que podemos
/// usar para verificar tudo a partir de filtros», modelo SAP): um só campo
/// Procurar sugere Job On, referências, ferramentas, operadores e
/// reparadores; o que se escolhe vira o filtro. A lista mostra as Produções
/// (CM, MF e BQ com a % de uso) ou os Registos (quem fez o quê, antes →
/// depois). Clicar numa ferramenta abre o painel dela com «Filtrar»; clicar
/// numa produção abre o painel do Job On com as «Opções». Só leitura.
/// O ADMIN entra sempre; os outros pela chave «registos» do template.
/// </summary>
[Authorize]
public sealed class IndexModel(
    IRegistosProcura procura,
    CurrentUserCapabilities capabilities,
    ShellStateFactory shellState) : PageModel
{
    /// <summary>Quantos registos mostram os painéis («Últimos registos»).</summary>
    public const int UltimosNoPainel = 8;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Texto { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "valor")]
    public string? Valor { get; set; }

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    [BindProperty(SupportsGet = true, Name = "vista")]
    public string? Vista { get; set; }

    [BindProperty(SupportsGet = true, Name = "pagina")]
    public int? Pagina { get; set; }

    /// <summary>A procura de onde se veio («← Voltar» depois de «Filtrar»).</summary>
    [BindProperty(SupportsGet = true, Name = "voltar")]
    public string? Voltar { get; set; }

    public SugestaoTipo? Tipo => Enum.TryParse<SugestaoTipo>(TipoTexto, true, out var t) && Enum.IsDefined(t) ? t : null;

    public bool VerRegistos => string.Equals(Vista, "registos", StringComparison.OrdinalIgnoreCase);

    public bool ComFiltro { get; private set; }

    public ProcuraProducoes Producoes { get; private set; } = new([], false);

    public PagedList<RegistoDetalhado> Registos { get; private set; } = PagedList<RegistoDetalhado>.Empty;

    public ListPager Pager => ListPager.For(Registos);

    public bool AbreControlo { get; private set; }

    public bool AbreReparacao { get; private set; }

    public bool AbreJobOn { get; private set; }

    public bool AbreBoquilhas { get; private set; }

    public bool AbreFerramentas { get; private set; }

    public bool AbreArmazem { get; private set; }

    /// <summary>O ADMIN entra sempre; os outros com a chave «registos»; quem não tem vai para o seu primeiro módulo.</summary>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var granted = await capabilities.GetAsync(HttpContext.RequestAborted);
        var admin = User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString);
        if (!admin && !granted.Contains(DmoCapabilities.Registos, StringComparer.Ordinal))
        {
            context.Result = new RedirectResult(ModuleEntryAttribute.HomeFor(User, granted));
            return;
        }

        // O ADMIN abre tudo (só leitura); os outros só os módulos que têm.
        bool Tem(params string[] chaves) => admin || chaves.Any(c => granted.Contains(c, StringComparer.Ordinal));
        AbreControlo = Tem(DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove);
        AbreReparacao = Tem(DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView);
        AbreJobOn = Tem(DmoCapabilities.JobOnCreate, DmoCapabilities.JobOnView);
        AbreBoquilhas = Tem(DmoCapabilities.Boquilhas, DmoCapabilities.BoquilhasView);
        AbreFerramentas = Tem(DmoCapabilities.Ferramentas);
        AbreArmazem = Tem(DmoCapabilities.ArmazemGestao);
        await next();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "Registos";
        ViewData["Shell"] = User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString)
            ? (await shellState.ForDestinationAsync("admin", new ModulePresentation("admin", "Administração"))) with
            {
                ActiveFunctionKey = AdminAreaNavigation.RegistosFunctionKey,
                Functions = AdminAreaNavigation.Functions(AdminAreaNavigation.RegistosFunctionKey, Url.Page("/Admin/Users/Index"), Url.Page("/Admin/Templates/Index"))
            }
            : await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("registos", "Registos"));

        var filtro = Filtro();
        ComFiltro = filtro.ComFiltro;
        if (!ComFiltro)
        {
            return Page();
        }

        if (VerRegistos)
        {
            Registos = await procura.RegistosAsync(filtro, Pagina, HttpContext.RequestAborted);
        }
        else
        {
            Producoes = await procura.ProducoesAsync(filtro, HttpContext.RequestAborted);
        }

        return Page();
    }

    /// <summary>As sugestões do campo Procurar (JSON), por tipo.</summary>
    public async Task<IActionResult> OnGetSugestoesAsync([FromQuery(Name = "q")] string? q) =>
        new JsonResult((await procura.SugestoesAsync(q ?? "", HttpContext.RequestAborted))
            .Select(s => new { tipo = s.Tipo.ToString(), grupo = Grupo(s.Tipo), valor = s.Valor, texto = s.Texto, detalhe = s.Detalhe }));

    /// <summary>O painel da ferramenta (HTML para o painel deslizante).</summary>
    public async Task<IActionResult> OnGetFerramentaAsync([FromQuery(Name = "tool_id")] string? toolId)
    {
        var ferramenta = toolId is null ? null : await procura.FerramentaAsync(toolId, HttpContext.RequestAborted);
        if (ferramenta is null)
        {
            return NotFound();
        }

        var ultimos = await procura.UltimosAsync(ferramenta.ToolId, null, UltimosNoPainel, HttpContext.RequestAborted);
        return Partial("_RegistosFerramenta", new RegistosFerramentaPainel(ferramenta, ultimos, Voltar, AbreFerramentas, AbreArmazem));
    }

    /// <summary>O painel do Job On (HTML para o painel deslizante).</summary>
    public async Task<IActionResult> OnGetJobOnAsync([FromQuery(Name = "id")] int? id)
    {
        var producao = id is { } j ? await procura.ProducaoAsync(j, HttpContext.RequestAborted) : null;
        if (producao is null)
        {
            return NotFound();
        }

        var ultimos = await procura.UltimosAsync(null, producao.JobOnId, UltimosNoPainel, HttpContext.RequestAborted);
        return Partial("_RegistosJobOn", new RegistosJobOnPainel(producao, ultimos, AbreControlo, AbreReparacao, AbreJobOn, AbreBoquilhas));
    }

    public ProcuraFiltro Filtro()
    {
        var texto = string.IsNullOrWhiteSpace(Texto) ? null : Texto.Trim();
        var valor = string.IsNullOrWhiteSpace(Valor) ? null : Valor.Trim();
        return (Tipo, valor) switch
        {
            (SugestaoTipo.JobOn, { } v) when int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id) => new ProcuraFiltro(null, null, null, null, null, De, Ate, JobOnId: id),
            (SugestaoTipo.Ferramenta, { } v) => new ProcuraFiltro(null, null, null, null, null, De, Ate, ToolId: v),
            (SugestaoTipo.Operador, { } v) when Guid.TryParse(v, out var user) => new ProcuraFiltro(null, user, null, null, null, De, Ate),
            (SugestaoTipo.Reparador, { } v) => new ProcuraFiltro(null, null, v, null, null, De, Ate),
            (SugestaoTipo.Referencia, { } v) => new ProcuraFiltro(v, null, null, null, null, De, Ate),
            _ => new ProcuraFiltro(texto, null, null, null, null, De, Ate)
        };
    }

    /// <summary>O endereço desta procura com outra vista ou outra página.</summary>
    public string Endereco(string? vista = null, int? pagina = null)
    {
        var partes = new List<string>();
        void Junta(string nome, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                partes.Add(nome + "=" + Uri.EscapeDataString(v));
            }
        }

        Junta("q", Texto);
        Junta("tipo", TipoTexto);
        Junta("valor", Valor);
        Junta("de", De?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Junta("ate", Ate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Junta("vista", vista ?? Vista);
        Junta("pagina", pagina?.ToString(CultureInfo.InvariantCulture));
        Junta("voltar", Voltar);
        return "/Registos" + (partes.Count == 0 ? "" : "?" + string.Join("&", partes));
    }

    /// <summary>O «← Voltar» só regressa a uma procura dos Registos.</summary>
    public string? VoltarSeguro => Voltar is { } v && v.StartsWith("/Registos", StringComparison.Ordinal) && !v.StartsWith("//", StringComparison.Ordinal) ? v : null;

    public static string Grupo(SugestaoTipo tipo) => tipo switch
    {
        SugestaoTipo.JobOn => "Job On",
        SugestaoTipo.Referencia => "Referências",
        SugestaoTipo.Ferramenta => "Ferramentas",
        SugestaoTipo.Operador => "Operadores",
        _ => "Reparadores"
    };

    /// <summary>O que ficou escolhido, ao lado das datas: «Ferramenta: CM 5447 · Lote 3».</summary>
    public static string Escolhido(SugestaoTipo tipo) => tipo switch
    {
        SugestaoTipo.JobOn => "Job On",
        SugestaoTipo.Referencia => "Referência",
        SugestaoTipo.Ferramenta => "Ferramenta",
        SugestaoTipo.Operador => "Operador",
        _ => "Reparador"
    };

    public static string Token(ToolType tipo) => tipo switch
    {
        ToolType.Cm => "CM",
        ToolType.Mf => "MF",
        _ => "BQ"
    };

    public static string Nome(ToolType tipo, string referencia, string lote) => $"{Token(tipo)} {referencia} · Lote {LoteTexto.Mostrar(lote)}";

    public static string Curto(ProducaoFerramenta f) => $"{f.Referencia} · L{LoteTexto.Mostrar(f.Lote)}";

    public static string Dia(DateOnly? dia) => dia?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";

    public static string Quando(DateTime emUtc) =>
        DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(emUtc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Uso(decimal? uso) => uso is { } u ? $"{u.ToString("0.##", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture)} %" : "—";

    public static string Modulo(RegistoModulo modulo) => IRegistosDosUtilizadores.Nome(modulo);
}

public sealed record RegistosFerramentaPainel(FerramentaResumo Ferramenta, IReadOnlyList<RegistoDetalhado> Ultimos, string? Voltar, bool AbreFicha, bool AbreArmazem);

public sealed record RegistosJobOnPainel(ProducaoLinha Producao, IReadOnlyList<RegistoDetalhado> Ultimos, bool AbreControlo, bool AbreReparacao, bool AbreJobOn, bool AbreBoquilhas);
