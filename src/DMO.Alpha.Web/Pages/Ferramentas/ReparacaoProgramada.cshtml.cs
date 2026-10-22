using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// A REPARAÇÃO PROGRAMADA, separador das Ferramentas (Diogo, 22/10/2026: «o
/// separador tem filtros para procurar a ferramenta e depois um modo para
/// associar»). À esquerda procura-se (abre vazia) e associa-se a uma lista
/// de um reparador (nova ou ativa); à direita a lista, que o Armazém vai
/// atualizando: a Saída mostra «Saída dd/mm», a Entrada pinta a linha de
/// cinza com «Entrada · Posição · nome», e a lista fecha quando a última
/// volta. «Imprimir» abre a vista para o browser imprimir (nada de PDF no
/// servidor). Só quem tem as Ferramentas entra.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.Ferramentas)]
public sealed class ReparacaoProgramadaModel(IReparacaoProgramada programada, ShellStateFactory shellState) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "tipo")]
    public string? TipoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "referencia")]
    public string? Referencia { get; set; }

    [BindProperty(SupportsGet = true, Name = "lote")]
    public string? Lote { get; set; }

    [BindProperty(SupportsGet = true, Name = "linha")]
    public string? Linha { get; set; }

    [BindProperty(SupportsGet = true, Name = "estado")]
    public string? EstadoTexto { get; set; }

    [BindProperty(SupportsGet = true, Name = "lista")]
    public int? ListaId { get; set; }

    [BindProperty(SupportsGet = true, Name = "vista")]
    public string? Vista { get; set; }

    /// <summary>O Histórico das listas criadas: filtra pelo reparador (nome) e pelo dia de criação.</summary>
    [BindProperty(SupportsGet = true, Name = "hist_reparador")]
    public string? HistReparador { get; set; }

    [BindProperty(SupportsGet = true, Name = "hist_desde")]
    public DateOnly? HistDesde { get; set; }

    [BindProperty(SupportsGet = true, Name = "hist_ate")]
    public DateOnly? HistAte { get; set; }

    /// <summary>Os históricos abrem vazios: só com um filtro.</summary>
    public bool HistoricoPedido => !string.IsNullOrWhiteSpace(HistReparador) || HistDesde is not null || HistAte is not null;

    /// <summary>As listas criadas (ativas e fechadas) que batem com os filtros do Histórico.</summary>
    public IReadOnlyList<ProgramadaListaResumo> Historico { get; private set; } = [];

    /// <summary>Os reparadores que já tiveram listas (para o filtro do Histórico).</summary>
    public IReadOnlyList<string> HistReparadores { get; private set; } = [];

    public IReadOnlyList<ProgramadaCandidata> Candidatas { get; private set; } = [];

    public IReadOnlyList<ProgramadaListaResumo> Ativas { get; private set; } = [];

    /// <summary>As fechadas só carregam quando se pedem (o histórico abre vazio).</summary>
    public IReadOnlyList<ProgramadaListaResumo> Fechadas { get; private set; } = [];

    public ProgramadaListaDetalhe? Lista { get; private set; }

    public IReadOnlyList<ArmazemReparadorOpcao> Reparadores { get; private set; } = [];

    public IReadOnlyList<string> Linhas { get; private set; } = [];

    public IReadOnlyList<string> Avisos { get; private set; } = [];

    public string? Mensagem { get; private set; }

    public string? Recusa { get; private set; }

    public bool VerFechadas => Vista == "fechadas";

    public ToolType? Tipo => TipoTexto switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        _ => null
    };

    public FerramentaEstadoTipo? Estado => Enum.TryParse<FerramentaEstadoTipo>(EstadoTexto, out var e) && Enum.IsDefined(e) ? e : null;

    public ProgramadaProcura Procura => new(Tipo, Referencia, Lote, Linha, Estado);

    public static readonly IReadOnlyList<FerramentaEstadoTipo> Estados =
        [FerramentaEstadoTipo.PorReparar, FerramentaEstadoTipo.Reparado, FerramentaEstadoTipo.Novo, FerramentaEstadoTipo.Sucatado];

    public async Task<IActionResult> OnGetAsync(int? associadas, string[]? aviso, string? recusa)
    {
        var ct = HttpContext.RequestAborted;
        ViewData["Title"] = "Reparação Programada — Ferramentas";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("ferramentas", "Ferramentas"))) with
        {
            ActiveFunctionKey = "programada",
            Functions = ShellStateFactory.FerramentasFunctions("programada")
        };

        Reparadores = await programada.ReparadoresAsync(ct);
        Linhas = await programada.LinhasAsync(ct);
        Candidatas = await programada.ProcurarAsync(Procura, ct);
        Ativas = await programada.ListasAsync(false, ct);
        // As fechadas são o histórico: abrem vazias e só com o reparador ou
        // as datas mostram as que batem (Diogo, 22/10/2026).
        var todasFechadas = VerFechadas ? await programada.ListasAsync(true, ct) : [];
        HistReparadores = Reparadores.Select(r => r.Nome)
            .Concat(todasFechadas.Select(a => a.Reparador))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (VerFechadas && HistoricoPedido)
        {
            Fechadas = todasFechadas
                .Where(a => string.IsNullOrWhiteSpace(HistReparador) || string.Equals(a.Reparador, HistReparador, StringComparison.OrdinalIgnoreCase))
                .Where(a => HistDesde is not { } desde || DateOnly.FromDateTime(DmoBusinessTime.ToLocal(a.CriadaEmUtc)) >= desde)
                .Where(a => HistAte is not { } ate || DateOnly.FromDateTime(DmoBusinessTime.ToLocal(a.CriadaEmUtc)) <= ate)
                .OrderByDescending(a => a.CriadaEmUtc)
                .ThenByDescending(a => a.Id)
                .ToList();
        }

        Historico = Fechadas;
        var escolhida = ListaId ?? (VerFechadas ? Fechadas.FirstOrDefault()?.Id : Ativas.FirstOrDefault()?.Id);
        if (escolhida is { } id)
        {
            Lista = await programada.ListaAsync(id, ct);
        }

        Avisos = aviso ?? [];
        Mensagem = associadas is > 0 ? (associadas == 1 ? "1 ferramenta associada." : $"{associadas} ferramentas associadas.") : null;
        Recusa = recusa switch
        {
            nameof(ProgramadaAssociacao.SemFerramentas) => "Marque as ferramentas a associar.",
            nameof(ProgramadaAssociacao.SemReparador) => "Escolha o reparador da lista nova.",
            nameof(ProgramadaAssociacao.ReparadorInvalido) => "Esse reparador já não está ativo. Escolha outro.",
            nameof(ProgramadaAssociacao.ListaNaoExiste) => "Essa lista já não existe.",
            nameof(ProgramadaAssociacao.ListaFechada) => "Essa lista já fechou: todas as ferramentas deram entrada. Associe a uma lista nova.",
            _ => null
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAssociarAsync(string[]? ferramenta, string? destino, int? reparador)
    {
        int? listaDestino = int.TryParse(destino, NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? l : null;
        var saida = await programada.AssociarAsync(ferramenta ?? [], listaDestino, reparador, HttpContext.RequestAborted);
        return RedirectToPage("/Ferramentas/ReparacaoProgramada", new
        {
            tipo = TipoTexto,
            referencia = Referencia,
            lote = Lote,
            linha = Linha,
            estado = EstadoTexto,
            lista = saida.ListaId ?? listaDestino,
            associadas = saida.Associou ? saida.Associadas : (int?)null,
            recusa = saida.Associou ? null : saida.Resultado.ToString(),
            aviso = saida.Avisos.ToArray()
        });
    }

    public static string Ferramenta(ToolType tipo, string referencia, string lote) => $"{ArmazemTextos.Tipo(tipo)} {referencia} · lote {lote}";

    public static string Uso(decimal? uso) => uso is { } u ? u.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";

    public static string Dia(DateTime utc) => DmoBusinessTime.ToLocal(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DiaCurto(DateTime utc) => DmoBusinessTime.ToLocal(utc).ToString("dd/MM", CultureInfo.InvariantCulture);

    /// <summary>O que aconteceu à ferramenta na lista: por retirar, «Saída dd/mm» ou «Entrada dd/mm/aaaa · Posição 4152 · nome».</summary>
    public static string Progresso(ProgramadaLinha linha)
    {
        if (linha.EntradaEmUtc is { } entrada)
        {
            var partes = new List<string> { $"Entrada {Dia(entrada)}" };
            if (linha.EntradaPosicao is { } posicao)
            {
                partes.Add($"Posição {posicao}");
            }

            if (!string.IsNullOrWhiteSpace(linha.EntradaPor))
            {
                partes.Add(linha.EntradaPor);
            }

            return string.Join(" · ", partes);
        }

        return linha.SaidaEmUtc is { } saida ? $"Saída {DiaCurto(saida)}" : "Por retirar";
    }

    public static string Titulo(ProgramadaListaResumo lista) =>
        $"{lista.Reparador} · {Dia(lista.CriadaEmUtc)}" + (lista.Ativa ? string.Empty : " · fechada");
}
