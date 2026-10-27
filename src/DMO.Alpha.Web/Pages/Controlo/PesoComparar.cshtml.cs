using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O «Comparar peso» (Diogo, 27/10/2026): no Histórico e na página de cada
/// Peso, um botão abre uma janela ao lado com as CM compatíveis (a mesma
/// referência e a mesma máquina), o Peso mais recente de cada CM, e deixa
/// trocar o tampão de cada uma para ver quanto muda o peso («alterar o tampão
/// usando o cálculo do volume do tampão altera o peso»). Com dois Pesos
/// marcados, «Comparar leituras» compara CM a CM. Nada é gravado.
/// A janela é o painel lateral da casa (dmo-support.js): esta página abre-se
/// lá dentro; sem JavaScript, abre-se inteira.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PesoCompararModel(
    IPesoCompararQuery compararQuery,
    PesoComparisonQuery comparacaoQuery,
    ShellStateFactory shellState) : PageModel
{
    public PesoCompararCms Cms { get; private set; } = new(null, null, null, [], [], []);

    /// <summary>O texto escrito na referência (o do Histórico), para o campo.</summary>
    public string? CmEscrita { get; private set; }

    /// <summary>O tampão escrito para todas.</summary>
    public string? TampaoTodas { get; private set; }

    public IReadOnlyList<Linha> Linhas { get; private set; } = [];

    public Resumo? Medias { get; private set; }

    public PesoComparacaoGuardadosView? Leituras { get; private set; }

    /// <summary>«Comparar leituras» pedido sem serem dois Pesos marcados.</summary>
    public bool LeiturasSemDois { get; private set; }

    /// <summary>De onde se veio: o Peso (a sua página) ou o Histórico.</summary>
    public string VoltarHref => Cms.PesoBaseId is int pesoId
        ? $"/Controlo/Peso?peso_id={pesoId}"
        : $"/Controlo/Peso?historico=1{(CmEscrita is { } cm ? $"&referencia={Uri.EscapeDataString(cm)}" : null)}";

    public sealed record Linha(
        PesoCompararLinha Peso,
        bool Marcada,
        string? TampaoEscrito,
        bool TampaoIlegivel,
        PesoTampaoSimulado? Simulado);

    public sealed record Resumo(int Cms, decimal PesoMedioGrams, decimal PesoSimuladoGrams, decimal DiferencaGrams, bool ComSimulacao);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "peso_id")] int? pesoId,
        [FromQuery(Name = "cm")] string? cm,
        [FromQuery(Name = "maquina")] string? maquina,
        [FromQuery(Name = "escolha")] string? escolha,
        [FromQuery(Name = "p")] int[]? marcados,
        [FromQuery(Name = "tp")] string? tampaoTodas,
        [FromQuery(Name = "leituras")] string? leituras)
    {
        CmEscrita = Limpo(cm);
        TampaoTodas = Limpo(tampaoTodas);
        Cms = await compararQuery.ListarAsync(new PesoCompararPedido(pesoId, CmEscrita, Limpo(maquina)), HttpContext.RequestAborted);

        // Ao abrir vêm todas marcadas; depois, as que o operador deixou marcadas.
        var escolhidos = escolha == "1" ? new HashSet<int>(marcados ?? []) : null;
        var linhas = new List<Linha>(Cms.Linhas.Count);
        foreach (var peso in Cms.Linhas)
        {
            var escrito = Limpo(Request.Query[$"tp_{peso.PesoId}"].ToString()) ?? TampaoTodas;
            PesoTampaoSimulado? simulado = null;
            var ilegivel = false;
            if (escrito is not null)
            {
                var lido = PesoTampao.Ler(escrito, peso.Tampao?.DiametroMm);
                ilegivel = lido is null;
                if (lido is not null && peso.Tampao is { } atual && peso.PesoMedioGrams is { } medio)
                {
                    simulado = PesoSuposicaoCalculation.Tampao(atual, lido, peso.GlassDensityGramsPerCm3, medio);
                }
            }

            linhas.Add(new Linha(peso, escolhidos?.Contains(peso.PesoId) ?? true, Limpo(Request.Query[$"tp_{peso.PesoId}"].ToString()), ilegivel, simulado));
        }

        Linhas = linhas;
        var comPeso = linhas.Where(l => l.Marcada && l.Peso.PesoMedioGrams is not null).ToList();
        if (comPeso.Count > 0)
        {
            var medio = comPeso.Average(l => l.Peso.PesoMedioGrams!.Value);
            var simuladoMedio = comPeso.Average(l => l.Simulado?.PesoSimuladoGrams ?? l.Peso.PesoMedioGrams!.Value);
            Medias = new Resumo(
                comPeso.Count,
                Arredondar(medio),
                Arredondar(simuladoMedio),
                Arredondar(simuladoMedio - medio),
                comPeso.Any(l => l.Simulado is not null));
        }

        if (leituras == "1")
        {
            var dois = linhas.Where(l => l.Marcada).Select(l => l.Peso.PesoId).ToList();
            if (dois.Count == 2)
            {
                Leituras = await comparacaoQuery.CompararGuardadosAsync(dois[0], dois[1], HttpContext.RequestAborted);
            }
            else
            {
                LeiturasSemDois = true;
            }
        }

        var historico = Cms.PesoBaseId is null;
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = historico ? "historico" : "peso",
            Functions = ShellStateFactory.ControloFunctions(historico ? "historico" : "peso")
        };

        return Page();
    }

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static string? Limpo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().Length > 60 ? texto.Trim()[..60] : texto.Trim();
}
