using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// DMO-546: quem corrige ou apaga uma leitura do Peso toma mesmo o trinco do
/// Peso. O teste segura o trinco partilhado, o de quem está a registar uma
/// leitura, e o handler da correção tem de esperar por ele; o outro teste
/// (LeituraDoPeso_EsperaPeloTrincoDoCorrigirEApagar) prova o sentido contrário.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorrigirEApagarUmaLeituraDoPeso_EsperamPeloTrincoDeQuemRegista(bool apagar)
    {
        SkipWithoutConnection();

        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marker = "TQ-" + Guid.NewGuid().ToString("N")[..8];
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
        var leitura = await new RecordPesoMeasurementHandler(setup).HandleAsync(new RecordPesoMeasurement(peso.Id, "1", 199.64m));
        Assert.Equal(RecordPesoMeasurementResult.Success, leitura.Result);
        var ancora = new PesoMeasurementAnchor(leitura.Measurement!.MeasurementId, peso.Id);
        try
        {
            // Outro pedido está a registar uma leitura deste Peso: segura o trinco partilhado.
            await using var registar = CreateContext();
            await using var transacao = await registar.Database.BeginTransactionAsync();
            await DbWrite.LockSharedAsync(registar, $"peso:registo:{peso.Id}", CancellationToken.None);

            await using var corrigir = CreateContext();
            var handler = new CorrectPesoMeasurementHandler(corrigir);
            var correcao = apagar
                ? handler.DeleteAsync(ancora)
                : handler.EditAsync(new EditPesoMeasurement(ancora, "2", 150.10m));
            await Task.Delay(500);
            Assert.False(correcao.IsCompleted, "a correção espera pelo trinco do Peso");

            await transacao.CommitAsync();
            Assert.Equal(CorrectPesoMeasurementResult.Success, await correcao.WaitAsync(TimeSpan.FromSeconds(30)));

            await using var leitor = CreateContext();
            var gravadas = await leitor.PesoMeasurements.AsNoTracking().Where(m => m.PesoId == peso.Id).ToListAsync();
            if (apagar)
            {
                Assert.Empty(gravadas);
            }
            else
            {
                Assert.Equal(("2", 150.10m), (Assert.Single(gravadas).CmSubject, gravadas[0].WaterWeightGrams));
            }
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"PesoMeasurements\" WHERE \"PesoId\" = {peso.Id}");
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Pesos\" WHERE \"Id\" = {peso.Id}");
            await setup.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
        }
    }
}
