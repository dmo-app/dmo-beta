using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova as fórmulas canónicas dos Pegamentos detidas pelo backend (spec
/// Controlo Create — Pegamentos L94-101; manual §7 L306-326, L342-354):
/// ovalização com SINAL PRESERVADO (decisão do Owner: «+0,3 e −0,3 são
/// fatos distintos»), média do gesto em cruz (= costura no eixo único),
/// corredor nominal ± tolerância com a regra de fronteira «atingir,
/// cruzar ou igualar o limite cria alerta» e o estado NotEvaluable sem
/// nominal inventado.
/// </summary>
public sealed class PegamentosCalculationTests
{
    [Fact]
    public void Ovalizacao_CosturaMinusContraCostura_SignPreserved()
    {
        // Ovalização = Costura − Contra costura (manual L308) com o sinal
        // preservado (manual L310): +0,20 e −0,20 são fatos distintos.
        Assert.Equal(0.20m, PegamentosCalculation.OvalizacaoMm(70.10m, 69.90m));
        Assert.Equal(-0.20m, PegamentosCalculation.OvalizacaoMm(69.90m, 70.10m));
        Assert.Equal(0.40m, PegamentosCalculation.OvalizacaoMm(70.20m, 69.80m));
        Assert.Equal(-0.40m, PegamentosCalculation.OvalizacaoMm(69.80m, 70.20m));
    }

    [Fact]
    public void Ovalizacao_SingleAxis_Undefined()
    {
        // Contra costura ausente: a ovalização fica indefinida, não
        // calculada — nunca um segundo eixo fabricado (spec L118, L128-131;
        // manual L322).
        Assert.Null(PegamentosCalculation.OvalizacaoMm(70.10m, null));
    }

    [Fact]
    public void Media_BothAxes_ArithmeticMean()
    {
        // Média = (Costura + Contra costura) / 2 (manual L312-314).
        Assert.Equal(70.00m, PegamentosCalculation.MediaMm(70.10m, 69.90m));
        Assert.Equal(70.00m, PegamentosCalculation.MediaMm(70.20m, 69.80m));
    }

    [Fact]
    public void Media_SingleAxis_EqualsCostura()
    {
        // Eixo único: Média = Costura (spec L132-134 caso CM; manual L323).
        Assert.Equal(70.10m, PegamentosCalculation.MediaMm(70.10m, null));
    }

    [Fact]
    public void Corridor_NominalPlusMinusTolerance()
    {
        // Corredor = nominal − tolerância até nominal + tolerância (manual
        // L342-346), com a tolerância default do Owner ±0,20 e tolerâncias
        // configuradas substitutas (CONFIGURATION.md L20).
        Assert.Equal(0.20m, PegamentosCalculation.DefaultToleranceMm);
        Assert.Equal(69.80m, PegamentosCalculation.CorridorLowerMm(70.00m, 0.20m));
        Assert.Equal(70.20m, PegamentosCalculation.CorridorUpperMm(70.00m, 0.20m));
        Assert.Equal(69.70m, PegamentosCalculation.CorridorLowerMm(70.00m, 0.30m));
        Assert.Equal(70.30m, PegamentosCalculation.CorridorUpperMm(70.00m, 0.30m));
    }

    [Theory]
    [InlineData(69.80)] // igualdade no limite inferior = alerta
    [InlineData(70.20)] // igualdade no limite superior = alerta
    [InlineData(69.79)] // cruzou o limite inferior = alerta
    [InlineData(70.21)] // cruzou o limite superior = alerta
    [InlineData(69.50)] // bem abaixo = alerta
    [InlineData(70.50)] // bem acima = alerta
    public void IsAlert_ReachingOrCrossingEitherBoundary_Alerts(decimal valueMm)
    {
        // Atingir o limite, cruzá-lo ou igualá-lo cria alerta (manual
        // L348-354: «atingir o limite cria alerta; cruzar o limite cria
        // alerta; igualdade no limite conta como alerta»).
        Assert.True(PegamentosCalculation.IsAlert(valueMm, 69.80m, 70.20m));
    }

    [Theory]
    [InlineData(69.81)] // estritamente dentro (1 décimo acima do inferior)
    [InlineData(70.19)] // estritamente dentro
    [InlineData(70.00)] // no centro = nominal
    public void IsAlert_StrictlyInside_NoAlert(decimal valueMm)
    {
        Assert.False(PegamentosCalculation.IsAlert(valueMm, 69.80m, 70.20m));
    }

    [Fact]
    public void EvaluateRow_BothAxes_IndependentVerification_CosturaAlertDoesNotHideBehindAverage()
    {
        // Costura, contra costura e média verificadas independentemente
        // (manual L338): uma costura exatamente no limite superior alerta
        // mesmo com a média dentro — a média não pode ocultar uma medição
        // individual no limite (spec L109).
        var evaluation = PegamentosCalculation.EvaluateRow(70.00m, 0.20m, 70.20m, 69.80m);

        Assert.Equal(0.40m, evaluation.OvalizacaoMm);
        Assert.Equal(70.00m, evaluation.MediaMm);
        Assert.Equal(69.80m, evaluation.CorridorLowerMm);
        Assert.Equal(70.20m, evaluation.CorridorUpperMm);
        Assert.False(evaluation.IsNotEvaluable);
        Assert.True(evaluation.HasAlert);

        var costura = evaluation.Values.Single(v => v.Axis == PegamentosEvaluatedAxis.Costura);
        Assert.True(costura.Alert);
        var contraCostura = evaluation.Values.Single(v => v.Axis == PegamentosEvaluatedAxis.ContraCostura);
        Assert.True(contraCostura.Alert);
        var media = evaluation.Values.Single(v => v.Axis == PegamentosEvaluatedAxis.Media);
        Assert.False(media.Alert);
    }

    [Fact]
    public void EvaluateRow_SingleAxis_CorridorAppliesToMediaWhichEqualsCostura()
    {
        // Eixo único (contra costura ausente): ovalização indefinida,
        // média = costura e o corredor aplica-se à média nesse caso
        // (manual L318-324).
        var evaluation = PegamentosCalculation.EvaluateRow(70.00m, 0.20m, 70.10m, null);

        Assert.Null(evaluation.OvalizacaoMm);
        Assert.Equal(70.10m, evaluation.MediaMm);
        Assert.Equal(2, evaluation.Values.Count); // costura + média (não há contra costura)
        Assert.DoesNotContain(evaluation.Values, v => v.Axis == PegamentosEvaluatedAxis.ContraCostura);
        Assert.False(evaluation.HasAlert);
    }

    [Fact]
    public void EvaluateRow_NoNominal_NotEvaluable_NoInventedValues()
    {
        // Sem nominal gargalo a linha é NotEvaluable: sem corredor, sem
        // valores avaliados — nenhum nominal é inventado (decisão do
        // Owner; PDF §10 L486).
        var evaluation = PegamentosCalculation.EvaluateRow(null, 0.20m, 70.10m, 69.90m);

        Assert.True(evaluation.IsNotEvaluable);
        Assert.Empty(evaluation.Values);
        Assert.False(evaluation.HasAlert);
        // Os factos observados continuam derivados (ovalização/média):
        // a linha regista-se, apenas não é avaliada.
        Assert.Equal(0.20m, evaluation.OvalizacaoMm);
        Assert.Equal(70.00m, evaluation.MediaMm);
    }

    [Fact]
    public void EvaluateRow_ConfiguredToleranceReplacesDefault()
    {
        // A tolerância configurada substitui o default no corredor
        // (CONFIGURATION.md L20): com ±0,30, 70.25 está DENTRO; com o
        // default ±0,20, 70.25 está FORA.
        var withConfigured = PegamentosCalculation.EvaluateRow(70.00m, 0.30m, 70.25m, null);
        Assert.False(withConfigured.HasAlert);

        var withDefault = PegamentosCalculation.EvaluateRow(70.00m, 0.20m, 70.25m, null);
        Assert.True(withDefault.HasAlert);
    }

    [Theory]
    [InlineData(70.11, 69.89)] // +0,22: os dois eixos e a média dentro do corredor
    [InlineData(69.89, 70.11)] // -0,22: o sinal não muda nada
    [InlineData(70.10, 69.90)] // +0,20 com 0,20: tocar o limite conta (cartão do Diogo, 19/10/2026)
    [InlineData(69.90, 70.10)] // -0,20 com 0,20
    public void EvaluateRow_OvalizacaoAtOrAboveTolerance_AlertsEvenWithEveryAxisInside(decimal costura, decimal contra)
    {
        // Diogo, 19/10/2026 (F8): «o alerta deve existir se qualquer medição
        // ultrapassar os 0.2, seja -0.2 ou +0.2». Antes, a linha ficava
        // «dentro» e a ovalização só ficava a vermelho na página.
        var evaluation = PegamentosCalculation.EvaluateRow(70.00m, 0.20m, costura, contra);

        Assert.True(evaluation.HasAlert);
        Assert.Contains(evaluation.Values, v => v.Axis == PegamentosEvaluatedAxis.Ovalizacao && v.Alert);
        Assert.DoesNotContain(evaluation.Values, v => v.Axis != PegamentosEvaluatedAxis.Ovalizacao && v.Alert);
    }

    [Theory]
    [InlineData(70.09, 69.91, 0.20)] // +0,18 com 0,20
    [InlineData(69.91, 70.09, 0.20)] // -0,18 com 0,20
    [InlineData(70.11, 69.89, 0.30)] // +0,22 com a tolerância das Definições a 0,30
    public void EvaluateRow_OvalizacaoBelowTolerance_NoAlert(decimal costura, decimal contra, decimal tolerancia)
    {
        var evaluation = PegamentosCalculation.EvaluateRow(70.00m, tolerancia, costura, contra);

        Assert.False(evaluation.HasAlert);
        Assert.Contains(evaluation.Values, v => v.Axis == PegamentosEvaluatedAxis.Ovalizacao && !v.Alert);
    }
}
