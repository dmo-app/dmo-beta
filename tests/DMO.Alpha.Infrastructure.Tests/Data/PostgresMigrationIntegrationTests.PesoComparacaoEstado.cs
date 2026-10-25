using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Budgets are the sum of the calls the Peso page made itself before the
    // extraction (picker find + list, Peso find, candidate list); the service
    // adds no query of its own and never writes.
    [SkippableTheory]
    [InlineData(0, 1)] // Typed reference, no requested production: only the list.
    [InlineData(1, 2)] // Requested production: find it, then list its reference.
    [InlineData(2, 1)] // Base Peso with readings.
    [InlineData(3, 1)] // CM candidates.
    public async Task PesoComparacaoEstado_CadaLeituraSoLeSemAumentarConsultas(int scenario, int budget)
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "PC-" + Guid.NewGuid().ToString("N")[..8];
        var cm = marker + "-cm";
        var bq = marker + "-bq";
        var production = new JobOn { Machine = new Machine { Code = marker }, Reference = marker, ProductionNumber = marker };
        db.JobOns.Add(production);
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = marker, Lot = "1" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = marker, Lot = "2" });
        var peso = new Peso
        {
            CmToolId = cm, BqToolId = bq, Process = "NNPB", WaterTemperatureC = 20,
            WaterDensityGramsPerCm3 = 0.9982m, GlassDensityGramsPerCm3 = 2.4m,
            VolumePuncaoCm3 = 7m, VolumeMarisaCm3 = 12m
        };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        db.PesoMeasurements.AddRange(
            new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 300m },
            new PesoMeasurement { PesoId = peso.Id, RowPosition = 2, CmSubject = "2", WaterWeightGrams = 310m });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var query = new PesoComparacaoEstadoQuery(
            new ProductionPickerQuery(db), new PesoQuery(db), new ToolCandidateQuery(db));
        commands.Reading = true;
        switch (scenario)
        {
            case 0:
                Assert.Single((await query.ProducoesDaReferenciaAsync(marker, null)).Producoes);
                break;
            case 1:
                Assert.Equal(production.Id, (await query.ProducoesDaReferenciaAsync(null, production.Id)).SelecionadaJobOnId);
                break;
            case 2:
                Assert.Equal(2, (await query.BaseAsync(peso.Id, null)).InfluenciaDaBase.Count);
                break;
            default:
                Assert.Contains(await query.ContraMoldesAsync(marker), t => t.ToolId == cm);
                break;
        }
        commands.Reading = false;

        Assert.Equal(budget, commands.Reads);
        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }
}
