using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova quanto uma CM mexe na média: na base (média com ela − média sem
/// ela) e nas CM pesadas agora (a mesma CM substitui a da base; sem par
/// entra como mais uma leitura).
/// </summary>
public sealed class PesoComparacaoInfluenciaCalculationTests
{
    [Fact]
    public void DaBase_MarcaACmQueMaisPuxa()
    {
        var r = PesoComparacaoInfluenciaCalculation.DaBase(new[] { ("1", 100m), ("2", 101m), ("3", 106m) });

        // média 102,33; sem a 3: 100,5 → a 3 puxa +1,83.
        Assert.Equal(3.67m, r[2].DiferencaParaMediaGrams);
        Assert.Equal(100.5m, r[2].MediaComEstaCmGrams);
        Assert.Equal(1.83m, r[2].EfeitoNaMediaGrams);
        Assert.True(r[2].MaiorEfeito);
        Assert.False(r[0].MaiorEfeito);
    }

    [Fact]
    public void Novas_ComParSubstituiSemParSoma()
    {
        var rows = new[]
        {
            new PesoComparacaoParRow(1, "2", 110m, "2", 101m, 9m, null),
            new PesoComparacaoParRow(2, "9", 102m, null, null, null, null)
        };

        var r = PesoComparacaoInfluenciaCalculation.Novas(rows, 102m, 3);

        // (102·3 − 101 + 110)/3 = 105; (102·3 + 102)/4 = 102.
        Assert.Equal(105m, r[0].MediaComEstaCmGrams);
        Assert.Equal(3m, r[0].EfeitoNaMediaGrams);
        Assert.True(r[0].MaiorEfeito);
        Assert.Equal(0m, r[1].EfeitoNaMediaGrams);
        Assert.Equal(0m, r[1].DiferencaParaMediaGrams);
    }
}
