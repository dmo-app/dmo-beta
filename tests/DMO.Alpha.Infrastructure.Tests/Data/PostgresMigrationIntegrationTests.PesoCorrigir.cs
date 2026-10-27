using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Diogo, 27/10/2026 (A-0231): no Peso corrige-se e apaga-se uma leitura a
    // qualquer momento. Na base real, apagar a do meio renumera as seguintes
    // sem chocar com a posição única por Peso, e o rasto fica gravado.

    [SkippableFact]
    public async Task PesoLeitura_CorrigirEApagarADoMeio_RenumeraEDeixaRasto()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            Assert.Equal(RecordPesoMeasurementResult.Success, (await GravarAsync(new RecordPesoMeasurement(
                peso, "1", 199.50m, [new PesoMeasurementRow("2", 199.60m), new PesoMeasurementRow("3", 199.70m), new PesoMeasurementRow("4", 199.80m)],
                Guid.NewGuid()), PessoaA)).Result);

            int segunda, terceira;
            await using (var db = CreateContext())
            {
                var ids = await db.PesoMeasurements.Where(m => m.PesoId == peso).OrderBy(m => m.RowPosition).Select(m => m.Id).ToListAsync();
                (segunda, terceira) = (ids[1], ids[2]);
            }

            await using (var db = CreateContext())
            {
                var handler = new CorrectPesoMeasurementHandler(db);
                Assert.Equal(CorrectPesoMeasurementResult.Success,
                    await handler.EditAsync(new EditPesoMeasurement(new PesoMeasurementAnchor(terceira, peso), "33", 199.75m)));
                Assert.Equal(CorrectPesoMeasurementResult.Success,
                    await handler.DeleteAsync(new PesoMeasurementAnchor(segunda, peso)));
            }

            Assert.Equal([(1, "1", 199.50m), (2, "33", 199.75m), (3, "4", 199.80m)], await LeiturasAsync(peso));
            await using (var db = CreateContext())
            {
                var rasto = await db.PesoMeasurementHistorico.AsNoTracking()
                    .Where(h => h.PesoId == peso).OrderBy(h => h.Id)
                    .Select(h => new { h.Numero, h.Apagada, h.CmSubjectAntes, h.CmSubjectDepois })
                    .ToListAsync();
                Assert.Equal(2, rasto.Count);
                Assert.Equal((3, false, "3", "33"), (rasto[0].Numero, rasto[0].Apagada, rasto[0].CmSubjectAntes, rasto[0].CmSubjectDepois));
                Assert.Equal((2, true, "2", (string?)null), (rasto[1].Numero, rasto[1].Apagada, rasto[1].CmSubjectAntes, rasto[1].CmSubjectDepois));
            }
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }
}
