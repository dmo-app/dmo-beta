using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Pages.Admin;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Definicoes;

/// <summary>
/// AS DEFINIÇÕES NUM SÓ SÍTIO (Diogo, 22/10/2026): uma página na barra de
/// cima, com duas partes. «Pessoal» vale só para quem a muda (o modo escuro
/// e as pastas deste computador). «Geral» vale para todos: lê-se sempre, mas
/// só a muda o ADMIN ou quem abriu o cadeado com o PIN das Definições
/// (criado pelo ADMIN em Definições → Admin → App; vale até sair). Em cada parte, uma secção por módulo, só dos módulos ativos para
/// o utilizador. Os formulários de cada módulo continuam a gravar pelos
/// handlers das páginas antigas (/Boquilhas/Definicoes…), que voltam aqui.
/// </summary>
[Authorize]
public sealed class IndexModel(
    ShellStateFactory shellState,
    IDefinicoesPessoais pessoais,
    ICurrentAccountContext account,
    CurrentUserCapabilities capabilities,
    MudancaDePalavraPasse mudancaDePalavraPasse,
    DesbloqueioDasDefinicoes desbloqueio,
    Controlo.DefinicoesModel controlo,
    Boquilhas.DefinicoesModel boquilhas,
    Ferramentas.DefinicoesModel ferramentas,
    Armazem.DefinicoesModel armazem,
    ReparacaoInterna.DefinicoesModel reparacao,
    Admin.Definicoes.IndexModel app) : PageModel
{
    public bool EAdmin { get; private set; }

    public bool PodeEditarGeral { get; private set; }

    /// <summary>TRUE quando o ADMIN já criou o PIN das Definições.</summary>
    public bool TemPin { get; private set; }

    /// <summary>O resultado do cadeado (?pin=ok|errado|espera|sem-pin).</summary>
    public string? PinResultado { get; private set; }

    /// <summary>O modo guardado para este utilizador: «light», «dark» ou null (o do navegador).</summary>
    public string? Tema { get; private set; }

    public string? Aviso { get; private set; }

    public Boquilhas.DefinicoesModel? Boquilhas { get; private set; }

    public Controlo.DefinicoesModel? Controlo { get; private set; }

    /// <summary>
    /// Os separadores do Controlo (Diogo, 22/10/2026: «o que é usado no Peso
    /// um separador com Peso, Pegamentos separador com Pegamentos», «email um
    /// separador com email»): cada um só com o que essa área lê.
    /// </summary>
    public static readonly IReadOnlyList<(string Chave, string Nome)> ControloSeparadores =
    [
        ("peso", "Peso"),
        ("pegamentos", "Pegamentos"),
        ("puncoes", "PU NNPB"),
        ("email", "Email"),
        ("pdfs", "PDFs")
    ];

    /// <summary>
    /// O grupo Admin, só do ADMIN (Diogo, 22/10/2026: «canal jobon e app
    /// ficam dentro das definições do admin, admin tem definições diferentes
    /// dos users»).
    /// </summary>
    public static readonly IReadOnlyList<(string Chave, string Nome)> AdminSeparadores =
    [
        ("app", "App")
    ];

    public Admin.Definicoes.IndexModel? App { get; private set; }

    /// <summary>A função aberta do grupo Admin (?sub=…).</summary>
    public string AdminSub { get; private set; } = "app";

    /// <summary>As funções de um grupo do menu (null para uma entrada simples).</summary>
    public IReadOnlyList<(string Chave, string Nome)>? FuncoesDe(string chave) => chave switch
    {
        "controlo" => ControloSeparadores,
        _ => null
    };

    /// <summary>A função aberta de um grupo.</summary>
    public string SubDe(string chave) => ControloSub;

    /// <summary>O separador do Controlo aberto (?sub=…).</summary>
    public string ControloSub { get; private set; } = "peso";

    public Ferramentas.DefinicoesModel? Ferramentas { get; private set; }

    public Armazem.DefinicoesModel? Armazem { get; private set; }

    public ReparacaoInterna.DefinicoesModel? Reparacao { get; private set; }


    /// <summary>A entrada do menu aberta: «pessoal» ou a de um módulo ativo (?secao=…).</summary>
    public string Secao { get; private set; } = "pessoal";

    /// <summary>As entradas do menu, pela ordem: Pessoal e os módulos ativos.</summary>
    public IReadOnlyList<(string Chave, string Nome)> Entradas { get; private set; } = [];

    /// <summary>«A minha conta» (só de leitura): nome, email, número e módulos.</summary>
    public string Nome { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? NumeroOperador { get; private set; }

    public IReadOnlyList<string> Modulos { get; private set; } = [];

    /// <summary>O aviso da mudança da palavra-passe (nada mudou).</summary>
    public string? PalavraPasseErro { get; private set; }

    public bool PalavraPasseMudou { get; private set; }

    public bool TemModulos => Boquilhas is not null || Controlo is not null || Ferramentas is not null || Armazem is not null || Reparacao is not null;

    public async Task<IActionResult> OnGetAsync()
    {
        EAdmin = DefinicoesAcesso.EAdmin(HttpContext);
        if (EAdmin && Request.Query["secao"] == "app")
        {
            // A App vive no grupo Admin das Definições (Diogo, 22/10/2026): o link antigo abre lá.
            return Redirect(Admin.Definicoes.IndexModel.Destino);
        }

        PodeEditarGeral = await DefinicoesAcesso.PodeEditarGeralAsync(HttpContext);
        ViewData["Title"] = "Definições";
        if (EAdmin)
        {
            var shell = await shellState.ForDestinationAsync("admin", new ModulePresentation("admin", "Administração"));
            ViewData["Shell"] = shell with
            {
                ActiveFunctionKey = "definicoes-gerais",
                Functions = AdminAreaNavigation.Functions("definicoes-gerais", Url.Page("/Admin/Users/Index"), Url.Page("/Admin/Templates/Index")),
                Planeamento = shell.Planeamento with { Visible = false }
            };
        }
        else
        {
            ViewData["Shell"] = await shellState.ForDestinationAsync("Definicoes", new ModulePresentation("definicoes", "Definições"));
        }

        var q = Request.Query;
        string? Q(string nome) => q.TryGetValue(nome, out var v) ? v.ToString() : null;
        TemPin = await desbloqueio.TemPinAsync(HttpContext.RequestAborted);
        PinResultado = Q("pin");
        Aviso = Q("geral") == "sem-permissao" ? DefinicoesMensagens.SemPermissao
            : Q("tema") == "invalido" ? "Escolha o modo claro ou o modo escuro." : null;

        var atual = account.Current;
        Nome = atual.DisplayName ?? string.Empty;
        Email = User.FindFirst(DmoClaimTypes.Email)?.Value;
        NumeroOperador = EAdmin ? null : atual.OperatorId;
        Modulos = EAdmin
            ? ["Administração"]
            : DmoCapabilities.MenuModules(await capabilities.GetAsync(HttpContext.RequestAborted)).Select(m => m.Label).ToArray();
        PalavraPasseErro = TempData[PalavraPasseErroTempData] as string;
        PalavraPasseMudou = Q("palavra-passe") == "ok";

        if (account.Current.ActorId is { } userId)
        {
            Tema = await pessoais.LerAsync(userId, UserSettingKeys.Tema, HttpContext.RequestAborted);
        }

        if (await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.ControloCreate))
        {
            Controlo = Ligar(controlo);
            ControloSub = Q("sub") is { } pedidoSub && ControloSeparadores.Any(t => t.Chave == pedidoSub) ? pedidoSub
                : Q("densidade") is not null ? "peso"
                : Q("save") is not null ? "pegamentos"
                : Q("envio") is not null ? "email"
                : Q("seccoes") is not null ? "puncoes"
                : Q("pasta") is not null ? "pdfs"
                : "peso";
            await Controlo.CarregarAsync(Q("save"), Q("envio"), Q("fora"), Q("densidade"), Q("seccoes"), Q("pasta"));
        }

        if (await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.Boquilhas))
        {
            Boquilhas = Ligar(boquilhas);
            await Boquilhas.CarregarAsync(Q("save"));
        }

        if (await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.Ferramentas))
        {
            Ferramentas = Ligar(ferramentas);
            await Ferramentas.CarregarAsync(int.TryParse(Q("guardado"), out var g) ? g : null);
        }

        if (await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.ArmazemGestao))
        {
            Armazem = Ligar(armazem);
            await Armazem.CarregarAsync(Q("gravacao"));
        }

        if (await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.ReparacaoInternaView))
        {
            Reparacao = Ligar(reparacao);
            await Reparacao.CarregarAsync(Q("gravacao"));
        }

        var entradas = new List<(string, string)> { ("pessoal", "Pessoal") };
        if (Controlo is not null) entradas.Add(("controlo", "Controlo"));
        if (Boquilhas is not null) entradas.Add(("boquilhas", "Boquilhas"));
        if (Ferramentas is not null) entradas.Add(("ferramentas", "Ferramentas"));
        if (Armazem is not null) entradas.Add(("armazem", "Armazém"));
        if (Reparacao is not null) entradas.Add(("reparacao", "Reparação"));
        if (EAdmin)
        {
            App = Ligar(app);
            await App.CarregarAsync(Q("app"), Q("email"), Q("pinapp"));
            AdminSub = "app";
            entradas.Add(("admin", "Admin"));
        }
        Entradas = entradas;
        var pedida = Q("secao");
        Secao = entradas.Any(e => e.Item1 == pedida) ? pedida! : "pessoal";
        return Page();
    }

    /// <summary>
    /// POST: o modo claro ou escuro, só deste utilizador (Pessoal). Vale em
    /// qualquer computador onde ele entre; o botão da barra de cima usa o
    /// mesmo handler.
    /// </summary>
    public async Task<IActionResult> OnPostTemaAsync([FromForm(Name = "tema")] string? tema)
    {
        var json = Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        if (tema is not ("light" or "dark") || account.Current.ActorId is not { } userId)
        {
            return json ? new JsonResult(new { ok = false }) { StatusCode = StatusCodes.Status422UnprocessableEntity }
                : RedirectToPage("/Definicoes/Index", new { tema = "invalido" });
        }

        await pessoais.GravarAsync(userId, UserSettingKeys.Tema, tema, HttpContext.RequestAborted);
        TemaPessoal.Escrever(HttpContext, tema);
        return json ? new JsonResult(new { ok = true, tema }) : RedirectToPage("/Definicoes/Index", new { tema = "ok" });
    }

    /// <summary>
    /// POST: abrir o cadeado da parte Geral com o PIN das Definições. Vale
    /// até sair. Nunca regista o PIN.
    /// </summary>
    public async Task<IActionResult> OnPostDesbloquearAsync(
        [FromForm(Name = "pin")] string? pin,
        [FromForm(Name = "secao")] string? secao)
    {
        var resultado = await desbloqueio.AbrirAsync(HttpContext, pin);
        var codigo = resultado switch
        {
            DesbloqueioDasDefinicoes.Resultado.Aberto => "ok",
            DesbloqueioDasDefinicoes.Resultado.Espera => "espera",
            DesbloqueioDasDefinicoes.Resultado.SemPin => "sem-pin",
            _ => "errado"
        };
        var destino = secao is { Length: > 0 and <= 20 } && secao.All(char.IsAsciiLetterLower) ? secao : "pessoal";
        return RedirectToPage("/Definicoes/Index", new { secao = destino, pin = codigo });
    }

    public string PinMensagem => PinResultado switch
    {
        "ok" => "Cadeado aberto: pode mudar as definições gerais até sair.",
        "errado" => "O PIN não está certo.",
        "espera" => $"Demasiadas tentativas erradas. Espere {(int)DesbloqueioDasDefinicoes.Espera.TotalSeconds} segundos e tente outra vez.",
        "sem-pin" => SemPinMensagem,
        _ => string.Empty
    };

    public const string SemPinMensagem = "Ainda não há PIN das Definições. Peça ao ADMIN para o criar em Definições → Admin → App.";

    private const string PalavraPasseErroTempData = "def-palavra-passe-erro";

    /// <summary>
    /// POST: mudar a palavra-passe (Pessoal). O mesmo caminho da mudança
    /// obrigatória (MudancaDePalavraPasse: as regras do PIN, o Supabase, as
    /// outras sessões terminam), mas pede a atual. Uma recusa não muda nada.
    /// </summary>
    public async Task<IActionResult> OnPostPalavraPasseAsync(
        [FromForm(Name = "atual")] string? atual,
        [FromForm(Name = "nova")] string? nova,
        [FromForm(Name = "confirmar")] string? confirmar)
    {
        var resultado = await mudancaDePalavraPasse.MudarAsync(
            User, pedirAtual: true, atual, nova, confirmar, "/Definicoes?secao=pessoal&palavra-passe=ok");
        if (resultado.Erro is not null)
        {
            if (resultado.Mudou)
            {
                return Redirect("/login");
            }

            TempData[PalavraPasseErroTempData] = resultado.Erro;
            return RedirectToPage("/Definicoes/Index", new { secao = "pessoal" });
        }

        return Redirect(resultado.Destino!);
    }

    /// <summary>A secção de um módulo usa o modelo da página antiga (os mesmos handlers e o mesmo armazenamento).</summary>
    private T Ligar<T>(T modelo) where T : PageModel
    {
        modelo.PageContext = PageContext;
        modelo.TempData = TempData;
        modelo.Url = Url;
        return modelo;
    }
}
