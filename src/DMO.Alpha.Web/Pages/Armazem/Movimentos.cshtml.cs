using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;
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
public sealed class MovimentosModel(
    IArmazemMovimentos movimentos, IArmazemHistorico historico, IArmazemLotesNovos lotesNovos, CurrentUserCapabilities capabilities, ShellStateFactory shellState) : PageModel
{
    /// <summary>Os três cartões do painel, por esta ordem (Diogo, 22/10/2026: «CM MF BQ»).</summary>
    public static readonly IReadOnlyList<ToolType> Tipos = [ToolType.Cm, ToolType.Mf, ToolType.Bq];

    /// <summary>O tipo escolhido no painel (CM, MF ou BQ); com uma ferramenta, é o dela.</summary>
    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    public ToolType? Tipo { get; private set; }

    /// <summary>As últimas entradas e saídas deste tipo (todas as pessoas), da mais recente para a mais antiga.</summary>
    public PagedList<ArmazemHistoricoLinha> Log { get; private set; } = PagedList<ArmazemHistoricoLinha>.Empty;

    /// <summary>Os lotes novos deste tipo ainda por criar nas Ferramentas (no topo do log, «Por criar»).</summary>
    public IReadOnlyList<ArmazemLoteNovoItem> PorCriar { get; private set; } = [];

    /// <summary>A recusa do registo de um lote novo, desenhada com o que a pessoa escreveu.</summary>
    public ArmazemLoteNovoResultado? LoteNovoRecusa { get; private set; }

    public string? LoteNovoReferencia { get; private set; }

    public string? LoteNovoLote { get; private set; }

    public string? LoteNovoPosicao { get; private set; }

    public static string TipoValor(ToolType tipo) => ArmazemTextos.Tipo(tipo);

    public static ToolType? TipoDe(string? texto) => texto?.Trim().ToUpperInvariant() switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        "BQ" => ToolType.Bq,
        _ => null
    };

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

    /// <summary>A Linha escolhida (Saída para Produção, ou a Linha com que vem na Entrada).</summary>
    public string? LinhaEscolhida { get; private set; }

    /// <summary>O reparador com que a ferramenta vem, como ficou no formulário da Entrada.</summary>
    public string? ReparadorDaVinda { get; private set; }

    public string? Data { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>
    /// A posição escrita antes de procurar (Diogo, 22/10/2026: primeiro a
    /// posição, depois a referência): segue para a ferramenta escolhida e
    /// a Entrada abre já com ela.
    /// </summary>
    /// <summary>Entrada ou Saída escolhida nos dois botões (sem nada: a que faz sentido).</summary>
    /// <summary>Com as Ferramentas, a linha das últimas abre a ficha; sem elas, a ferramenta aqui.</summary>
    public bool AbreFicha { get; private set; }

    /// <summary>As Linhas e o % de uso das ferramentas da lista das últimas.</summary>
    public IReadOnlyDictionary<string, ArmazemLinhasEUso> LinhasEUso { get; private set; } = new Dictionary<string, ArmazemLinhasEUso>();

    /// <summary>
    /// Entrada ou Saída no «Registar …» (Diogo, 23/10/2026: «No armazém
    /// deveria primeiro aparecer entrada saída porque isso ajuda a filtrar o
    /// que existe para entrar e sair»): ?modo=entrada|saida; sem nada, a da
    /// ferramenta aberta, senão a Entrada.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "modo")]
    public string? ModoTexto { get; set; }

    /// <summary>O modo escolhido: «entrada» ou «saida».</summary>
    public string Modo => ModoTexto?.Trim().ToLowerInvariant() switch
    {
        "entrada" => "entrada",
        "saida" => "saida",
        _ => Acao switch
        {
            "entrada" => "entrada",
            "saida" => "saida",
            _ => Situacao is { NoArmazem: true } ? "saida" : "entrada"
        }
    };

    public bool ModoSaida => Modo == "saida";

    [BindProperty(SupportsGet = true, Name = "acao")]
    public string? Acao { get; set; }

    [BindProperty(SupportsGet = true, Name = "pos")]
    public string? PosicaoPedida { get; set; }

    /// <summary>A posição pedida, só se tem 4 algarismos.</summary>
    public string? PosicaoPedidaValida => PosicaoPedida?.Trim() is { Length: 4 } p && p.All(char.IsAsciiDigit) ? p : null;

    public ArmazemDestino? Destino => DestinoDe(DestinoTexto);

    public bool Gravou => Gravacao is "lote-novo" or "entrada" or "entrada-par" or "saida" or "corrigir" or "repetido";

    public string Mensagem => Gravacao switch
    {
        "lote-novo" => $"Lote novo registado para a posição {RecusaPosicao}. Fica «Por criar» até quem tem o módulo Ferramentas criar a ferramenta; a entrada fica registada nessa altura.",
        "entrada" => $"Entrada registada na posição {RecusaPosicao}.",
        "entrada-par" => $"Entrada da CM e do MF registada na posição {RecusaPosicao}.",
        "saida" => "Saída registada.",
        "corrigir" => "Localização corrigida.",
        "repetido" => "Este movimento já estava registado.",
        _ when LoteNovoRecusa is { } loteRecusa => loteRecusa switch
        {
            ArmazemLoteNovoResultado.SemReferencia => "Escreva a referência.",
            ArmazemLoteNovoResultado.SemLote => "Escreva o lote.",
            ArmazemLoteNovoResultado.PosicaoInvalida => "Escreva a posição com os 4 algarismos (ex.: 0042).",
            ArmazemLoteNovoResultado.ReferenciaLonga => $"A referência tem até {ArmazemLoteNovo.ReferenciaMaxLength} caracteres.",
            ArmazemLoteNovoResultado.LoteLongo => $"O lote tem até {ArmazemLoteNovo.LoteMaxLength} caracteres.",
            _ => string.Empty
        },
        _ => Recusa switch
        {
            ArmazemMovimentoResultado.FerramentaNaoExiste => "Esta ferramenta já não existe.",
            ArmazemMovimentoResultado.JaNoArmazem => $"Esta ferramenta já está no armazém, na posição {RecusaPosicao}.",
            ArmazemMovimentoResultado.NaoEstaNoArmazem => "Esta ferramenta não está no armazém.",
            ArmazemMovimentoResultado.PosicaoInvalida => "Escreva a posição com os 4 algarismos (ex.: 0042).",
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

    public int Pagina { get; private set; } = 1;

    /// <summary>As últimas entradas e saídas só com uma ferramenta aberta ou procurada.</summary>
    public bool MostraLog { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? gravacao, string? posicao, string[]? aviso, int? pagina)
    {
        Pagina = pagina is > 0 ? pagina.Value : 1;
        Gravacao = gravacao;
        RecusaPosicao = posicao;
        Avisos = aviso ?? [];
        return await DesenharAsync();
    }

    /// <summary>A procura ao escrever (?handler=Procurar&amp;q=): só a lista, por baixo do campo.</summary>
    public async Task<IActionResult> OnGetProcurarAsync(string? q)
    {
        Tipo = TipoDe(TipoTexto);
        ViewData["ArmModo"] = Modo;
        var encontradas = DoModo(DoTipo(await movimentos.ProcurarAsync(q, HttpContext.RequestAborted)));
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
        [FromForm(Name = "linha")] int? linha = null,
        [FromForm(Name = "reparador_vinda")] string? reparadorVinda = null,
        [FromForm(Name = "vinda")] string? vinda = null)
    {
        // Com os campos da vinda no formulário, grava o que lá ficou (vazio
        // fica vazio); sem eles (outro cliente), os da última Saída.
        var saida = await movimentos.EntradaAsync(
            new PedidoDeEntrada(Operacao(operacaoId), ToolId ?? string.Empty, posicao, OrigemDe(origem), EstadoDe(estado),
                DataUtc(data), observacoes, criarPosicao == "1", false,
                LinhaId: linha, Reparador: reparadorVinda, LinhaEReparadorDoFormulario: vinda == "1"),
            HttpContext.RequestAborted);
        return await Responder(saida, "entrada", operacaoId, () =>
        {
            Posicao = posicao;
            OrigemEscolhida = origem;
            EstadoEscolhido = estado;
            LinhaEscolhida = linha?.ToString(CultureInfo.InvariantCulture);
            ReparadorDaVinda = reparadorVinda;
            Data = data;
            Observacoes = observacoes;
        });
    }

    /// <summary>
    /// Registar lote novo (Diogo, 22/10/2026): a ferramenta não existe; o
    /// Armazém guarda o tipo do cartão, a referência, o lote e a posição como
    /// «por criar», e quem tem o módulo Ferramentas cria-a. Nunca cria a ferramenta.
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostLoteNovoAsync(
        [FromForm(Name = "referencia")] string? referencia,
        [FromForm(Name = "lote")] string? lote,
        [FromForm(Name = "posicao")] string? posicao)
    {
        if (TipoDe(TipoTexto) is not { } tipo)
        {
            return RedirectToPage("/Armazem/Movimentos");
        }

        var saida = await lotesNovos.RegistarAsync(tipo, referencia, lote, posicao, HttpContext.RequestAborted);
        if (saida.Gravou)
        {
            return RedirectToPage("/Armazem/Movimentos", new
            {
                tipo = TipoValor(tipo),
                modo = "entrada",
                gravacao = "lote-novo",
                posicao = posicao?.Trim(),
                aviso = (saida.Avisos ?? []).ToArray()
            });
        }

        LoteNovoRecusa = saida.Resultado;
        LoteNovoReferencia = referencia;
        LoteNovoLote = lote;
        LoteNovoPosicao = posicao;
        ViewData["LoteNovoReferencia"] = referencia ?? string.Empty;
        ViewData["LoteNovoLote"] = lote ?? string.Empty;
        ViewData["LoteNovoPosicao"] = posicao ?? string.Empty;
        Procura = referencia;
        return await DesenharAsync();
    }

    [EnvioUnico]
    public async Task<IActionResult> OnPostSaidaAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparador")] int? reparador,
        [FromForm(Name = "fabrica")] string? fabrica,
        [FromForm(Name = "estado")] string? estado,
        [FromForm(Name = "data")] string? data,
        [FromForm(Name = "observacoes")] string? observacoes,
        [FromForm(Name = "linha")] int? linha = null)
    {
        var saida = await movimentos.SaidaAsync(
            new PedidoDeSaida(Operacao(operacaoId), ToolId ?? string.Empty, Destino, reparador, fabrica, EstadoDe(estado),
                DataUtc(data), observacoes, Destino == ArmazemDestino.Producao ? linha : null),
            HttpContext.RequestAborted);
        return await Responder(saida, "saida", operacaoId, () =>
        {
            ReparadorEscolhido = reparador?.ToString(CultureInfo.InvariantCulture);
            Fabrica = fabrica;
            LinhaEscolhida = linha?.ToString(CultureInfo.InvariantCulture);
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
                // O modo continua o mesmo: quem dá entradas continua nas entradas.
                modo = de is "entrada" or "saida" ? de : null,
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
        ViewData["Title"] = "Registo — Armazém";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("armazem", "Armazém"))) with
        {
            ActiveFunctionKey = "movimentos",
            Functions = ShellStateFactory.ArmazemFunctions("movimentos")
        };

        Tipo = TipoDe(TipoTexto);
        if (!string.IsNullOrWhiteSpace(ToolId))
        {
            Situacao = await movimentos.SituacaoAsync(ToolId, HttpContext.RequestAborted);
            FerramentaNaoExiste = Situacao is null;
            // O cartão é sempre o do tipo da ferramenta: um link de outra
            // página (a ficha, o Mapa, o par) abre já no cartão certo.
            if (Situacao is not null)
            {
                Tipo = Situacao.Tipo;
            }
        }
        else if (Tipo is not null && !string.IsNullOrWhiteSpace(Procura))
        {
            Encontradas = DoModo(DoTipo(await movimentos.ProcurarAsync(Procura, HttpContext.RequestAborted)));
        }
        else if (Tipo is not null && ModoSaida && PosicaoPedidaValida is { } posSaida)
        {
            // Na Saída, a posição procura o que lá está.
            Encontradas = DoModo(DoTipo(await movimentos.ProcurarAsync(posSaida, HttpContext.RequestAborted)));
        }

        ViewData["ArmModo"] = Modo;

        // Nunca uma lista de ferramentas sem procura (Diogo, 27/10/2026: «está
        // sempre em branco até começar a escrever a referência»): as últimas
        // entradas e saídas só aparecem com uma ferramenta aberta ou procurada.
        if (Tipo is { } tipo && (Situacao is not null || Gravou || !string.IsNullOrWhiteSpace(Procura)))
        {
            MostraLog = true;
            AbreFicha = (await capabilities.GetAsync(HttpContext.RequestAborted)).Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
            Log = await historico.ListarAsync(new ArmazemHistoricoFiltro(TipoFerramenta: tipo), Pagina, HttpContext.RequestAborted);
            // Também a ferramenta aberta: a sua utilização vai para a barra da «Ferramenta selecionada».
            LinhasEUso = await movimentos.LinhasEUsoAsync(Log.Items.Select(m => m.ToolId).Append(Situacao?.ToolId).OfType<string>().ToList(), HttpContext.RequestAborted);
            if (Pagina == 1)
            {
                PorCriar = await lotesNovos.PorCriarAsync(tipo, HttpContext.RequestAborted);
            }
        }


        return Page();
    }

    private IReadOnlyList<ArmazemProcuraItem> DoTipo(IReadOnlyList<ArmazemProcuraItem> itens) =>
        Tipo is { } tipo ? itens.Where(i => i.Tipo == tipo).ToList() : [];

    /// <summary>
    /// O filtro do modo (Diogo, 23/10/2026): a Entrada só mostra as que estão
    /// fora do armazém; a Saída só as que estão numa posição (e, com a
    /// posição escrita, só as dessa posição).
    /// </summary>
    private IReadOnlyList<ArmazemProcuraItem> DoModo(IReadOnlyList<ArmazemProcuraItem> itens)
    {
        var pos = PosicaoPedidaValida;
        var ficam = ModoSaida
            ? itens.Where(i => i.NoArmazem && (pos is null || i.OndeEsta.EndsWith(pos, StringComparison.Ordinal))).ToList()
            : itens.Where(i => !i.NoArmazem).ToList();
        // Nada se perde: as que ficam de fora contam-se, com o outro modo.
        ViewData["ArmOcultas"] = ModoSaida ? itens.Count(i => !i.NoArmazem) : itens.Count(i => i.NoArmazem);
        return ficam;
    }

    /// <summary>O movimento, curto: Entrada, Saída · destino, Corrigir localização.</summary>
    public static string Movimento(ArmazemHistoricoLinha m) => UltimosModel.Movimento(m);

    public static string NomeCurto(ToolType tipo, string referencia, string lote) => FerramentaNome.Curto(tipo, referencia, lote);

    public static Microsoft.AspNetCore.Html.IHtmlContent Nome(ToolType tipo, string referencia, string lote) => FerramentaNome.Html(tipo, referencia, lote);

    public static string DataHora(DateTime utc) => DataLocal(utc);

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
