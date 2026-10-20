using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using DMO.Alpha.Web.Pages.Boquilhas;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-2/F-14): a procura do «Procurar / trocar de
/// boquilha ou produção» traduzida no PostgreSQL. O InMemory dos outros
/// testes avalia o LINQ em C# e não apanha uma expressão que o Npgsql não
/// traduz. Só corre com DMO_POSTGRES_CONNECTION numa base descartável cujo
/// nome contenha «test» (a do CI local); sem ela, salta.
/// </summary>
public sealed class BoquilhasProcuraPostgresTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");

    [SkippableFact]
    public async Task ProcuraBq_PeloInicioDaReferenciaOuDaParteBq_NoPostgres()
    {
        Skip.If(string.IsNullOrWhiteSpace(Connection), "DMO_POSTGRES_CONNECTION is not configured (disposable PostgreSQL test database).");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(Connection!) { Pooling = false };
        Skip.IfNot(
            builder.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "DMO_POSTGRES_CONNECTION must name a disposable database containing «test».");

        await using var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(builder.ConnectionString).Options);
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var machine = new Machine { Code = $"PGB-{letras}" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = $"BQ-PGA-{p}", Type = ToolType.Bq, Reference = $"Q{letras}", Lot = "1" },
            new Tool { ToolId = $"BQ-PGB-{p}", Type = ToolType.Bq, Reference = $"W{letras}", Lot = $"L{letras}" });
        await db.SaveChangesAsync();
        // A BQ «A» foi usada na produção {p}1Q…; a «B» na 9{p}… (o prefixo a meio).
        var jA = new JobOn { Reference = $"{p}1Q{letras}", ProductionNumber = $"N{letras}A", MachineId = machine.Id };
        var jB = new JobOn { Reference = $"9{p}2W{letras}", ProductionNumber = $"N{letras}B", MachineId = machine.Id };
        db.JobOns.AddRange(jA, jB);
        await db.SaveChangesAsync();
        db.BqContexts.AddRange(
            new BqContext { JobOnId = jA.Id, ToolId = $"BQ-PGA-{p}" },
            new BqContext { JobOnId = jB.Id, ToolId = $"BQ-PGB-{p}" });
        await db.SaveChangesAsync();

        try
        {
            async Task<string[]> Procura(string texto) =>
                await IndexModel.BqSearchTools(db, ReferenceMatch.Normalize(texto)!).Select(t => t.ToolId).ToArrayAsync();

            // A referência de uma produção pelo início; a meio, não.
            Assert.Equal(new[] { $"BQ-PGA-{p}" }, await Procura(p));
            Assert.Empty(await Procura($"{p}2W"));
            // A parte BQ da referência da BQ e da produção, sem distinguir maiúsculas.
            Assert.Equal(new[] { $"BQ-PGA-{p}" }, await Procura($"q{letras.ToLowerInvariant()}"));
            Assert.Equal(new[] { $"BQ-PGB-{p}" }, await Procura($"w{letras}"));
            // O lote e o nº da produção por parte do texto.
            Assert.Equal(new[] { $"BQ-PGB-{p}" }, await Procura($"l{letras}"));
            Assert.Equal(new[] { $"BQ-PGB-{p}" }, await Procura($"{letras}B"));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.BqContexts.Where(c => c.JobOnId == jA.Id || c.JobOnId == jB.Id).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == $"BQ-PGA-{p}" || t.ToolId == $"BQ-PGB-{p}").ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}
