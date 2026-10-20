using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Prova que o registo canónico de Tools resolve uma Tool a partir do ToolId
/// já persistido num contexto de produção (BqContext), sem derivar identidade
/// dos factos da Tool e sem alterar a semântica dos contextos existentes.
/// </summary>
public sealed class ToolRegistryIntegrationTests : IDisposable
{
    private readonly DmoDbContext _db;

    public ToolRegistryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task BqContext_ToolId_ResolvesCanonicalTool()
    {
        var machine = new Machine { Code = "M-001" };
        var jobOn = new JobOn
        {
            Reference = "REF-1001",
            ProductionNumber = "PN-1001",
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" };

        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        _db.Tools.Add(new Tool
        {
            ToolId = "BQ-TOOL-9",
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "12"
        });
        await _db.SaveChangesAsync();

        var resolved = await (
            from context in _db.BqContexts
            join tool in _db.Tools on context.ToolId equals tool.ToolId
            where context.Id == bq.Id
            select tool
        ).FirstOrDefaultAsync();

        Assert.NotNull(resolved);
        Assert.Equal("BQ-TOOL-9", resolved.ToolId);
        Assert.Equal(ToolType.Bq, resolved.Type);
        Assert.Equal("9389T194", resolved.Reference);
        Assert.Equal("12", resolved.Lot);
    }

    [Fact]
    public async Task BqContext_ToolIdWithoutRegisteredTool_DoesNotResolve()
    {
        var machine = new Machine { Code = "M-002" };
        var jobOn = new JobOn
        {
            Reference = "REF-1002",
            ProductionNumber = "PN-1002",
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-UNREGISTERED" };

        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();

        var resolved = await (
            from context in _db.BqContexts
            join tool in _db.Tools on context.ToolId equals tool.ToolId
            where context.Id == bq.Id
            select tool
        ).FirstOrDefaultAsync();

        // Sem Tool registada não há resolução nem identidade inferida.
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Tools_SameReferenceDifferentLot_PersistAsDistinctCanonicalTools()
    {
        // Um lote diferente é uma Tool diferente e portanto um tool_id
        // diferente; a referência comum não cria unicidade nem rejeita a
        // segunda linha.
        _db.Tools.Add(new Tool
        {
            ToolId = "BQ-TOOL-A",
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "12"
        });
        _db.Tools.Add(new Tool
        {
            ToolId = "BQ-TOOL-B",
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "13"
        });
        await _db.SaveChangesAsync();

        var lot12 = await _db.Tools.FindAsync("BQ-TOOL-A");
        var lot13 = await _db.Tools.FindAsync("BQ-TOOL-B");

        Assert.NotNull(lot12);
        Assert.NotNull(lot13);
        Assert.Equal("9389T194", lot12!.Reference);
        Assert.Equal("9389T194", lot13!.Reference);
        Assert.NotEqual(lot12.Lot, lot13.Lot);
        Assert.NotEqual(lot12.ToolId, lot13.ToolId);
    }
}
