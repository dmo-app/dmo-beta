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
/// escolhida); sem nenhuma não há a que ligar e nada se grava. Na tabela,
/// clicar no número corrige-o e o «×» anula a reparação (peça 2), cada um
/// numa janela pequena com o motivo opcional; quem tem o módulo pode fazê-lo
/// (RI-04) e fica o rasto. Gravar troca só a tabela, com a mensagem no canto.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ReparacaoInterna)]
public sealed class IndexModel(IReparacoesInternas reparacoes, IMachineListQuery maquinas, ShellStateFactory shellState) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "maquina")]
    public string? MaquinaCodigo { get; set; }

    [BindProperty(SupportsGet = true, Name = "producao")]
    public int? ProducaoPedida { get; set; }

    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

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

    /// <summary>Corrigir para o mesmo número: nada mudou, só avisa.</summary>
    public bool Avisou => Correcao is "igual";

    public string Mensagem => Gravacao switch
    {
        "ok" => $"Reparação do nº {Numero} registada.",
        "repetido" => $"A reparação do nº {Numero} já estava registada.",
        _ when Correcao is not null => Correcao switch
        {
            "corrigido" => $"Número corrigido para {Numero}.",
            "anulado" => $"Reparação do nº {Numero} anulada.",
            "igual" => $"O número já era {Numero}. Nada mudou.",
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
            ReparacaoResultado.SemProducao => "Não há produção desta máquina para hoje: não há a que ligar a reparação.",
            ReparacaoResultado.ProducaoMudou => "A produção do dia desta máquina mudou. Confira e registe outra vez.",
            ReparacaoResultado.SemFerramenta => $"Esta produção não tem {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)}.",
            ReparacaoResultado.FerramentaMudou => $"A {RiTextos.Tipo(Tipo ?? ReparacaoTipo.Cm)} desta produção mudou no Job On. Confira e registe outra vez.",
            _ => string.Empty
        }
    };

    public async Task<IActionResult> OnGetAsync(string? gravacao, string? correcao, string? numero, int? pagina)
    {
        Gravacao = gravacao;
        Correcao = correcao;
        Numero = numero;
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

    /// <summary>POST: registar o número escrito (Enter). Grava uma vez por envio.</summary>
    [EnvioUnico]
    public async Task<IActionResult> OnPostRegistarAsync(
        [FromForm(Name = "operacao_id")] Guid operacaoId,
        [FromForm(Name = "contexto")] int? contexto,
        [FromForm(Name = "numero")] string? numero)
    {
        var lista = await maquinas.ListAsync(HttpContext.RequestAborted);
        var maquina = lista.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        var operacao = operacaoId == Guid.Empty ? Guid.NewGuid() : operacaoId;
        var saida = await reparacoes.RegistarAsync(
            new PedidoDeReparacao(operacao, maquina?.MachineId, ProducaoPedida, Tipo, contexto, numero),
            HttpContext.RequestAborted);
        if (saida.Gravou)
        {
            return RedirectToPage("/ReparacaoInterna/Index", new
            {
                maquina = maquina?.Code,
                producao = ProducaoPedida,
                tipo = TipoTexto,
                gravacao = saida.Resultado == ReparacaoResultado.JaRegistado ? "repetido" : "ok",
                numero = saida.Numero
            });
        }

        // Nada ficou gravado com este identificador: o mesmo envio continua,
        // com o número escrito no campo.
        Recusa = saida.Resultado;
        NumeroEscrito = numero;
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

        Maquinas = await maquinas.ListAsync(HttpContext.RequestAborted);
        Maquina = Maquinas.FirstOrDefault(m => string.Equals(m.Code, MaquinaCodigo, StringComparison.OrdinalIgnoreCase));
        if (Maquina is null)
        {
            return Page();
        }

        DoDia = await reparacoes.ProducaoDoDiaAsync(Maquina.MachineId, HttpContext.RequestAborted);
        Producao = DoDia.Escolhida(ProducaoPedida);
        if (Producao is not null)
        {
            Lista = await reparacoes.ListarAsync(Maquina.MachineId, Producao.JobOnId, pagina, HttpContext.RequestAborted);
            Pager = ListPager.For(Lista);
        }

        return Page();
    }

    /// <summary>A hora do registo, em hora local da fábrica: «21/10 08:42».</summary>
    public static string Hora(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
