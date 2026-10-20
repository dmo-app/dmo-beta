using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O WORKED EXAMPLE REAL do PDF-alvo dos Pegamentos («Pegamentos_v1_14» —
/// 5447T173, produção 202610, 2026-10-10; extraído em
/// dmo-pdf\pegamentos-real\text.txt): os gargalos/nominais REAIS do
/// conjunto (CM 24,80 · BQ 25,00 · MF 25,20 — corredores ±0,20 SOBREPOSTOS:
/// o nominal do vizinho é fronteira) e as 12 medições reais da folha.
/// A regra do Owner (2026-10-10: «qualquer uma das mediçoes que ultrapassa
/// o minimo ou maximo deve dar alerta e nao apenas o delta/media»):
/// QUALQUER valor individual que ATINGA ou CRUZE min/max alerta — as
/// células individuais, não apenas a ovalização/média. O alvo marca ⚠ em
/// 3 médias (CM 45 25,06 · BQ 1 25,26 · MF 21 25,48); a regra apanha
/// TAMBÉM as células individuais que o próprio alvo não marca (CM nº 3
/// costura 25,01 · MF nº 22 contra 25,42 · MF nº 42 contra 25,45) — a
/// média nunca oculta uma medição individual fora do corredor.
/// </summary>
public sealed class PegamentosRealTargetTests
{
    // Os componentes reais do conjunto 5447T173 (tolerância congelada ±0,20):
    // CM 24,80 → corredor 24,60–25,00 (o upper = o nominal da BQ);
    // BQ 25,00 → corredor 24,80–25,20 (o upper = o nominal do MF);
    // MF 25,20 → corredor 25,00–25,40.
    private const decimal Tolerance = 0.20m;
    private const decimal CmNominal = 24.80m;
    private const decimal BqNominal = 25.00m;
    private const decimal MfNominal = 25.20m;

    private static PegamentosRowEvaluation Eval(decimal nominal, decimal costura, decimal contra) =>
        PegamentosCalculation.EvaluateRow(nominal, Tolerance, costura, contra);

    private static bool Alerts(PegamentosRowEvaluation evaluation, PegamentosEvaluatedAxis axis) =>
        evaluation.Values.Single(v => v.Axis == axis).Alert;

    [Fact]
    public void RealCm_Measurements_IndividualCells_AlertBeyondTheTargetsOwnMarks()
    {
        // NOTA DE EXIBIÇÃO: o backend guarda o DECIMAL EXATO ((D1+D2)/2,
        // manual §7 L312-314); o PDF-alvo exibe o arredondamento float do
        // gerador HTML (24,825 → «24,82», 24,895 → «24,90»). Os asserts
        // seguem o decimal exato — a exibição da folha («0.00» pt-PT,
        // half-away-from-zero) mostra 24,83/24,90; uma diferença de 1
        // centésimo conhecida, decidível na refinação se o Owner preferir
        // o arredondamento do alvo.
        // nº 21 (24,84/24,81 → média exata 24,825): tudo dentro, sem alerta.
        var n21 = Eval(CmNominal, 24.84m, 24.81m);
        Assert.Equal(0.03m, n21.OvalizacaoMm);
        Assert.Equal(24.825m, n21.MediaMm);
        Assert.False(n21.HasAlert);

        // nº 2 (24,87/24,92 → média exata 24,895): dentro, sem alerta.
        var n2 = Eval(CmNominal, 24.87m, 24.92m);
        Assert.Equal(-0.05m, n2.OvalizacaoMm);
        Assert.Equal(24.895m, n2.MediaMm);
        Assert.False(n2.HasAlert);

        // nº 3 (25,01/24,97 → média exata 24,99 DENTRO): a COSTURA individual
        // 25,01 atingiu/cruzou o limite superior 25,00 (o nominal da BQ —
        // fronteira partilhada) — a célula alerta mesmo com a média dentro.
        // O PDF-alvo NÃO marca esta linha: a regra do Owner apanha-na.
        var n3 = Eval(CmNominal, 25.01m, 24.97m);
        Assert.Equal(24.99m, n3.MediaMm);
        Assert.True(Alerts(n3, PegamentosEvaluatedAxis.Costura));
        Assert.False(Alerts(n3, PegamentosEvaluatedAxis.ContraCostura));
        Assert.False(Alerts(n3, PegamentosEvaluatedAxis.Media));
        Assert.True(n3.HasAlert);

        // nº 45 (25,08/25,04 → média 25,06 ⚠ do alvo: «ultrapassou Nominal
        // da Boquilha em 0.06»): costura, contra costura E média, todas
        // fora do corredor 24,60–25,00.
        var n45 = Eval(CmNominal, 25.08m, 25.04m);
        Assert.Equal(25.06m, n45.MediaMm);
        Assert.True(Alerts(n45, PegamentosEvaluatedAxis.Costura));
        Assert.True(Alerts(n45, PegamentosEvaluatedAxis.ContraCostura));
        Assert.True(Alerts(n45, PegamentosEvaluatedAxis.Media));
        Assert.True(n45.HasAlert);
    }

    [Fact]
    public void RealBq_Measurements_SharedBoundaryWithMf()
    {
        // nº 23 (25,01/25,07 → 25,04): dentro de 24,80–25,20.
        var n23 = Eval(BqNominal, 25.01m, 25.07m);
        Assert.Equal(25.04m, n23.MediaMm);
        Assert.False(n23.HasAlert);

        // nº 45 (24,98/24,97 → média exata 24,975): dentro.
        var n45 = Eval(BqNominal, 24.98m, 24.97m);
        Assert.Equal(24.975m, n45.MediaMm);
        Assert.False(n45.HasAlert);

        // nº 1 (25,21/25,31 → média 25,26 ⚠ do alvo: «ultrapassou Nominal
        // do Molde final em 0.06»): costura, contra E média fora de
        // 24,80–25,20 (o upper = o nominal do MF — fronteira partilhada).
        var n1 = Eval(BqNominal, 25.21m, 25.31m);
        Assert.Equal(25.26m, n1.MediaMm);
        Assert.True(Alerts(n1, PegamentosEvaluatedAxis.Costura));
        Assert.True(Alerts(n1, PegamentosEvaluatedAxis.ContraCostura));
        Assert.True(Alerts(n1, PegamentosEvaluatedAxis.Media));

        // nº 23-2 (25,05/25,00 → média exata 25,025): dentro.
        var n23b = Eval(BqNominal, 25.05m, 25.00m);
        Assert.Equal(25.025m, n23b.MediaMm);
        Assert.False(n23b.HasAlert);
    }

    [Fact]
    public void RealMf_Measurements_IndividualContraCells_TheTargetDoesNotMark()
    {
        // nº 63 (25,21/25,24 → média exata 25,225): dentro de 25,00–25,40.
        var n63 = Eval(MfNominal, 25.21m, 25.24m);
        Assert.Equal(25.225m, n63.MediaMm);
        Assert.False(n63.HasAlert);

        // nº 21 (25,47/25,50 → média exata 25,485 ⚠ do alvo: «ultrapassou
        // Limite máximo do Molde final em 0.09»): todas as células fora.
        var n21 = Eval(MfNominal, 25.47m, 25.50m);
        Assert.Equal(25.485m, n21.MediaMm);
        Assert.True(Alerts(n21, PegamentosEvaluatedAxis.Costura));
        Assert.True(Alerts(n21, PegamentosEvaluatedAxis.ContraCostura));
        Assert.True(Alerts(n21, PegamentosEvaluatedAxis.Media));

        // nº 22 (25,34/25,42 → média 25,38 DENTRO): a CONTRA COSTURA
        // individual 25,42 cruzou o limite superior 25,40 — a célula alerta
        // mesmo com a média dentro. O PDF-alvo NÃO marca esta linha.
        var n22 = Eval(MfNominal, 25.34m, 25.42m);
        Assert.Equal(25.38m, n22.MediaMm);
        Assert.False(Alerts(n22, PegamentosEvaluatedAxis.Costura));
        Assert.True(Alerts(n22, PegamentosEvaluatedAxis.ContraCostura));
        Assert.False(Alerts(n22, PegamentosEvaluatedAxis.Media));
        Assert.True(n22.HasAlert);

        // nº 42 (25,09/25,45 → média 25,27 DENTRO, ovalização −0,36 — a
        // maior da folha): a CONTRA 25,45 cruzou 25,40 — célula em alerta
        // individual; o sinal da ovalização fica preservado (−0,36 é um
        // facto distinto de +0,36).
        var n42 = Eval(MfNominal, 25.09m, 25.45m);
        Assert.Equal(-0.36m, n42.OvalizacaoMm);
        Assert.Equal(25.27m, n42.MediaMm);
        Assert.True(Alerts(n42, PegamentosEvaluatedAxis.ContraCostura));
        Assert.False(Alerts(n42, PegamentosEvaluatedAxis.Media));
        Assert.True(n42.HasAlert);
    }

    [Fact]
    public void RealCorridors_AreSharedBoundaries()
    {
        // OS CORREDORES SOBREPOSTOS (a geometria real do conjunto): o
        // upper de cada componente é o nominal do vizinho — CM 24,60–25,00,
        // BQ 24,80–25,20, MF 25,00–25,40 (CM −0,20 e MF +0,20 são as
        // fronteiras externas do mapa).
        Assert.Equal(24.60m, PegamentosCalculation.CorridorLowerMm(CmNominal, Tolerance));
        Assert.Equal(25.00m, PegamentosCalculation.CorridorUpperMm(CmNominal, Tolerance)); // = nominal da BQ
        Assert.Equal(24.80m, PegamentosCalculation.CorridorLowerMm(BqNominal, Tolerance));   // = nominal da CM
        Assert.Equal(25.20m, PegamentosCalculation.CorridorUpperMm(BqNominal, Tolerance)); // = nominal do MF
        Assert.Equal(25.00m, PegamentosCalculation.CorridorLowerMm(MfNominal, Tolerance));   // = nominal da BQ
        Assert.Equal(25.40m, PegamentosCalculation.CorridorUpperMm(MfNominal, Tolerance));

        // O relatório do alvo: as médias globais medidas CM 24,94 · BQ
        // 25,07 · MF 25,34 (a média das médias das linhas, decimal exato —
        // o alvo exibe 25,07 para a BQ por arredondamento float do gerador
        // HTML; o backend decimal exato é 25,075, exibido como 25,08).
        Assert.Equal(24.9425m, (24.82m + 24.90m + 24.99m + 25.06m) / 4m);
        Assert.Equal(25.075m, (25.04m + 24.98m + 25.26m + 25.02m) / 4m);
        Assert.Equal(25.34m, (25.23m + 25.48m + 25.38m + 25.27m) / 4m);
    }
}
