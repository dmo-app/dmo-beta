using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// As outras colunas da ZFMD no PostgreSQL (Diogo, 27/10/2026): a migração
/// acrescenta-as vazias e a leitura do relatório grava-as com as casas que o
/// SAP dá, também o Δ do peso negativo.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task SapFabricoColunasZfmd_GravaTodasAsColunasDoRelatorio_NoPostgreSql()
    {
        SkipWithoutConnection();
        var material = "ZZ" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() + "BRZ";
        var relatorio = "Material\tLine\tPlant\tBegin Date\tEnd Date\tProd. Days\tCadência\tGob\tSectores\tKCR\tAvg weight\tΔ weight\tGross Prod\tNet Prod.\tPack to pull\tNet Quant\tPallet\tTotal Rej.\tRej.MIL\tHotEnd%\n"
            + $"{material}\tC3\tMG\t30.06.2026\t06.07.2026\t6\t489\t3\t12\t170,195\t157\t-3\t656.341\t597.103\t90,97\t3.802.656\t1.418,0\t1,55\t4,33\t95,56\n";
        var leitura = FabricoSapExportacao.Ler(relatorio);
        try
        {
            await using (var db = CreateContext())
            {
                await new FerramentasFabricoSap(db, new SessaoUsoSap()).ImportarAsync(leitura.Linhas, "dmo-fabrico-sap.txt", DateTime.UtcNow, leitura.Detalhes);
            }

            await using var depois = CreateContext();
            var gravada = await depois.SapFabricos.AsNoTracking().SingleAsync(s => s.Material == material);
            Assert.Equal(("MG", 489, 3, 12, 170.195m, -3m, 90.97m),
                (gravada.Centro, gravada.Cadencia!.Value, gravada.Gob!.Value, gravada.Sectores!.Value, gravada.Kcr!.Value, gravada.DeltaPesoGramas!.Value, gravada.PackToPull!.Value));
            Assert.Equal((3_802_656L, 1_418m, 1.55m, 4.33m, 95.56m),
                (gravada.NetQuant!.Value, gravada.Paletes!.Value, gravada.RejeicaoTotal!.Value, gravada.RejeicaoMil!.Value, gravada.HotEnd!.Value));
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.SapFabricos.Where(s => s.Material == material).ExecuteDeleteAsync();
        }
    }
}
