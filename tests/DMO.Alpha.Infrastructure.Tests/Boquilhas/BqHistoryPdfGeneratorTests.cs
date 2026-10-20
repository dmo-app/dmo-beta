using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Boquilhas;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Os 3 GERADORES de PDF do histórico das Boquilhas (a A5 — o padrão da
/// casa Peso/Pegamentos/Resumo, QuestPDF): o gerador SÓ FORMATA o read
/// model que a A4 já compôs — o Input transporta TUDO (nunca recolhe
/// dados, nunca calcula; a regra do Owner 14/10 verbatim: «estes so
/// formatam a informaçao que depois e metida no pdf, isso para facilitar
/// alteraçoes»). A GRELHA (o mais importante): o eixo Y = os dias (o dia
/// da semana + a data), o eixo X = as máquinas B1..C3, a célula com
/// «Saiu N · entrou N» (as PEÇAS movidas — as somas de quantidade do
/// read model). A LISTA: uma máquina / um mês (o mesmo formato).
/// PDFs reais: a cabeça %PDF e o fim %%EOF (o padrão do Peso).
/// </summary>
public sealed class BqHistoryPdfGeneratorTests
{
    private static readonly string[] Machines = ["B1", "B2", "B3", "C1", "C2", "C3"];

    private static BqDailySummaryEntry Entry(
        DateOnly day,
        string machine,
        string reference,
        string? lot,
        int saidas,
        int entradas,
        int saidasQuantidade,
        int entradasQuantidade) =>
        new(day, machine, 1, reference, lot, "202601", "5447T194",
            entradas, saidas, 0,
            entradasQuantidade, saidasQuantidade, 0);

    private static void AssertRealPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 1000, "o PDF tem corpo");
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        var tail = System.Text.Encoding.ASCII.GetString(bytes[^32..]);
        Assert.Contains("%%EOF", tail);
    }

    [Fact]
    public void DailyMatrix_FormatsTheGrid_FromTheA4ReadModel()
    {
        // O EXEMPLO DO OWNER (14/10, verbatim: «B1 segunda feira 12/10/2026
        // B1 T173 lote 8 Saiu 21 entrou 21, B2... por ai fora»): o dia é
        // segunda-feira (12/10/2026), a célula da B1 transporta as PEÇAS
        // movidas (Saiu 21 · entrou 21) — as somas de quantidade que o
        // read model da A4 transporta, formatadas pelo gerador.
        var segundaFeira = new DateOnly(2026, 10, 12);
        Assert.Equal(DayOfWeek.Monday, segundaFeira.DayOfWeek);

        var entries = new[]
        {
            Entry(segundaFeira, "B1", "T173", "8", saidas: 2, entradas: 2,
                saidasQuantidade: 21, entradasQuantidade: 21),
            Entry(segundaFeira, "B2", "T194", "12", saidas: 1, entradas: 0,
                saidasQuantidade: 5, entradasQuantidade: 0)
        };

        var bytes = new BqHistoryPdfGenerator().GenerateDailyMatrix(
            new BqHistoryPdfGenerator.DailyMatrixInput(
                Subtitle: "Período 12/10/2026 — 12/10/2026",
                Entries: entries,
                Machines: Machines,
                GeneratedBy: "4100"));

        AssertRealPdf(bytes);
        var text = PdfText.Of(bytes);
        Assert.Contains("Período 12/10/2026 — 12/10/2026", text);
        Assert.Contains("segunda-feira T173 lote 8 T194 lote 12", text);
        Assert.Contains("Saiu 21 · entrou 21 Saiu 5 · entrou 0", text);
        Assert.Contains("Gerado por 4100", text);
    }

    [Fact]
    public void Pdfs_WithAWarning_StillProduceTheSheet_WithTheWarningOnPaper()
    {
        // O AVISO NO PAPEL (o período cortado, todas as máquinas): o PDF é
        // gerado na mesma e leva a faixa âmbar com o texto do caller.
        var generator = new BqHistoryPdfGenerator();
        var matrix = new BqHistoryPdfGenerator.DailyMatrixInput(
            Subtitle: "Período 01/01/2025 — 01/01/2026",
            Entries: Array.Empty<BqDailySummaryEntry>(),
            Machines: Machines);
        var list = new BqHistoryPdfGenerator.MovementListInput(
            Title: "Boquilhas · Movimentos — Todas as máquinas",
            Subtitle: "Período 01/10/2026 — 31/10/2026 · 0 movimento(s)",
            Movements: Array.Empty<BqHistoryMovementItem>());
        const string warning = "O resumo diário mostra no máximo 366 dias; mostramos os primeiros 366.";

        var matrixWithWarning = generator.GenerateDailyMatrix(matrix with { Warning = warning });
        var listWithWarning = generator.GenerateMovementList(list with { Warning = warning });

        AssertRealPdf(matrixWithWarning);
        AssertRealPdf(listWithWarning);
        // A faixa do aviso acrescenta conteúdo ao papel.
        Assert.True(matrixWithWarning.Length > generator.GenerateDailyMatrix(matrix).Length);
        Assert.True(listWithWarning.Length > generator.GenerateMovementList(list).Length);
        Assert.Contains("Aviso: " + warning, PdfText.Of(matrixWithWarning));
        Assert.Contains("Aviso: " + warning, PdfText.Of(listWithWarning));
        Assert.DoesNotContain("Aviso:", PdfText.Of(generator.GenerateMovementList(list)));
    }

    [Fact]
    public void DailyMatrix_ComMovimentosSemMaquina_AvisaNoPapel()
    {
        // Sweep de 19/10/2026, B-2: a grelha não tem coluna para os
        // movimentos sem máquina; o papel diz quantos ficaram de fora.
        var generator = new BqHistoryPdfGenerator();
        var matrix = new BqHistoryPdfGenerator.DailyMatrixInput(
            Subtitle: "Período 01/08/2026 — 31/08/2026",
            Entries: Array.Empty<BqDailySummaryEntry>(),
            Machines: Machines,
            WithoutMachineWarning: "2 movimentos sem máquina não aparecem na grelha, mas contam nos totais. Estão na vista «Por máquina / Job On».");

        Assert.Contains(
            "Aviso: 2 movimentos sem máquina não aparecem na grelha, mas contam nos totais. Estão na vista «Por máquina / Job On».",
            PdfText.Of(generator.GenerateDailyMatrix(matrix)));
        Assert.DoesNotContain("Aviso:", PdfText.Of(generator.GenerateDailyMatrix(matrix with { WithoutMachineWarning = null })));
    }

    [Fact]
    public void DailyMatrix_WithEmptyPeriod_StillProducesTheSheet()
    {
        var bytes = new BqHistoryPdfGenerator().GenerateDailyMatrix(
            new BqHistoryPdfGenerator.DailyMatrixInput(
                Subtitle: "Período 01/08/2026 — 31/08/2026",
                Entries: Array.Empty<BqDailySummaryEntry>(),
                Machines: Machines));

        AssertRealPdf(bytes);
        Assert.Contains("Período 01/08/2026 — 31/08/2026 Dia B1 B2 B3 C1 C2 C3", PdfText.Of(bytes));
    }

    [Fact]
    public void MovementList_FormatsTheMachineAndMonthLists_FromTheA4ReadModel()
    {
        // O MESMO formato serve os dois PDFs (uma máquina / um mês): o
        // título e o subtítulo são dados do caller; a lista é o read model
        // do ListMovementsAsync (a data real, o reparador, a produção —
        // «—» quando em branco).
        var movements = new[]
        {
            new BqHistoryMovementItem(
                1, 1, "Saida", 21, null,
                DmoBusinessTime.ToUtc(
                    DateTime.SpecifyKind(new DateTime(2026, 10, 12, 9, 30, 0), DateTimeKind.Unspecified)),
                "4100", "T173", "8", "B1", "202601", "5447T194", "Vidroart"),
            new BqHistoryMovementItem(
                2, 2, "Entrada", 3, null,
                DmoBusinessTime.ToUtc(
                    DateTime.SpecifyKind(new DateTime(2026, 10, 12, 15, 0, 0), DateTimeKind.Unspecified)),
                "4100", "T173", "8", "B1", null, null, null)
        };

        var bytes = new BqHistoryPdfGenerator().GenerateMovementList(
            new BqHistoryPdfGenerator.MovementListInput(
                Title: "Boquilhas · Movimentos — Máquina B1",
                Subtitle: "Período 12/10/2026 — 12/10/2026 · 2 movimento(s)",
                Movements: movements,
                GeneratedBy: "4100"));

        AssertRealPdf(bytes);
        // A hora impressa é a de Lisboa, a mesma com que os movimentos foram criados.
        var text = PdfText.Of(bytes);
        Assert.Contains("Boquilhas · Movimentos — Máquina B1", text);
        Assert.Contains("12/10/2026 09:30 T173 · Lote 8 Saída 21 Vidroart B1 202601 · 5447T194", text);
        Assert.Contains("12/10/2026 15:00 T173 · Lote 8 Entrada 3 — B1 — —", text);
    }
    [Fact]
    public void GroupedHistory_PreservesEveryMovementIncludingUnknownMachineAndSeparateLots()
    {
        var at = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var first = new BqHistoryMovementItem(1, 1, "Saida", 4, null, at, null, "T173", "8", "B1", "202601", "5447T173");
        var movements = new[]
        {
            first with { MovementId = 2, OccurredAtUtc = at.AddHours(1), MovementType = "Entrada" },
            first,
            first with { MovementId = 3, MachineCode = "C3" },
            first with { MovementId = 4, BqRepairTraceId = 2, ToolLot = "9" },
            first with { MovementId = 5, BqRepairTraceId = 3, MachineCode = null, ProductionNumber = null }
        };
        var groups = BqHistoryMovementGroup.From(movements);
        Assert.Equal(4, groups.Count);
        Assert.Equal(5, groups.SelectMany(g => g.Movements).Select(m => m.MovementId).Distinct().Count());
        Assert.Equal(new[] { 1, 2 }, groups.Single(g => g.TraceId == 1 && g.MachineCode == "B1").Movements.Select(m => m.MovementId));
        Assert.Single(groups, g => g.MachineCode is null);
        var pdf = new BqHistoryPdfGenerator().GenerateMovementList(new(
            "Boquilhas · Por máquina / Job On", "Todos os dias", movements, GroupByMachineAndTrace: true));
        AssertRealPdf(pdf);
        var text = PdfText.Of(pdf);
        Assert.Contains("Máquina Por indicar · Sem Job On · 5447T173 · BQ T173 · Lote 8", text);
        Assert.Contains("Máquina B1 · Job On 202601 · 5447T173 · BQ T173 · Lote 9", text);
        Assert.Contains("Máquina C3 · Job On 202601 · 5447T173 · BQ T173 · Lote 8", text);
    }

}
