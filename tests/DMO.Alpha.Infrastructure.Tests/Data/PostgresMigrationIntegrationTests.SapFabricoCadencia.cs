using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Das outras colunas da ZFMD no PostgreSQL fica só a cadência (Diogo,
/// 27/10/2026: «podemos guardar cadência mas acho que o resto não vale a
/// pena»): a leitura do relatório grava-a e as outras 11 colunas já não
/// existem.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task SapFabricoCadencia_GravaSoACadencia_NoPostgreSql()
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
                await new FerramentasFabricoSap(db, new SessaoUsoSap(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance).ImportarAsync(leitura.Linhas, "dmo-fabrico-sap.txt", DateTime.UtcNow, leitura.Detalhes);
            }

            await using var depois = CreateContext();
            var gravada = await depois.SapFabricos.AsNoTracking().SingleAsync(s => s.Material == material);
            Assert.Equal((489, 656_341L, 597_103L, 157m, 6), (gravada.Cadencia!.Value, gravada.ProducaoBruta!.Value, gravada.ProducaoLiquida!.Value, gravada.PesoMedioGramas!.Value, gravada.DiasProducao!.Value));
            var colunas = await depois.Database.SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'SapFabricos'
                """).ToListAsync();
            Assert.Contains("Cadencia", colunas);
            Assert.DoesNotContain(colunas, c => c is "Centro" or "Gob" or "Sectores" or "Kcr" or "DeltaPesoGramas" or "PackToPull"
                or "NetQuant" or "Paletes" or "RejeicaoTotal" or "RejeicaoMil" or "HotEnd");
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.SapFabricos.Where(s => s.Material == material).ExecuteDeleteAsync();
        }
    }
}
