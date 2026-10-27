using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O tampão simulado do «Comparar peso» (Diogo, 27/10/2026: «alterar o tampão
/// usando o cálculo do volume do tampão altera o peso»): o texto «D x calote»
/// e o peso com outro tampão, pela mesma calote esférica da app.
/// </summary>
public sealed class PesoTampaoSimuladoTests
{
    [Theory]
    [InlineData("24.95 x 4.5mm", 24.95, 4.5)]
    [InlineData("24,95x5,2", 24.95, 5.2)]
    [InlineData("26 × 4.5 mm", 26, 4.5)]
    [InlineData("24.95 X 0", 24.95, 0)]
    public void Ler_DiametroXCalote(string texto, double diametro, double calote)
    {
        var tampao = PesoTampao.Ler(texto);

        Assert.NotNull(tampao);
        Assert.Equal((decimal)diametro, tampao.DiametroMm);
        Assert.Equal((decimal)calote, tampao.CaloteMm);
    }

    [Fact]
    public void Ler_UmSoNumero_MudaACalote_EFicaODiametro()
    {
        Assert.Equal(new PesoTampao(24.95m, 5.2m), PesoTampao.Ler("5.2", 24.95m));
        Assert.Null(PesoTampao.Ler("5.2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0 x 4.5")]
    [InlineData("24.95 x -1")]
    [InlineData("24.95 x 4.5 x 3")]
    public void Ler_TextoQueNaoSeLe_DaNull(string texto) => Assert.Null(PesoTampao.Ler(texto, 24.95m));

    [Fact]
    public void Texto_SemCasasAMais() => Assert.Equal("24.95 x 4.5", new PesoTampao(24.95m, 4.50m).Texto);

    [Fact]
    public void Tampao_CaloteMaisAlta_JuntaOVolumeVezesADensidade()
    {
        var r = PesoSuposicaoCalculation.Tampao(new PesoTampao(24.95m, 4.5m), new PesoTampao(24.95m, 5.2m), 2.5m, 361.42m);

        Assert.Equal(1.148m, r.VolumeAtualCm3);
        Assert.Equal(1.345m, r.VolumeSimuladoCm3);
        Assert.Equal(0.197m, r.DeltaVolumeCm3);
        Assert.Equal(361.91m, r.PesoSimuladoGrams);
        Assert.Equal(0.49m, r.DiferencaGrams);
        Assert.Equal(0.14m, r.VariacaoPercent);
    }

    [Fact]
    public void Tampao_OutroDiametro_TambemMudaOPeso()
    {
        var r = PesoSuposicaoCalculation.Tampao(new PesoTampao(24.95m, 4.5m), new PesoTampao(26m, 4.5m), 2.5m, 361.42m);

        Assert.Equal(361.66m, r.PesoSimuladoGrams);
        Assert.Equal(0.24m, r.DiferencaGrams);
    }

    [Fact]
    public void Tampao_OMesmo_NaoMudaNada()
    {
        var r = PesoSuposicaoCalculation.Tampao(new PesoTampao(24.95m, 4.5m), new PesoTampao(24.95m, 4.5m), 2.5m, 361.42m);

        Assert.Equal(0m, r.DiferencaGrams);
        Assert.Equal(361.42m, r.PesoSimuladoGrams);
    }

    [Fact]
    public void Tampao_CaloteMaisBaixa_TiraPeso()
    {
        var r = PesoSuposicaoCalculation.Tampao(new PesoTampao(24.95m, 5.2m), new PesoTampao(24.95m, 4.5m), 2.5m, 361.91m);

        Assert.Equal(-0.49m, r.DiferencaGrams);
    }
}
