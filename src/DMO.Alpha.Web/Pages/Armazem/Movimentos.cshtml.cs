using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Armazem;

/// <summary>
/// Os movimentos do Armazém (blueprint armazem/OVERVIEW §1): procurar a
/// ferramenta pela referência, pelo lote ou pela posição; ver onde está; dar
/// Entrada (se está fora) ou Saída (se está numa posição); e, no «Opções»,
/// Corrigir localização. A Entrada propõe a origem e o estado pela última
/// Saída (D11); a pessoa confirma. Uma posição que não existe cria-se com
/// «Criar e registar». Só se recusa o impossível ou o que falta; o resto
/// grava e avisa.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ArmazemGestao)]
public sealed class MovimentosModel(IArmazemMovimentos movimentos, IArmazemMapa mapa, ShellStateFactory shellState) : PageModel
{
    private const string FormatoData = "yyyy-MM-ddTHH:mm";

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Procura { get; set; }

    [BindProperty(SupportsGet = true, Name = "ferramenta")]
    public string? ToolId { get; set; }

    [BindProperty(SupportsGet = true, Name = "destino")]
    public string? DestinoTexto { get; set; }

    public IReadOnlyList<ArmazemProcuraItem> Encontradas { get; private set; } = [];

    public ArmazemSituacao? Situacao { get; private set; }

    /// <summary>A ferramenta pedida não existe (ou foi eliminada).</summary>
    public bool FerramentaNaoExiste { get; private set; }

    /// <summary>O identificador do envio que o formulário leva (envio único).</summary>
    public Guid OperacaoId { get; private set; } = Guid.NewGuid();

    /// <summary>O que gravou, pelo redirect (PRG): entrada, saida, corrigir ou repetido.</summary>
    public string? Gravacao { get; private set; }

    public IReadOnlyList<string> Avisos { get; private set; } = [];

    /// <summary>A recusa de um envio, desenhada com o que a pessoa escreveu.</summary>
    public ArmazemMovimentoResultado? Recusa { get; private set; }

    /// <summary>O código de que a recusa fala (a posição).</summary>
    public string? RecusaPosicao { get; private set; }

    /// <summary>De que formulário é a recusa: entrada, saida ou corrigir.</summary>
    public string? RecusaDe { get; private set; }

    // O que a pessoa escreveu, para voltar a desenhar o formulário recusado.
    public string? Posicao { get; private set; }

    public string? OrigemEscolhida { get; private set; }

    public string? EstadoEscolhido { get; private set; }

    public string? ReparadorEscolhido { get; private set; }

    public string? Fabrica { get; private set; }

    public string? Data { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>Se o par entra também (por omissão, sim; a recusa guarda o que a pessoa escolheu).</summary>
    public bool ComPar { get; private set; } = true;

    /// <summary>As posições livres, sugeridas no campo da posição da Entrada.</summary>
    public IReadOnlyList<string> PosicoesLivres { get; private set; } = [];

    public ArmazemDestino? Destino => DestinoDe(DestinoTexto);

    public bool Gravou => Gravacao is "entrada" or "entrada-par" or "saida" or "corrigir" or "repetido";

    public string Mensagem => Gravacao switch
    {
        "entrada" => $"Entrada registada na posição {RecusaPosicao}.",
        "entrada-par" => $"Entrada da CM e do MF registada na posição {RecusaPosicao}.",
        "saida" => "Saída registada.",
        "corrigir" => "Localização corrigida.",
        "repetido" => "Este movimento já estava registado.",
        _ => Recusa switch
        {
            ArmazemMovimentoResultado.FerramentaNaoExiste => "Esta ferramenta já não existe.",
            ArmazemMovimentoResultado.JaNoArmazem => $"Esta ferramenta já está no armazém, na posição {RecusaPosicao}.",
            ArmazemMovimentoResultado.NaoEstaNoArmazem => "Esta ferramenta não está no armazém.",
            ArmazemMovimentoResultado.PosicaoInvalida => "Escreva a posição: só letras e algarismos, até 16 (ex.: 0042).",
            ArmazemMovimentoResultado.PosicaoNaoExiste => $"A posição {RecusaPosicao} não existe. Criar?",
            ArmazemMovimentoResultado.SemOrigem => "Escolha a origem.",
            ArmazemMovimentoResultado.SemEstado => "Escolha o estado.",
            ArmazemMovimentoResultado.SemDestino => "Escolha o destino.",
            ArmazemMovimentoResultado.SemReparador => "Escolha o reparador.",
            ArmazemMovimentoResultado.ReparadorInvalido => "Esse reparador já não está na lista. Escolha outro.",
            ArmazemMovimentoResultado.ObservacoesLongas => $"As observações têm até {ArmazemMovimento.ObservacoesMaxLength} caracteres.",
            ArmazemMovimentoResultado.FabricaLonga => $"A fábrica tem até {ArmazemMovimento.FabricaMaxLength} caracteres.",
            ArmazemMovimentoResultado.NadaAMudar => "A ferramenta já está aí. Nada mudou.",
            ArmazemMovimentoResultado.MudouEntretanto => "Outra pessoa registou um movimento desta ferramenta ao mesmo tempo. Veja onde está agora.",
            _ => string.Empty
        }
    };

    public async Task<IActionResult> OnGetAsync(string? gravacao, string? posicao, string[]? aviso)
    {
        Gravacao = gravacao;
        RecusaPosicao = posicao;
        Avisos = aviso ?? [];
        return await DesenharAsync();
    }

    /// <summary>A procura ao escrever (?handler=Procurar&amp;q=): só a lista, por baixo do campo.</summary>
    public async Task<IActionResult> OnGetProcurarAsync(string? q)
    {
        var encontradas = await movimentos.ProcurarAsync(q, HttpContext.RequestAborted);
        return Partial("_ArmazemProcura", encontradas);
    }

    [EnvioUnico]
    public async Task<IActionResult> OnPostEntradaAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "posicao")] string? posicao,
        [FromForm(Name = "origem")] string? origem,
        [FromForm(Name = "estado")] string? estado,
        [FromForm(Name = "data")] string? data,
        [FromForm(Name = "observacoes")] string? observacoes,
        [FromForm(Name = "criar_posicao")] string? criarPosicao,
        [FromForm(Name = "com_par")] string? comPar)
    {
        var saida = await movimentos.EntradaAsync(
            new PedidoDeEntrada(Operacao(operacaoId), ToolId ?? string.Empty, posicao, OrigemDe(origem), EstadoDe(estado),
                DataUtc(data), observacoes, criarPosicao == "1", comPar == "1"),
            HttpContext.RequestAborted);
        return await Responder(saida, saida.ComPar ? "entrada-par" : "entrada", operacaoId, () =>
        {
            ComPar = comPar == "1";
            Posicao = posicao;
            OrigemEscolhida = origem;
            EstadoEscolhido = estado;
            Data = data;
            Observacoes = observacoes;
        });
    }

    [EnvioUnico]
    public async Task<IActionResult> OnPostSaidaAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparador")] int? reparador,
        [FromForm(Name = "fabrica")] string? fabrica,
        [FromForm(Name = "estado")] string? estado,
        [FromForm(Name = "data")] string? data,
        [FromForm(Name = "observacoes")] string? observacoes)
    {
        var saida = await movimentos.SaidaAsync(
            new PedidoDeSaida(Operacao(operacaoId), ToolId ?? string.Empty, Destino, reparador, fabrica, EstadoDe(estado),
                DataUtc(data), observacoes),
            HttpContext.RequestAborted);
        return await Responder(saida, "saida", operacaoId, () =>
        {
            ReparadorEscolhido = reparador?.ToString(CultureInfo.InvariantCulture);
            Fabrica = fabrica;
            EstadoEscolhido = estado;
            Data = data;
            Observacoes = observacoes;
        });
    }

    [EnvioUnico]
    public async Task<IActionResult> OnPostCorrigirAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "posicao")] string? posicao,
        [FromForm(Name = "observacoes")] string? observacoes,
        [FromForm(Name = "criar_posicao")] string? criarPosicao)
    {
        var saida = await movimentos.CorrigirAsync(
            new PedidoDeCorrecao(Operacao(operacaoId), ToolId ?? string.Empty, posicao, observacoes, criarPosicao == "1"),
            HttpContext.RequestAborted);
        return await Responder(saida, "corrigir", operacaoId, () =>
        {
            Posicao = posicao;
            Observacoes = observacoes;
        });
    }

    private async Task<IActionResult> Responder(ArmazemMovimentoSaida saida, string de, Guid operacaoId, Action guardar)
    {
        if (saida.Gravou)
        {
            return RedirectToPage("/Armazem/Movimentos", new
            {
                ferramenta = ToolId,
                gravacao = saida.Resultado == ArmazemMovimentoResultado.JaRegistado ? "repetido" : de,
                posicao = saida.Posicao,
                aviso = saida.Avisos.ToArray()
            });
        }

        Recusa = saida.Resultado;
        RecusaPosicao = saida.Posicao;
        RecusaDe = de;
        // O mesmo envio continua: nada ficou gravado com este identificador.
        OperacaoId = Operacao(operacaoId);
        guardar();
        return await DesenharAsync();
    }

    private async Task<IActionResult> DesenharAsync()
    {
        ViewData["Title"] = "Movimentos — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "movimentos",
            Functions = ShellStateFactory.ArmazemFunctions("movimentos")
        };

        if (!string.IsNullOrWhiteSpace(ToolId))
        {
            Situacao = await movimentos.SituacaoAsync(ToolId, HttpContext.RequestAborted);
            FerramentaNaoExiste = Situacao is null;
            if (Situacao is { NoArmazem: false })
            {
                PosicoesLivres = (await mapa.MapaAsync(ArmazemArmarios.Todos, ArmazemMapaFiltro.Livres, HttpContext.RequestAborted))
                    .Lista.Select(p => p.Codigo).ToList();
            }
        }
        else if (!string.IsNullOrWhiteSpace(Procura))
        {
            Encontradas = await movimentos.ProcurarAsync(Procura, HttpContext.RequestAborted);
        }

        return Page();
    }

    /// <summary>O valor de uma opção no formulário.</summary>
    public static string Valor<T>(T valor) where T : struct, Enum => Convert.ToInt32(valor, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    /// <summary>A data local, como o campo a mostra.</summary>
    public static string DataLocal(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Dia(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static Guid Operacao(Guid operacaoId) => operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId;

    private static ArmazemDestino? DestinoDe(string? texto) => Enumerado<ArmazemDestino>(texto);

    private static ArmazemOrigem? OrigemDe(string? texto) => Enumerado<ArmazemOrigem>(texto);

    private static FerramentaEstadoTipo? EstadoDe(string? texto) => Enumerado<FerramentaEstadoTipo>(texto);

    private static T? Enumerado<T>(string? texto) where T : struct, Enum =>
        int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && Enum.IsDefined(typeof(T), n)
            ? (T)Enum.ToObject(typeof(T), n)
            : null;

    // A data e hora reais chegam em hora local da fábrica; vazio ou ilegível
    // é agora (a data nunca recusa). A hora que cai no salto da mudança de
    // hora passa para a seguinte.
    private static DateTime? DataUtc(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)
            || !DateTime.TryParseExact(texto, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return DmoBusinessTime.ToUtc(DmoBusinessTime.NextValidLocal(local, out _));
    }
}
