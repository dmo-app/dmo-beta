using System.Globalization;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A folha A4 das punções NNPB (a folha do Owner de 17/10/2026): a
/// identificação da produção, os três totais e as punções por ordem de
/// registo. Sozinha a partir da página PU NNPB ou no fim do PDF único do
/// Resumo. Gerada em memória; nunca grava no servidor. Sem selo de
/// aprovação: o PU NNPB não tem aprovar (Diogo, 19/10/2026: «PU nnpb nao
/// tem aprovar (…) so peso e pegamtnos»).
/// </summary>
public sealed class PuncoesPdfGenerator
{
    static PuncoesPdfGenerator() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly CultureInfo Pt = DmoNumberFormat.Culture;

    private const string Ink = DmoPdfPalette.Ink;
    private const string Accent = DmoPdfPalette.Accent;
    private const string Muted = DmoPdfPalette.Muted;
    private const string Line = DmoPdfPalette.Line;
    // As cores das punções no PDF do Resumo (sweep de 19/10/2026, 3.16):
    // «Por verificar» saía no castanho do erro e «Visto» noutro verde.
    private const string Ok = DmoPdfPalette.Ok;
    private const string Pending = DmoPdfPalette.ToCheck;
    private const string Change = DmoPdfPalette.Change;

    public sealed record Input(
        string ProductionNumber,
        string Reference,
        string Machine,
        string? CmReference,
        string? CmLot,
        string PunchReference,
        string Lot,
        IReadOnlyList<Row> Rows,
        string? GeneratedBy = null);

    public sealed record Row(int Position, string Number, string Lot, bool PunchSeen, bool CoolerSeen, bool IsAlteracao);

    public byte[] GenerateBytes(Input input) =>
        Document.Create(container => Compose(container, input)).GeneratePdf();

    public static Input FromRegisto(PuncoesSheet sheet, PuncoesReadModel registo, string? generatedBy = null) =>
        new(sheet.ProductionNumber, sheet.Reference, sheet.Machine, sheet.CmReference, sheet.CmLot,
            registo.PunchReference, registo.Lot,
            registo.Rows.Select(r => new Row(r.Position, r.Number, r.Lot, r.PunchSeen, r.CoolerSeen, r.IsAlteracao)).ToArray(),
            generatedBy);

    /// <summary>Um registo sem Job On: a produção fica «Sem Job On» e a CM por associar.</summary>
    public static Input FromStandalone(PuncoesStandaloneSheet sheet, string? generatedBy = null) =>
        new("Sem Job On", sheet.Reference, "—", null, null,
            sheet.Registo.PunchReference, sheet.Registo.Lot,
            sheet.Registo.Rows.Select(r => new Row(r.Position, r.Number, r.Lot, r.PunchSeen, r.CoolerSeen, r.IsAlteracao)).ToArray(),
            generatedBy);

    public void Compose(IDocumentContainer container, Input input, bool combined = false)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(10, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(8).FontColor(Ink));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Text("Controlo de Punções NNPB").FontSize(16).SemiBold();
                    row.ConstantItem(220).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"{input.Reference} · {input.ProductionNumber} · {input.Machine}").FontSize(8).SemiBold();
                        c.Item().AlignRight().Text(DmoBusinessTime.Now.ToString("d", Pt)).Light();
                    });
                });
                header.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(Accent);
            });

            page.Content().PaddingVertical(8).Column(content =>
            {
                content.Item().Section("puncoes");
                if (combined) content.Item().SectionLink("resumo").Text("← Voltar ao Resumo").FontColor(Accent);

                content.Item().PaddingTop(4).Text("IDENTIFICAÇÃO DA PRODUÇÃO").FontSize(7).SemiBold().FontColor(Muted);
                content.Item().PaddingTop(3).Row(row =>
                {
                    row.Spacing(5);
                    Field(row.RelativeItem(), "PRODUÇÃO", input.ProductionNumber);
                    Field(row.RelativeItem(), "REFERÊNCIA TOTAL", input.Reference);
                    Field(row.RelativeItem(), "REFERÊNCIA DA PUNÇÃO", input.PunchReference);
                    Field(row.RelativeItem(), "LOTE DA PRODUÇÃO", input.Lot);
                    Field(row.RelativeItem(), "CM", input.CmReference is null ? "—" : $"{input.CmReference} · lote {input.CmLot ?? "—"}");
                });

                content.Item().PaddingTop(8).Row(row =>
                {
                    row.Spacing(5);
                    Total(row.RelativeItem(), "Total de punções", input.Rows.Count);
                    Total(row.RelativeItem(), "Punções vistas", input.Rows.Count(r => r.PunchSeen));
                    Total(row.RelativeItem(), "Arrefecedores vistos", input.Rows.Count(r => r.CoolerSeen));
                });

                content.Item().PaddingTop(10).Text("INSPEÇÃO INDIVIDUAL · POR ORDEM DE REGISTO").FontSize(7).SemiBold().FontColor(Muted);
                if (input.Rows.Count == 0)
                {
                    content.Item().PaddingTop(4).Text("Sem punções registadas.").FontColor(Muted);
                }
                else
                {
                    content.Item().PaddingTop(3).Table(t =>
                    {
                        t.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                        });

                        static IContainer Head(IContainer c) => c.Background(DmoPdfPalette.Tint).BorderBottom(0.6f).BorderColor(DmoPdfPalette.Mist)
                            .PaddingHorizontal(4).PaddingVertical(4);
                        t.Header(h =>
                        {
                            h.Cell().Element(Head).Text("#").SemiBold();
                            h.Cell().Element(Head).Text("N.º da punção").SemiBold();
                            h.Cell().Element(Head).Text("Lote").SemiBold();
                            h.Cell().Element(Head).Text("Punção visto").SemiBold();
                            h.Cell().Element(Head).Text("Arrefecedor visto").SemiBold();
                        });

                        var index = 0;
                        foreach (var r in input.Rows)
                        {
                            var background = index++ % 2 == 0 ? DmoPdfPalette.White : DmoPdfPalette.Soft;
                            IContainer Body(IContainer c) => c.Background(background).BorderBottom(0.3f).BorderColor(Line)
                                .PaddingHorizontal(4).PaddingVertical(3);
                            t.Cell().Element(Body).Text(r.Position.ToString(Pt)).FontColor(Muted);
                            t.Cell().Element(Body).Text(text =>
                            {
                                text.Span(r.Number).SemiBold();
                                if (r.IsAlteracao) text.Span("  Alteração").FontSize(7).SemiBold().FontColor(Change);
                            });
                            t.Cell().Element(Body).Text(r.Lot).FontColor(Accent);
                            t.Cell().Element(Body).Text(r.PunchSeen ? "Visto" : "Por verificar").FontColor(r.PunchSeen ? Ok : Pending);
                            t.Cell().Element(Body).Text(r.CoolerSeen ? "Visto" : "Por verificar").FontColor(r.CoolerSeen ? Ok : Pending);
                        }
                    });
                }
            });

            page.Footer().Row(footer =>
            {
                var now = DmoBusinessTime.Now.ToString("g", Pt);
                var generated = input.GeneratedBy is { } by ? $"Gerado por {by} · a {now}" : $"Gerado em {now}";
                footer.RelativeItem().Text($"Portal DMO · {generated} · {input.ProductionNumber}").Light();
                footer.RelativeItem().AlignRight().Text("Desenvolvido por Diogo Oliveira").Light();
            });
        });
    }

    private static void Field(IContainer container, string label, string value) =>
        container.Border(0.5f).BorderColor(Line).Padding(6).Column(c =>
        {
            c.Item().Text(label).FontSize(6.5f).FontColor(Muted);
            c.Item().PaddingTop(2).Text(value).FontSize(10).SemiBold();
        });

    private static void Total(IContainer container, string label, int value) =>
        container.Background(DmoPdfPalette.Tint).BorderLeft(2).BorderColor(DmoPdfPalette.Steel).Padding(6).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Muted);
            c.Item().Text(value.ToString(Pt)).FontSize(15).SemiBold();
        });
}
