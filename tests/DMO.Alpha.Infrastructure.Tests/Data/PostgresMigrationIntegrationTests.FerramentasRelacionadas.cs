using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O Job On novo (Diogo, 26/10/2026: «um CM já inserido deveria filtrar»): a
/// ordem e o filtro das ferramentas relacionadas (usadas com as escolhidas,
/// usadas na referência total, ligadas à linha) traduzidos no PostgreSQL. O
/// InMemory avalia o LINQ em C# e não apanha uma consulta que o Npgsql não
/// traduz. Os dados têm um prefixo ao acaso, para a base partilhada não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task FerramentasRelacionadas_OrdemEFiltro_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var bqRef = $"T{p}";
        var total = $"{p}{bqRef}";
        var b1 = new Machine { Code = $"PG1-{p}" };
        var b2 = new Machine { Code = $"PG2-{p}" };
        db.Machines.AddRange(b1, b2);
        db.Tools.AddRange(
            new Tool { ToolId = $"CM-{p}", Type = ToolType.Cm, Reference = p, Lot = "12" },
            new Tool { ToolId = $"BQ1-{p}", Type = ToolType.Bq, Reference = bqRef, Lot = "1" },
            new Tool { ToolId = $"BQ2-{p}", Type = ToolType.Bq, Reference = bqRef, Lot = "2" },
            new Tool { ToolId = $"BQ3-{p}", Type = ToolType.Bq, Reference = bqRef, Lot = "3" },
            new Tool { ToolId = $"BQ4-{p}", Type = ToolType.Bq, Reference = bqRef, Lot = "4" });
        await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = $"BQ1-{p}", MachineId = b1.Id });
        var comCm = new JobOn { Reference = total, ProductionNumber = $"{p}1", MachineId = b2.Id };
        var outra = new JobOn { Reference = total, ProductionNumber = $"{p}2", MachineId = b2.Id };
        db.JobOns.AddRange(comCm, outra);
        await db.SaveChangesAsync();
        db.CmContexts.Add(new CmContext { JobOnId = comCm.Id, ToolId = $"CM-{p}" });
        db.BqContexts.AddRange(
            new BqContext { ReplacedJobOnId = comCm.Id, ReplacedAtUtc = DateTime.UtcNow, ToolId = $"BQ3-{p}" },
            new BqContext { JobOnId = outra.Id, ToolId = $"BQ2-{p}" });
        await db.SaveChangesAsync();

        var query = new ToolCandidateQuery(db);
        async Task<string[]> Find(bool soRelacionadas) => (await query.FindPageAsync(new ToolCandidateFilter(
                Type: ToolType.Bq, MachineId: b1.Id, ProductionReference: total, IncluirSemLinha: true,
                CompanheiraToolIds: new[] { $"CM-{p}" }, ReferenciaTotal: total, SoRelacionadas: soRelacionadas), 1, 20))
            .Items.Select(c => c.ToolId).ToArray();

        Assert.Equal(new[] { $"BQ3-{p}", $"BQ2-{p}", $"BQ1-{p}", $"BQ4-{p}" }, await Find(false));
        Assert.Equal(new[] { $"BQ3-{p}", $"BQ2-{p}", $"BQ1-{p}" }, await Find(true));
    }
}
