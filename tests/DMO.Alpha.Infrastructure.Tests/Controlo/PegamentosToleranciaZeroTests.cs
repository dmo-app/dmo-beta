using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Tolerância 0 nos Pegamentos (audit externo P1.1, 16/11/2026; Diogo:
/// «tolerancia 0 pode ficar como passa […] mais disso começa a ser o
/// fora»): a medição igual ao nominal passa, qualquer desvio é fora. Com
/// outra tolerância, tocar o limite continua a ser alerta (19/10/2026).
/// </summary>
public sealed class PegamentosToleranciaZeroTests
{
    [Fact]
    public void ToleranciaZero_NominalExato_Passa_QualquerDesvio_EFora()
    {
        var baixo = PegamentosCalculation.CorridorLowerMm(30.00m, 0m);
        var cima = PegamentosCalculation.CorridorUpperMm(30.00m, 0m);

        Assert.False(PegamentosCalculation.IsAlert(30.00m, baixo, cima));
        Assert.True(PegamentosCalculation.IsAlert(30.01m, baixo, cima));
        Assert.True(PegamentosCalculation.IsAlert(29.99m, baixo, cima));
        Assert.False(PegamentosCalculation.IsOvalizacaoAlert(0m, 0m));
        Assert.True(PegamentosCalculation.IsOvalizacaoAlert(0.01m, 0m));
        Assert.True(PegamentosCalculation.IsOvalizacaoAlert(-0.01m, 0m));
    }

    [Fact]
    public void OutraTolerancia_TocarOLimite_ContinuaAlerta()
    {
        Assert.True(PegamentosCalculation.IsAlert(70.20m, 69.80m, 70.20m));
        Assert.True(PegamentosCalculation.IsOvalizacaoAlert(0.20m, 0.20m));
        Assert.False(PegamentosCalculation.IsOvalizacaoAlert(0.19m, 0.20m));
    }
}
