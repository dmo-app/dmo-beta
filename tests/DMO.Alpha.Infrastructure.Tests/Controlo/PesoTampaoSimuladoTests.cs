using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O tampão do «Comparar peso» (Diogo, 27/10/2026: «tp no peso altera-se
/// apenas a calote e depois faz o cálculo com a forma já implementada»): o
/// texto «D x calote» e a mudança de calote pela «Calote Tampo».
/// </summary>
public sealed class PesoTampaoSimuladoTests
{
    [Fact]
    public void Texto_DiametroXCalote_SemZerosAMais()
    {
        Assert.Equal("24.95 x 4.5", new PesoTampao(24.95m, 4.5m).Texto);
        Assert.Equal("26 x 4", new PesoTampao(26m, 4m).Texto);
    }

    [Fact]
    public void SoACaloteMuda_OPesoMudaPelaCaloteTampo()
    {
        var tampao = new PesoTampao(24.95m, 4.5m);

        var r = PesoSuposicaoCalculation.Calote(tampao.DiametroMm, tampao.CaloteMm, 5.2m, 2.5m, 100m, null);

        Assert.Equal(0.197m, r.DeltaVolumeCm3);
        Assert.Equal(0.49m, r.DeltaPesoGrams);
        Assert.Equal(100.49m, r.PesoMedioSupostoGrams);
    }
}
