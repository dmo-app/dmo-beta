using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a tabela canónica DMO de densidade da água usada pelo Peso: o
/// intervalo suportado 5–35 °C, o arredondamento ao grau inteiro (sem
/// interpolação — apenas a linha exata do grau) e os valores de referência
/// por grau inteiro.
/// </summary>
public sealed class PesoWaterDensityTableTests
{
    [Theory]
    [InlineData(5, 0.99888)]
    [InlineData(10, 0.99863)]
    [InlineData(15, 0.99805)]
    [InlineData(20, 0.99717)]
    [InlineData(25, 0.99603)]
    [InlineData(30, 0.99465)] // CETIE DT9; a folha «Temp» tinha 0.99485
    [InlineData(35, 0.99305)]
    public void Resolve_ReturnsCanonicalDensityForWholeDegree(int degree, decimal expected)
    {
        var density = WaterDensityTable.Resolve(degree);

        Assert.Equal(expected, density);
    }

    [Fact]
    public void Resolve_MatchesTheOfficialCetieDt9Table()
    {
        // A ficha oficial CETIE DT9 (setembro de 1993), fator K de 5 a
        // 35 °C, enviada pelo Diogo a 19/10/2026: a folha «Temp» tinha
        // gralhas nos 6, 18 e 30 °C.
        decimal[] dt9 =
        {
            0.99888m, 0.99886m, 0.99882m, 0.99877m, 0.99871m, 0.99863m, 0.99854m,
            0.99844m, 0.99832m, 0.99819m, 0.99805m, 0.99789m, 0.99773m, 0.99755m,
            0.99737m, 0.99717m, 0.99696m, 0.99674m, 0.99652m, 0.99628m, 0.99603m,
            0.99577m, 0.99551m, 0.99523m, 0.99494m, 0.99465m, 0.99435m, 0.99403m,
            0.99371m, 0.99339m, 0.99305m
        };

        for (var degree = WaterDensityTable.MinTemperatureC; degree <= WaterDensityTable.MaxTemperatureC; degree++)
        {
            Assert.Equal(dt9[degree - WaterDensityTable.MinTemperatureC], WaterDensityTable.Resolve(degree));
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(36)]
    [InlineData(100)]
    public void Resolve_OutsideSupportedRange_ReturnsNull(int degree)
    {
        // O intervalo suportado é 5–35 °C: fora dele a tabela canónica não
        // resolve densidade (nenhum valor é inventado nem extrapolado).
        Assert.Null(WaterDensityTable.Resolve(degree));
    }

    [Theory]
    [InlineData(20.0, 20)]
    [InlineData(20.4, 20)]
    [InlineData(20.5, 21)]   // arredondamento comercial: .5 sobe
    [InlineData(19.5, 20)]
    [InlineData(4.5, 5)]
    [InlineData(35.4, 35)]
    [InlineData(35.5, 36)]
    public void RoundToWholeDegree_RoundsMeasuredTemperature(decimal measured, int expected)
    {
        // A temperatura usada no lookup é a medida arredondada ao grau
        // inteiro; o comportamento de meio grau (.5 sobe) é fixo e
        // determinístico.
        Assert.Equal(expected, WaterDensityTable.RoundToWholeDegree(measured));
    }

    [Fact]
    public void RoundToWholeDegree_ThenResolve_UsesExactRowNoInterpolation()
    {
        // 20.6 arredonda a 21: a densidade é a linha exata de 21 (0.99696 —
        // tabela real da fábrica, decisão Owner 1b), nunca uma interpolação
        // entre 20 e 21.
        var degree = WaterDensityTable.RoundToWholeDegree(20.6m);

        Assert.Equal(21, degree);
        Assert.Equal(0.99696m, WaterDensityTable.Resolve(degree));
    }
}
