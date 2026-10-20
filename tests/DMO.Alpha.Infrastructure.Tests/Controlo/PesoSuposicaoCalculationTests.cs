using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova o «supomos» da calote do tampão: ΔV pela mesma calote esférica da
/// app (D 50: h 3 → 2,959 cm³; h 5,2 → 5,179 cm³), Δ peso = ΔV × densidade do
/// vidro e a diferença para a produção comparada antes e depois.
/// </summary>
public sealed class PesoSuposicaoCalculationTests
{
    [Fact]
    public void Calote_MaisAlta_TrocaAParteDoPesoQueVemDaCalote()
    {
        var r = PesoSuposicaoCalculation.Calote(50m, 3m, 5.2m, 2.5m, 325.39m, 320m);

        Assert.Equal(2.959m, r.VolumeAtualCm3);
        Assert.Equal(5.179m, r.VolumeSupostoCm3);
        Assert.Equal(2.219m, r.DeltaVolumeCm3);
        Assert.Equal(7.40m, r.PesoDaCaloteAtualGrams);
        Assert.Equal(12.95m, r.PesoDaCaloteSupostaGrams);
        Assert.Equal(5.55m, r.DeltaPesoGrams);
        Assert.Equal(330.94m, r.PesoMedioSupostoGrams);
        Assert.Equal(5.39m, r.DiferencaAtualGrams);
        Assert.Equal(10.94m, r.DiferencaSupostaGrams);
        Assert.Equal(3.42m, r.VariacaoSupostaPercent);
    }

    [Fact]
    public void Calote_MaisBaixa_TiraPeso()
    {
        var r = PesoSuposicaoCalculation.Calote(50m, 5.2m, 3m, 2.5m, 330m, null);

        Assert.Equal(-5.55m, r.DeltaPesoGrams);
        Assert.Equal(324.45m, r.PesoMedioSupostoGrams);
        Assert.Null(r.DiferencaSupostaGrams);
        Assert.Null(r.VariacaoSupostaPercent);
    }

    [Fact]
    public void Calote_Igual_NaoMudaNada()
    {
        var r = PesoSuposicaoCalculation.Calote(28.45m, 4.5m, 4.5m, 2.4027m, 183m, 183m);

        Assert.Equal(0m, r.DeltaPesoGrams);
        Assert.Equal(183m, r.PesoMedioSupostoGrams);
        Assert.Equal(0m, r.DiferencaSupostaGrams);
    }
}
