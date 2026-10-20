using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova as âncoras canónicas do registo de reparação de Boquilhas:
/// o trace resolve o tool_id canónico; pode existir antes do Job On com
/// bq_id não resolvido, mas no máximo UM trace não resolvido por tool_id
/// (índice único parcial sobre ToolId WHERE "BqContextId" IS NULL); a
/// associação referencia o bq_id sem mudar a identidade do trace nem do
/// contexto de produção; a mesma Tool pode ter vários traces de produção
/// ao longo do tempo (um por bq_id) porque a âncora direta é limpa na
/// associação e a Tool passa a alcançar-se via bq_id → BqContext.ToolId.
/// </summary>
public sealed class BqRepairTraceIntegrationTests : IDisposable
{
    private readonly DmoDbContext _db;

    public BqRepairTraceIntegrationTests()
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

    private async Task<Tool> SeedBqToolAsync(string toolId)
    {
        var tool = new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "12"
        };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqContext> SeedProductionBqContextAsync(
        string toolId,
        string productionNumber)
    {
        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = productionNumber,
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = toolId };

        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        return bq;
    }

    [Fact]
    public async Task Trace_ResolvesCanonicalToolId()
    {
        var tool = await SeedBqToolAsync("BQ-TOOL-9");

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var resolved = await _db.BqRepairTraces
            .AsNoTracking()
            .Include(t => t.Tool)
            .SingleAsync(t => t.Id == trace.Id);

        Assert.Equal("BQ-TOOL-9", resolved.ToolId);
        Assert.NotNull(resolved.Tool);
        Assert.Equal(tool.ToolId, resolved.Tool!.ToolId);
        Assert.Equal(ToolType.Bq, resolved.Tool.Type);
        Assert.Equal("9389T194", resolved.Tool.Reference);
        Assert.Equal("12", resolved.Tool.Lot);
    }

    [Fact]
    public async Task PreProductionTrace_PersistsWithoutBqContext_OneUnresolvedPerToolByIndex()
    {
        // Um trace pode existir antes do Job On, com bq_id não resolvido. A
        // regra fechada é "no máximo um trace não resolvido por tool_id",
        // imposta por um índice único parcial (ToolId WHERE "BqContextId"
        // IS NULL) — não pela coexistência de dois pendentes da mesma Tool.
        await SeedBqToolAsync("BQ-TOOL-9");

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var pending = await _db.BqRepairTraces
            .AsNoTracking()
            .Where(t => t.ToolId == "BQ-TOOL-9" && t.BqContextId == null)
            .ToListAsync();
        Assert.Single(pending);

        // O modelo garante a unicidade parcial sobre a âncora não resolvida.
        var entityType = _db.Model.FindEntityType(typeof(BqRepairTrace));
        Assert.NotNull(entityType);
        var toolIndex = entityType!.GetIndexes()
            .Single(i => i.Properties.Count == 1
                && i.Properties[0].Name == nameof(BqRepairTrace.ToolId));
        Assert.True(toolIndex.IsUnique);
        Assert.Equal("\"BqContextId\" IS NULL", toolIndex.GetFilter());
    }

    [Fact]
    public async Task Association_ClearsToolIdAnchor_ToolReachedViaBqContext()
    {
        // Regra 3/4: a associação referencia o bq_id sem mudar a identidade do
        // trace, LIMPA a âncora direta tool_id (que passa a alcançar-se via
        // bq_id → BqContext.ToolId) e não muta o contexto de produção. É
        // exercitada através do handler canónico de associação, não por
        // mutação direta da entidade.
        await SeedBqToolAsync("BQ-TOOL-9");

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");
        var bqJobOnId = bq.JobOnId;

        var handler = new AssociateBqRepairTraceToContextHandler(_db);
        var outcome = await handler.HandleAsync(
            new AssociateBqRepairTraceToContext(trace.Id, bq.Id));

        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, outcome.Result);
        Assert.NotNull(outcome.Trace);
        // Mesma identidade preservada (nenhum trace recriado/copiado).
        Assert.Equal(trace.Id, outcome.Trace!.BqRepairTraceId);
        Assert.Equal(bq.Id, outcome.Trace.BqContextId);
        // O outcome reporta o tool_id EFETIVO (via bq_id), não a âncora direta.
        Assert.Equal("BQ-TOOL-9", outcome.Trace.ToolId);

        var resolved = await _db.BqRepairTraces
            .AsNoTracking()
            .Include(t => t.Tool)
            .Include(t => t.BqContext!)
            .ThenInclude(c => c.JobOn)
            .SingleAsync(t => t.Id == trace.Id);

        // Âncora direta LIMPA na associação: a Tool já não é navegável
        // diretamente a partir do trace.
        Assert.Null(resolved.ToolId);
        Assert.Null(resolved.Tool);

        // A Tool canónica alcança-se via bq_id → BqContext.ToolId.
        Assert.Equal(bq.Id, resolved.BqContextId);
        Assert.NotNull(resolved.BqContext);
        Assert.Equal(bq.Id, resolved.BqContext!.Id);
        Assert.Equal("BQ-TOOL-9", resolved.BqContext.ToolId);

        // O contexto de produção existente não foi mutado pela associação.
        var contextAfter = await _db.BqContexts
            .AsNoTracking()
            .Include(c => c.JobOn)
            .SingleAsync(c => c.Id == bq.Id);
        Assert.Equal("BQ-TOOL-9", contextAfter.ToolId);
        Assert.Equal(bqJobOnId, contextAfter.JobOnId);
        Assert.Equal("PN-202609", contextAfter.JobOn!.ProductionNumber);
    }

    [Fact]
    public async Task SameTool_ManyProductionTracesAcrossTime()
    {
        // A mesma Tool física pode ter muitos traces ao longo do tempo: cada
        // produção recebe o seu próprio bq_id e o seu próprio trace. São
        // traces já associados — a âncora direta tool_id está limpa e a Tool
        // alcança-se via bq_id → BqContext.ToolId; por isso não colidem com
        // o índice único parcial (que só cobre linhas não resolvidas).
        await SeedBqToolAsync("BQ-TOOL-9");

        var firstBq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");
        var secondBq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202610");

        _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = null, BqContextId = firstBq.Id });
        _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = null, BqContextId = secondBq.Id });
        await _db.SaveChangesAsync();

        var traces = await _db.BqRepairTraces
            .AsNoTracking()
            .Include(t => t.BqContext!)
            .Where(t => t.BqContextId == firstBq.Id || t.BqContextId == secondBq.Id)
            .OrderBy(t => t.Id)
            .ToListAsync();

        Assert.Equal(2, traces.Count);
        // Âncora direta limpa nos traces associados; a Tool canónica é
        // alcançada via bq_id → BqContext.ToolId.
        Assert.All(traces, t => Assert.Null(t.ToolId));
        Assert.All(traces, t => Assert.Equal("BQ-TOOL-9", t.BqContext!.ToolId));
        Assert.Equal(
            new int?[] { firstBq.Id, secondBq.Id },
            traces.Select(t => t.BqContextId).ToArray());
        Assert.Equal(2, traces.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void ModelShape_UniqueBqContext_AndPartialUniqueToolIdOnUnresolved()
    {
        // Relações canónicas decididas: um bq_id tem um único trace (índice
        // único sobre BqContextId) E no máximo um trace não resolvido por
        // tool_id (índice único parcial sobre ToolId WHERE "BqContextId" IS
        // NULL). O filtro parcial preserva os vários traces históricos já
        // associados da mesma Tool.
        var entityType = _db.Model.FindEntityType(typeof(BqRepairTrace));
        Assert.NotNull(entityType);

        var bqContextIndex = entityType!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 1
                && i.Properties[0].Name == nameof(BqRepairTrace.BqContextId));
        Assert.NotNull(bqContextIndex);
        Assert.True(bqContextIndex!.IsUnique);
        Assert.Null(bqContextIndex.GetFilter());

        var toolIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 1
                && i.Properties[0].Name == nameof(BqRepairTrace.ToolId));
        Assert.NotNull(toolIndex);
        Assert.True(toolIndex!.IsUnique);
        Assert.Equal("\"BqContextId\" IS NULL", toolIndex.GetFilter());
    }

    [Fact]
    public void RelationalSchema_UniqueBqContext_AndPartialUniqueToolId()
    {
        // Confirma no SQL relacional (Npgsql, sem servidor) as duas
        // unicidades decididas: um trace por bq_id e o índice único parcial
        // sobre ToolId restrito às linhas não resolvidas.
        var relationalOptions = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql("Host=localhost;Database=dmo_alpha_test;Username=dmo;Password=dmo")
            .Options;

        using var relationalDb = new DmoDbContext(relationalOptions);
        var sql = relationalDb.Database.GenerateCreateScript();

        Assert.Contains("BqRepairTraces", sql);
        Assert.Contains("UNIQUE INDEX \"IX_BqRepairTraces_BqContextId\"", sql);
        Assert.Contains("UNIQUE INDEX \"IX_BqRepairTraces_ToolId\"", sql);
        Assert.Contains("\"BqContextId\" IS NULL", sql);
    }
}
