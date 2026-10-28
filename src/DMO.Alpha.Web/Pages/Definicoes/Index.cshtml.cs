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
/// AS DEFINIÇÕES NUM SÓ SÍTIO (Diogo, 22/10/2026), em três separadores
/// (Diogo, 26/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraXJAtaKT9Zq5Rd7rwLJZDkP):
/// «A minha conta» (só para quem a muda), «Este computador» (as pastas, que
/// cada navegador guarda) e «Gerais da fábrica» (valem para todos, uma
/// secção por módulo ativo). As pastas e as gerais leem-se sempre, mas só as
/// muda o ADMIN ou quem abriu o cadeado com o PIN das Definições (criado
/// pelo ADMIN em Definições → Admin → App; vale até sair). Os formulários de cada módulo continuam a gravar pelos
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
    public int SessaoHoras { get; private set; } = SessaoDuracao.PorOmissao;

    public string? Aviso { get; private set; }

    public Boquilhas.DefinicoesModel? Boquilhas { get; private set; }

    public Controlo.DefinicoesModel? Controlo { get; private set; }

    public Admin.Definicoes.IndexModel? App { get; private set; }

    /// <summary>A função aberta do grupo Admin (?sub=…).</summary>
    public string AdminSub { get; private set; } = "app";

    public Ferramentas.DefinicoesModel? Ferramentas { get; private set; }

    /// <summary>As definições do Job On (a pasta das imagens dos artigos, deste computador); só para quem vê o Job On.</summary>
    public bool TemJobOn { get; private set; }

    public Armazem.DefinicoesModel? Armazem { get; private set; }

    public ReparacaoInterna.DefinicoesModel? Reparacao { get; private set; }


    /// <summary>A secção aberta (?secao=…): «pessoal», «computador», a de um módulo ativo ou «admin».</summary>
    public string Secao { get; private set; } = "pessoal";

    /// <summary>
    /// Os separadores, pela ordem: A minha conta, Este computador (quem tem
    /// um módulo com pastas), Gerais da fábrica (quem tem um módulo com
    /// definições gerais) e Admin (só o ADMIN). Cada um abre a sua primeira secção.
    /// </summary>
    public IReadOnlyList<(string Chave, string Nome, string Abre)> Separadores { get; private set; } = [];

    /// <summary>As secções dos módulos ativos, dentro de «Gerais da fábrica».</summary>
    public IReadOnlyList<(string Chave, string Nome)> Modulos { get; private set; } = [];

    /// <summary>O separador da secção aberta.</summary>
    public string Separador => SeparadorDe(Secao);

    public static string SeparadorDe(string secao) => secao is "pessoal" or "computador" or "admin" ? secao : "gerais";

    /// <summary>A secção aberta tem definições que só se mudam com o cadeado aberto (as pastas e as gerais).</summary>
    public bool SecaoComCadeado => Separador is "computador" or "gerais";

    /// <summary>«Este computador»: as pastas do Controlo, das Boquilhas, das Ferramentas e do Job On.</summary>
    public bool TemComputador => Controlo is not null || Boquilhas is not null || Ferramentas is not null || TemJobOn;

    /// <summary>«A minha conta» (só de leitura): nome, email, número e módulos.</summary>
    public string Nome { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? NumeroOperador { get; private set; }

    /// <summary>
    /// Os módulos da conta, cada um com o que a pessoa pode fazer lá (Diogo,
    /// 26/10/2026: «os módulos em seguida com vírgulas fica mal, preferível
    /// uma tabela»). Pela ordem da barra principal; um por nome.
    /// </summary>
    public IReadOnlyList<(string Modulo, string Acesso)> ModulosAcesso { get; private set; } = [];

    /// <summary>O nome curto de cada acesso, como na gestão de templates.</summary>
    public static string NomeDoAcesso(string capability) => capability switch
    {
        DmoCapabilities.JobOnCreate or DmoCapabilities.Boquilhas or DmoCapabilities.ControloCreate => "Criar",
        DmoCapabilities.JobOnView or DmoCapabilities.BoquilhasView or DmoCapabilities.ReparacaoInternaView => "Ver",
        DmoCapabilities.ControloApprove => "Aprovar",
        DmoCapabilities.Ferramentas => "Ver e editar",
        DmoCapabilities.ArmazemGestao => "Gestão",
        DmoCapabilities.ReparacaoInterna => "Registar",
        _ => capability
    };

    /// <summary>O aviso da mudança da palavra-passe (nada mudou).</summary>
    public string? PalavraPasseErro { get; private set; }

    public bool PalavraPasseMudou { get; private set; }

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
        var dadas = EAdmin ? [] : await capabilities.GetAsync(HttpContext.RequestAborted);
        ModulosAcesso = EAdmin
            ? [("Administração", "Gerir")]
            : DmoCapabilities.Modules.Where(m => dadas.Contains(m.Key, StringComparer.Ordinal))
                .GroupBy(m => m.Label)
                .Select(g => (g.Key, string.Join(" · ", g.Select(m => NomeDoAcesso(m.Key)))))
                .ToArray();
        PalavraPasseErro = TempData[PalavraPasseErroTempData] as string;
        PalavraPasseMudou = Q("palavra-passe") == "ok";

        if (account.Current.ActorId is { } userId)
        {
            Tema = await pessoais.LerAsync(userId, UserSettingKeys.Tema, HttpContext.RequestAborted);
            SessaoHoras = SessaoDuracao.Ler(await pessoais.LerAsync(userId, UserSettingKeys.SessaoHoras, HttpContext.RequestAborted));
        }

        if (!EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.ControloCreate))
        {
            Controlo = Ligar(controlo);
            await Controlo.CarregarAsync(Q("save"), Q("envio"), Q("fora"), Q("densidade"), Q("seccoes"));
        }

        if (!EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.Boquilhas))
        {
            Boquilhas = Ligar(boquilhas);
            await Boquilhas.CarregarAsync(Q("save"));
        }

        TemJobOn = !EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.JobOnView, DmoCapabilities.JobOnCreate);

        if (!EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.Ferramentas))
        {
            Ferramentas = Ligar(ferramentas);
            await Ferramentas.CarregarAsync(int.TryParse(Q("guardado"), out var g) ? g : null);
        }

        if (!EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.ArmazemGestao))
        {
            Armazem = Ligar(armazem);
            await Armazem.CarregarAsync(Q("gravacao"));
        }

        // Diogo, 25/10/2026: o ADMIN só vê as definições do Admin; cada módulo só a quem o tem.
        if (!EAdmin && await DefinicoesAcesso.VeModuloAsync(HttpContext, DmoCapabilities.Boquilhas))
        {
            Reparacao = Ligar(reparacao);
            await Reparacao.CarregarAsync(Q("gravacao"));
        }

        var modulos = new List<(string, string)>();
        if (Controlo is not null) modulos.Add(("controlo", "Controlo"));
        if (Boquilhas is not null) modulos.Add(("boquilhas", "Boquilhas"));
        if (Ferramentas is not null) modulos.Add(("ferramentas", "Ferramentas"));
        if (Armazem is not null) modulos.Add(("armazem", "Armazém"));
        if (Reparacao is not null) modulos.Add(("reparacao", "Reparação"));
        Modulos = modulos;

        var separadores = new List<(string, string, string)> { ("pessoal", "A minha conta", "pessoal") };
        if (TemComputador) separadores.Add(("computador", "Este computador", "computador"));
        if (modulos.Count > 0) separadores.Add(("gerais", "Gerais da fábrica", modulos[0].Item1));
        if (EAdmin)
        {
            App = Ligar(app);
            await App.CarregarAsync(Q("app"), Q("email"), Q("pinapp"));
            AdminSub = Q("sub") == "sap" ? "sap" : "app";
            if (AdminSub == "sap")
            {
                await App.CarregarSapAsync(Q("sap"));
            }
            separadores.Add(("admin", "Admin", "admin"));
        }
        Separadores = separadores;

        // Os links antigos: o Job On só tinha a pasta das imagens; a pasta dos PDFs do Controlo grava e volta a «Este computador».
        var pedida = Q("secao") switch
        {
            "jobon" => "computador",
            "gerais" => modulos.Count > 0 ? modulos[0].Item1 : null,
            "controlo" when Q("pasta") is not null => "computador",
            var s => s
        };
        Secao = separadores.Any(t => t.Item3 == pedida) || modulos.Any(m => m.Item1 == pedida) ? pedida! : "pessoal";
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
    /// POST: a duração da sessão, só deste utilizador (Pessoal); vale a partir
    /// da próxima entrada.
    /// </summary>
    public async Task<IActionResult> OnPostSessaoAsync([FromForm(Name = "horas")] int horas)
    {
        if (!SessaoDuracao.Opcoes.Contains(horas) || account.Current.ActorId is not { } userId)
        {
            return RedirectToPage("/Definicoes/Index", new { geral = "sem-permissao" });
        }

        await pessoais.GravarAsync(userId, UserSettingKeys.SessaoHoras, horas.ToString(System.Globalization.CultureInfo.InvariantCulture), HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index");
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
