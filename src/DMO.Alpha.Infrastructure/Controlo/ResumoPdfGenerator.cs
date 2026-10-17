using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Controlo;

public sealed class ResumoPdfGenerator
{
    static ResumoPdfGenerator() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly CultureInfo Pt = DmoNumberFormat.Culture;

    public sealed record Input(
        string ProductionNumber,
        string Reference,
        string Machine,
        DateOnly? InicioEm,
        DateOnly? FimEm,
        IReadOnlyList<ToolCard> Tools,
        PesoSummary? Peso,
        PegamentosSummary? Pegamentos,
        IReadOnlyList< Viewing> Viewings,
        /// <summary>O selo de aprovação (o corte D): o operador e o momento; null = não aprovado.</summary>
        ControloApprovalReadModel? Approval,
        /// <summary>Quem gerou o papel (o rodapé do alvo: «Diogo Oliveira 28/07/2026, 14:52:28»).</summary>
        string? GeneratedBy = null,
        PesoReadModel? PesoDetalhe = null,
        PegamentosSheetReadModel? PegamentosDetalhe = null,
        PesoPdfGenerator.Input? PesoDocument = null,
        PegamentosPdfGenerator.Input? PegamentosDocument = null,
        /// <summary>O estado do documento por área (CM/BQ/MF/PU/CS); null ou ausente = «não registado».</summary>
        IReadOnlyList<AreaState>? AreaStates = null,
        /// <summary>O registo das punções NNPB; null = produção não NNPB ou sem registo (sem cartão).</summary>
        PuncoesSummary? Puncoes = null,
        PuncoesPdfGenerator.Input? PuncoesDocument = null);

    /// <summary>Os três totais do registo das punções NNPB.</summary>
    public sealed record PuncoesSummary(int Total, int PunchSeen, int CoolerSeen);

    /// <summary>
    /// OK/NOK (null = não registado), o comentário de uma área do documento
    /// e quem a marcou e quando (Diogo, 17/10/2026).
    /// </summary>
    public sealed record AreaState(string Area, string? Status, string? Comment, string? By = null, DateTime? AtUtc = null);

    /// <summary>Uma ferramenta da combinação (os fatos humanos).</summary>
    public sealed record ToolCard(string Slot, string Reference, string? Lot);

    public sealed record PesoSummary(
        int ReadingCount,
        decimal? AverageGlassWeightGrams,
        decimal WaterTemperatureC,
        decimal? NominalWeightGrams = null);

    public sealed record PegamentosSummary(
        int MeasurementCount,
        int AlertCount,
        decimal ToleranceMm,
        int NotEvaluableCount = 0);

    /// <summary>Uma visita registada (o «Visto por»).</summary>
    public sealed record Viewing(string OperatorId, DateTime ViewedAtUtc);

    public byte[] GenerateBytes(Input input) => CreateDocument(input).GeneratePdf();

    private static readonly byte[] Logo = LoadLogo();
    private static byte[] LoadLogo()
    {
        using var stream = typeof(ResumoPdfGenerator).Assembly.GetManifestResourceStream("DMO.Pdf.Logo.png")
            ?? throw new InvalidOperationException("PDF logo resource is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    // The same palette as the Peso sheet, so the combined PDF reads as one document.
    private const string Ink = DmoPdfPalette.Ink;
    private const string Muted = DmoPdfPalette.Muted;
    private const string Line = DmoPdfPalette.Line;
    private const string Blue = DmoPdfPalette.Accent;
    private const string Navy = DmoPdfPalette.Deep;
    private const string Soft = DmoPdfPalette.Soft;

    private static IDocument CreateDocument(Input input) => Document.Create(container =>
    {
        var hasPeso = input.PesoDocument is not null || input.PesoDetalhe is not null;
        var hasPegamentos = input.PegamentosDocument is not null || input.PegamentosDetalhe?.Pegamentos is not null;

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
                    row.RelativeItem().PaddingLeft(10).PaddingTop(3).Text("Resumo do Controlo").FontSize(24).Bold();
                });
                header.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"{input.Reference}     ·     {input.ProductionNumber}     ·     {input.Machine}").FontSize(10);
                    row.ConstantItem(85).AlignRight().Text(DmoBusinessTime.Now.ToString("d", Pt)).FontColor(Muted);
                });
                header.Item().PaddingTop(9).LineHorizontal(0.7f).LineColor(Line);
            });

            page.Content().Column(content =>
            {
                content.Item().Section("resumo");

                // A JANELA TEMPORAL (o corte B) e o selo de aprovação (o corte D).
                content.Item().Row(row =>
                {
                    row.Spacing(7);
                    SmallCard(row.RelativeItem(), "INÍCIO EM MÁQUINA", input.InicioEm?.ToString("d", Pt) ?? "—",
                        input.InicioEm is null ? "Janela não definida" : "Data de início");
                    SmallCard(row.RelativeItem(), "FIM EM MÁQUINA", input.FimEm?.ToString("d", Pt) ?? (input.InicioEm is null ? "—" : "Em aberto"),
                        input.FimEm is null ? "Produção sem fim registado" : "Data de fim");
                    SmallCard(row.RelativeItem(), "DURAÇÃO", input.InicioEm is { } inicio && input.FimEm is { } fim
                        ? (fim.DayNumber - inicio.DayNumber + 1) + " dia(s)" : "—", "Inclusiva");
                    ApprovalCard(row.RelativeItem(), input.Approval);
                });

                SectionTitle(content.Item().PaddingTop(14), "Controlos associados");
                content.Item().PaddingTop(7).Row(row =>
                {
                    row.Spacing(8);
                    PesoCard(row.RelativeItem(), input, hasPeso);
                    PegamentosCard(row.RelativeItem(), input.Pegamentos, hasPegamentos);
                    if (input.Puncoes is { } puncoes)
                        PuncoesCard(row.RelativeItem(), puncoes, input.PuncoesDocument is not null);
                });

                // AS FERRAMENTAS da combinação.
                SectionTitle(content.Item().PaddingTop(14), "A combinação da produção");
                if (input.Tools.Count == 0)
                {
                    content.Item().PaddingTop(6).Text("Sem ferramentas registadas na produção.").FontSize(9).FontColor(Muted);
                }
                else
                {
                    content.Item().PaddingTop(7).Row(row =>
                    {
                        row.Spacing(7);
                        foreach (var tool in input.Tools)
                            SmallCard(row.RelativeItem(), ToolLabel(tool.Slot), tool.Reference, "Lote " + (tool.Lot ?? "—"));
                    });
                }

                SectionTitle(content.Item().PaddingTop(14), "Estado do documento");
                var areaStates = input.AreaStates ?? [];
                content.Item().PaddingTop(7).Row(row =>
                {
                    row.Spacing(6);
                    foreach (var slot in new[] { "CM", "BQ", "MF", "PU", "CS" })
                    {
                        var state = areaStates.FirstOrDefault(a => a.Area == slot);
                        var (label, color, background, border) = state?.Status switch
                        {
                            "OK" => ("OK", "#15803d", "#effaf3", "#9fd5b3"),
                            "NOK" => ("NOK", "#b91c1c", "#fdf2f2", "#efb1b1"),
                            _ => ("não registado", Muted, Soft, Line),
                        };
                        row.RelativeItem().Element(c => Box(c, background, border)).Padding(8).Column(c =>
                        {
                            c.Item().Text(slot).FontSize(8).FontColor(Muted);
                            c.Item().PaddingTop(3).Text(label).FontSize(state?.Status is null ? 9 : 15).SemiBold().FontColor(color);
                            if (state?.Status is not null && state.AtUtc is { } areaAt)
                                c.Item().PaddingTop(3).Text($"Por {state.By ?? "—"} · {DmoBusinessTime.ToLocal(areaAt).ToString("g", Pt)}").FontSize(7).FontColor(Muted);
                        });
                    }
                });
                var comments = areaStates.Where(a => !string.IsNullOrWhiteSpace(a.Comment)).ToArray();
                if (comments.Length > 0)
                {
                    content.Item().PaddingTop(7).Element(c => Box(c)).Padding(9).Column(c =>
                    {
                        c.Item().Text("COMENTÁRIOS").FontSize(7).FontColor(Muted);
                        foreach (var commented in comments)
                            c.Item().PaddingTop(4).Text(t =>
                            {
                                t.Span(commented.Area + "  ").FontSize(9).SemiBold();
                                t.Span(commented.Comment!).FontSize(9);
                            });
                    });
                }

                // O REGISTO DE VISÃO (a GATE 4: a informação, nunca o gate) e,
                // ao lado, quem aprovou (Diogo, 17/10/2026).
                content.Item().PaddingTop(14).Row(people =>
                {
                    people.Spacing(12);
                    people.RelativeItem().Column(column =>
                    {
                        SectionTitle(column.Item(), "Visto por");
                        if (input.Viewings.Count == 0)
                        {
                            column.Item().PaddingTop(6).Text("Ninguém viu ainda — a informação existe, ninguém a abriu.").FontSize(9).FontColor(Muted);
                        }
                        else
                        {
                            column.Item().PaddingTop(7).Element(c => Box(c)).Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                t.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("OPERADOR").FontSize(7).SemiBold();
                                    h.Cell().Element(HeaderCell).Text("VISTO A").FontSize(7).SemiBold();
                                });
                                var index = 0;
                                foreach (var viewing in input.Viewings)
                                {
                                    var background = index++ % 2 == 0 ? "#ffffff" : DmoPdfPalette.Soft;
                                    t.Cell().Element(c => BodyCell(c, background)).Text("Operador " + viewing.OperatorId).FontSize(9);
                                    t.Cell().Element(c => BodyCell(c, background)).Text(DmoBusinessTime.ToLocal(viewing.ViewedAtUtc).ToString("g", Pt)).FontSize(9);
                                }
                            });
                        }
                    });
                    people.RelativeItem().Column(column =>
                    {
                        SectionTitle(column.Item(), "Aprovado por");
                        column.Item().PaddingTop(7).Element(c => Box(c)).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            t.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("OPERADOR").FontSize(7).SemiBold();
                                h.Cell().Element(HeaderCell).Text("APROVADO A").FontSize(7).SemiBold();
                            });
                            if (input.Approval?.ApprovedByOperatorId is { } approver)
                            {
                                t.Cell().Element(c => BodyCell(c, "#ffffff")).Text("Operador " + approver).FontSize(9);
                                t.Cell().Element(c => BodyCell(c, "#ffffff")).Text(input.Approval.ApprovedAtUtc is { } approvedAt ? DmoBusinessTime.ToLocal(approvedAt).ToString("g", Pt) : "—").FontSize(9);
                            }
                            else
                            {
                                t.Cell().ColumnSpan(2).Element(c => BodyCell(c, "#ffffff")).Text("Por aprovar.").FontSize(9).FontColor(Muted);
                            }
                        });
                    });
                });
            });

            page.Footer().PaddingTop(8).BorderTop(.6f).BorderColor(Line).PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("Portal DMO · " + (input.GeneratedBy is { } by ? "Gerado por " + by : "Gerado por: sem identificação"))
                    .FontSize(7).FontColor(Muted);
                row.RelativeItem().AlignCenter().Text("Desenvolvido por Diogo Oliveira").FontSize(7).FontColor(Muted);
                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7).FontColor(Muted)).Text(t =>
                {
                    t.Span(DmoBusinessTime.Now.ToString("g", Pt) + " · ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
        var pesoInput = input.PesoDocument ?? (input.PesoDetalhe is { } peso
            ? PesoPdfGenerator.FromModel(peso, input.ProductionNumber, input.Reference, input.Machine,
                "", input.Approval, input.GeneratedBy, inicio: input.InicioEm, fim: input.FimEm) : null);
        if (pesoInput is not null) new PesoPdfGenerator().Compose(container, pesoInput, combined: true);
        var pegInput = input.PegamentosDocument ?? (input.PegamentosDetalhe is { } peg
            ? PegamentosPdfGenerator.FromSheet(peg, input.Approval, input.GeneratedBy) : null);
        if (pegInput is not null) new PegamentosPdfGenerator().Compose(container, pegInput, combined: true);
        if (input.PuncoesDocument is { } puncoesInput) new PuncoesPdfGenerator().Compose(container, puncoesInput, combined: true);
    });

    private static void PesoCard(IContainer container, Input input, bool linked)
    {
        var average = input.Peso?.AverageGlassWeightGrams;
        var nominal = input.Peso?.NominalWeightGrams
            ?? input.PesoDocument?.NominalWeightGrams ?? input.PesoDetalhe?.NominalWeightGrams;
        container.Element(c => Box(c, Navy, Navy)).Padding(12).MinHeight(128)
            .DefaultTextStyle(x => x.FontColor("#ffffff")).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text("Peso").FontSize(10).SemiBold();
                    if (linked) r.AutoItem().SectionLink("peso").Text("Ver detalhe →").FontSize(8).FontColor(DmoPdfPalette.OnDeep).Underline();
                });
                c.Item().PaddingTop(8).Text(t =>
                {
                    if (average is { } avg)
                    {
                        t.Span(Fmt(avg)).FontSize(34).SemiBold();
                        t.Span(" g").FontSize(17).SemiBold();
                    }
                    else
                    {
                        t.Span("Sem leituras registadas").FontSize(15).SemiBold();
                    }
                });
                c.Item().Text("Peso médio do vidro").FontSize(8).FontColor(DmoPdfPalette.OnDeep);
                if (input.Peso is { } peso)
                    c.Item().PaddingTop(8).Text($"{peso.ReadingCount} leitura(s) · água a {Fmt(peso.WaterTemperatureC, "0.#")} °C")
                        .FontSize(8).FontColor(DmoPdfPalette.OnDeep);
                var deviation = PesoCalculation.NominalDeviation(average, nominal);
                if (nominal is { } n)
                    c.Item().PaddingTop(2).Text("Nominal " + Fmt(n) + " g" + (deviation.Grams is { } g
                        ? " · " + Signed(g) + " g" + (deviation.Percent is { } p ? " (" + Signed(p) + " %)" : "") : ""))
                        .FontSize(8).FontColor(DmoPdfPalette.OnDeep);
            });
    }

    private static void PegamentosCard(IContainer container, PegamentosSummary? summary, bool linked)
    {
        var (status, color, background) = summary is not { MeasurementCount: > 0 }
            ? ("Sem medições registadas", Muted, Soft)
            : summary.AlertCount > 0 ? ("Existem medições fora das tolerâncias", "#b91c1c", "#fdf2f2")
            : summary.NotEvaluableCount > 0 ? ("Existem medições não avaliáveis", "#a16207", "#fdf8ec")
            : ("Todas as medições dentro das tolerâncias", "#15803d", "#effaf3");
        container.Element(c => Box(c, background, color)).Padding(12).MinHeight(128).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Text("Pegamentos").FontSize(10).SemiBold();
                if (linked) r.AutoItem().SectionLink("pegamentos").Text("Ver detalhe →").FontSize(8).FontColor(Blue).Underline();
            });
            c.Item().PaddingTop(8).Text(status).FontSize(13).SemiBold().FontColor(color);
            if (summary is { MeasurementCount: > 0 })
            {
                c.Item().PaddingTop(10).Row(r =>
                {
                    Stat(r.RelativeItem(), "MEDIÇÕES", summary.MeasurementCount.ToString(Pt), Ink);
                    Stat(r.RelativeItem(), "EM ALERTA", summary.AlertCount.ToString(Pt), summary.AlertCount > 0 ? "#b91c1c" : Ink);
                    Stat(r.RelativeItem(), "NÃO AVALIÁVEIS", summary.NotEvaluableCount.ToString(Pt), summary.NotEvaluableCount > 0 ? "#a16207" : Ink);
                });
                c.Item().PaddingTop(6).Text("Tolerância ± " + Fmt(summary.ToleranceMm) + " mm").FontSize(8).FontColor(Muted);
            }
        });
    }

    private static void PuncoesCard(IContainer container, PuncoesSummary summary, bool linked)
    {
        var pending = summary.Total * 2 - summary.PunchSeen - summary.CoolerSeen;
        var (status, color, background) = summary.Total == 0
            ? ("Sem punções registadas", Muted, Soft)
            : pending > 0 ? ("Existem punções por verificar", "#a16207", "#fdf8ec")
            : ("Punções e arrefecedores vistos", "#15803d", "#effaf3");
        container.Element(c => Box(c, background, color)).Padding(12).MinHeight(128).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Text("PU NNPB").FontSize(10).SemiBold();
                if (linked) r.AutoItem().SectionLink("puncoes").Text("Ver detalhe →").FontSize(8).FontColor(Blue).Underline();
            });
            c.Item().PaddingTop(8).Text(status).FontSize(13).SemiBold().FontColor(color);
            if (summary.Total > 0)
            {
                c.Item().PaddingTop(10).Row(r =>
                {
                    Stat(r.RelativeItem(), "TOTAL", summary.Total.ToString(Pt), Ink);
                    Stat(r.RelativeItem(), "PU VISTAS", summary.PunchSeen.ToString(Pt), summary.PunchSeen < summary.Total ? "#a16207" : Ink);
                    Stat(r.RelativeItem(), "ARREF. VISTOS", summary.CoolerSeen.ToString(Pt), summary.CoolerSeen < summary.Total ? "#a16207" : Ink);
                });
            }
        });
    }

    private static void Stat(IContainer container, string label, string value, string color) => container.Column(c =>
    {
        c.Item().Text(label).FontSize(6.5f).FontColor(Muted);
        c.Item().PaddingTop(2).Text(value).FontSize(16).SemiBold().FontColor(color);
    });

    private static void ApprovalCard(IContainer container, ControloApprovalReadModel? approval)
    {
        var approved = approval?.ApprovedByOperatorId is not null;
        container.Element(c => approved ? Box(c, "#effaf3", "#3d7a5a") : Box(c, "#fdf8ec", "#e3c58f")).Padding(9).Column(c =>
        {
            c.Item().Text("APROVAÇÃO").FontSize(7).FontColor(Muted);
            c.Item().PaddingTop(5).Text(approved ? "Aprovado" : "Sem registo").FontSize(15).SemiBold()
                .FontColor(approved ? "#3d7a5a" : "#a97943");
            c.Item().PaddingTop(4).Text(approved
                ? "Operador " + approval!.ApprovedByOperatorId + (approval.ApprovedAtUtc is { } at ? " · " + DmoBusinessTime.ToLocal(at).ToString("g", Pt) : "")
                : "Por aprovar").FontSize(7).FontColor(Muted);
        });
    }

    private static string ToolLabel(string slot) => slot switch
    {
        "CM" => "CM · CONTRA-MOLDE",
        "MF" => "MF · MOLDE FINAL",
        "BQ" => "BQ · BOQUILHA",
        _ => slot,
    };

    private static IContainer Box(IContainer c, string background = "#ffffff", string border = Line) =>
        c.Background(background).Border(.7f).BorderColor(border).CornerRadius(7);

    private static void SmallCard(IContainer container, string label, string value, string caption) =>
        container.Element(c => Box(c)).Padding(9).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Muted);
            c.Item().PaddingTop(5).Text(value).FontSize(value.Length > 14 ? 11 : 15).SemiBold();
            c.Item().PaddingTop(4).Text(caption).FontSize(7).FontColor(Muted);
        });

    private static void SectionTitle(IContainer container, string title) =>
        container.BorderLeft(3).BorderColor(Blue).PaddingLeft(8).Text(title).FontSize(13).SemiBold();

    private static string Fmt(decimal v, string pattern = "0.00") => v.ToString(pattern, Pt);

    private static string Signed(decimal v) => DmoNumberFormat.Signed(v);

    private static IContainer HeaderCell(IContainer c) => c
        .Background(DmoPdfPalette.Tint).BorderBottom(.7f).BorderColor(Line)
        .PaddingHorizontal(7).PaddingVertical(6);

    private static IContainer BodyCell(IContainer c, string background) => c
        .Background(background).BorderBottom(.4f).BorderColor(Line)
        .PaddingHorizontal(7).PaddingVertical(5);
}
