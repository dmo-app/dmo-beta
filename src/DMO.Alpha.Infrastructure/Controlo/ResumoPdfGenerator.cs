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
        /// <summary>
        /// O estado da aprovação do controlo: aprovado, não aprovado (com a
        /// nota) ou por aprovar (null ou vazio).
        /// </summary>
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
        /// <summary>
        /// O PDF das punções, gravado como ficheiro à parte na mesma pasta da
        /// produção (Diogo, 22/10/2026): o cartão liga-o pelo nome relativo;
        /// a tabela não entra neste PDF. Null sem registo ou no PDF só do Resumo.
        /// </summary>
        string? PuncoesFicheiro = null,
        /// <summary>
        /// Todas as folhas de Pegamentos da produção, pela ordem (
        /// 18/10/2026): uma secção por folha, cada uma com as suas
        /// ferramentas. Null ou vazia = só <see cref="PegamentosDetalhe"/>.
        /// </summary>
        IReadOnlyList<PegamentosSheetReadModel>? PegamentosDetalhes = null,
        /// <summary>
        /// Todos os Pesos da produção, pela ordem, quando há mais do que um
        /// (a CM ou a BQ mudou a meio; 18/10/2026): uma secção por Peso,
        /// cada uma com as suas ferramentas. Null ou vazia = só
        /// <see cref="PesoDocument"/>.
        /// </summary>
        IReadOnlyList<PesoPdfGenerator.Input>? PesoDocuments = null,
        /// <summary>
        /// A secção de cada folha de <see cref="PegamentosDetalhes"/>, pela
        /// mesma ordem, quando o PDF completo deixa de fora folhas sem
        /// medições (19/10/2026). Null = pela posição (1, 2, …).
        /// </summary>
        IReadOnlyList<string>? PegamentosSecoes = null,
        /// <summary>
        /// A decisão do Peso, para a folha dele no PDF completo (sweep de
        /// 19/10/2026, C-3). Null = o estado do controlo. Os Pegamentos não
        /// se aprovam (Diogo, 19/10/2026).
        /// </summary>
        ControloApprovalReadModel? PesoApproval = null);

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
        decimal? NominalWeightGrams = null)
    {
        /// <summary>
        /// Uma linha por Peso quando a produção tem mais do que um (a CM ou a
        /// BQ mudou a meio); null com um só (o cartão fica como sempre). Os
        /// valores acima são os do Peso das ferramentas atuais, senão do mais
        /// recente.
        /// </summary>
        public IReadOnlyList<PesoSheetLine>? Sheets { get; init; }

        /// <summary>A secção do detalhe do Peso destes valores («peso» com um só); null = sem detalhe no PDF.</summary>
        public string? Section { get; init; } = "peso";
    }

    /// <summary>Um Peso no cartão do resumo: as ferramentas, as leituras, a média e a secção do detalhe.</summary>
    public sealed record PesoSheetLine(
        string Label,
        int ReadingCount,
        decimal? AverageGlassWeightGrams,
        decimal? NominalWeightGrams,
        string? Section);

    /// <summary>As ferramentas congeladas num Peso («CM 5447 · Lote 8 · BQ T173 · Lote 4»).</summary>
    public static string ToolsOf(PesoReadModel peso) =>
        $"CM {peso.CmToolReference ?? "—"}{(peso.CmToolLot is { } cmLot ? " · Lote " + cmLot : "")}"
        + $" · BQ {peso.BqToolReference ?? "—"}{(peso.BqToolLot is { } bqLot ? " · Lote " + bqLot : "")}";

    /// <summary>A secção do detalhe do Peso n (1, 2, …): o primeiro continua «peso».</summary>
    public static string PesoSection(int pesoNumber) =>
        pesoNumber <= 1 ? "peso" : $"peso-{pesoNumber}";

    public sealed record PegamentosSummary(
        int MeasurementCount,
        int AlertCount,
        decimal ToleranceMm,
        int NotEvaluableCount = 0)
    {
        /// <summary>
        /// Uma linha por folha quando a produção tem mais do que uma (a CM, a
        /// BQ ou o MF mudou a meio); null com uma só (o cartão fica como
        /// sempre). Os totais acima somam todas.
        /// </summary>
        public IReadOnlyList<PegamentosSheetLine>? Sheets { get; init; }

        /// <summary>A secção para onde o cartão liga («pegamentos» com uma só); null = sem detalhe no PDF.</summary>
        public string? Section { get; init; } = "pegamentos";
    }

    /// <summary>Uma folha de Pegamentos no cartão do resumo: as ferramentas, os totais e a secção do detalhe.</summary>
    public sealed record PegamentosSheetLine(
        string Label,
        int MeasurementCount,
        int AlertCount,
        int NotEvaluableCount,
        decimal ToleranceMm,
        string? Section);

    /// <summary>As ferramentas guardadas numa folha («CM 123 · BQ 456 · MF 789»), sem as que ela não tem.</summary>
    public static string ToolsOf(PegamentosReadModel sheet) => string.Join(" · ", new[]
    {
        sheet.CmToolReference is { } cm ? "CM " + cm : null,
        sheet.BqToolReference is { } bq ? "BQ " + bq : null,
        sheet.MfToolReference is { } mf ? "MF " + mf : null
    }.Where(t => t is not null));

    /// <summary>A secção do detalhe da folha n (1, 2, …): a primeira continua «pegamentos».</summary>
    public static string PegamentosSection(int sheetNumber) =>
        sheetNumber <= 1 ? "pegamentos" : $"pegamentos-{sheetNumber}";

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
    private const string Soft = DmoPdfPalette.Soft;
    // O vermelho do NOK, para o «Não aprovado».
    private const string NotApproved = DmoPdfPalette.Nok;

    private static IDocument CreateDocument(Input input) => Document.Create(container =>
    {
        var hasPeso = input.PesoDocument is not null || input.PesoDetalhe is not null
            || input.PesoDocuments is { Count: > 0 };
        var hasPegamentos = input.PegamentosDocument is not null || input.PegamentosDetalhe?.Pegamentos is not null
            || input.PegamentosDetalhes?.Any(d => d.Pegamentos is not null) == true;

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

                SectionTitle(content.Item().PaddingTop(14), "Controlo");
                content.Item().PaddingTop(7).Row(row =>
                {
                    row.Spacing(8);
                    PesoCard(row.RelativeItem(), input, hasPeso);
                    PegamentosCard(row.RelativeItem(), input.Pegamentos, hasPegamentos);
                    if (input.Puncoes is { } puncoes)
                        PuncoesCard(row.RelativeItem(), puncoes, input.PuncoesFicheiro);
                });

                // AS FERRAMENTAS da combinação.
                SectionTitle(content.Item().PaddingTop(14), "Ferramenta");
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

                SectionTitle(content.Item().PaddingTop(14), "Verificação");
                var areaStates = input.AreaStates ?? [];
                content.Item().PaddingTop(7).Row(row =>
                {
                    row.Spacing(6);
                    foreach (var slot in new[] { "CM", "BQ", "MF", "PU", "CS" })
                    {
                        var state = areaStates.FirstOrDefault(a => a.Area == slot);
                        var (label, color, background, border) = state?.Status switch
                        {
                            "OK" => ("OK", DmoPdfPalette.Ok, DmoPdfPalette.OkBackground, DmoPdfPalette.OkBorder),
                            "NOK" => ("NOK", DmoPdfPalette.Nok, DmoPdfPalette.NokBackground, DmoPdfPalette.NokBorder),
                            _ => ("não registado", Muted, DmoPdfPalette.White, Line),
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
                                    var background = index++ % 2 == 0 ? DmoPdfPalette.White : DmoPdfPalette.Soft;
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
                            // Os três estados (sweep de 19/10/2026, C-3): o não
                            // aprovado diz quem, quando e a nota.
                            if (input.Approval is { ApprovedByOperatorId: not null } aprovado)
                            {
                                t.Cell().Element(c => BodyCell(c, DmoPdfPalette.White)).Text(aprovado.Quem).FontSize(9);
                                t.Cell().Element(c => BodyCell(c, DmoPdfPalette.White)).Text(aprovado.ApprovedAtUtc is { } approvedAt ? DmoBusinessTime.ToLocal(approvedAt).ToString("g", Pt) : "—").FontSize(9);
                            }
                            else if (input.Approval is { NaoAprovado: true } naoAprovado)
                            {
                                t.Cell().ColumnSpan(2).Element(c => BodyCell(c, DmoPdfPalette.White)).Text(naoAprovado.Frase(Pt)).FontSize(9).FontColor(NotApproved);
                            }
                            else
                            {
                                t.Cell().ColumnSpan(2).Element(c => BodyCell(c, DmoPdfPalette.White)).Text("Por aprovar.").FontSize(9).FontColor(Muted);
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
        // Uma secção por Peso, cada um com as suas ferramentas (18/10/2026);
        // com um só, como sempre.
        if (input.PesoDocuments is { Count: > 0 } pesos)
        {
            foreach (var pesoDocument in pesos)
                new PesoPdfGenerator().Compose(container, pesoDocument, combined: true);
        }
        else
        {
            var pesoInput = input.PesoDocument ?? (input.PesoDetalhe is { } peso
                ? PesoPdfGenerator.FromModel(peso, input.ProductionNumber, input.Reference, input.Machine,
                    "", input.PesoApproval ?? input.Approval, input.GeneratedBy, inicio: input.InicioEm, fim: input.FimEm) : null);
            if (pesoInput is not null) new PesoPdfGenerator().Compose(container, pesoInput, combined: true);
        }
        // Uma secção por folha de Pegamentos, cada uma com as suas
        // ferramentas (18/10/2026); com uma só, como sempre.
        if (input.PegamentosDocument is null && input.PegamentosDetalhes is { Count: > 0 } folhas)
        {
            foreach (var (folha, numero) in folhas.Select((f, i) => (f, i + 1)))
            {
                new PegamentosPdfGenerator().Compose(container, PegamentosPdfGenerator.FromSheet(folha, input.GeneratedBy)
                    with { SectionName = input.PegamentosSecoes?.ElementAtOrDefault(numero - 1) ?? PegamentosSection(numero) }, combined: true);
            }
        }
        else
        {
            var pegInput = input.PegamentosDocument ?? (input.PegamentosDetalhe is { } peg
                ? PegamentosPdfGenerator.FromSheet(peg, input.GeneratedBy) : null);
            if (pegInput is not null) new PegamentosPdfGenerator().Compose(container, pegInput, combined: true);
        }
    });

    private static void PesoCard(IContainer container, Input input, bool linked)
    {
        var average = input.Peso?.AverageGlassWeightGrams;
        var nominal = input.Peso?.NominalWeightGrams
            ?? input.PesoDocument?.NominalWeightGrams ?? input.PesoDetalhe?.NominalWeightGrams;
        container.Element(c => Box(c)).Padding(12).MinHeight(128)
            .DefaultTextStyle(x => x.FontColor(Ink)).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text("Peso").FontSize(10).SemiBold();
                    if (linked && (input.Peso is null ? "peso" : input.Peso.Section) is { } pesoSection) r.AutoItem().SectionLink(pesoSection).Text("Ver detalhe →").FontSize(8).FontColor(Blue).Underline();
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
                c.Item().Text("Peso médio do vidro").FontSize(8).FontColor(Muted);
                if (input.Peso is { } peso)
                    c.Item().PaddingTop(8).Text($"{peso.ReadingCount} leitura(s) · água a {Fmt(peso.WaterTemperatureC, "0.#")} °C")
                        .FontSize(8).FontColor(Muted);
                var deviation = PesoCalculation.NominalDeviation(average, nominal);
                if (nominal is { } n)
                    c.Item().PaddingTop(2).Text("Nominal " + Fmt(n) + " g" + (deviation.Grams is { } g
                        ? " · " + Signed(g) + " g" + (deviation.Percent is { } p ? " (" + Signed(p) + " %)" : "") : ""))
                        .FontSize(8).FontColor(Muted);
                // Mais do que um Peso (a CM ou a BQ mudou a meio): uma linha
                // por Peso, com as ferramentas e a média dele, para nenhum
                // ficar escondido.
                foreach (var linha in input.Peso?.Sheets ?? Array.Empty<PesoSheetLine>())
                {
                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text(linha.Label).FontSize(7.5f).SemiBold();
                        if (linked && linha.Section is { } linhaSection) r.AutoItem().PaddingLeft(4).SectionLink(linhaSection).Text("Ver →").FontSize(7.5f).FontColor(Blue).Underline();
                    });
                    var linhaDesvio = PesoCalculation.NominalDeviation(linha.AverageGlassWeightGrams, linha.NominalWeightGrams);
                    c.Item().Text($"{linha.ReadingCount} leitura(s)"
                            + (linha.AverageGlassWeightGrams is { } media ? " · média " + Fmt(media) + " g" : "")
                            + (linhaDesvio.Grams is { } dg ? " · " + Signed(dg) + " g do nominal" : ""))
                        .FontSize(7.5f).FontColor(Muted);
                }
            });
    }

    private static void PegamentosCard(IContainer container, PegamentosSummary? summary, bool linked)
    {
        var (status, color, background) = summary is not { MeasurementCount: > 0 }
            ? ("Sem medições registadas", Muted, Soft)
            : summary.AlertCount > 0 ? ("Existem medições fora das tolerâncias", DmoPdfPalette.Nok, DmoPdfPalette.NokBackground)
            : summary.NotEvaluableCount > 0 ? ("Existem medições não avaliáveis", DmoPdfPalette.NotEvaluable, DmoPdfPalette.NotEvaluableBackground)
            : ("Todas as medições dentro das tolerâncias", DmoPdfPalette.Ok, DmoPdfPalette.OkBackground);
        container.Element(c => Box(c, background)).Padding(12).MinHeight(128).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Text("Pegamentos").FontSize(10).SemiBold();
                if (linked && (summary is null ? "pegamentos" : summary.Section) is { } pegSection) r.AutoItem().SectionLink(pegSection).Text("Ver detalhe →").FontSize(8).FontColor(Blue).Underline();
            });
            c.Item().PaddingTop(8).Text(status).FontSize(13).SemiBold().FontColor(color);
            if (summary is { MeasurementCount: > 0 })
            {
                c.Item().PaddingTop(10).Row(r =>
                {
                    Stat(r.RelativeItem(), "MEDIÇÕES", summary.MeasurementCount.ToString(Pt), Ink);
                    Stat(r.RelativeItem(), "EM ALERTA", summary.AlertCount.ToString(Pt), summary.AlertCount > 0 ? DmoPdfPalette.Nok : Ink);
                    Stat(r.RelativeItem(), "NÃO AVALIÁVEIS", summary.NotEvaluableCount.ToString(Pt), summary.NotEvaluableCount > 0 ? DmoPdfPalette.NotEvaluable : Ink);
                });
                if (summary.Sheets is null)
                    c.Item().PaddingTop(6).Text("Tolerância ± " + Fmt(summary.ToleranceMm) + " mm").FontSize(8).FontColor(Muted);
                // Mais do que uma folha (a CM, a BQ ou o MF mudou a meio):
                // uma linha por folha, com a tolerância dela, para nenhum
                // alerta ficar escondido.
                foreach (var folha in summary.Sheets ?? Array.Empty<PegamentosSheetLine>())
                {
                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text(folha.Label).FontSize(7.5f).SemiBold();
                        if (linked && folha.Section is { } folhaSection) r.AutoItem().PaddingLeft(4).SectionLink(folhaSection).Text("Ver →").FontSize(7.5f).FontColor(Blue).Underline();
                    });
                    c.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                        t.Span($"{folha.MeasurementCount} medição(ões)");
                        if (folha.AlertCount > 0) t.Span($" · {folha.AlertCount} em alerta").FontColor(DmoPdfPalette.Nok);
                        if (folha.NotEvaluableCount > 0) t.Span($" · {folha.NotEvaluableCount} não avaliável(eis)").FontColor(DmoPdfPalette.NotEvaluable);
                        t.Span(" · ± " + Fmt(folha.ToleranceMm) + " mm");
                    });
                }
            }
        });
    }

    private static void PuncoesCard(IContainer container, PuncoesSummary summary, string? ficheiro)
    {
        var pending = summary.Total * 2 - summary.PunchSeen - summary.CoolerSeen;
        var (status, color, background) = summary.Total == 0
            ? ("Sem punções registadas", Muted, Soft)
            : pending > 0 ? ("Existem punções por verificar", DmoPdfPalette.ToCheck, DmoPdfPalette.ToCheckBackground)
            : ("Punções e arrefecedores vistos", DmoPdfPalette.Ok, DmoPdfPalette.OkBackground);
        container.Element(c => Box(c, background)).Padding(12).MinHeight(128).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Text("PU NNPB").FontSize(10).SemiBold();
                // O PDF das punções é outro ficheiro, na mesma pasta: a ligação abre-o pelo nome.
                if (ficheiro is not null) r.AutoItem().Hyperlink(Uri.EscapeDataString(ficheiro)).Text("Abrir PDF das punções →").FontSize(8).FontColor(Blue).Underline();
            });
            c.Item().PaddingTop(8).Text(status).FontSize(13).SemiBold().FontColor(color);
            if (summary.Total > 0)
            {
                c.Item().PaddingTop(10).Row(r =>
                {
                    Stat(r.RelativeItem(), "TOTAL", summary.Total.ToString(Pt), Ink);
                    Stat(r.RelativeItem(), "PU VISTAS", summary.PunchSeen.ToString(Pt), summary.PunchSeen < summary.Total ? DmoPdfPalette.ToCheck : Ink);
                    Stat(r.RelativeItem(), "ARREF. VISTOS", summary.CoolerSeen.ToString(Pt), summary.CoolerSeen < summary.Total ? DmoPdfPalette.ToCheck : Ink);
                });
            }
        });
    }

    // A etiqueta tem sempre a altura de duas linhas, para os números de uma
    // linha ficarem alinhados («NÃO AVALIÁVEIS» parte em duas).
    private static void Stat(IContainer container, string label, string value, string color) => container.Column(c =>
    {
        c.Item().Height(16).AlignBottom().Text(label).FontSize(6.5f).FontColor(Muted);
        c.Item().PaddingTop(2).Text(value).FontSize(16).SemiBold().FontColor(color);
    });

    private static void ApprovalCard(IContainer container, ControloApprovalReadModel? approval)
    {
        // Os três estados (sweep de 19/10/2026, C-3): aprovado, não aprovado
        // (com quem, quando e a nota) e por aprovar.
        var (estado, fundo, _, cor) = approval switch
        {
            { ApprovedByOperatorId: not null } => ("Aprovado", DmoPdfPalette.OkBackground, DmoPdfPalette.Approved, DmoPdfPalette.Approved),
            { NaoAprovado: true } => ("Não aprovado", DmoPdfPalette.NokBackground, DmoPdfPalette.NokBorder, NotApproved),
            _ => ("Sem registo", DmoPdfPalette.ToApproveBackground, DmoPdfPalette.ToApproveBorder, DmoPdfPalette.ToApprove)
        };
        container.Element(c => Box(c, fundo)).Padding(9).Column(c =>
        {
            c.Item().Text("APROVAÇÃO").FontSize(7).FontColor(Muted);
            c.Item().PaddingTop(5).Text(estado).FontSize(15).SemiBold().FontColor(cor);
            c.Item().PaddingTop(4).Text(approval?.Detalhe(Pt) ?? "Por aprovar").FontSize(7).FontColor(Muted);
        });
    }

    private static string ToolLabel(string slot) => slot switch
    {
        "CM" => "CM · CONTRA-MOLDE",
        "MF" => "MF · MOLDE FINAL",
        "BQ" => "BQ · BOQUILHA",
        _ => slot,
    };

    private static IContainer Box(IContainer c, string background = DmoPdfPalette.White, string border = Line) =>
        c.Background(background).BorderTop(.7f).BorderColor(Line);

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
