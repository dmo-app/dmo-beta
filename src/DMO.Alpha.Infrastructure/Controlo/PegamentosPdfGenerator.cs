using System.Globalization;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Controlo;

public sealed class PegamentosPdfGenerator
{
    static PegamentosPdfGenerator() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly CultureInfo Pt = DmoNumberFormat.Culture;

    /// <summary>Linhas vazias por componente sem medições (folha para preencher à mão).</summary>
    public const int BlankRowsPerComponent = 6;

    public sealed record Input(
        string ProductionNumber,
        string Reference,
        string Machine,
        IReadOnlyList<ComponentCard> Components,
        IReadOnlyList<ComponentTable> Tables,
        decimal ToleranceMm,
        /// <summary>O selo de aprovação (o corte D): o operador e o momento; null = não aprovado.</summary>
        ControloApprovalReadModel? Approval = null,
        /// <summary>Quem gerou o papel (o rodapé do alvo: «Diogo Oliveira 28/07/2026, 14:52:28»).</summary>
        string? GeneratedBy = null)
    {
        /// <summary>
        /// A folha em branco para preencher à mão: só no download separado de
        /// uma produção ainda sem registo de Pegamentos. O PDF combinado do
        /// Resumo e um componente sem medições num registo existente ficam
        /// como antes.
        /// </summary>
        public bool BlankForHandFilling { get; init; }
    }

    /// <summary>A ficha de um componente da combinação (os fatos humanos).</summary>
    public sealed record ComponentCard(
        string Component,
        string Reference,
        string? Lot,
        decimal? NominalGargaloMm);

    /// <summary>A tabela das medições de um componente + as médias dele.</summary>
    public sealed record ComponentTable(
        string Component,
        IReadOnlyList<MeasurementRow> Rows,
        int SampleCount,
        int AlertCount,
        decimal? AverageCosturaMm,
        decimal? AverageContraCosturaMm,
        decimal? AverageOvalizacaoMm,
        decimal? AverageMediaMm);

    public sealed record MeasurementRow(
        int Position,
        decimal CosturaMm,
        decimal? ContraCosturaMm,
        decimal? OvalizacaoMm,
        decimal? MediaMm,
        bool IsAlert,
        bool NotEvaluable,
        decimal? NominalMm = null, decimal? LowerMm = null, decimal? UpperMm = null);

    public byte[] GenerateBytes(Input input) =>
        Document.Create(container => Compose(container, input)).GeneratePdf();

    public void Compose(IDocumentContainer container, Input input, bool combined = false)
    {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                // O QuestPDF 2026+: o Lato EMBUTIDO no pacote — determinístico.
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Controlo de Pegamentos").FontSize(16).SemiBold().FontColor(DmoPdfPalette.Ink);
                        row.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().Text($"{input.Reference} · {input.ProductionNumber} · {input.Machine}").FontSize(8).SemiBold();
                            c.Item().Text(DmoBusinessTime.Now.ToString("d", Pt)).Light();
                        });
                    });
                    header.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(DmoPdfPalette.Accent);
                });

                page.Content().PaddingVertical(8).Column(content =>
                {
                    content.Item().Section("pegamentos");
                    if (combined) content.Item().SectionLink("resumo").Text("← Voltar ao Resumo").FontColor(DmoPdfPalette.Accent);
                    content.Item().PaddingTop(4).Row(row =>
                    {
                        row.Spacing(5);
                        foreach (var card in input.Components)
                        {
                            var table = input.Tables.FirstOrDefault(t => t.Component.Equals(card.Component, StringComparison.OrdinalIgnoreCase));
                            row.RelativeItem().Border(0.5f).BorderColor(DmoPdfPalette.Sky).BorderLeft(2).Padding(6).Column(c =>
                            {
                                c.Item().Text(Label(card.Component) + " — média medida").FontColor(DmoPdfPalette.Muted);
                                c.Item().Text(table?.AverageMediaMm is { } avg ? Fmt(avg) + " mm" : "Sem medições").FontSize(13).SemiBold();
                                c.Item().Text(NominalText(input, card, table)).FontSize(7).FontColor(DmoPdfPalette.Muted);
                            });
                        }
                    });
                    var status = Status(input);
                    var statusColor = input.Tables.Any(t => t.Rows.Any(r => r.IsAlert)) ? "#b91c1c"
                        : input.Tables.All(t => t.Rows.Count == 0) ? DmoPdfPalette.Muted
                        : input.Tables.Any(t => t.Rows.Any(r => r.NotEvaluable)) ? "#a16207" : "#15803d";
                    content.Item().PaddingTop(7).Border(0.6f).BorderColor(statusColor).Padding(7)
                        .Text(status).FontSize(10).SemiBold().FontColor(statusColor);
                    content.Item().PaddingTop(9).Text("Mapa de limites").FontSize(10).SemiBold().FontColor(DmoPdfPalette.Accent);
                    content.Item().PaddingTop(4).Border(0.5f).BorderColor(DmoPdfPalette.Sky).Padding(5).Column(map =>
                    {
                        map.Item().Height(160).Svg(PegamentosLimitMap.Render(input));
                        map.Item().Text("Linhas = nominais · Círculos = médias de medições · Losango = média global · Vermelho = alerta").FontSize(6.5f);
                        map.Item().PaddingTop(3).Text(status).FontSize(7).FontColor(statusColor);
                    });

                    // AS TABELAS POR COMPONENTE.
                    foreach (var table in input.Tables)
                    {
                        content.Item().PaddingTop(8).Border(0.5f).BorderColor(DmoPdfPalette.Sky).BorderLeft(2).Padding(4).Text(TableTitle(input, table)).FontSize(8).SemiBold().FontColor(DmoPdfPalette.Accent);

                        content.Item().PaddingTop(3).Table(t =>
                        {
                            t.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(32);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();

                            });

                            IContainer Cell(IContainer c) => c.Element(HeaderCell);
                            t.Header(h =>
                            {
                                h.Cell().Element(Cell).Text("N.º");
                                h.Cell().Element(Cell).Text("Costura (mm)");
                                h.Cell().Element(Cell).Text("90° (mm)");
                                h.Cell().Element(Cell).Text("Ovalização (mm)");
                                h.Cell().Element(Cell).Text("Média (mm)");

                            });

                            foreach (var measurement in table.Rows)
                            {


                                // Linha em alerta: borda vermelha em cima e em baixo; só o valor fora do corredor leva fundo.
                                var rowAlert = measurement.IsAlert && !measurement.NotEvaluable;
                                bool Outside(decimal? value) => rowAlert && value is { } v
                                    && measurement.LowerMm is { } lower && measurement.UpperMm is { } upper
                                    && PegamentosCalculation.IsAlert(v, lower, upper);
                                IContainer Body(IContainer c, bool outside = false)
                                {
                                    c = rowAlert
                                        ? c.BorderTop(1).BorderBottom(1).BorderColor("#b91c1c")
                                            .Padding(0).BorderLeft(0.3f).BorderRight(0.3f).BorderColor(DmoPdfPalette.Line)
                                        // Sem borda superior: a linha seguinte a um alerta não tapa a borda vermelha.
                                        : c.BorderLeft(0.3f).BorderRight(0.3f).BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line);
                                    return c.Background(outside ? "#f8d4d4" : "#ffffff")
                                        .PaddingHorizontal(3).PaddingVertical(2.5f);
                                }

                                t.Cell().Element(c => Body(c)).Text(measurement.Position.ToString());
                                t.Cell().Element(c => Body(c, Outside(measurement.CosturaMm))).Text(Fmt(measurement.CosturaMm));
                                t.Cell().Element(c => Body(c, Outside(measurement.ContraCosturaMm))).Text(measurement.ContraCosturaMm is { } contra ? Fmt(contra) : "—");
                                t.Cell().Element(c => Body(c)).Text(Signed(measurement.OvalizacaoMm));
                                t.Cell().Element(c => Body(c, Outside(measurement.MediaMm))).Text(measurement.MediaMm is { } media ? Fmt(media) : "—");

                            }

                            // A FOLHA EM BRANCO: produção ainda sem registo,
                            // linhas vazias para preencher à mão (Owner
                            // 16/10/2026: aviso em vez de recusa).
                            var blankRows = BlankRowsFor(input, table);
                            var blankSheet = blankRows > 0;
                            if (blankSheet)
                            {
                                IContainer Blank(IContainer c) => c
                                    .BorderLeft(0.3f).BorderRight(0.3f).BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line)
                                    .MinHeight(16).PaddingHorizontal(3).PaddingVertical(2.5f);
                                for (var blank = 1; blank <= blankRows; blank++)
                                {
                                    t.Cell().Element(Blank).Text(blank.ToString(Pt));
                                    for (var column = 0; column < 4; column++)
                                    {
                                        t.Cell().Element(Blank);
                                    }
                                }
                            }

                            // A LINHA DAS MÉDIAS por componente.
                            t.Footer(f =>
                            {
                                IContainer AvgCell(IContainer c) => c
                                    .Background(DmoPdfPalette.Soft).Border(0.3f).BorderColor(DmoPdfPalette.Line)
                                    .PaddingHorizontal(3).PaddingVertical(2.5f);
                                string Shown(string value) => blankSheet ? string.Empty : value;

                                f.Cell().Element(AvgCell).Text("AVG").SemiBold();
                                f.Cell().Element(AvgCell).Text(Shown(Avg(table.AverageCosturaMm)));
                                f.Cell().Element(AvgCell).Text(Shown(Avg(table.AverageContraCosturaMm)));
                                f.Cell().Element(AvgCell).Text(Shown(Signed(table.AverageOvalizacaoMm)));
                                f.Cell().Element(AvgCell).Text(Shown(Avg(table.AverageMediaMm))).SemiBold();

                            });
                        });
                    }

                    if (input.Tables.Any(t => t.Rows.Any(r => r.ContraCosturaMm is null)
                        && t.Rows.Any(r => r.ContraCosturaMm is not null)))
                    {
                        content.Item().PaddingTop(4).Text("AVG: média dos resultados por leitura; contra costura e ovalização incluem apenas os valores medidos.")
                            .FontSize(8).FontColor(DmoPdfPalette.Muted);
                    }

                    // O SELO DE APROVAÇÃO (o corte D): o estado no papel —
                    // nunca gate, a informação do histórico.
                    var approvedColor = input.Approval?.ApprovedByOperatorId is null ? "#a97943" : "#3d7a5a";
                    content.Item().PaddingTop(8).Text(t =>
                    {
                        if (input.Approval?.ApprovedByOperatorId is { } approver)
                        {
                            t.Span($"APROVADO — operador {approver}").SemiBold().FontColor(approvedColor);
                        }
                        else
                        {
                            t.Span("Aprovação: sem registo").FontColor(approvedColor);
                        }
                    });
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

    public static Input FromSheet(PegamentosSheetReadModel sheet,
        ControloApprovalReadModel? approval = null, string? generatedBy = null)
    {
        var record = sheet.Pegamentos;
        var cards = new List<ComponentCard>();
        var tables = new List<ComponentTable>();
        var contexts = sheet.Components.ToList();
        if (record is not null)
        {
            foreach (var component in Enum.GetValues<PegamentosComponent>())
            {
                var toolId = component switch { PegamentosComponent.Cm => record.CmToolId,
                    PegamentosComponent.Bq => record.BqToolId, _ => record.MfToolId };
                if (!contexts.Any(c => c.Component == component) && (toolId is not null || record.Measurements.Any(m => m.Component == component)))
                    contexts.Add(new(component, toolId ?? "—", null, null, null));
            }
        }
        foreach (var context in contexts.OrderBy(c => c.Component))
        {
            var measurements = record?.Measurements.Where(m => m.Component == context.Component).ToArray()
                ?? Array.Empty<PegamentosMeasurementReadModel>();
            // Historical nominal only. An unmeasured component may show its current nominal.
            var nominal = measurements.Length > 0 ? measurements[0].NominalGargaloMm : context.DiametroGargaloMm;
            var name = context.Component.ToString().ToUpperInvariant();
            var reference = context.Component switch { PegamentosComponent.Cm => record?.CmToolReference,
                PegamentosComponent.Bq => record?.BqToolReference, _ => record?.MfToolReference };
            var lot = context.Component switch { PegamentosComponent.Cm => record?.CmToolLot,
                PegamentosComponent.Bq => record?.BqToolLot, _ => record?.MfToolLot };
            cards.Add(new(name, reference ?? context.ToolReference ?? context.ToolId, lot ?? context.ToolLot, nominal));
            var avg = record?.ComponentAverages.FirstOrDefault(a => a.Component == context.Component);
            tables.Add(new(name, measurements.Select(m => new MeasurementRow(m.RowPosition, m.CosturaMm,
                m.ContraCosturaMm, m.Evaluation.OvalizacaoMm, m.Evaluation.MediaMm, m.Evaluation.HasAlert,
                m.Evaluation.IsNotEvaluable, m.NominalGargaloMm, m.Evaluation.CorridorLowerMm, m.Evaluation.CorridorUpperMm)).ToArray(),
                avg?.SampleCount ?? 0, avg?.AlertCount ?? 0, avg?.AverageCosturaMm,
                avg?.AverageContraCosturaMm, avg?.AverageOvalizacaoMm, avg?.AverageMediaMm));
        }
        return new(sheet.ProductionNumber, sheet.Reference, sheet.Machine, cards, tables,
            record?.ToleranceMm ?? sheet.CurrentToleranceMm, approval, generatedBy);
    }

    internal static string Label(string component) => component.ToUpperInvariant() switch
    {
        "CM" => "CM — Contra-molde", "BQ" => "Boquilha", "MF" => "Molde final", _ => component
    };

    internal static string Status(Input input)
    {
        var rows = input.Tables.SelectMany(t => t.Rows).ToArray();
        return rows.Length == 0 ? "Sem medições registadas"
            : rows.Any(r => r.IsAlert) ? "Existem medições que atingem ou ultrapassam os limites de tolerância"
            : rows.Any(r => r.NotEvaluable) ? "Existem medições não avaliáveis — nominal não registado"
            : "Todas as medições dentro das tolerâncias";
    }

    private static string NominalText(Input input, ComponentCard card, ComponentTable? table)
    {
        var first = table?.Rows.FirstOrDefault();
        var nominal = first is null ? card.NominalGargaloMm : first.NominalMm;
        if (nominal is not { } n) return "Nominal não registado · Sem corredor avaliável";
        var lower = first?.LowerMm ?? n - input.ToleranceMm;
        var upper = first?.UpperMm ?? n + input.ToleranceMm;
        return $"Nominal {Fmt(n)} mm · Corredor {Fmt(lower)}–{Fmt(upper)} mm";
    }

    /// <summary>Linhas vazias a desenhar na tabela: só na folha em branco e só sem medições.</summary>
    public static int BlankRowsFor(Input input, ComponentTable table) =>
        input.BlankForHandFilling && table.Rows.Count == 0 ? BlankRowsPerComponent : 0;

    public static string TableTitle(Input input, ComponentTable table)
    {
        var card = input.Components.FirstOrDefault(c => c.Component.Equals(table.Component, StringComparison.OrdinalIgnoreCase));
        // Na folha em branco, o título mostra o nominal e o corredor da
        // ferramenta (precisa deles para ser preenchida à mão).
        return Label(table.Component) + $" — {card?.Reference ?? "—"} · Lote {card?.Lot ?? "—"} · " +
            (table.Rows.Count > 0 ? NominalText(input, card!, table)
                : input.BlankForHandFilling && card is not null ? NominalText(input, card, table)
                : "Sem medições");
    }

    private static string Fmt(decimal v, string pattern = "0.00") => v.ToString(pattern, Pt);

    private static string Avg(decimal? v) => v is { } value ? Fmt(value) : "—";

    private static string Signed(decimal? v) => v is { } value ? DmoNumberFormat.Signed(value) : "—";

    private static IContainer HeaderCell(IContainer c) => c
        .Background(DmoPdfPalette.Soft).BorderBottom(1).BorderColor(DmoPdfPalette.Accent)
        .PaddingHorizontal(3).PaddingVertical(3);
}
