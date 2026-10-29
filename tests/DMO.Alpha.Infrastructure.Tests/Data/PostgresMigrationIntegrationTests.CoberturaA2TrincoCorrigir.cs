using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// DMO-546 (A-0281): o apagar e o corrigir da leitura do Peso (os que
/// renumeram) esperam por uma leitura nova que está a ser registada. O teste
/// do trinco já existente segura o trinco exclusivo à mão e prova a espera do
/// registo; estes seguram o trinco partilhado do registo e correm o
/// CorrectPesoMeasurementHandler verdadeiro: se o corrigir/apagar mudar de
/// trinco, deixam de esperar e o teste falha.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ApagarUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso_ENaoDeixaBuracoNasPosicoes()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            Assert.Equal(RecordPesoMeasurementResult.Success, (await GravarAsync(new RecordPesoMeasurement(
                peso, "1", 199.50m, [new PesoMeasurementRow("2", 199.60m), new PesoMeasurementRow("3", 199.70m)],
                Guid.NewGuid()), PessoaA)).Result);
            int primeira;
            await using (var db = CreateContext())
            {
                primeira = await db.PesoMeasurements.Where(m => m.PesoId == peso).OrderBy(m => m.RowPosition).Select(m => m.Id).FirstAsync();
            }

            // Uma leitura nova em curso: segura o trinco do Peso (partilhado) até ao commit.
            await using var emCurso = CreateContext();
            await using var transacao = await emCurso.Database.BeginTransactionAsync();
            await DbWrite.LockSharedAsync(emCurso, $"peso:registo:{peso}", CancellationToken.None);

            await using var apagarDb = CreateContext();
            var apagar = new CorrectPesoMeasurementHandler(apagarDb).DeleteAsync(new PesoMeasurementAnchor(primeira, peso));
            await Task.Delay(500);
            Assert.False(apagar.IsCompleted, "o apagar espera pela leitura nova em curso");

            await transacao.CommitAsync();
            Assert.Equal(CorrectPesoMeasurementResult.Success, await apagar.WaitAsync(TimeSpan.FromSeconds(30)));

            // As duas que ficam são a 1 e a 2, sem buraco.
            Assert.Equal([(1, "2", 199.60m), (2, "3", 199.70m)], await LeiturasAsync(peso));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    [SkippableFact]
    public async Task CorrigirUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            Assert.Equal(RecordPesoMeasurementResult.Success, (await GravarAsync(new RecordPesoMeasurement(
                peso, "1", 199.50m, null, Guid.NewGuid()), PessoaA)).Result);
            int leitura;
            await using (var db = CreateContext())
            {
                leitura = await db.PesoMeasurements.Where(m => m.PesoId == peso).Select(m => m.Id).SingleAsync();
            }

            await using var emCurso = CreateContext();
            await using var transacao = await emCurso.Database.BeginTransactionAsync();
            await DbWrite.LockSharedAsync(emCurso, $"peso:registo:{peso}", CancellationToken.None);

            await using var corrigirDb = CreateContext();
            var corrigir = new CorrectPesoMeasurementHandler(corrigirDb)
                .EditAsync(new EditPesoMeasurement(new PesoMeasurementAnchor(leitura, peso), "9", 199.55m));
            await Task.Delay(500);
            Assert.False(corrigir.IsCompleted, "o corrigir espera pela leitura nova em curso");

            await transacao.CommitAsync();
            Assert.Equal(CorrectPesoMeasurementResult.Success, await corrigir.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal([(1, "9", 199.55m)], await LeiturasAsync(peso));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }
}
