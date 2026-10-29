using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// A cadência do relatório da ZFMD: das outras colunas, a única que o DMO
/// guarda (Diogo, 27/10/2026: «podemos guardar cadência mas acho que o resto
/// não vale a pena»; «cadência para meter no Job On»).
/// </summary>
public sealed class FabricoSapCadenciaTests
{
    // Do dmo-fabrico-sap.txt que o Diogo mandou a 27/10/2026 (BOM, fim de linha do Windows).
    private const string Zfmd =
        "﻿Material\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tCadência\tGob\tSectores\tKCR\tAvg weight\tΔ weight\tGross Prod\tNet Prod.\tPack to pull\tNet Quant\tPallet\tTotal Rej.\tRej.MIL\tHotEnd%\r\n" +
        "6018Y232BRZ\tC2\tMG\t15.06.2026\t03.09.2026\t80\t660\t4\t11\t135,000\t81\t1\t6.023.524\t5.507.566\t91,43\t67.990.944\t7.439,0\t0,75\t5,18\t96,09\r\n" +
        "9122Y100BRZ\tC3\tMG\t30.06.2026\t06.07.2026\t6\t489\t3\t12\t170,195\t157\t-3\t656.341\t597.103\t90,97\t3.802.656\t1.418,0\t1,55\t4,33\t95,56\r\n";

    [Fact]
    public void ARelatorioDaZfmd_DaACadenciaDeCadaProducao()
    {
        var leitura = FabricoSapExportacao.Ler(Zfmd);

        Assert.True(leitura.CabecalhoEncontrado);
        Assert.Equal(
            [
                new FabricoSapDetalhe("6018Y232BRZ", "C2", new DateOnly(2026, 6, 15), new DateOnly(2026, 9, 3)) { Cadencia = 660 },
                new FabricoSapDetalhe("9122Y100BRZ", "C3", new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 6)) { Cadencia = 489 }
            ],
            leitura.Detalhes);
        // As colunas de antes não mudam.
        Assert.Equal(new FabricoSapLinha("9122Y100BRZ", "C3", new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 6), 656_341, 597_103, 157m, 6), leitura.Linhas[1]);
    }

    [Fact]
    public void UmRelatorioSemCadencia_DeixaAVazia()
    {
        var relatorio = "Material\tLine\tBegin Date\tEnd Date\tGross Prod\n5538T324BRA\tC3\t25.06.2026\t30.06.2026\t499.954\n";

        Assert.Null(Assert.Single(FabricoSapExportacao.Ler(relatorio).Detalhes).Cadencia);
    }

    [Fact]
    public async Task Importar_GravaACadencia_ESemElaNaoApagaAJaGravada()
    {
        using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-fabrico-cadencia-" + Guid.NewGuid().ToString("N"))
            .Options);
        var servico = new FerramentasFabricoSap(db, new Sessao(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance);
        var leitura = FabricoSapExportacao.Ler(Zfmd);

        await servico.ImportarAsync(leitura.Linhas, "dmo-fabrico-sap.txt", DateTime.UtcNow, leitura.Detalhes);
        // Sem a cadência (um relatório antigo): fica a gravada.
        await servico.ImportarAsync(leitura.Linhas, "antigo.txt", DateTime.UtcNow);

        var gravada = await db.SapFabricos.AsNoTracking().SingleAsync(s => s.Material == "9122Y100BRZ");
        Assert.Equal(489, gravada.Cadencia);
    }

    [Fact]
    public void ACadenciaDoJobOn_EADaOrdemComMaisDias_EmpatadaAMaisRecente()
    {
        static SapFabrico Ordem(int dia, int ateDia, int? cadencia) =>
            new() { Material = "X", Linha = "C3", InicioEm = new DateOnly(2026, 7, dia), FimEm = new DateOnly(2026, 7, ateDia), Cadencia = cadencia };
        var inicio = new DateOnly(2026, 7, 1);

        // 489 tem 4 + 2 dias, 500 tem 5; a ordem sem cadência não conta.
        Assert.Equal(489, FabricoSapCadencia.DoJobOn([Ordem(1, 5, 489), Ordem(5, 10, 500), Ordem(10, 12, 489), Ordem(12, 30, null)], inicio, new DateOnly(2026, 7, 31)));
        // Empatadas, a mais recente.
        Assert.Equal(500, FabricoSapCadencia.DoJobOn([Ordem(1, 5, 489), Ordem(5, 9, 500)], inicio, null));
        Assert.Null(FabricoSapCadencia.DoJobOn([Ordem(1, 5, null)], inicio, null));
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, DisplayName = "Rui", OperatorId = "2165" };
    }
}
