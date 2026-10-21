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
/// Registar na máquina (blueprint reparacao-interna/OVERVIEW §1): escolher a
/// máquina; a produção do dia vem sozinha (como nas Boquilhas, com a hora de
/// viragem da Reparação Interna); escolher CM ou MF; escrever o número e
/// Enter. O campo fica vazio e com o cursor para o número seguinte, e a
/// reparação aparece na tabela por baixo. Com duas ou mais produções para o
/// dia a página avisa e a pessoa escolhe (a de maior <c>jobon_id</c> vem
/// escolhida). A CM ou o MF na máquina pode não ser a do Job On (trocada
/// antes de a troca chegar lá): «É outra CM/MF» mostra a referência e o lote
/// a escrever, e a reparação grava-se sem o contexto, por associar (peça 4,
/// Diogo, 21/10/2026). Sem produção para o dia, a página avisa e grava-se
/// sempre assim, sem Job On; a tabela mostra as dessa máquina no dia. Na tabela,
/// clicar no número corrige-o e o «×» anula a reparação (peça 2), cada um
/// numa janela pequena com o motivo opcional; quem tem o módulo pode fazê-lo
/// (RI-04) e fica o rasto. Gravar troca só a tabela, com a mensagem no canto.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ReparacaoInterna)]
public sealed class IndexModel(IReparacoesInternas reparacoes, IMachineListQuery maquinas, CurrentUserCapabilities capabilities, ShellStateFactory shellState) : PageModel
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

    public PagedList<RiLinha> Lista { get; private set; } = PagedList<RiLinha>.Empty;

    public ListPager Pager { get; private set; } = new(1, 1, 0);

    /// <summary>O identificador do envio que o formulário leva (envio único).</summary>
    public Guid OperacaoId { get; private set; } = Guid.NewGuid();

    /// <summary>O que gravou, pelo redirect (PRG): ok ou repetido.</summary>
    public string? Gravacao { get; private set; }

    /// <summary>O número de que fala a mensagem.</summary>
    public string? Numero { get; private set; }

    public ReparacaoResultado? Recusa { get; private set; }

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
            "motivo-longo" => $"O motivo tem até {ReparacaoRasto.MotivoMaxLength} caracteres. Nada mudou.",
            _ => string.Empty
        },
        _ => Recusa switch
        {
            ReparacaoResultado.SemNumero => "Escreva o número reparado.",
            ReparacaoResultado.NumeroLongo => $"O número tem até {Reparacao.NumeroMaxLength} caracteres.",
            ReparacaoResultado.SemTipo => "Escolha CM ou MF.",
            ReparacaoResultado.MaquinaNaoExiste => "Escolha a máquina.",
            ReparacaoResultado.SemReferencia => $"Escreva a referência da {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.ReferenciaLonga => $"A referência tem até {Reparacao.ReferenciaMaxLength} caracteres.",
            ReparacaoResultado.SemLote => $"Escreva o lote da {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.LoteLongo => $"O lote tem até {Reparacao.LoteMaxLength} caracteres.",
            ReparacaoResultado.ProducaoMudou => "A produção do dia desta máquina mudou. Confira e registe outra vez.",
            ReparacaoResultado.SemFerramenta => $"Esta produção não tem {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.FerramentaMudou => $"A {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)} desta produção mudou no Job On. Confira e registe outra vez.",
            _ => string.Empty
        }
    };

    /// <summary>«Editar» no cartão: abre as reparações da máquina para corrigir (o número) ou anular (o «×»).</summary>
    [BindProperty(SupportsGet = true, Name = "editar")]
    public bool Editar { get; set; }

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
        [FromForm(Name = "pagina")] int? pagina)
    {
        var saida = await reparacoes.CorrigirAsync(
            new PedidoDeCorrecao(operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId, reparacaoId, numero, motivo),
            HttpContext.RequestAborted);
        return DepoisDaCorrecao(saida, CorrecaoResultado.Corrigido, pagina);
    }

    /// <summary>POST: anular uma reparação (o «×» e a confirmação). Grava uma vez por envio.</summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostAnularAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "reparacao_id")] int reparacaoId,
        [FromForm(Name = "motivo")] string? motivo,
        [FromForm(Name = "pagina")] int? pagina)
    {
        var saida = await reparacoes.AnularAsync(
            new PedidoDeAnulacao(operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId, reparacaoId, motivo),
            HttpContext.RequestAborted);
        return DepoisDaCorrecao(saida, CorrecaoResultado.Anulado, pagina);
    }

    // Volta à mesma máquina, produção e página, com a mensagem (PRG). O mesmo
    // envio outra vez mostra o que o primeiro mostrou.
    private RedirectToPageResult DepoisDaCorrecao(CorrecaoSaida saida, CorrecaoResultado feito, int? pagina) =>
        RedirectToPage("/ReparacaoInterna/Index", new
        {
            maquina = MaquinaCodigo,
            producao = ProducaoPedida,
            tipo = TipoTexto,
            referencia = ReferenciaEscrita,
            lote = LoteEscrito,
            pagina,
            correcao = (saida.Resultado == CorrecaoResultado.JaFeito ? feito : saida.Resultado) switch
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
            },
            numero = saida.Numero
        });

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
        // O cartão leva o contexto da CM e o do MF; o botão CM/MF escolhe.
        contexto ??= Tipo == ReparacaoTipo.Mf ? contextoMf : Tipo == ReparacaoTipo.Cm ? contextoCm : null;
        var saida = await reparacoes.RegistarAsync(
            new PedidoDeReparacao(operacao, maquina?.MachineId, ProducaoPedida, Tipo, contexto, numero, escrita,
                escrita ? ReferenciaEscrita : null, escrita ? LoteEscrito : null),
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
        ViewData["Title"] = "Reparação Interna";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("reparacao-interna", "Reparação Interna"))) with
        {
            ActiveFunctionKey = "registar",
            Functions = ShellStateFactory.ReparacaoInternaFunctions("registar")
        };

        // Todas as máquinas, cada uma no seu cartão (Diogo, 21/10/2026): as
        // B em cima, as C em baixo, as outras depois, pelo código. O cartão
        // da máquina do pedido (a do último registo ou da recusa) leva a
        // produção, o tipo, a página e o que se escreveu; os outros abrem
        // com a produção de maior jobon_id e a CM.
        var cancelar = HttpContext.RequestAborted;
        Maquinas = await maquinas.ListAsync(cancelar);
        Maquina = Maquinas.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        AbreFerramentas = (await capabilities.GetAsync(cancelar)).Contains(DmoCapabilities.Ferramentas, StringComparer.Ordinal);
        var cartoes = new List<RiCartao>();
        foreach (var m in Maquinas.OrderBy(m => Fila(m.Code)).ThenBy(m => m.Code, StringComparer.OrdinalIgnoreCase))
        {
            var foco = m.MachineId == Maquina?.MachineId;
            var doDia = await reparacoes.ProducaoDoDiaAsync(m.MachineId, cancelar);
            var producao = doDia.Escolhida(foco ? ProducaoPedida : null);
            var paginaDoCartao = foco ? pagina : null;
            var lista = producao is not null
                ? await reparacoes.ListarAsync(m.MachineId, producao.JobOnId, paginaDoCartao, cancelar)
                : doDia.SemProducao
                    ? await reparacoes.ListarSemProducaoAsync(m.MachineId, doDia.Dia, paginaDoCartao, cancelar)
                    : PagedList<RiLinha>.Empty;
            var ultima = lista.PageNumber <= 1 ? lista.Items.FirstOrDefault()
                : producao is not null ? (await reparacoes.ListarAsync(m.MachineId, producao.JobOnId, 1, cancelar)).Items.FirstOrDefault()
                : (await reparacoes.ListarSemProducaoAsync(m.MachineId, doDia.Dia, 1, cancelar)).Items.FirstOrDefault();
            cartoes.Add(new RiCartao(m, doDia, producao, foco ? Tipo ?? ReparacaoTipo.Cm : ReparacaoTipo.Cm, foco, lista, ultima, foco && (pagina is > 1 || Editar || Correcao is not null)));
            if (foco)
            {
                DoDia = doDia;
                Producao = producao;
                Lista = lista;
                Pager = ListPager.For(lista);
            }
        }

        Cartoes = cartoes;
        return Page();
    }

    // As B na primeira fila, as C na segunda, as outras depois.
    public static int Fila(string codigo) => codigo.StartsWith('B') || codigo.StartsWith('b') ? 0 : codigo.StartsWith('C') || codigo.StartsWith('c') ? 1 : 2;

    /// <summary>Os cartões das máquinas, pela ordem da página.</summary>
    public IReadOnlyList<RiCartao> Cartoes { get; private set; } = [];

    /// <summary>A hora do registo, em hora local da fábrica: «21/10 08:42».</summary>
    public static string Hora(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// O cartão de uma máquina: a produção do dia, o tipo escolhido (CM por
/// omissão), se é o cartão do pedido (o foco, com o que se escreveu), as
/// reparações e a última delas, e se a tabela abre (numa página além da 1).
/// </summary>
public sealed record RiCartao(
    MachineOption Maquina,
    RiProducaoDoDia DoDia,
    RiProducao? Producao,
    ReparacaoTipo Tipo,
    bool Foco,
    PagedList<RiLinha> Lista,
    RiLinha? Ultima,
    bool Aberta)
{
    public bool SemProducao => DoDia.SemProducao;
}
