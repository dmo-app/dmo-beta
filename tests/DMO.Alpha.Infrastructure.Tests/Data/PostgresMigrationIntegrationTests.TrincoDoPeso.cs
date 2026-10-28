using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O trinco do Peso (relatório de backend do dmo-Improvements de 28/10/2026,
/// BE08): uma leitura nova espera enquanto outro pedido corrige ou apaga e
/// renumera as leituras do mesmo Peso. No PostgreSQL, com duas ligações.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task LeituraDoPeso_EsperaPeloTrincoDoCorrigirEApagar()
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marker = "TP-" + Guid.NewGuid().ToString("N")[..8];
        setup.Tools.Add(new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        setup.Tools.Add(new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        var peso = new Peso
        {
            CmToolId = marker + "-CM",
            BqToolId = marker + "-BQ",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m
        };
        setup.Pesos.Add(peso);
        await setup.SaveChangesAsync();
        try
        {
            // Outro pedido a apagar e renumerar segura o trinco do Peso.
            await using var corrigir = CreateContext();
            await using var transacao = await corrigir.Database.BeginTransactionAsync();
            await DbWrite.LockAsync(corrigir, $"peso:registo:{peso.Id}", CancellationToken.None);

            await using var registar = CreateContext();
            var leitura = new RecordPesoMeasurementHandler(registar).HandleAsync(new RecordPesoMeasurement(peso.Id, "1", 199.64m));
            await Task.Delay(500);
            Assert.False(leitura.IsCompleted, "a leitura nova espera pelo trinco");

            await transacao.CommitAsync();
            var outcome = await leitura.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(RecordPesoMeasurementResult.Success, outcome.Result);
            Assert.Equal(1, outcome.Measurement!.RowPosition);
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"PesoMeasurements\" WHERE \"PesoId\" = {peso.Id}");
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Pesos\" WHERE \"Id\" = {peso.Id}");
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
        }
    }
}
