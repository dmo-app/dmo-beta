using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Os três estados da aprovação no PostgreSQL (sweep de 19/10/2026, C-3): a
/// decisão de cada parte para o PDF dela e a linha do Histórico com o «Não
/// aprovado» e a nota leem-se no SQL como no InMemory.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task TresEstados_ADecisaoDeCadaParte_EOHistorico_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var (machine, jobOn, controloId) = await NovaProducaoComPesoAsync(db, p, new DateTime(2002, 3, 4, 10, 0, 0, DateTimeKind.Utc));
        try
        {
            var handler = new ControloApprovalHandler(db);
            var historico = new ControloHistorySearchQuery(db);
            Assert.Equal(new ControloApprovalReadModel(null, null), await handler.ParteAsync(controloId, ControloParte.Peso));

            await handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: false, "2003", "Repetir a pesagem.");

            var peso = await handler.ParteAsync(controloId, ControloParte.Peso);
            Assert.True(peso!.NaoAprovado);
            Assert.Equal("2003", peso.NotApprovedByOperatorId);
            Assert.Equal("Repetir a pesagem.", peso.ReviewNote);
            Assert.Null(await handler.ParteAsync(-controloId, ControloParte.Peso));
            var resumo = Assert.Single((await historico.SearchPageAsync(
                null, jobOn.ProductionNumber, null, ControloHistoryKind.Resumo, null, 1, 50)).Items);
            Assert.Equal("Não aprovado por 2003 · Repetir a pesagem.", resumo.Resumo);
            Assert.Equal(peso.NotApprovedAtUtc, resumo.DataUtc);
        }
        finally
        {
            await ApagarProducaoAsync(db, p, machine, controloId);
        }
    }
}
