using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Controlo;

public sealed class PesoPdfGenerator
{
    static PesoPdfGenerator() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly CultureInfo Pt = DmoNumberFormat.Culture;

    public sealed record Input(
        string ProductionNumber,
        string Reference,
        string Machine,
        string CmToolId,
        string? CmReference,
        string? CmLot,
        string Process,
        int WaterTemperatureC,
        decimal WaterDensity,
        decimal GlassDensity,
        decimal VolumeMarisaCm3,
        decimal VolumePuncaoCm3,
        decimal? NominalWeightGrams,
        IReadOnlyList<Reading> Readings,
        PesoAveragesReadModel? Averages,
        PesoComparisonView? Comparison,
        string OutputFilePath,
        /// <summary>O selo de aprovação (o corte D): o operador e o momento; null = não aprovado.</summary>
        ControloApprovalReadModel? Approval = null,
        /// <summary>Quem gerou o papel (o rodapé do alvo: «Diogo Oliveira 28/07/2026, 14:52:28»).</summary>
        string? GeneratedBy = null,
        PesoComparisonView? PreviousPesoComparison = null,
        DateOnly? InicioEm = null, DateOnly? FimEm = null,
        /// <summary>O volume do tampão desta produção (varia com a calote); null = tampão não registado na CM.</summary>
        decimal? VolumeTampaoCm3 = null,
        decimal? CaloteMm = null,
        /// <summary>A produção anterior com o peso do SAP (o Owner 2026-10-17); null = por indicar.</summary>
        PesoProducaoAnteriorReadModel? ProducaoAnterior = null)
    {
        /// <summary>
        /// «Peso 2 de 2 · CM 5448 · Lote 2 · BQ T194 · Lote 7» numa produção
        /// com mais do que um Peso (a CM ou a BQ mudou a meio; 18/10/2026);
        /// null com um só, como sempre.
        /// </summary>
        public string? SheetLabel { get; init; }

        /// <summary>Média e moda do peso da produção anterior (relatório de produções); null = sem dados.</summary>
        public DMO.Alpha.Core.Tools.PesoSapResumo? PesoProducaoSap { get; init; }

        /// <summary>A secção deste Peso no PDF composto do Resumo (uma por Peso).</summary>
        public string SectionName { get; init; } = "peso";
    }

    /// <summary>
    /// O Peso numa produção com mais do que um: o número, o total e as
    /// ferramentas dele. Null com um só Peso (o papel fica como sempre).
    /// </summary>
    public static string? SheetLabelOf(PesoReadModel peso, IReadOnlyList<PesoReadModel> pesosDaProducao)
    {
        var numero = pesosDaProducao.ToList().FindIndex(p => p.PesoId == peso.PesoId) + 1;
        if (pesosDaProducao.Count < 2 || numero < 1)
        {
            return null;
        }

        return $"Peso {numero} de {pesosDaProducao.Count} · CM {peso.CmToolReference ?? "—"} · Lote {peso.CmToolLot ?? "—"}"
            + $" · BQ {peso.BqToolReference ?? "—"} · Lote {peso.BqToolLot ?? "—"}"
            + (peso.IsCurrent ? "" : " · de antes da troca");
    }

    public sealed record Reading(
        int Position,
        string CmSubject,
        decimal WaterWeightGrams,
        decimal CapacityCm3,
        decimal GlassWeightGrams);

    public byte[] GenerateBytes(Input input) =>
        Document.Create(container => Compose(container, input)).GeneratePdf();

    private static readonly byte[] Logo = LoadLogo();
    private static byte[] LoadLogo()
    {
        using var stream = typeof(PesoPdfGenerator).Assembly.GetManifestResourceStream("DMO.Pdf.Logo.png")
            ?? throw new InvalidOperationException("PDF logo resource is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private const string Ink = DmoPdfPalette.Ink;
    private const string Muted = DmoPdfPalette.Muted;
    private const string Line = DmoPdfPalette.Line;
    private const string Blue = DmoPdfPalette.Accent;

    public void Compose(IDocumentContainer container, Input input, bool combined = false)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(16, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(10).FontColor(Ink));
            page.Header().PaddingBottom(12).Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.ConstantItem(35).Height(35).Image(Logo);
                    row.RelativeItem().PaddingLeft(10).PaddingTop(3).Text("Controlo de Peso").FontSize(24).Bold();
                });
                header.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"{input.Reference}     ·     {input.ProductionNumber}     ·     {input.Machine}").FontSize(10);
                    row.ConstantItem(85).AlignRight().Text(DmoBusinessTime.Now.ToString("d", Pt)).FontColor(Muted);
                });
                if (input.SheetLabel is { } sheetLabel)
                {
                    header.Item().PaddingTop(3).Text(sheetLabel).FontSize(9).SemiBold().FontColor(Blue);
                }
                header.Item().PaddingTop(9).LineHorizontal(0.7f).LineColor(Line);
            });
            page.Content().Column(content =>
            {
                content.Item().Section(input.SectionName);
                content.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem(1.3f).Element(c => Box(c)).Padding(12).MinHeight(102).DefaultTextStyle(x => x.FontColor(DmoPdfPalette.Ink)).Column(c =>
                    {
                        c.Item().Text("Peso atual").FontSize(10).SemiBold();
                        c.Item().PaddingTop(12).Text(t =>
                        {
                            t.Span(Avg(input.Averages?.AverageGlassWeightGrams)).FontSize(40).SemiBold();
                            t.Span(" g").FontSize(20).SemiBold();
                        });
                    });
                    // O controlo anterior num só cartão, com a diferença ao lado
                    // (Diogo, 28/10/2026: sem rótulos repetidos um por cima do outro).
                    row.RelativeItem().Element(x => Box(x)).Padding(12).MinHeight(102).Column(card =>
                    {
                        card.Item().Text("Controlo anterior").FontSize(10).SemiBold();
                        if (input.Comparison?.HistoricalAverages is { } previous)
                        {
                            card.Item().PaddingTop(12).Text(t =>
                            {
                                t.Span(Fmt(previous.AverageGlassWeightGrams) + " g").FontSize(22).SemiBold();
                                if (input.Comparison.Deltas.GlassWeightDifferenceGrams is { } d)
                                    t.Span("   " + Signed(d) + " g").FontSize(14).SemiBold();
                                if (input.Comparison.Deltas.GlassWeightVariationPercent is { } variation)
                                    t.Span(" · " + Signed(variation) + " %").FontSize(10).FontColor(Muted);
                            });
                        }
                        else
                        {
                            card.Item().PaddingTop(12).Text("Sem comparação").FontSize(12).FontColor(Muted);
                        }
                    });
                });
                content.Item().PaddingTop(8).Row(row =>
                {
                    row.Spacing(7);
                    // A produção anterior: a média e a moda do peso dela (Diogo,
                    // 28/10/2026: «começarmos a usar os dois»); sem ele, o peso
                    // escrito à mão de antes; senão o segundo Peso escolhido.
                    var atual = input.Averages?.AverageGlassWeightGrams;
                    if (input.ProducaoAnterior is { } anteriorComPeso && input.PesoProducaoSap is { } pesoProducao)
                    {
                        var titulo = "Produção anterior" + (anteriorComPeso.ProductionNumber is { } np ? " · " + np : "");
                        var deltaMedia = PesoCalculation.NominalDeviation(atual, pesoProducao.MediaGramas);
                        PairedCard(row.RelativeItem(), titulo + " · Média", Fmt(pesoProducao.MediaGramas) + " g",
                            SignedOrDash(deltaMedia.Grams) + " g", SignedOrDash(deltaMedia.Percent) + " %");
                        // A moda mostra os dias em que esse peso se usou, não a diferença (Diogo, 29/10/2026).
                        PairedCard(row.RelativeItem(), titulo + " · Moda", Fmt(pesoProducao.ModaGramas) + " g",
                            "", "", pesoProducao.ModaDias + " de " + pesoProducao.Dias + " dias");
                    }
                    else if (input.ProducaoAnterior is { PesoGramas: { } pesoSap } anterior)
                    {
                        var delta = anterior.Delta(input.Averages?.AverageGlassWeightGrams);
                        PairedCard(row.RelativeItem(), "Produção anterior" + (anterior.ProductionNumber is { } n ? " · " + n : ""),
                            Fmt(pesoSap) + " g", SignedOrDash(delta.Grams) + " g", SignedOrDash(delta.Percent) + " %");
                    }
                    else
                    {
                        var productionComparison = input.PreviousPesoComparison is { } previousProduction
                            && !string.IsNullOrWhiteSpace(previousProduction.HistoricalProductionNumber) ? previousProduction : null;
                        PairedCard(row.RelativeItem(), "Produção anterior",
                            productionComparison?.HistoricalAverages is { } previous ? Fmt(previous.AverageGlassWeightGrams) + " g" : "Sem dados",
                            SignedOrDash(productionComparison?.Deltas.GlassWeightDifferenceGrams) + " g",
                            SignedOrDash(productionComparison?.Deltas.GlassWeightVariationPercent) + " %");
                    }
                    var deviation = PesoCalculation.NominalDeviation(input.Averages?.AverageGlassWeightGrams, input.NominalWeightGrams);
                    PairedCard(row.RelativeItem(), "Nominal do desenho", Avg(input.NominalWeightGrams) + " g",
                        SignedOrDash(deviation.Grams) + " g", SignedOrDash(deviation.Percent) + " %");
                });

                // As leituras e a comparação com o controlo anterior numa só
                // tabela (Diogo, 20/10/2026: «as tabelas do peso deveriam ser
                // as duas juntas»): a massa de água, o volume e o peso de cada
                // CM, ao lado da CM anterior e dos deltas, e a média no fim.
                SectionTitle(content.Item().PaddingTop(13), input.Comparison is null ? "Leituras por CM" : "Leituras por CM e comparação com o controlo anterior");
                content.Item().PaddingTop(6).Element(c => Box(c)).Element(c => ComposeWeightTable(c, input, input.Comparison));
                ComposeTechnicalContext(content.Item().PaddingTop(13), input);
            });
            page.Footer().PaddingTop(8).BorderTop(.6f).BorderColor(Line).PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(input.GeneratedBy is { } by ? "Gerado por " + by : "Gerado por: sem identificação").FontSize(7).FontColor(Muted);
                if (combined) row.ConstantItem(80).AlignCenter().SectionLink("resumo").Text("Voltar ao Resumo").FontSize(7).FontColor(Blue);
                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7).FontColor(Muted)).Text(t =>
                {
                    t.Span(DmoBusinessTime.Now.ToString("g", Pt) + " · ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
    }

    private static IContainer Box(IContainer c, string background = DmoPdfPalette.White, string border = Line) =>
        c.Background(background).BorderTop(.7f).BorderColor(Line);

    private static void SmallCard(IContainer container, string label, string value, string caption) =>
        container.Element(c => Box(c)).Padding(9).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Muted);
            c.Item().PaddingTop(5).Text(value).FontSize(value.Length > 17 ? 11 : 15).SemiBold();
            if (caption.Length > 0) c.Item().PaddingTop(4).Text(caption).FontSize(7).FontColor(Muted);
        });

    private static void PairedCard(IContainer container, string label, string weight, string difference, string percent, string? dias = null) =>
        container.Element(c => Box(c)).Padding(9).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Muted);
            c.Item().PaddingTop(4).Text(t =>
            {
                t.Span(weight).FontSize(17).SemiBold();
                if (dias is not null)
                {
                    t.Span("   " + dias).FontSize(12).SemiBold();
                    return;
                }
                t.Span("   " + difference).FontSize(14).SemiBold();
                t.Span(" · " + percent).FontSize(10).FontColor(Muted);
            });
        });

    private static void SectionTitle(IContainer container, string title) =>
        container.BorderLeft(3).BorderColor(Blue).PaddingLeft(8).Text(title).FontSize(13).SemiBold();

    private static void ComposeTechnicalContext(IContainer container, Input input) => container.Column(c =>
    {
        SectionTitle(c.Item(), "Referências técnicas");
        c.Item().PaddingTop(7).Row(row =>
        {
            row.Spacing(7);
            SmallCard(row.RelativeItem(), "PROCESSO", input.Process, "");
            SmallCard(row.RelativeItem(), "CONTRA-MOLDE · LOTE", (input.CmReference ?? input.CmToolId) + " · " + (input.CmLot ?? "—"), "");
            SmallCard(row.RelativeItem(), "TEMPERATURA DA ÁGUA", input.WaterTemperatureC + " °C",
                "Densidade da água " + Fmt(input.WaterDensity, "0.#####") + " g/cm³");
        });
        c.Item().PaddingTop(7).Row(row =>
        {
            row.Spacing(7);
            SmallCard(row.RelativeItem(), "DENSIDADE DO VIDRO", Fmt(input.GlassDensity, "0.#####"), "g/cm³");
            SmallCard(row.RelativeItem(), "VOLUME MARISA / PUNÇÃO", Fmt(input.VolumeMarisaCm3) + " / " + Fmt(input.VolumePuncaoCm3), "cm³");
            SmallCard(row.RelativeItem(), "VOLUME DO TAMPÃO", input.VolumeTampaoCm3 is { } tampao ? Fmt(tampao) : "—",
                input.VolumeTampaoCm3 is null ? "Tampão não registado na CM"
                : "cm³" + (input.CaloteMm is { } calote ? " · calote " + Fmt(calote) + " mm" : ""));
        });
        c.Item().PaddingTop(6).Text("Produção atual · início: " + (input.InicioEm?.ToString("d", Pt) ?? "—") +
            " · fim: " + (input.FimEm?.ToString("d", Pt) ?? "—")).FontSize(7).FontColor(Muted);
        if (input.ProducaoAnterior is { ProductionNumber: { } anteriorNumero } anterior)
        {
            c.Item().PaddingTop(3).Text("Produção anterior " + anteriorNumero + " · " + (anterior.Machine ?? "—") +
                " · início: " + (anterior.InicioEm?.ToString("d", Pt) ?? "—") +
                " · fim: " + (anterior.FimEm?.ToString("d", Pt) ?? "—") +
                (input.PesoProducaoSap is { } producao
                    ? " · peso da produção: média " + Fmt(producao.MediaGramas) + " g · moda " + Fmt(producao.ModaGramas) + " g (" + producao.ModaDias + " de " + producao.Dias + " dias)"
                    : "")).FontSize(7).FontColor(Muted);
        }
        // A decisão do Peso (sweep de 19/10/2026, C-3): aprovado, não aprovado
        // com a nota, ou por aprovar.
        c.Item().PaddingTop(5).Text(input.Approval?.Frase(Pt) ?? "Por aprovar").FontSize(8);
    });

    public static Input FromModel(PesoReadModel peso, string production, string reference, string machine,
        string path, ControloApprovalReadModel? approval = null, string? generatedBy = null,
        PesoComparisonView? comparison = null, PesoComparisonView? previousPesoComparison = null,
        DateOnly? inicio = null, DateOnly? fim = null, PesoCapSupportReadModel? cap = null,
        PesoProducaoAnteriorReadModel? producaoAnterior = null) =>
        new(production, reference, machine, peso.CmToolId, peso.CmToolReference, peso.CmToolLot, peso.Process,
            peso.WaterTemperatureC, peso.WaterDensityGramsPerCm3, peso.GlassDensityGramsPerCm3,
            peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.NominalWeightGrams,
            peso.Measurements.Select(m => new Reading(m.RowPosition, m.CmSubject, m.WaterWeightGrams,
                m.CapacityCm3, m.GlassWeightGrams)).ToArray(), peso.Averages, comparison, path, approval,
            generatedBy, previousPesoComparison, inicio, fim, cap?.VolumeEfetivoCm3, cap?.AlturaEfetivaMm, producaoAnterior);

    // Uma linha por CM, pela ordem das leituras: a massa de água, o volume e o
    // peso atuais e, com controlo anterior, a CM dele, o volume, o peso e os
    // deltas, sem exigir o mesmo número de CM (o Owner 2026-10-17: «comparando
    // cm com cm, não precisa de ser par»), e as médias no fim.
    private static void ComposeWeightTable(IContainer container, Input input, PesoComparisonView? comparison) => container.Table(t =>
    {
        var titles = comparison is null
            ? new[] { "CM", "MASSA DE\nÁGUA (G)", "VOLUME\n(CM³)", "PESO DO\nVIDRO (G)" }
            : new[] { "CM\nATUAL", "MASSA DE\nÁGUA (G)", "VOLUME\nATUAL (CM³)", "PESO\nATUAL (G)", "CM\nANTERIOR", "VOLUME\nANTERIOR (CM³)", "PESO\nANTERIOR (G)", "Δ VOLUME\n(CM³)", "Δ PESO\n(G)" };
        t.ColumnsDefinition(c => { foreach (var _ in titles) c.RelativeColumn(); });
        t.Header(h =>
        {
            foreach (var title in titles)
                h.Cell().Element(HeaderCell).Text(title).FontSize(6.5f).SemiBold();
        });
        int index = 0;
        foreach (var reading in input.Readings)
        {
            var atual = new[] { reading.CmSubject, Fmt(reading.WaterWeightGrams), Fmt(reading.CapacityCm3, "0.000"), Fmt(reading.GlassWeightGrams) };
            var anterior = comparison?.Rows.FirstOrDefault(r => r.Position == reading.Position);
            var values = comparison is null ? atual : atual.Concat(new[] {
                anterior?.HistoricalCmSubject is { Length: > 0 } cmAnterior ? cmAnterior : "—",
                Avg3(anterior?.HistoricalCapacityCm3), Avg(anterior?.HistoricalGlassWeightGrams),
                anterior?.HistoricalCapacityCm3 is { } hcap ? Signed(reading.CapacityCm3 - hcap) : "—",
                anterior is not null ? Signed(reading.GlassWeightGrams - anterior.HistoricalGlassWeightGrams) : "—" });
            foreach (var value in values)
                t.Cell().Element(c => BodyCell(c, index % 2 == 0 ? DmoPdfPalette.White : DmoPdfPalette.Soft)).Text(value).FontSize(8.5f);
            index++;
        }
        var media = new[] { "Média", Avg(input.Averages?.AverageWaterWeightGrams), Avg3(input.Averages?.AverageCapacityCm3), Avg(input.Averages?.AverageGlassWeightGrams) };
        var mediaValues = comparison is null ? media : media.Concat(new[] { "",
            Avg3(comparison.HistoricalAverages?.AverageCapacityCm3), Avg(comparison.HistoricalAverages?.AverageGlassWeightGrams),
            SignedOrDash(comparison.Deltas.CapacityDifferenceCm3),
            SignedOrDash(comparison.Deltas.GlassWeightDifferenceGrams) + (comparison.Deltas.GlassWeightVariationPercent is { } pct ? " (" + Signed(pct) + " %)" : "") });
        foreach (var value in mediaValues)
            t.Cell().Element(c => BodyCell(c, DmoPdfPalette.Tint)).Text(value).FontSize(8.5f).SemiBold();
    });

    private static string Avg3(decimal? v) => v is { } value ? Fmt(value, "0.000") : "—";

    private static string SignedOrDash(decimal? value) => value is { } v ? Signed(v) : "—";

    private static string Fmt(decimal v, string pattern = "0.00") => v.ToString(pattern, Pt);

    private static string Signed(decimal v) => DmoNumberFormat.Signed(v);

    private static string Avg(decimal? v) => v is { } value ? Fmt(value) : "—";

    private static IContainer HeaderCell(IContainer c) => c
        .Background(DmoPdfPalette.Tint).BorderBottom(.7f).BorderRight(.3f).BorderColor(Line)
        .PaddingHorizontal(7).PaddingVertical(9);

    private static IContainer BodyCell(IContainer c, string background) => c
        .Background(background).BorderBottom(.4f).BorderRight(.3f).BorderColor(Line)
        .PaddingHorizontal(7).PaddingVertical(7);
}
