using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Planeamento;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Sumario_MaisDeQuinhentasPendencias_TodasAcessiveisSemRepetir()
    {
        SkipWithoutConnection();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = "SP" + Guid.NewGuid().ToString("N")[..8];
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var machine = new Machine { Code = marker };
        var jobs = Enumerable.Range(0, 501).Select(i => new JobOn
        {
            Machine = machine, Reference = marker, ProductionNumber = i.ToString(), InicioEm = hoje, CriadoEmUtc = DateTime.UtcNow
        }).ToArray();
        db.JobOns.AddRange(jobs);
        await db.SaveChangesAsync();
        var query = new SumarioQuery(db);
        var first = await query.PorResolverPaginaAsync(hoje, [SumarioTipo.JobOnPorVer], 1);
        var second = await query.PorResolverPaginaAsync(hoje, [SumarioTipo.JobOnPorVer], 2);
        // A base descartável pode conter factos de outros testes: os nossos
        // são os mais recentes; as primeiras duas páginas têm de os abranger.
        Assert.True(first.HaMais);
        var ids = first.Items.Concat(second.Items).Where(i => i.Titulo.StartsWith(marker, StringComparison.Ordinal))
            .Select(i => i.JobOnId!.Value).ToArray();
        Assert.Equal(jobs.Select(j => j.Id).Order(), ids.Order());
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
