using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O «Comparar peso» (Diogo, 27/10/2026): no Histórico e na página de cada
/// Peso, um botão abre ao lado os Pesos compatíveis (a mesma referência da CM
/// e a mesma máquina); um clique na linha escolhe o Peso com que comparar e
/// aparece a tabela da comparação igual à do Peso: CM com CM, o volume, o
/// peso e as diferenças. Em cada lado pode-se mudar só a calote do tampão
/// («altera-se apenas a calote e depois faz o cálculo com a forma já
/// implementada»: <see cref="PesoSuposicaoCalculation.Calote"/>). Nada é
/// gravado. A janela é o painel lateral da casa (dmo-support.js); sem
/// JavaScript, a página abre-se inteira.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PesoCompararModel(
    IPesoCompararQuery compararQuery,
    IPesoQuery pesoQuery,
    ShellStateFactory shellState) : PageModel
{
    public PesoCompararCms Cms { get; private set; } = new(null, null, null, [], [], []);

    /// <summary>O texto escrito na referência (o do Histórico), para o campo.</summary>
    public string? CmEscrita { get; private set; }

    /// <summary>O Peso de onde se parte (no Histórico, o escolhido no campo «Peso»).</summary>
    public Lado? Atual { get; private set; }

    /// <summary>O Peso escolhido na lista para comparar.</summary>
    public Lado? Anterior { get; private set; }

    /// <summary>As linhas da tabela, CM com CM pela posição da leitura, como na tabela do Peso.</summary>
    public IReadOnlyList<LinhaTabela> Tabela { get; private set; } = [];

    /// <summary>De onde se veio: o Peso (a sua página) ou o Histórico.</summary>
    public string VoltarHref => Cms.PesoBaseId is int pesoId
        ? $"/Controlo/Peso?peso_id={pesoId}"
        : $"/Controlo/Peso?historico=1{(CmEscrita is { } cm ? $"&referencia={Uri.EscapeDataString(cm)}" : null)}";

    /// <summary>
    /// Um lado da comparação: o Peso, a calote escrita e, com ela, a mudança
    /// (ΔV e Δ peso, a mesma para todas as CM desse Peso).
    /// </summary>
    public sealed record Lado(
        PesoCompararLinha Peso,
        IReadOnlyList<PesoMeasurementReadModel> Leituras,
        PesoAveragesReadModel? Medias,
        string? CaloteEscrita,
        bool CaloteIlegivel,
        PesoSuposicaoCalote? Simulada)
    {
        public decimal DeltaVolumeCm3 => Simulada?.DeltaVolumeCm3 ?? 0m;
        public decimal DeltaPesoGrams => Simulada?.DeltaPesoGrams ?? 0m;
        public decimal? VolumeMedioCm3 => Medias is { } m ? m.AverageCapacityCm3 + DeltaVolumeCm3 : null;
        public decimal? PesoMedioGrams => Medias is { } m ? m.AverageGlassWeightGrams + DeltaPesoGrams : null;
    }

    public sealed record LinhaTabela(
        string? CmAtual,
        decimal? VolumeAtual,
        decimal? PesoAtual,
        string? CmAnterior,
        decimal? VolumeAnterior,
        decimal? PesoAnterior);

    /// <summary>Os campos do pedido, para os links das linhas levarem o resto como está.</summary>
    private Dictionary<string, string?> _pedido = [];

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "peso_id")] int? pesoId,
        [FromQuery(Name = "cm")] string? cm,
        [FromQuery(Name = "maquina")] string? maquina,
        [FromQuery(Name = "atual")] int? atualId,
        [FromQuery(Name = "com")] int? comId,
        [FromQuery(Name = "calote_atual")] string? caloteAtual,
        [FromQuery(Name = "calote_com")] string? caloteCom)
    {
        CmEscrita = Limpo(cm);
        Cms = await compararQuery.ListarAsync(new PesoCompararPedido(pesoId, CmEscrita, Limpo(maquina)), HttpContext.RequestAborted);

        // O atual é o Peso de onde se parte (no Histórico, o escolhido; senão o
        // mais recente); o anterior é o escolhido na lista, senão o seguinte.
        var atual = Cms.Linhas.FirstOrDefault(l => l.PesoId == (Cms.PesoBaseId ?? atualId)) ?? Cms.Linhas.FirstOrDefault();
        var anterior = Cms.Linhas.FirstOrDefault(l => l.PesoId == comId && l.PesoId != atual?.PesoId)
            ?? Cms.Linhas.FirstOrDefault(l => l.PesoId != atual?.PesoId);
        Atual = atual is null ? null : await LadoAsync(atual, Limpo(caloteAtual));
        Anterior = anterior is null ? null : await LadoAsync(anterior, Limpo(caloteCom));
        _pedido = new()
        {
            ["peso_id"] = Cms.PesoBaseId?.ToString(),
            ["cm"] = Cms.PesoBaseId is null ? Cms.CmReferencia ?? CmEscrita : null,
            ["maquina"] = Cms.Maquina,
            ["atual"] = Cms.PesoBaseId is null ? atual?.PesoId.ToString() : null,
            ["calote_atual"] = Atual?.CaloteEscrita,
            ["calote_com"] = Anterior?.CaloteEscrita
        };

        if (Atual is not null && Anterior is not null)
        {
            // Como na tabela do Peso: as leituras do atual pela ordem, e a do
            // anterior na mesma posição; as do anterior sem par no fim.
            var linhas = new List<LinhaTabela>();
            foreach (var leitura in Atual.Leituras.OrderBy(l => l.RowPosition))
            {
                var par = Anterior.Leituras.FirstOrDefault(l => l.RowPosition == leitura.RowPosition);
                linhas.Add(new LinhaTabela(
                    leitura.CmSubject,
                    leitura.CapacityCm3 + Atual.DeltaVolumeCm3,
                    leitura.GlassWeightGrams + Atual.DeltaPesoGrams,
                    par?.CmSubject,
                    par is null ? null : par.CapacityCm3 + Anterior.DeltaVolumeCm3,
                    par is null ? null : par.GlassWeightGrams + Anterior.DeltaPesoGrams));
            }

            foreach (var sobra in Anterior.Leituras.Where(l => Atual.Leituras.All(a => a.RowPosition != l.RowPosition)).OrderBy(l => l.RowPosition))
            {
                linhas.Add(new LinhaTabela(null, null, null, sobra.CmSubject,
                    sobra.CapacityCm3 + Anterior.DeltaVolumeCm3, sobra.GlassWeightGrams + Anterior.DeltaPesoGrams));
            }

            Tabela = linhas;
        }

        var historico = Cms.PesoBaseId is null;
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = historico ? "historico" : "peso",
            Functions = ShellStateFactory.ControloFunctions(historico ? "historico" : "peso")
        };

        return Page();
    }

    /// <summary>O endereço desta página com outro Peso para comparar, o resto como está.</summary>
    public string HrefCom(int pesoId)
    {
        var partes = _pedido.Where(p => p.Value is not null).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}").ToList();
        partes.Add($"com={pesoId}");
        return "/Controlo/PesoComparar?" + string.Join("&", partes);
    }

    /// <summary>
    /// Só a calote muda; o diâmetro fica o do tampão com que se pesou, e o
    /// cálculo é o da «Calote Tampo» (Diogo, 27/10/2026).
    /// </summary>
    private async Task<Lado> LadoAsync(PesoCompararLinha peso, string? caloteEscrita)
    {
        var lido = await pesoQuery.FindPesoAsync(peso.PesoId, HttpContext.RequestAborted);
        PesoSuposicaoCalote? simulada = null;
        var ilegivel = false;
        if (caloteEscrita is not null)
        {
            var calote = MeasurementDecimal.Parse(caloteEscrita);
            ilegivel = calote is not >= 0m;
            if (!ilegivel && peso.Tampao is { } tampao && lido?.Averages is { } medias)
            {
                simulada = PesoSuposicaoCalculation.Calote(
                    tampao.DiametroMm, tampao.CaloteMm, calote!.Value, peso.GlassDensityGramsPerCm3, medias.AverageGlassWeightGrams, null);
            }
        }

        return new Lado(peso, lido?.Measurements ?? [], lido?.Averages, caloteEscrita, ilegivel, simulada);
    }

    private static string? Limpo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().Length > 60 ? texto.Trim()[..60] : texto.Trim();
}
