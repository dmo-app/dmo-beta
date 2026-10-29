using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O fabrico pelo SAP no PostgreSQL: a migração guarda as datas e as peças,
/// ler o relatório outra vez atualiza a mesma linha, e a base não deixa duas
/// linhas com o mesmo material, linha e datas.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task FabricoSap_LerDuasVezesUmaSoLinha_NoPostgreSql()
    {
        SkipWithoutConnection();
        var material = "ZZ" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() + "BRA";
        var linha = new FabricoSapLinha(material, "C3", new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234, 1_309_264, 132.125m, 14);
        try
        {
            await using (var db = CreateContext())
            {
                Assert.Equal(new ImportarFabricoSapOutcome(1, 1), await new FerramentasFabricoSap(db, new SessaoUsoSap(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance).ImportarAsync([linha], "fabrico.txt", DateTime.UtcNow));
            }

            await using (var db = CreateContext())
            {
                Assert.Equal(new ImportarFabricoSapOutcome(1, 0), await new FerramentasFabricoSap(db, new SessaoUsoSap(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>.Instance).ImportarAsync([linha with { ProducaoLiquida = 1_300_000 }], "fabrico.txt", DateTime.UtcNow));
            }

            await using var leitura = CreateContext();
            var gravada = await leitura.SapFabricos.AsNoTracking().SingleAsync(s => s.Material == material);
            Assert.Equal((new DateOnly(2025, 1, 21), new DateOnly(2025, 2, 4), 1_404_234L, 1_300_000L, "Rui", 132.125m, 14),
                (gravada.InicioEm, gravada.FimEm, gravada.ProducaoBruta!.Value, gravada.ProducaoLiquida!.Value, gravada.LidoPor, gravada.PesoMedioGramas!.Value, gravada.DiasProducao!.Value));

            leitura.SapFabricos.Add(new SapFabrico { Material = material, Linha = "C3", InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), LidoEmUtc = DateTime.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => leitura.SaveChangesAsync());
        }
        finally
        {
            await using var limpar = CreateContext();
            await limpar.SapFabricos.Where(s => s.Material == material).ExecuteDeleteAsync();
        }
    }
}
