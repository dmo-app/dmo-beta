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
using Microsoft.AspNetCore.WebUtilities;

namespace DMO.Alpha.Web.Pages.ReparacaoInterna;

/// <summary>
/// Registar (blueprint reparacao-interna/OVERVIEW §1; Diogo, 22/10/2026:
/// «um sistema similar ao das boquilhas»): o painel lateral das Boquilhas
/// (o rail único da casa) com as linhas, as B e depois as C, cada uma com o
/// que está em produção no dia (a referência, a CM e o MF com o lote). Um
/// clique numa linha abre o registo dela: CM ou MF (o ativo em azul claro),
/// o número e OK; o dia muda-se no campo «Dia» (as datas são livres, só
/// avisa); «É outra CM/MF» escreve a referência e o lote (peça 4); sem
/// produção grava-se sem Job On, por associar. Por baixo, o registo (log)
/// das reparações desta pessoa nessa linha nesse dia: sem botões nas
/// linhas, o duplo clique (ou «Editar» com a linha escolhida) abre a
/// reparação para corrigir o número ou anular, com o motivo opcional; nada
/// se recusa além do impossível. A tab «Histórico» é o histórico desta
/// pessoa (é a página de cada reparador: sem filtro de quem), pela linha,
/// os dias (de / até), a referência e a produção; abre vazia. Gravar troca
/// só as zonas que mudam (DmoTroca).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ReparacaoInterna)]
public sealed class IndexModel(
    IReparacoesInternas reparacoes,
    IReparacoesInternasConsulta consulta,
    IMachineListQuery maquinas,
    IProductionPickerQuery producoes,
    ICurrentAccountContext conta,
    CurrentUserCapabilities capabilities,
    ShellStateFactory shellState) : PageModel
{
    /// <summary>Quem tem as Ferramentas abre a ficha da ferramenta pelo link; os outros veem só o texto.</summary>
    public bool AbreFerramentas { get; private set; }

    [BindProperty(SupportsGet = true, Name = "maquina")]
    public string? MaquinaCodigo { get; set; }

    [BindProperty(SupportsGet = true, Name = "producao")]
    public int? ProducaoPedida { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    /// <summary>
    /// A referência e o lote escritos que ficam no formulário: numa recusa,
    /// e sem Job On depois de gravar (aí todas as reparações levam a
    /// ferramenta escrita, e a seguinte é quase sempre da mesma).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "referencia")]
    public string? ReferenciaEscrita { get; set; }

    [BindProperty(SupportsGet = true, Name = "lote")]
    public string? LoteEscrito { get; set; }

    /// <summary>«É outra CM/MF» marcado (só numa recusa: depois de gravar volta a desmarcado).</summary>
    public bool OutraMarcada { get; private set; }

    /// <summary>A ferramenta escrita que a mensagem de gravado refere: falta associar.</summary>
    public string? Escrita { get; private set; }

    /// <summary>Gravou sem Job On.</summary>
    public bool SemJobOn { get; private set; }

    public IReadOnlyList<MachineOption> Maquinas { get; private set; } = [];

    public MachineOption? Maquina { get; private set; }

    /// <summary>A produção do dia da máquina escolhida (null sem máquina).</summary>
    public RiProducaoDoDia? DoDia { get; private set; }

    public RiProducao? Producao { get; private set; }

    public ReparacaoTipo? Tipo => RiTextos.TipoDe(TipoTexto);

    /// <summary>A CM ou o MF da produção, conforme o tipo escolhido.</summary>
    public RiFerramenta? Ferramenta => Tipo is { } tipo ? Producao?.Ferramenta(tipo) : null;

    /// <summary>O identificador do envio que o formulário leva (envio único).</summary>
    public Guid OperacaoId { get; private set; } = Guid.NewGuid();

    /// <summary>O que gravou, pelo redirect (PRG): ok ou repetido.</summary>
    public string? Gravacao { get; private set; }

    /// <summary>O número de que fala a mensagem.</summary>
    public string? Numero { get; private set; }

    public ReparacaoResultado? Recusa { get; private set; }

    /// <summary>O número reparado: só algarismos, até 3 (Diogo, 23/10/2026).</summary>
    public const int NumeroAlgarismos = 3;

    /// <summary>O número escrito não é só algarismos ou passa de <see cref="NumeroAlgarismos"/>: nada foi gravado.</summary>
    public bool NumeroInvalido { get; private set; }

    public static bool NumeroValido(string? numero) =>
        numero?.Trim() is { Length: > 0 and <= NumeroAlgarismos } n && n.All(char.IsAsciiDigit);

    /// <summary>O número escrito numa recusa, para ficar no campo.</summary>
    public string? NumeroEscrito { get; private set; }

    /// <summary>Como acabou uma correção ou uma anulação, pelo redirect (PRG).</summary>
    public string? Correcao { get; private set; }

    public bool Gravou => Gravacao is "ok" or "repetido" || Correcao is "corrigido" or "anulado";

    /// <summary>A mensagem no canto: gravado com a ferramenta escrita só avisa (falta associar).</summary>
    public string Tom => Gravacao is "ok" && Escrita is not null ? "aviso" : Gravou ? "ok" : "erro";

    /// <summary>Sem produção para o dia: grava-se sempre com a ferramenta escrita, sem Job On.</summary>
    public bool SemProducao => DoDia is { SemProducao: true };

    /// <summary>O campo da ferramenta escrita com a recusa (para o marcar).</summary>
    public bool ErroReferencia => Recusa is ReparacaoResultado.SemReferencia or ReparacaoResultado.ReferenciaLonga;

    public bool ErroLote => Recusa is ReparacaoResultado.SemLote or ReparacaoResultado.LoteLongo;

    /// <summary>Corrigir para o mesmo número: nada mudou, só avisa.</summary>
    public bool Avisou => Correcao is "igual";

    public string Mensagem => Gravacao switch
    {
        "ok" when Escrita is not null && SemJobOn => $"Reparação gravada sem Job On, com a {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)} {Escrita}. Falta associar.",
        "ok" when Escrita is not null => $"Reparação gravada com outra {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}: {Escrita}. Falta associar.",
        "ok" => $"Reparação do nº {Numero} registada.",
        "repetido" => $"A reparação do nº {Numero} já estava registada.",
        _ when Correcao is not null => Correcao switch
        {
            "corrigido" => $"Número corrigido para {Numero}.",
            "anulado" => $"Reparação do nº {Numero} anulada.",
            // O mesmo número: nada muda e não se avisa (Diogo, 21/10/2026).
            "igual" => string.Empty,
            "nao-existe" => "Esta reparação já não existe. Nada mudou.",
            "ja-anulada" => $"A reparação do nº {Numero} já tinha sido anulada. Nada mudou.",
            "mudou" => $"Outra pessoa mudou esta reparação entretanto: o número agora é {Numero}. Confira e corrija outra vez.",
            "sem-numero" => "Escreva o número corrigido. Nada mudou.",
            "numero-longo" => $"O número tem até {Reparacao.NumeroMaxLength} caracteres. Nada mudou.",
            "numero-invalido" => $"O número tem só algarismos, até {NumeroAlgarismos}. Nada mudou.",
            "motivo-longo" => $"O motivo tem até {ReparacaoRasto.MotivoMaxLength} caracteres. Nada mudou.",
            _ => string.Empty
        },
        _ when NumeroInvalido => $"O número tem só algarismos, até {NumeroAlgarismos}.",
        _ => Recusa switch
        {
            ReparacaoResultado.SemNumero => "Escreva o número reparado.",
            ReparacaoResultado.NumeroLongo => $"O número tem até {Reparacao.NumeroMaxLength} caracteres.",
            ReparacaoResultado.SemTipo => "Escolha CM ou MF.",
            ReparacaoResultado.MaquinaNaoExiste => "Escolha a linha.",
            ReparacaoResultado.SemReferencia => $"Escreva a referência da {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.ReferenciaLonga => $"A referência tem até {Reparacao.ReferenciaMaxLength} caracteres.",
            ReparacaoResultado.SemLote => $"Escreva o lote da {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.LoteLongo => $"O lote tem até {Reparacao.LoteMaxLength} caracteres.",
            ReparacaoResultado.ProducaoMudou => "A produção do dia desta linha mudou. Confira e registe outra vez.",
            ReparacaoResultado.SemFerramenta => $"Esta produção não tem {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.FerramentaMudou => $"A {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)} desta produção mudou no Job On. Confira e registe outra vez.",
            _ => string.Empty
        }
    };

    /// <summary>A tab «Histórico»: o histórico desta pessoa.</summary>
    [BindProperty(SupportsGet = true, Name = "historico")]
    public string? HistoricoTexto { get; set; }

    public bool Historico => HistoricoTexto is "1" or "true";

    /// <summary>O dia escolhido no registo (as datas são livres); sem ele, o dia operacional de agora.</summary>
    [BindProperty(SupportsGet = true, Name = "dia")]
    public DateOnly? DiaPedido { get; set; }

    [BindProperty(SupportsGet = true, Name = "de")]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true, Name = "ate")]
    public DateOnly? Ate { get; set; }

    /// <summary>O dia operacional de agora (a viragem da Reparação Interna).</summary>
    public DateOnly Hoje { get; private set; }

    /// <summary>O dia do registo: o escolhido ou o de agora.</summary>
    public DateOnly DiaDoRegisto => DiaPedido ?? Hoje;

    /// <summary>Regista-se noutro dia que não o de agora: só avisa.</summary>
    public bool OutroDia => DiaPedido is { } d && d != Hoje;

    /// <summary>As linhas do painel lateral, pela ordem (as B, as C, as outras).</summary>
    public IReadOnlyList<RiCartao> Linhas { get; private set; } = [];

    /// <summary>A linha aberta no registo (null: nenhuma escolhida).</summary>
    public RiCartao? Cartao { get; private set; }

    /// <summary>O registo (log) desta pessoa na linha aberta, no dia do registo.</summary>
    public PagedList<RiConsultaLinha> Log { get; private set; } = PagedList<RiConsultaLinha>.Empty;

    /// <summary>A referência do filtro do Histórico, sem espaços nas pontas e em maiúsculas.</summary>
    public string? ReferenciaFiltro => string.IsNullOrWhiteSpace(ReferenciaEscrita) ? null : ReferenciaEscrita.Trim().ToUpperInvariant();

    /// <summary>O segundo passo do Histórico: as últimas 5 produções da referência.</summary>
    public IReadOnlyList<ProductionPickItem> Producoes { get; private set; } = [];

    /// <summary>O Histórico tem um filtro (sem nenhum abre vazio).</summary>
    public bool ComFiltro { get; private set; }

    public PagedList<RiConsultaLinha> Historia { get; private set; } = PagedList<RiConsultaLinha>.Empty;

    public ListPager HistoriaPager { get; private set; } = new(1, 1, 0);

    public bool ProducaoEscolhida(ProductionPickItem item) => item.JobOnId == ProducaoPedida;

    /// <summary>O endereço desta vista, para a janela da reparação voltar a ele.</summary>
    public string Volta => $"{Request.Path}{Request.QueryString}";

    public async Task<IActionResult> OnGetAsync(string? gravacao, string? correcao, string? numero, string? escrita, string? semjobon, int? pagina)
    {
        Gravacao = gravacao;
        Correcao = correcao;
        Numero = numero;
        Escrita = string.IsNullOrWhiteSpace(escrita) ? null : escrita;
        SemJobOn = semjobon == "1";
        return await DesenharAsync(pagina);
    }

    /// <summary>POST: corrigir o número de uma reparação (a janela do número). Grava uma vez por envio.</summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostCorrigirAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparacao_id")] int reparacaoId,
        [FromForm(Name = "numero")] string? numero,
        [FromForm(Name = "motivo")] string? motivo,
        [FromForm(Name = "pagina")] int? pagina,
        [FromForm(Name = "volta")] string? volta)
    {
        // Só algarismos, até 3, como no registo (Diogo, 24/10/2026); vazio segue para a recusa de sempre.
        if (!string.IsNullOrWhiteSpace(numero) && !NumeroValido(numero))
        {
            return VoltarComCorrecao("numero-invalido", null, pagina, volta);
        }

        var saida = await reparacoes.CorrigirAsync(
            new PedidoDeCorrecao(operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId, reparacaoId, numero, motivo),
            HttpContext.RequestAborted);
        return DepoisDaCorrecao(saida, CorrecaoResultado.Corrigido, pagina, volta);
    }

    /// <summary>POST: anular uma reparação (o «×» e a confirmação). Grava uma vez por envio.</summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostAnularAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparacao_id")] int reparacaoId,
        [FromForm(Name = "motivo")] string? motivo,
        [FromForm(Name = "pagina")] int? pagina,
        [FromForm(Name = "volta")] string? volta)
    {
        var saida = await reparacoes.AnularAsync(
            new PedidoDeAnulacao(operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId, reparacaoId, motivo),
            HttpContext.RequestAborted);
        return DepoisDaCorrecao(saida, CorrecaoResultado.Anulado, pagina, volta);
    }

    // Volta à mesma linha, dia e página, com a mensagem (PRG); da tab
    // Histórico, à mesma vista com os filtros (volta, só desta página). O
    // mesmo envio outra vez mostra o que o primeiro mostrou.
    private IActionResult DepoisDaCorrecao(CorrecaoSaida saida, CorrecaoResultado feito, int? pagina, string? volta)
    {
        var correcao = (saida.Resultado == CorrecaoResultado.JaFeito ? feito : saida.Resultado) switch
        {
            CorrecaoResultado.Corrigido => "corrigido",
            CorrecaoResultado.Anulado => "anulado",
            CorrecaoResultado.Igual => "igual",
            CorrecaoResultado.NaoExiste => "nao-existe",
            CorrecaoResultado.JaAnulada => "ja-anulada",
            CorrecaoResultado.Mudou => "mudou",
            CorrecaoResultado.SemNumero => "sem-numero",
            CorrecaoResultado.NumeroLongo => "numero-longo",
            _ => "motivo-longo"
        };
        return VoltarComCorrecao(correcao, saida.Numero, pagina, volta);
    }

    private IActionResult VoltarComCorrecao(string correcao, string? numero, int? pagina, string? volta)
    {
        if (VoltaDestaPagina(volta) is { } destino)
        {
            var q = destino.IndexOf('?', StringComparison.Ordinal);
            var query = QueryHelpers.ParseQuery(destino[q..])
                .Where(p => p.Key is not ("correcao" or "numero" or "gravacao" or "escrita" or "semjobon"))
                .SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string?>(p.Key, v)))
                .Append(new("correcao", correcao))
                .Append(new("numero", numero));
            return LocalRedirect(QueryHelpers.AddQueryString(destino[..q], query));
        }

        return RedirectToPage("/ReparacaoInterna/Index", new
        {
            maquina = MaquinaCodigo,
            producao = ProducaoPedida,
            tipo = TipoTexto,
            dia = DiaPedido is { } d ? Data(d) : null,
            referencia = ReferenciaEscrita,
            lote = LoteEscrito,
            pagina,
            correcao,
            numero
        });
    }

    // Só um endereço desta página (nunca outro sítio): «/ReparacaoInterna?…».
    private static string? VoltaDestaPagina(string? volta) =>
        volta is not null && volta.StartsWith("/ReparacaoInterna?", StringComparison.Ordinal) && !volta.Contains("//", StringComparison.Ordinal)
            ? volta
            : null;

    /// <summary>
    /// POST: registar o número escrito (Enter). Grava uma vez por envio. Com
    /// «É outra CM/MF» (ou sem produção, o formulário leva-o sempre), a
    /// referência e o lote escritos.
    /// </summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostRegistarAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "contexto")] int? contexto,
        [FromForm(Name = "contexto_cm")] int? contextoCm,
        [FromForm(Name = "contexto_mf")] int? contextoMf,
        [FromForm(Name = "numero")] string? numero,
        [FromForm(Name = "outra")] string? outra)
    {
        var lista = await maquinas.ListAsync(HttpContext.RequestAborted);
        var maquina = lista.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        var operacao = operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId;
        var escrita = outra is "1" or "on";
        // Só algarismos, até 3 (Diogo, 23/10/2026); vazio segue para a recusa de sempre.
        if (!string.IsNullOrWhiteSpace(numero) && !NumeroValido(numero))
        {
            NumeroInvalido = true;
            NumeroEscrito = numero;
            OutraMarcada = escrita;
            OperacaoId = operacao;
            return await DesenharAsync(null);
        }
        // O cartão leva o contexto da CM e o do MF; o botão CM/MF escolhe.
        contexto ??= Tipo == ReparacaoTipo.Mf ? contextoMf : Tipo == ReparacaoTipo.Cm ? contextoCm : null;
        var saida = await reparacoes.RegistarAsync(
            new PedidoDeReparacao(operacao, maquina?.MachineId, ProducaoPedida, Tipo, contexto, numero, escrita,
                escrita ? ReferenciaEscrita : null, escrita ? LoteEscrito : null, DiaPedido),
            HttpContext.RequestAborted);
        if (saida.Gravou)
        {
            // «É outra CM/MF» volta a desmarcado: a seguinte é quase sempre a
            // do Job On. Sem Job On, a referência e o lote ficam escritos.
            return RedirectToPage("/ReparacaoInterna/Index", new
            {
                maquina = maquina?.Code,
                producao = ProducaoPedida,
                tipo = TipoTexto,
                dia = DiaPedido is { } d ? Data(d) : null,
                referencia = saida.SemJobOn ? saida.Referencia : null,
                lote = saida.SemJobOn ? saida.Lote : null,
                gravacao = saida.Resultado == ReparacaoResultado.JaRegistado ? "repetido" : "ok",
                numero = saida.Numero,
                escrita = saida.Referencia is { } referencia ? RiTextos.Escrita(referencia, saida.Lote) : null,
                semjobon = saida.PorAssociar && saida.SemJobOn ? "1" : null
            });
        }

        // Nada ficou gravado com este identificador: o mesmo envio continua,
        // com o número, a referência e o lote escritos nos campos.
        Recusa = saida.Resultado;
        NumeroEscrito = numero;
        OutraMarcada = escrita;
        OperacaoId = operacao;
        return await DesenharAsync(null);
    }

    private async Task<IActionResult> DesenharAsync(int? pagina)
    {
        var chave = Historico ? "historico" : "registar";
        ViewData["Title"] = Historico ? "Histórico — Reparação" : "Reparação";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("reparacao-interna", "Reparação Interna"))) with
        {
            ActiveFunctionKey = chave,
            Functions = ShellStateFactory.ReparacaoInternaFunctions(chave)
        };

        var cancelar = HttpContext.RequestAborted;
        Hoje = await reparacoes.DiaOperacionalAsync(cancelar);
        Maquinas = await maquinas.ListAsync(cancelar);
        Maquina = Maquinas.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        AbreFerramentas = (await capabilities.GetAsync(cancelar)).Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
        // É a página de cada reparador: o log e o histórico são só dele.
        var eu = conta.Current is { IsAuthenticated: true, IsAdmin: false, ActorId: { } id } ? id : (Guid?)null;

        if (Historico)
        {
            await HistoricoAsync(eu, pagina, cancelar);
            return Page();
        }

        // O painel: todas as linhas, as B em cima, as C depois, as outras no
        // fim, cada uma com a produção do dia do registo. A linha do pedido
        // leva a produção escolhida, o tipo e o que se escreveu.
        var linhas = new List<RiCartao>();
        foreach (var m in Maquinas.OrderBy(m => Fila(m.Code)).ThenBy(m => m.Code, StringComparer.OrdinalIgnoreCase))
        {
            var foco = m.MachineId == Maquina?.MachineId;
            var doDia = await reparacoes.ProducaoDoDiaAsync(m.MachineId, DiaDoRegisto, cancelar);
            var producao = doDia.Escolhida(foco ? ProducaoPedida : null);
            var cartao = new RiCartao(m, doDia, producao, foco ? Tipo ?? ReparacaoTipo.Cm : ReparacaoTipo.Cm, foco);
            linhas.Add(cartao);
            if (foco)
            {
                Cartao = cartao;
                DoDia = doDia;
                Producao = producao;
            }
        }

        Linhas = linhas;
        if (Cartao is not null && eu is { } pessoa)
        {
            Log = await consulta.ConsultarAsync(new RiConsultaFiltro(Cartao.Maquina.MachineId, DiaDoRegisto, DiaDoRegisto, pessoa), pagina, cancelar);
        }

        return Page();
    }

    // O Histórico desta pessoa: a linha, os dias, a referência e a produção
    // (em dois passos, como na Consulta). Abre vazio: só lê com um filtro.
    private async Task HistoricoAsync(Guid? eu, int? pagina, CancellationToken cancelar)
    {
        if (ReferenciaFiltro is { } referencia)
        {
            Producoes = await producoes.ListByReferenceAsync(referencia, cancelar);
            if (ProducaoPedida is { } pedida && Producoes.All(p => p.JobOnId != pedida)
                && await producoes.FindAsync(pedida, cancelar) is { } guardada)
            {
                Producoes = [.. Producoes, guardada];
            }
        }

        var producao = Producoes.Any(p => p.JobOnId == ProducaoPedida) ? ProducaoPedida : null;
        ComFiltro = Maquina is not null || De is not null || Ate is not null || ReferenciaFiltro is not null || producao is not null;
        if (!ComFiltro || eu is not { } pessoa)
        {
            return;
        }

        Historia = await consulta.ConsultarAsync(new RiConsultaFiltro(Maquina?.MachineId, De, Ate, pessoa, null, ReferenciaFiltro, producao), pagina, cancelar);
        HistoriaPager = ListPager.For(Historia);
    }

    /// <summary>O endereço de uma linha no painel: abre o registo dela, no mesmo dia.</summary>
    public string HrefDaLinha(string codigo) =>
        Url.Page("/ReparacaoInterna/Index", new { maquina = codigo, dia = DiaPedido is { } d ? Data(d) : null })!;

    /// <summary>A mesma linha no dia de agora (o «Voltar a hoje» do aviso).</summary>
    public string HrefDaLinhaHoje(string codigo) =>
        Url.Page("/ReparacaoInterna/Index", new { maquina = codigo, tipo = TipoTexto })!;

    public static string Data(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // As B na primeira fila, as C na segunda, as outras depois.
    public static int Fila(string codigo) => codigo.StartsWith('B') || codigo.StartsWith('b') ? 0 : codigo.StartsWith('C') || codigo.StartsWith('c') ? 1 : 2;

    /// <summary>A hora do registo, em hora local da fábrica: «08:42».</summary>
    public static string Hora(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Uma linha do painel e, aberta, o registo dela: a produção do dia do
/// registo, a escolhida, o tipo (CM por omissão) e se é a linha do pedido
/// (o foco, com o que se escreveu).
/// </summary>
public sealed record RiCartao(
    MachineOption Maquina,
    RiProducaoDoDia DoDia,
    RiProducao? Producao,
    ReparacaoTipo Tipo,
    bool Foco)
{
    public bool SemProducao => DoDia.SemProducao;

    /// <summary>No painel: a referência e o nº da produção, ou «Sem produção».</summary>
    public string Detalhe => Producao is { } p ? $"{p.Referencia} · {p.Producao}" : "Sem produção";

    /// <summary>No painel: a CM e o MF, cada um com o lote.</summary>
    public string? Ferramentas => Producao is { } p
        ? $"{Uma(ReparacaoTipo.Cm, p.Cm)} · {Uma(ReparacaoTipo.Mf, p.Mf)}"
        : null;

    private static string Uma(ReparacaoTipo tipo, RiFerramenta? f) => f switch
    {
        null => $"Sem {RiTextos.Tipo(tipo)}",
        { Lote: null or "" } => RiTextos.Tipo(tipo),
        _ => $"{RiTextos.Tipo(tipo)} lote {f.Lote}"
    };
}
