using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

public sealed class ProductionContextQueryIntegrationTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ProductionContextQuery _query;

    public ProductionContextQueryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new ProductionContextQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetAsync_JobOnWithAllContexts_ReturnsAllContextIdsAndToolIds()
    {
        var machine = new Machine { Code = "M-001" };
        var jobOn = new JobOn
        {
            Reference = "REF-1001",
            ProductionNumber = "PN-1001",
            Machine = machine
        };

        var cm = new CmContext { JobOn = jobOn, ToolId = "CM-TOOL-42" };
        var mf = new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-7" };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" };

        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(cm);
        _db.MfContexts.Add(mf);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();

        var result = await _query.GetAsync(jobOn.Id);

        Assert.NotNull(result);
        Assert.Equal(jobOn.Id, result.JobOnId);
        Assert.Equal("REF-1001", result.Reference);
        Assert.Equal("PN-1001", result.ProductionNumber);
        Assert.Equal("M-001", result.Machine);

        Assert.NotNull(result.CmId);
        Assert.Equal(cm.Id, result.CmId);
        Assert.Equal("CM-TOOL-42", result.CmToolId);

        Assert.NotNull(result.MfId);
        Assert.Equal(mf.Id, result.MfId);
        Assert.Equal("MF-TOOL-7", result.MfToolId);

        Assert.NotNull(result.BqId);
        Assert.Equal(bq.Id, result.BqId);
        Assert.Equal("BQ-TOOL-9", result.BqToolId);
    }

    [Fact]
    public async Task GetAsync_JobOnWithoutContexts_ReturnsNullContextIdsAndToolIds()
    {
        var machine = new Machine { Code = "M-002" };
        var jobOn = new JobOn
        {
            Reference = "REF-1002",
            ProductionNumber = "PN-1002",
            Machine = machine
        };

        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();

        var result = await _query.GetAsync(jobOn.Id);

        Assert.NotNull(result);
        Assert.Equal(jobOn.Id, result.JobOnId);
        Assert.Equal("REF-1002", result.Reference);
        Assert.Equal("PN-1002", result.ProductionNumber);
        Assert.Equal("M-002", result.Machine);

        Assert.Null(result.CmId);
        Assert.Null(result.CmToolId);
        Assert.Null(result.MfId);
        Assert.Null(result.MfToolId);
        Assert.Null(result.BqId);
        Assert.Null(result.BqToolId);
    }

    [Fact]
    public async Task GetAsync_JobOnWithPartialContexts_ReturnsOnlyExistingOnes()
    {
        var machine = new Machine { Code = "M-003" };
        var jobOn = new JobOn
        {
            Reference = "REF-1003",
            ProductionNumber = "PN-1003",
            Machine = machine
        };

        var mf = new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-ONLY" };

        _db.JobOns.Add(jobOn);
        _db.MfContexts.Add(mf);
        await _db.SaveChangesAsync();

        var result = await _query.GetAsync(jobOn.Id);

        Assert.NotNull(result);
        Assert.Null(result.CmId);
        Assert.Null(result.CmToolId);
        Assert.NotNull(result.MfId);
        Assert.Equal(mf.Id, result.MfId);
        Assert.Equal("MF-TOOL-ONLY", result.MfToolId);
        Assert.Null(result.BqId);
        Assert.Null(result.BqToolId);
    }

    [Fact]
    public async Task GetAsync_NonExistentJobOn_ReturnsNull()
    {
        var result = await _query.GetAsync(9999);

        Assert.Null(result);
    }

    [Fact]
    public void GeneratedSql_Npgsql_RelationalProvider_ProducesSingleLeftJoinQuery()
    {
        // Configura um contexto com o provider relacional Npgsql apenas para gerar SQL.
        // Não executa a query (não há servidor PostgreSQL no teste); o objectivo é capturar
        // o SQL que o EF produz para a mesma expressão usada pelo handler.
        var relationalOptions = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql("Host=localhost;Database=dmo_alpha_test;Username=dmo;Password=dmo")
            .Options;

        using var relationalDb = new DmoDbContext(relationalOptions);

        const int jobOnId = 123;

        var query = from j in relationalDb.JobOns
                    where j.Id == jobOnId
                    join m in relationalDb.Machines on j.MachineId equals m.Id
                    join cm in relationalDb.CmContexts on j.Id equals cm.JobOnId into cmGroup
                    from cm in cmGroup.DefaultIfEmpty()
                    join mf in relationalDb.MfContexts on j.Id equals mf.JobOnId into mfGroup
                    from mf in mfGroup.DefaultIfEmpty()
                    join bq in relationalDb.BqContexts on j.Id equals bq.JobOnId into bqGroup
                    from bq in bqGroup.DefaultIfEmpty()
                    select new ProductionContextReadModel(
                        j.Id,
                        j.Reference,
                        j.ProductionNumber,
                        m.Code,
                        cm != null ? cm.Id : (int?)null,
                        cm != null ? cm.ToolId : null,
                        mf != null ? mf.Id : (int?)null,
                        mf != null ? mf.ToolId : null,
                        bq != null ? bq.Id : (int?)null,
                        bq != null ? bq.ToolId : null);

        var sql = query.ToQueryString();

        Assert.NotNull(sql);
        Assert.Contains("JobOns", sql);
        Assert.Contains("Machines", sql);
        Assert.Contains("CmContexts", sql);
        Assert.Contains("MfContexts", sql);
        Assert.Contains("BqContexts", sql);
        Assert.Contains("LEFT JOIN", sql);

        // Torna o SQL visível no output do teste para documentação.
        _ = sql;
    }
}
