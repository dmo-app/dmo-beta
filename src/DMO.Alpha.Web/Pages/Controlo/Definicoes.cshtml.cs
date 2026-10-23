using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// As Definições do Controlo (Diogo, 17/10/2026: «remover do main, pertence
/// ao controlo» e «associaçao do dir fica no controlo.create»): a pasta
/// partilhada dos PDFs, associada em cada computador, e a tolerância dos
/// Pegamentos. Saíram do Admin → Definições; quem tem o Controlo Criar
/// muda-as aqui. Também as listas e os modelos do envio do PDF do Peso por
/// email (Diogo, 19/10/2026, D9) e, desde 19/10/2026 (D15), a densidade do
/// vidro NNPB e PS.
/// </summary>
[Authorize]
[DefinicoesEntry("controlo", DmoCapabilities.ControloCreate)]
public sealed class DefinicoesModel(
    IPegamentosToleranceConfiguration pegamentosTolerance,
    ISavePegamentosToleranceHandler savePegamentosTolerance,
    EnvioEmailDefinicoes envioEmail,
    IGlassDensityConfiguration glassDensity,
    ISaveGlassDensityHandler saveGlassDensity,
    PuncoesLinhaDefinicoes puncoesLinhas,
    IModuleSettingsQuery moduleSettings,
    ISaveModuleSettingHandler saveModuleSetting) : PageModel
{
    /// <summary>As secções de cada linha para as punções PU NNPB (22/10/2026).</summary>
    public IReadOnlyList<PuncoesLinha> PuncoesLinhas { get; private set; } = Array.Empty<PuncoesLinha>();

    /// <summary>Resultado da gravação das secções (PRG): Gravado, Invalido ou LinhaNaoExiste.</summary>
    public string? SeccoesResult { get; private set; }

    public bool SeccoesSucceeded => SeccoesResult == nameof(PuncoesSeccoesGravacao.Gravado);

    public string SeccoesResultMessage => SeccoesResult switch
    {
        nameof(PuncoesSeccoesGravacao.Gravado) => "Secções guardadas. A grelha das punções desta linha já as usa.",
        nameof(PuncoesSeccoesGravacao.Invalido) => $"Indique as secções com um número inteiro de {PuncoesPosicoes.SeccoesMinimo} a {PuncoesPosicoes.SeccoesMaximo}. Não foram guardadas.",
        nameof(PuncoesSeccoesGravacao.LinhaNaoExiste) => "Essa linha já não existe. Nada foi guardado.",
        _ => string.Empty
    };

    /// <summary>As listas de destinatários, por nome.</summary>
    public IReadOnlyList<EmailLista> Listas { get; private set; } = Array.Empty<EmailLista>();

    /// <summary>Os modelos do envio, por nome.</summary>
    public IReadOnlyList<EmailModelo> Modelos { get; private set; } = Array.Empty<EmailModelo>();

    /// <summary>Os endereços que ficaram de fora da última lista gravada (não são emails).</summary>
    public string? Ignorados { get; private set; }

    /// <summary>A lista que volta aberta depois de lhe mexer (?lista=id); as outras ficam fechadas.</summary>
    public int? ListaAberta { get; private set; }

    /// <summary>O resultado das listas e dos modelos (PRG), só apresentação.</summary>
    public string? EnvioResult { get; private set; }

    public bool EnvioSucceeded => EnvioResult is "ok-lista" or "ok-modelo" or "apagada-lista" or "apagado-modelo";

    public bool EnvioWarned => EnvioResult == "aviso-lista";

    public string EnvioResultMessage => EnvioResult switch
    {
        "ok-lista" => "Lista gravada.",
        "aviso-lista" => $"Lista gravada. Ficaram de fora, por não serem emails: {Ignorados}.",
        "ok-modelo" => "Modelo gravado.",
        "apagada-lista" => "Lista apagada.",
        "apagado-modelo" => "Modelo apagado.",
        _ => EnvioRecusa(EnvioResult)
    };

    /// <summary>A mensagem de uma recusa das listas ou dos modelos (também a do JSON da linha).</summary>
    public static string EnvioRecusa(string? estado) => estado switch
    {
        nameof(EnvioEmailEstado.NomeVazio) => "Escreva um nome. Não foi gravado.",
        nameof(EnvioEmailEstado.NomeRepetido) => "Já existe outro com esse nome. Não foi gravado.",
        nameof(EnvioEmailEstado.TextoLongo) => $"Texto demasiado longo (nome até {EnvioEmailRegras.NomeMax}, assunto até {EnvioEmailRegras.AssuntoMax} e corpo até {EnvioEmailRegras.CorpoMax} caracteres). Não foi gravado.",
        nameof(EnvioEmailEstado.SemEnderecos) => "Escreva pelo menos um email válido (por exemplo producao@fabrica.pt). Não foi gravado.",
        nameof(EnvioEmailEstado.DemasiadosEnderecos) => $"Uma lista leva até {EnvioEmailRegras.DestinatariosMax} emails. Não foi gravada.",
        nameof(EnvioEmailEstado.RemetenteInvalido) => "O email que envia não é um email válido. Não foi gravado.",
        nameof(EnvioEmailEstado.NaoExiste) => "Já não existe (outra pessoa apagou-o). Nada mudou.",
        nameof(EnvioEmailEstado.EmailRepetido) => "Esse email já está na lista. Não foi gravado.",
        _ => string.Empty
    };

    /// <summary>A densidade do vidro NNPB guardada (g/cm³); null quando ainda não há.</summary>
    public decimal? DensidadeNnpb { get; private set; }

    /// <summary>A densidade do vidro PS guardada (g/cm³); null quando ainda não há.</summary>
    public decimal? DensidadePs { get; private set; }

    /// <summary>Resultado da gravação da densidade, por query (PRG): ok, invalida ou aviso-…</summary>
    public string? DensidadeResult { get; private set; }

    public bool DensidadeSucceeded => DensidadeResult == "ok";

    public bool DensidadeWarned => DensidadeResult?.StartsWith("aviso", StringComparison.Ordinal) == true;

    /// <summary>
    /// A mensagem da gravação da densidade. O aviso fica sem link: resolve-se
    /// aqui, nos mesmos campos.
    /// </summary>
    public string DensidadeResultMessage => DensidadeResult switch
    {
        "ok" => "Densidade do vidro guardada. Vale para os próximos Pesos; os Pesos já gravados ficam com a que usaram.",
        "invalida" => "A densidade do vidro é um número maior que zero, em g/cm³ (por exemplo 2,4027), nos dois campos. Não foi guardada.",
        _ when DensidadeWarned => DensidadeWarningMessage(),
        _ => string.Empty
    };

    private string DensidadeWarningMessage()
    {
        var partes = new List<string>();
        foreach (var (codigo, nome, valor) in new[] { ("nnpb", "NNPB", DensidadeNnpb), ("ps", "PS", DensidadePs) })
        {
            var arredondada = DensidadeResult!.Contains($"-arredondada-{codigo}", StringComparison.Ordinal);
            var fora = DensidadeResult.Contains($"-fora-{codigo}", StringComparison.Ordinal);
            if (!arredondada && !fora)
            {
                continue;
            }

            var detalhe = (arredondada, fora) switch
            {
                (true, true) => "arredondada a quatro casas decimais e fora do habitual",
                (true, false) => "arredondada a quatro casas decimais",
                _ => "fora do habitual"
            };
            partes.Add($"{nome} {FormatarDensidade(valor)} g/cm³, {detalhe}");
        }

        return $"Densidade do vidro guardada: {string.Join("; ", partes)} (o habitual é de {FormatarDensidade(GlassDensityLimits.Minimum)} a {FormatarDensidade(GlassDensityLimits.Maximum)} g/cm³). Vale para os próximos Pesos.";
    }

    /// <summary>A densidade como se mostra e se escreve no campo: quatro casas, vírgula.</summary>
    public static string FormatarDensidade(decimal? valor) =>
        valor is { } v ? v.ToString("0.0000", DmoNumberFormat.Culture) : string.Empty;

    /// <summary>A tolerância efetiva do corredor (a configurada, ou a norma 0,20 mm).</summary>
    public decimal PegamentosToleranceMm { get; private set; } = PegamentosCalculation.DefaultToleranceMm;

    /// <summary>TRUE quando ainda não há tolerância guardada (vale a norma).</summary>
    public bool PegamentosToleranceIsDefault { get; private set; } = true;

    /// <summary>Resultado do save transportado por query (PRG), apenas apresentação.</summary>
    public string? SaveResult { get; private set; }

    /// <summary>TRUE quando o save transportado é uma gravação feita sem aviso (a mensagem fecha sozinha).</summary>
    public bool SaveSucceeded => SaveResult == "ok-tolerancia";

    /// <summary>
    /// TRUE quando gravou com aviso (Diogo, 19/10/2026: a tolerância «nunca
    /// bloqueia»): fora do habitual ou arredondada. O aviso fica sem link,
    /// porque se resolve aqui, no mesmo campo.
    /// </summary>
    public bool SaveWarned => SaveResult?.StartsWith("aviso-tolerancia", StringComparison.Ordinal) == true;

    public string SaveResultMessage => SaveResult switch
    {
        "ok-tolerancia" => "Tolerância guardada. Vale para os próximos registos de Pegamentos.",
        "tolerancia-invalida" => "A tolerância é um número em mm, sem sinal (por exemplo 0,20). Não foi guardada.",
        _ when SaveWarned => WarningMessage(),
        _ => string.Empty
    };

    private string WarningMessage()
    {
        var mm = PegamentosToleranceMm.ToString("0.00", DmoNumberFormat.Culture);
        var detalhe = (SaveResult!.Contains("-arredondada", StringComparison.Ordinal), SaveResult.Contains("-fora", StringComparison.Ordinal)) switch
        {
            (true, true) => $"arredondada a duas casas decimais e fora do habitual ({MinimoHabitual} a {MaximoHabitual} mm)",
            (true, false) => "arredondada a duas casas decimais",
            _ => $"fora do habitual ({MinimoHabitual} a {MaximoHabitual} mm)"
        };
        return $"Tolerância guardada: ± {mm} mm, {detalhe}. Vale para os próximos registos de Pegamentos.";
    }

    private static string MinimoHabitual => PegamentosToleranceLimits.MinimumMm.ToString("0.00", DmoNumberFormat.Culture);

    private static string MaximoHabitual => PegamentosToleranceLimits.MaximumMm.ToString("0.00", DmoNumberFormat.Culture);

    /// <summary>GET: as definições vivem na página única /Definicoes (Diogo, 22/10/2026).</summary>
    public IActionResult OnGet() => Redirect(DefinicoesAcesso.Nova(Request, "controlo"));

    /// <summary>Carrega o que a secção deste módulo nas Definições mostra.</summary>
    /// <summary>O nome da pasta partilhada dos PDFs (Geral); null sem nome definido.</summary>
    public string? PastaPdf { get; private set; }

    public string? PastaResult { get; private set; }

    public bool PastaSucceeded => PastaResult == "ok";

    public string PastaResultMessage => PastaResult switch
    {
        "ok" => "Pasta dos PDFs guardada.",
        "invalida" => $"Indique o nome da pasta (no máximo {ModuleSettingKeys.ControloPastaPdfMax} caracteres).",
        _ => string.Empty
    };

    /// <summary>POST: o nome da pasta partilhada dos PDFs, para todos (Geral).</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSavePastaPdfAsync([FromForm(Name = "pasta")] string? pasta)
    {
        var outcome = await saveModuleSetting.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ControloPastaPdf, pasta ?? string.Empty), HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index", new { pasta = outcome.Result == SaveModuleSettingResult.Success ? "ok" : "invalida" });
    }

    public async Task CarregarAsync(string? save, string? envio, string? fora, string? densidade = null, string? seccoes = null, string? pasta = null)
    {
        PastaResult = pasta;
        PastaPdf = await moduleSettings.FindValueAsync(ModuleSettingKeys.ControloPastaPdf, HttpContext.RequestAborted);
        SeccoesResult = seccoes;
        PuncoesLinhas = await puncoesLinhas.ListarAsync(HttpContext.RequestAborted);

        SaveResult = save;
        DensidadeResult = densidade;
        DensidadeNnpb = await glassDensity.FindByProcessAsync(DMO.Alpha.Core.Tools.CmProcess.Nnpb, HttpContext.RequestAborted);
        DensidadePs = await glassDensity.FindByProcessAsync(DMO.Alpha.Core.Tools.CmProcess.Ps, HttpContext.RequestAborted);
        var configured = await pegamentosTolerance.FindConfiguredAsync(HttpContext.RequestAborted);
        PegamentosToleranceIsDefault = configured is null;
        PegamentosToleranceMm = configured ?? PegamentosCalculation.DefaultToleranceMm;
        EnvioResult = envio;
        Ignorados = fora;
        ListaAberta = int.TryParse(Request.Query["lista"], out var aberta) ? aberta : null;
        Listas = await envioEmail.ListasAsync(HttpContext.RequestAborted);
        Modelos = await envioEmail.ModelosAsync(HttpContext.RequestAborted);
    }

    /// <summary>POST: criar uma lista de destinatários. Os endereços que não são email ficam de fora, com aviso.</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostCriarListaAsync(string? nome, string? destinatarios, string? pessoa)
    {
        var resultado = await envioEmail.CriarListaAsync(nome, destinatarios, HttpContext.RequestAborted, pessoa);
        var limpo = nome?.Trim() ?? "";
        var id = resultado.Gravado
            ? (await envioEmail.ListasAsync(HttpContext.RequestAborted)).FirstOrDefault(l => string.Equals(l.Nome, limpo, StringComparison.OrdinalIgnoreCase))?.Id
            : null;
        return VoltarDaLista(resultado, id);
    }

    /// <summary>
    /// POST: mudar o nome ou o email de uma pessoa da lista no próprio valor
    /// da linha (data-dmo-valor). Sem JavaScript, volta à página com a lista aberta.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostMudarPessoaAsync(
        [FromForm(Name = "lista_id")] int? listaId, [FromForm(Name = "pessoa")] int? pessoa, string? nome, string? email)
    {
        var resultado = await envioEmail.MudarPessoaAsync(listaId ?? 0, pessoa ?? -1, email is null ? nome ?? "" : null, email, HttpContext.RequestAborted);
        if (Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            var aviso = resultado.Gravado ? null
                : resultado.Estado == EnvioEmailEstado.SemEnderecos ? $"«{string.Join(", ", resultado.Ignorados)}» não é um email. Não foi gravado."
                : EnvioRecusa(resultado.Estado.ToString());
            return new JsonResult(new { ok = resultado.Gravado, valor = resultado.Valor, aviso });
        }

        return VoltarDaLista(resultado, listaId);
    }

    /// <summary>POST: tirar uma pessoa da lista (o × da linha).</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostRemoverPessoaAsync([FromForm(Name = "lista_id")] int? listaId, [FromForm(Name = "pessoa")] int? pessoa)
    {
        var resultado = await envioEmail.RemoverPessoaAsync(listaId ?? 0, pessoa ?? -1, HttpContext.RequestAborted);
        return VoltarDaLista(resultado, listaId);
    }

    /// <summary>
    /// POST: mudar o nome ou os emails de uma lista no próprio valor da linha
    /// (data-dmo-valor: chega só o campo mudado). Sem JavaScript, volta à página.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostMudarListaAsync(
        [FromForm(Name = "lista_id")] int? listaId, string? nome, string? destinatarios)
    {
        var resultado = await envioEmail.MudarListaAsync(listaId ?? 0, nome, nome is null ? destinatarios ?? "" : null, HttpContext.RequestAborted);
        if (Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            var aviso = !resultado.Gravado ? EnvioRecusa(resultado.Estado.ToString())
                : resultado.Ignorados.Count > 0 ? $"Ficaram de fora, por não serem emails: {string.Join(", ", resultado.Ignorados)}." : null;
            return new JsonResult(new { ok = resultado.Gravado, valor = resultado.Valor, aviso });
        }

        return VoltarDaLista(resultado);
    }

    [DefinicaoGeral]
    public async Task<IActionResult> OnPostApagarListaAsync([FromForm(Name = "lista_id")] int? listaId)
    {
        var apagada = await envioEmail.ApagarListaAsync(listaId ?? 0, HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index", new { envio = apagada ? "apagada-lista" : nameof(EnvioEmailEstado.NaoExiste) });
    }

    /// <summary>POST: criar (sem modelo_id) ou guardar um modelo, na janela do modelo.</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostGuardarModeloAsync(
        [FromForm(Name = "modelo_id")] int? modeloId,
        string? nome,
        string? remetente,
        string? assunto,
        string? corpo,
        [FromForm(Name = "lista_id")] int? listaId)
    {
        var resultado = await envioEmail.GuardarModeloAsync(modeloId, nome, remetente, assunto, corpo, listaId, HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index", new { envio = resultado.Gravado ? "ok-modelo" : resultado.Estado.ToString() });
    }

    [DefinicaoGeral]
    public async Task<IActionResult> OnPostApagarModeloAsync([FromForm(Name = "modelo_id")] int? modeloId)
    {
        var apagado = await envioEmail.ApagarModeloAsync(modeloId ?? 0, HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index", new { envio = apagado ? "apagado-modelo" : nameof(EnvioEmailEstado.NaoExiste) });
    }

    private IActionResult VoltarDaLista(EnvioEmailGravacao resultado, int? lista = null) =>
        RedirectToPage("/Definicoes/Index", resultado switch
        {
            { Gravado: true, Ignorados.Count: > 0 } => new { envio = "aviso-lista", fora = (string?)string.Join(", ", resultado.Ignorados), lista },
            { Gravado: true } => new { envio = "ok-lista", fora = (string?)null, lista },
            _ => new { envio = resultado.Estado.ToString(), fora = (string?)null, lista }
        });

    /// <summary>
    /// POST: gravar a tolerância do corredor de Pegamentos (mm). Aceita
    /// vírgula ou ponto. Só os registos criados depois usam o novo valor.
    /// Fora do habitual ou com mais de duas casas grava com aviso; só o que
    /// não é um número sem sinal fica por gravar.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSavePegamentosToleranceAsync([FromForm(Name = "tolerance_mm")] string? toleranceMm)
    {
        var save = "tolerancia-invalida";
        var text = toleranceMm?.Trim().Replace(',', '.');
        if (decimal.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            var result = await savePegamentosTolerance.SaveAsync(value, HttpContext.RequestAborted);
            if (result.Saved)
            {
                save = !result.Rounded && !result.OutsideUsualRange
                    ? "ok-tolerancia"
                    : "aviso-tolerancia" + (result.Rounded ? "-arredondada" : null) + (result.OutsideUsualRange ? "-fora" : null);
            }
        }

        return RedirectToPage("/Definicoes/Index", new { save });
    }

    /// <summary>
    /// POST: gravar a densidade do vidro NNPB e PS (g/cm³; D15, 19/10/2026).
    /// Aceita vírgula ou ponto. Só os Pesos criados depois usam o valor novo.
    /// Fora do habitual ou com mais de quatro casas grava com aviso; só o que
    /// não é um número maior que zero, num dos campos, deixa os dois por
    /// gravar.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSaveGlassDensityAsync(
        [FromForm(Name = "densidade_nnpb")] string? nnpb,
        [FromForm(Name = "densidade_ps")] string? ps)
    {
        var densidade = "invalida";
        if (LerDensidade(nnpb) is { } valorNnpb && LerDensidade(ps) is { } valorPs)
        {
            var result = await saveGlassDensity.SaveAsync(valorNnpb, valorPs, HttpContext.RequestAborted);
            if (result.Saved)
            {
                densidade = result.Rounded.Count == 0 && result.OutsideUsualRange.Count == 0
                    ? "ok"
                    : "aviso"
                        + string.Concat(result.Rounded.Select(p => "-arredondada-" + Codigo(p)))
                        + string.Concat(result.OutsideUsualRange.Select(p => "-fora-" + Codigo(p)));
            }
        }

        return RedirectToPage("/Definicoes/Index", new { densidade });
    }

    /// <summary>As linhas com PU NNPB (Diogo, 22/10/2026: «NNPB só existe na B1 e por vezes na C3»).</summary>
    public static readonly IReadOnlyList<string> LinhasNnpb = ["B1", "C3"];

    /// <summary>
    /// POST: as secções de todas as linhas do cartão de uma vez (um só
    /// «Guardar»). Só as linhas NNPB; o primeiro erro é o que se mostra.
    /// </summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSavePuncoesSeccoesTodasAsync(
        [FromForm(Name = "maquina")] List<string>? maquinas,
        [FromForm(Name = "seccoes")] List<string>? seccoes)
    {
        maquinas ??= [];
        seccoes ??= [];
        var resultado = PuncoesSeccoesGravacao.Gravado;
        for (var i = 0; i < Math.Min(maquinas.Count, seccoes.Count); i++)
        {
            if (!LinhasNnpb.Contains(maquinas[i]))
            {
                continue;
            }

            var r = await puncoesLinhas.GuardarAsync(maquinas[i], seccoes[i], HttpContext.RequestAborted);
            if (r != PuncoesSeccoesGravacao.Gravado && resultado == PuncoesSeccoesGravacao.Gravado)
            {
                resultado = r;
            }
        }

        return RedirectToPage("/Definicoes/Index", new { seccoes = resultado.ToString() });
    }

    /// <summary>POST: as secções de uma linha (compatibilidade: o cartão grava todas com SavePuncoesSeccoesTodas).</summary>
    [DefinicaoGeral]
    public async Task<IActionResult> OnPostSavePuncoesSeccoesAsync(
        [FromForm(Name = "maquina")] string? maquina,
        [FromForm(Name = "seccoes")] string? seccoes)
    {
        var result = await puncoesLinhas.GuardarAsync(maquina, seccoes, HttpContext.RequestAborted);
        return RedirectToPage("/Definicoes/Index", new { seccoes = result.ToString() });
    }

    private static string Codigo(DMO.Alpha.Core.Tools.CmProcess process) =>
        process == DMO.Alpha.Core.Tools.CmProcess.Nnpb ? "nnpb" : "ps";

    private static decimal? LerDensidade(string? texto) =>
        decimal.TryParse(texto?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
}
