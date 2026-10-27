using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// As outras colunas do relatório da ZFMD (Diogo, 27/10/2026: «está a faltar
/// muita informação que não foi aplicada»): além do material, da linha, das
/// datas, dos dias, do peso e das garrafas, o DMO guarda o centro, a
/// cadência, as gotas, os sectores, o KCR, o Δ do peso, o Pack to pull, o Net
/// Quant, as paletes, as rejeições e o HotEnd de cada produção.
/// </summary>
public sealed class FabricoSapColunasZfmdTests
{
    // Do dmo-fabrico-sap.txt que o Diogo mandou a 27/10/2026 (BOM, fim de
    // linha do Windows); a segunda linha tem o Δ do peso negativo.
    private const string Zfmd =
        "﻿Material\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tCadência\tGob\tSectores\tKCR\tAvg weight\tΔ weight\tGross Prod\tNet Prod.\tPack to pull\tNet Quant\tPallet\tTotal Rej.\tRej.MIL\tHotEnd%\r\n" +
        "6018Y232BRZ\tC2\tMG\t15.06.2026\t03.09.2026\t80\t660\t4\t11\t135,000\t81\t1\t6.023.524\t5.507.566\t91,43\t67.990.944\t7.439,0\t0,75\t5,18\t96,09\r\n" +
        "9122Y100BRZ\tC3\tMG\t30.06.2026\t06.07.2026\t6\t489\t3\t12\t170,195\t157\t-3\t656.341\t597.103\t90,97\t3.802.656\t1.418,0\t1,55\t4,33\t95,56\r\n";

    [Fact]
    public void ARelatorioDaZfmd_DaTodasAsColunasDeCadaProducao()
    {
        var leitura = FabricoSapExportacao.Ler(Zfmd);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(2, leitura.Linhas.Count);
        Assert.Equal(
            new FabricoSapDetalhe("6018Y232BRZ", "C2", new DateOnly(2026, 6, 15), new DateOnly(2026, 9, 3))
            {
                Centro = "MG", Cadencia = 660, Gob = 4, Sectores = 11, Kcr = 135m, DeltaPesoGramas = 1m, PackToPull = 91.43m,
                NetQuant = 67_990_944, Paletes = 7_439m, RejeicaoTotal = 0.75m, RejeicaoMil = 5.18m, HotEnd = 96.09m
            },
            leitura.Detalhes[0]);
        Assert.Equal(-3m, leitura.Detalhes[1].DeltaPesoGramas);
        Assert.Equal((170.195m, 95.56m), (leitura.Detalhes[1].Kcr!.Value, leitura.Detalhes[1].HotEnd!.Value));
        // As colunas de antes não mudam.
        Assert.Equal(new FabricoSapLinha("9122Y100BRZ", "C3", new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 6), 656_341, 597_103, 157m, 6), leitura.Linhas[1]);
    }

    [Theory]
    [InlineData("3-", -3)]
    [InlineData("-0,5", -0.5)]
    [InlineData("12", 12)]
    public void ODeltaDoPeso_LeSeComOSinal(string celula, double esperado)
    {
        var relatorio = "Material\tLine\tBegin Date\tEnd Date\tGross Prod\tΔ weight\n5538T324BRA\tC3\t25.06.2026\t30.06.2026\t499.954\t" + celula + "\n";

        Assert.Equal((decimal)esperado, Assert.Single(FabricoSapExportacao.Ler(relatorio).Detalhes).DeltaPesoGramas);
    }

    [Fact]
    public void UmRelatorioSemAsColunasNovas_DeixaAsVazias()
    {
        var relatorio = "Material\tLine\tBegin Date\tEnd Date\tGross Prod\n5538T324BRA\tC3\t25.06.2026\t30.06.2026\t499.954\n";

        Assert.Equal(new FabricoSapDetalhe("5538T324BRA", "C3", new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 30)),
            Assert.Single(FabricoSapExportacao.Ler(relatorio).Detalhes));
    }

    [Fact]
    public async Task Importar_GravaAsColunasNovas_ESemElasNaoApagaAsJaGravadas()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-fabrico-zfmd-" + Guid.NewGuid().ToString("N"))
            .Options);
        var servico = new FerramentasFabricoSap(db, new Sessao());
        var leitura = FabricoSapExportacao.Ler(Zfmd);

        await servico.ImportarAsync(leitura.Linhas, "dmo-fabrico-sap.txt", DateTime.UtcNow, leitura.Detalhes);
        // Sem as colunas novas (um relatório antigo): ficam as gravadas.
        await servico.ImportarAsync(leitura.Linhas, "antigo.txt", DateTime.UtcNow);

        var gravada = await db.SapFabricos.AsNoTracking().SingleAsync(s => s.Material == "9122Y100BRZ");
        Assert.Equal(("MG", 489, 3, 12, 170.195m, -3m, 90.97m), (gravada.Centro, gravada.Cadencia!.Value, gravada.Gob!.Value, gravada.Sectores!.Value, gravada.Kcr!.Value, gravada.DeltaPesoGramas!.Value, gravada.PackToPull!.Value));
        Assert.Equal((3_802_656L, 1_418m, 1.55m, 4.33m, 95.56m), (gravada.NetQuant!.Value, gravada.Paletes!.Value, gravada.RejeicaoTotal!.Value, gravada.RejeicaoMil!.Value, gravada.HotEnd!.Value));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}
