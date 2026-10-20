using System.Globalization;
using DMO.Alpha.Core.Boquilhas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// O GERADOR dos 3 PDFs do histórico das Boquilhas (o primeiro gerador do
/// módulo — o padrão da casa Peso/Pegamentos/Resumo, QuestPDF): o gerador
/// SÓ FORMATA o read model que a A4 já compôs — NUNCA recolhe dados, NUNCA
/// calcula (a regra do Owner 14/10, verbatim: «estes so formatam a
/// informaçao que depois e metida no pdf, isso para facilitar
/// alteraçoes»):
/// 1. A GRELHA (o mais importante): o eixo Y = os DIAS (o dia da semana +
///    a data — «segunda-feira, 12/10/2026»), o eixo X = as máquinas
///    B1..C3; cada célula: a referência/lote com «Saiu N · entrou N» (as
///    PEÇAS movidas — as somas de quantidade que o read model transporta).
/// 2. UMA MÁQUINA / 3. UM MÊS: a lista dos movimentos (o mesmo formato —
///    o título e o subtítulo são dados do caller).
/// Os métodos devolvem os bytes do PDF (o download na folha — o fluxo do
/// histórico não escreve em directório nenhum).
/// </summary>
public sealed class BqHistoryPdfGenerator
{
    static BqHistoryPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly CultureInfo Pt = DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture;

    /// <summary>As máquinas do eixo X da grelha (B1..C3 — as 6 da casa).</summary>
    public static readonly string[] HouseMachines = ["B1", "B2", "B3", "C1", "C2", "C3"];

    /// <summary>
    /// A GRELHA dia×máquina: as entradas do read model da A4
    /// (BqDailySummaryEntry — os agregados do query), o período e as
    /// máquinas do eixo X. Apenas os dias COM movimentos entram no eixo Y
    /// (o mesmo uso da grelha na app). <see cref="WithoutMachineWarning"/>
    /// diz quantos movimentos ficaram de fora por não terem máquina.
    /// </summary>
    public sealed record DailyMatrixInput(
        string Subtitle,
        IReadOnlyList<BqDailySummaryEntry> Entries,
        IReadOnlyList<string> Machines,
        string? GeneratedBy = null,
        string? Warning = null,
        string? WithoutMachineWarning = null);

    /// <summary>
    /// A LISTA dos movimentos (os PDFs de uma máquina e de um mês — o
    /// mesmo formato; o título/subtítulo são dados do caller).
    /// </summary>
    public sealed record MovementListInput(
        string Title,
        string Subtitle,
        IReadOnlyList<BqHistoryMovementItem> Movements,
        string? GeneratedBy = null,
        bool GroupByMachineAndTrace = false,
        string? Warning = null);

    /// <summary>O PDF DA GRELHA (o mais importante).</summary>
    public byte[] GenerateDailyMatrix(DailyMatrixInput input)
    {
        var days = input.Entries
            .GroupBy(e => e.Day)
            .OrderBy(g => g.Key)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(14, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text("Boquilhas · Resumo diário — B1, B2, B3, C1, C2, C3")
                        .FontSize(15).SemiBold().FontColor(DmoPdfPalette.Ink);
                    header.Item().PaddingTop(2).Text(input.Subtitle).FontSize(10).Light();
                    ComposeWarning(header, input.Warning);
                    ComposeWarning(header, input.WithoutMachineWarning);
                    header.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(DmoPdfPalette.Accent);
                });

                page.Content().PaddingVertical(8).Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(110);
                        foreach (var _ in input.Machines)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    // O EIXO X: as máquinas.
                    t.Cell().Background(DmoPdfPalette.Ink).Border(0.5f).BorderColor(DmoPdfPalette.White)
                        .Padding(4).Text("Dia").FontSize(9).SemiBold().FontColor(DmoPdfPalette.White);
                    foreach (var machine in input.Machines)
                    {
                        t.Cell().Background(DmoPdfPalette.Ink).Border(0.5f).BorderColor(DmoPdfPalette.White)
                            .Padding(4).AlignCenter().Text(machine).FontSize(9).SemiBold().FontColor(DmoPdfPalette.White);
                    }

                    // O EIXO Y: os dias com movimentos (o dia da semana + a
                    // data — o Owner: «segunda feira 12/10/2026»).
                    foreach (var day in days)
                    {
                        t.Cell().Border(0.5f).BorderColor(DmoPdfPalette.Line).Padding(4).Column(column =>
                        {
                            column.Item().Text(
                                day.Key.ToString("dddd", Pt)).FontSize(8).Light();
                            column.Item().Text(
                                day.Key.ToString("dd/MM/yyyy", Pt)).FontSize(9).SemiBold();
                        });

                        foreach (var machine in input.Machines)
                        {
                            var cellEntries = day
                                .Where(e => e.MachineCode == machine)
                                .ToList();
                            t.Cell().Border(0.5f).BorderColor(DmoPdfPalette.Line).Padding(4).Column(column =>
                            {
                                if (cellEntries.Count == 0)
                                {
                                    column.Item().Text("—").FontColor(DmoPdfPalette.Mist);
                                    return;
                                }

                                foreach (var entry in cellEntries)
                                {
                                    column.Item().Text(
                                        $"{entry.ToolReference}{(entry.ToolLot is { } lot ? $" lote {lot}" : string.Empty)}")
                                        .FontSize(9).SemiBold();
                                    column.Item().Text(
                                        $"Saiu {entry.SaidasQuantidade} · entrou {entry.EntradasQuantidade}")
                                        .FontSize(9);
                                    if (entry.EntradasSemReparacaoQuantidade > 0)
                                    {
                                        column.Item().Text(
                                            $"sem reparação {entry.EntradasSemReparacaoQuantidade}")
                                            .FontSize(8).FontColor(DmoPdfPalette.Warning);
                                    }
                                }
                            });
                        }
                    }
                });

                ComposeFooter(page, input.GeneratedBy);
            });
        }).GeneratePdf();
    }

    /// <summary>A LISTA dos movimentos (uma máquina / um mês).</summary>
    public byte[] GenerateMovementList(MovementListInput input)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(14, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text(input.Title).FontSize(15).SemiBold().FontColor(DmoPdfPalette.Ink);
                    header.Item().PaddingTop(2).Text(input.Subtitle).FontSize(10).Light();
                    ComposeWarning(header, input.Warning);
                    header.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(DmoPdfPalette.Accent);
                });

                page.Content().PaddingVertical(8).Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(105);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.4f);
                        columns.ConstantColumn(34);
                        columns.RelativeColumn(1.4f);
                        columns.ConstantColumn(38);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(34);
                    });

                    static IContainer HeadCell(IContainer c) => c
                        .Background(DmoPdfPalette.Ink).Border(0.5f).BorderColor(DmoPdfPalette.White)
                        .Padding(4);

                    foreach (var head in new[]
                             {
                                 "Data/hora", "Boquilha / lote", "Movimento", "Qtd.",
                                 "Reparador", "Máquina", "Produção", "Saldo"
                             })
                    {
                        t.Cell().Element(HeadCell).Text(head);
                    }

                    var grouped = BqHistoryMovementGroup.From(input.Movements);
                    var orderedMovements = input.GroupByMachineAndTrace
                        ? grouped.SelectMany(g => g.Movements).ToList() : input.Movements;
                    (string? Machine, int Trace)? previousGroup = null;
                    foreach (var movement in orderedMovements)
                    {
                        var key = (movement.MachineCode, movement.BqRepairTraceId);
                        if (input.GroupByMachineAndTrace && previousGroup != key)
                        {
                            t.Cell().ColumnSpan(8).Background(DmoPdfPalette.Tint).Padding(6).Text(
                                $"Máquina {movement.MachineCode ?? "Por indicar"} · " +
                                (movement.ProductionNumber is { } groupNumber ? $"Job On {groupNumber}" : "Sem Job On") +
                                $" · {movement.Reference} · BQ {movement.ToolReference} · Lote {movement.ToolLot}")
                                .SemiBold();
                            previousGroup = key;
                        }
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(movement.OccurredAtUtc).ToString("g", Pt));
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text($"{movement.ToolReference}{(movement.ToolLot is { } lot ? $" · Lote {lot}" : string.Empty)}");
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(MovementLabel(movement.MovementType));
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(movement.Quantity.ToString(Pt));
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(movement.BqRepairerNameFrozen ?? "—");
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(movement.MachineCode ?? "—");
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(movement.ProductionNumber is string number && movement.Reference is string reference
                                ? $"{number} · {reference}"
                                : movement.ProductionNumber ?? movement.Reference ?? "—");
                        t.Cell().BorderBottom(0.3f).BorderColor(DmoPdfPalette.Line).Padding(4)
                            .Text(movement.Discrepancy is int discrepancy ? discrepancy.ToString(Pt) : "—");
                    }
                });

                ComposeFooter(page, input.GeneratedBy);
            });
        }).GeneratePdf();
    }

    /// <summary>A LINGUAGEM dos movimentos no papel: o gerador não inventa
    /// vocabulário — delega no único tradutor da casa (a A5 repair: o read
    /// model do Histórico fala no NOME do enum, o do registo fala no TOKEN de
    /// armazenamento; os dois têm de sair em PT, nunca o cru visto pelo
    /// operador — «EntradaSemReparacao» nunca pode ir para o papel).</summary>
    private static string MovementLabel(string type) => BqMovementLabels.Of(type);

    /// <summary>
    /// O AVISO no papel (o mesmo âmbar dos avisos da app): o PDF foi gerado,
    /// mas com um ajuste que o leitor tem de saber (o período cortado, todas
    /// as máquinas). O texto vem do caller; o gerador só o formata.
    /// </summary>
    private static void ComposeWarning(ColumnDescriptor header, string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        header.Item().PaddingTop(4).Background(DmoPdfPalette.WarningBackground).BorderLeft(3).BorderColor(DmoPdfPalette.Warning)
            .PaddingVertical(4).PaddingHorizontal(6)
            .Text($"Aviso: {warning}").FontSize(9).FontColor(DmoPdfPalette.WarningText);
    }

    private static void ComposeFooter(PageDescriptor page, string? generatedBy)
    {
        page.Footer().Row(footer =>
        {
            var now = DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.ToString("g", Pt);
            var generated = generatedBy is { } by ? $"Gerado por {by} · a {now}" : $"Gerado em {now}";
            footer.RelativeItem().Text($"Portal DMO · {generated} · Boquilhas").Light();
            footer.RelativeItem().AlignRight().Text("Desenvolvido por Diogo Oliveira").Light();
        });
    }
}
