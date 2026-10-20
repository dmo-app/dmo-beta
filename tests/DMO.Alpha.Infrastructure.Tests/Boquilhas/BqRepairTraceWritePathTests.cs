using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova o write path do registo de reparação de Boquilhas: criação de um
/// bq_repair_trace_id ancorado a um tool_id canónico (bq_id não resolvido,
/// sem inferir Job On, no máximo um trace não resolvido por tool_id — uma
/// segunda criação reutiliza o existente) e associação posterior do mesmo
/// trace ao seu bq_id (preserva identidade e movimentos; LIMPA a âncora
/// direta tool_id, que passa a alcançar-se via bq_id; não altera JobOn nem
/// BqContext; respeita um-trace-por-bq_id; não reassocia nem mistura
/// tool_ids).
/// </summary>
public sealed class BqRepairTraceWritePathTests : IDisposable
{
    /// <summary>Sem chaves de config: as saídas deste teste gravam sem reparador.</summary>
    private sealed class NoRouteSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private readonly DmoDbContext _db;
    private readonly CreateBqRepairTraceHandler _createHandler;
    private readonly AssociateBqRepairTraceToContextHandler _associateHandler;
    private readonly RegisterBqMovementHandler _movementHandler;

    public BqRepairTraceWritePathTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _createHandler = new CreateBqRepairTraceHandler(_db);
        _associateHandler = new AssociateBqRepairTraceToContextHandler(_db);
        _movementHandler = new RegisterBqMovementHandler(_db, new NoRouteSettings());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Tool> SeedToolAsync(string toolId)
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

    private Task<CreateBqRepairTraceOutcome> CreateTraceAsync(string toolId) =>
        _createHandler.HandleAsync(new CreateBqRepairTrace(toolId));

    private Task<AssociateBqRepairTraceToContextOutcome> AssociateAsync(
        int traceId,
        int bqContextId) =>
        _associateHandler.HandleAsync(
            new AssociateBqRepairTraceToContext(traceId, bqContextId));

    // ------------------------------------------------------------------
    // Create
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistingToolId_CreatesTraceWithNullBqContextId()
    {
        await SeedToolAsync("BQ-TOOL-9");

        var outcome = await CreateTraceAsync("BQ-TOOL-9");

        Assert.Equal(CreateBqRepairTraceResult.Success, outcome.Result);
        Assert.NotNull(outcome.Trace);
        Assert.Equal("BQ-TOOL-9", outcome.Trace!.ToolId);
        Assert.Null(outcome.Trace.BqContextId);

        var persisted = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == outcome.Trace.BqRepairTraceId);
        Assert.Equal("BQ-TOOL-9", persisted.ToolId);
        Assert.Null(persisted.BqContextId);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task UnknownToolId_Refused()
    {
        var outcome = await CreateTraceAsync("BQ-TOOL-UNREGISTERED");

        Assert.Equal(CreateBqRepairTraceResult.ToolNotFound, outcome.Result);
        Assert.Null(outcome.Trace);
        Assert.Equal(0, await _db.BqRepairTraces.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task SecondCreate_ForSameTool_ReusesExistingUnresolvedTrace()
    {
        // No máximo um trace pré-produção não resolvido por tool_id: uma
        // segunda criação para a mesma Tool reutiliza o trace existente em
        // vez de criar um segundo.
        await SeedToolAsync("BQ-TOOL-9");

        var first = await CreateTraceAsync("BQ-TOOL-9");
        var second = await CreateTraceAsync("BQ-TOOL-9");

        Assert.Equal(CreateBqRepairTraceResult.Success, first.Result);
        Assert.Equal(CreateBqRepairTraceResult.ReusedExistingUnresolvedTrace, second.Result);
        Assert.NotNull(second.Trace);
        Assert.Equal(first.Trace!.BqRepairTraceId, second.Trace!.BqRepairTraceId);
        Assert.Equal("BQ-TOOL-9", second.Trace.ToolId);
        Assert.Null(second.Trace.BqContextId);

        var unresolved = await _db.BqRepairTraces.AsNoTracking()
            .Where(t => t.ToolId == "BQ-TOOL-9")
            .ToListAsync();
        Assert.Single(unresolved);
        Assert.Null(unresolved[0].BqContextId);
        Assert.Equal(1, await _db.BqRepairTraces.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_AfterAssociation_CreatesNewTraceBecauseAnchorWasCleared()
    {
        // Após a associação a âncora direta é limpa (ToolId = null), pelo
        // que o trace associado deixa de contar como "não resolvido" para o
        // tool_id: uma nova criação abre um trace pré-produção novo.
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");
        var associated = await AssociateAsync(created.Trace!.BqRepairTraceId, bq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, associated.Result);

        var next = await CreateTraceAsync("BQ-TOOL-9");

        Assert.Equal(CreateBqRepairTraceResult.Success, next.Result);
        Assert.NotEqual(created.Trace.BqRepairTraceId, next.Trace!.BqRepairTraceId);
        Assert.Null(next.Trace.BqContextId);
        Assert.Equal(2, await _db.BqRepairTraces.AsNoTracking().CountAsync());
    }

    // ------------------------------------------------------------------
    // Associate
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistingTrace_AssociatesToExistingBqContext()
    {
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");

        var outcome = await AssociateAsync(created.Trace!.BqRepairTraceId, bq.Id);

        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, outcome.Result);
        Assert.NotNull(outcome.Trace);
        Assert.Equal(created.Trace.BqRepairTraceId, outcome.Trace!.BqRepairTraceId);
        // O outcome reporta o tool_id EFETIVO (alcançado via bq_id →
        // BqContext.ToolId); a âncora DIRETA persistida é limpa na associação
        // (verificada abaixo na entidade persistida).
        Assert.Equal("BQ-TOOL-9", outcome.Trace.ToolId);
        Assert.Equal(bq.Id, outcome.Trace.BqContextId);

        var persisted = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == created.Trace.BqRepairTraceId);
        Assert.Equal(bq.Id, persisted.BqContextId);
        Assert.Null(persisted.ToolId);
    }

    [Fact]
    public async Task Association_PreservesTraceIdentityAndMovements_ClearsToolId()
    {
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var traceId = created.Trace!.BqRepairTraceId;

        // Movimentos pré-produção no trace.
        var saida = await _movementHandler.HandleAsync(
            new RegisterBqMovement(traceId, "saida", 5));
        var entrada = await _movementHandler.HandleAsync(
            new RegisterBqMovement(traceId, "entrada", 7));
        Assert.Equal(RegisterBqMovementResult.Success, saida.Result);
        Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);

        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");
        var outcome = await AssociateAsync(traceId, bq.Id);

        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, outcome.Result);
        Assert.Equal(traceId, outcome.Trace!.BqRepairTraceId);
        // O outcome reporta o tool_id efetivo (via bq_id); a âncora direta
        // persistida é limpa na associação (verificada abaixo na entidade).
        Assert.Equal("BQ-TOOL-9", outcome.Trace.ToolId);

        // O mesmo trace (mesma identidade), agora com bq_id e sem âncora
        // direta; os movimentos existentes permanecem exatamente como
        // estavam.
        var traceAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == traceId);
        Assert.Null(traceAfter.ToolId);
        Assert.Equal(bq.Id, traceAfter.BqContextId);

        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == traceId)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(saida.Movement!.MovementId, movements[0].Id);
        Assert.Equal(entrada.Movement!.MovementId, movements[1].Id);
        Assert.Equal(BqMovementType.Saida, movements[0].Type);
        Assert.Equal(5, movements[0].Quantity);
        Assert.Null(movements[0].Discrepancy);
        Assert.Equal(BqMovementType.Entrada, movements[1].Type);
        Assert.Equal(7, movements[1].Quantity);
        Assert.Equal(-2, movements[1].Discrepancy);

        // Nenhum trace foi recriado nem copiado.
        Assert.Equal(1, await _db.BqRepairTraces.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task UnknownTrace_Refused()
    {
        await SeedToolAsync("BQ-TOOL-9");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");

        var outcome = await AssociateAsync(9999, bq.Id);

        Assert.Equal(AssociateBqRepairTraceToContextResult.TraceNotFound, outcome.Result);
        Assert.Null(outcome.Trace);
        Assert.False(await _db.BqRepairTraces.AsNoTracking()
            .AnyAsync(t => t.BqContextId == bq.Id));
    }

    [Fact]
    public async Task UnknownBqContext_Refused()
    {
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");

        var outcome = await AssociateAsync(created.Trace!.BqRepairTraceId, 9999);

        Assert.Equal(AssociateBqRepairTraceToContextResult.BqContextNotFound, outcome.Result);
        Assert.Null(outcome.Trace);

        var traceAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == created.Trace.BqRepairTraceId);
        Assert.Null(traceAfter.BqContextId);
    }

    [Fact]
    public async Task BqContextAlreadyTracedByAnotherTrace_RefusedCleanly()
    {
        await SeedToolAsync("BQ-TOOL-9");
        await SeedToolAsync("BQ-TOOL-7");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");

        var first = await CreateTraceAsync("BQ-TOOL-9");

        // Um segundo trace não resolvido, semeado diretamente com um tool_id
        // DIFERENTE (o handler de criação reutilizaria o primeiro para a mesma
        // Tool). tool_id distinto mantém a fixture schema-legal em ambos os
        // providers: duas linhas não resolvidas com o mesmo tool_id violariam o
        // índice único parcial no PostgreSQL. Prova apenas o guarda "um bq_id
        // tem um único trace" contra um candidato distinto — esse guarda (4)
        // corre ANTES do guarda de correspondência de tool (5), pelo que o
        // tool_id diferente não altera o resultado provado.
        var secondTrace = new BqRepairTrace { ToolId = "BQ-TOOL-7", BqContextId = null };
        _db.BqRepairTraces.Add(secondTrace);
        await _db.SaveChangesAsync();

        var firstOutcome = await AssociateAsync(first.Trace!.BqRepairTraceId, bq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, firstOutcome.Result);

        // Um bq_id tem um único bq_repair_trace_id: recusa tipada limpa.
        var secondOutcome = await AssociateAsync(secondTrace.Id, bq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.BqContextAlreadyTraced, secondOutcome.Result);
        Assert.Null(secondOutcome.Trace);

        var secondAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == secondTrace.Id);
        Assert.Null(secondAfter.BqContextId);
        // A âncora direta permanece intacta numa associação recusada.
        Assert.Equal("BQ-TOOL-7", secondAfter.ToolId);

        var owner = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.BqContextId == bq.Id);
        Assert.Equal(first.Trace.BqRepairTraceId, owner.Id);
    }

    [Fact]
    public async Task Association_DoesNotModifyJobOnOrBqContextData()
    {
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");

        var jobOnId = bq.JobOnId;
        var machineId = (await _db.JobOns.AsNoTracking()
            .SingleAsync(j => j.Id == jobOnId)).MachineId;

        var outcome = await AssociateAsync(created.Trace!.BqRepairTraceId, bq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, outcome.Result);

        var jobOnAfter = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal("9389T194", jobOnAfter.Reference);
        Assert.Equal("PN-202609", jobOnAfter.ProductionNumber);
        Assert.Equal(machineId, jobOnAfter.MachineId);

        var contextAfter = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == bq.Id);
        Assert.Equal(jobOnId, contextAfter.JobOnId);
        Assert.Equal("BQ-TOOL-9", contextAfter.ToolId);
    }

    // ------------------------------------------------------------------
    // Derived refusals grounded in current authority
    // ------------------------------------------------------------------

    [Fact]
    public async Task AlreadyAssociatedTrace_ReassociationRefused()
    {
        // Uma produção posterior nunca toma posse de um trace anterior:
        // os retornos tardios ficam no trace de origem.
        await SeedToolAsync("BQ-TOOL-9");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var firstBq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202609");
        var secondBq = await SeedProductionBqContextAsync("BQ-TOOL-9", "PN-202610");

        var first = await AssociateAsync(created.Trace!.BqRepairTraceId, firstBq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, first.Result);

        var second = await AssociateAsync(created.Trace.BqRepairTraceId, secondBq.Id);
        Assert.Equal(AssociateBqRepairTraceToContextResult.TraceAlreadyAssociated, second.Result);
        Assert.Null(second.Trace);

        var traceAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == created.Trace.BqRepairTraceId);
        Assert.Equal(firstBq.Id, traceAfter.BqContextId);
        Assert.False(await _db.BqRepairTraces.AsNoTracking()
            .AnyAsync(t => t.BqContextId == secondBq.Id));
    }

    [Fact]
    public async Task ToolMismatchBetweenTraceAndBqContext_Refused()
    {
        // A correlação trace ↔ bq_id faz-se pelo mesmo tool_id canónico.
        await SeedToolAsync("BQ-TOOL-9");
        await SeedToolAsync("BQ-TOOL-7");
        var created = await CreateTraceAsync("BQ-TOOL-9");
        var otherToolBq = await SeedProductionBqContextAsync("BQ-TOOL-7", "PN-202609");

        var outcome = await AssociateAsync(created.Trace!.BqRepairTraceId, otherToolBq.Id);

        Assert.Equal(
            AssociateBqRepairTraceToContextResult.TraceToolDoesNotMatchBqContextTool,
            outcome.Result);
        Assert.Null(outcome.Trace);

        var traceAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == created.Trace.BqRepairTraceId);
        Assert.Null(traceAfter.BqContextId);
        Assert.False(await _db.BqRepairTraces.AsNoTracking()
            .AnyAsync(t => t.BqContextId == otherToolBq.Id));
    }

    // ------------------------------------------------------------------
    // O CASO BACKFILL (o Owner 2026-10-14, verbatim: «criar um jobon, e
    // poder associar um registo ao mesmo»): o registo pré-produção com
    // movimentos antigos (as datas/horas reais da A1) associa-se DEPOIS a
    // um Job On RETROATIVO criado pelo fluxo existente do JobOn (as datas
    // do passado gravam como vêm) — os movimentos, as suas datas reais e o
    // trace mantêm-se intactos; nada é recriado nem copiado; o histórico
    // passa a mostrar o nº/referência da produção (o fim do «em branco até
    // ser associado»).
    // ------------------------------------------------------------------

    [Fact]
    public async Task PreProductionTrace_WithRetroactiveMovements_AssociatesToRetroactiveJobOn_KeepsMovementsAndDates()
    {
        var tool = await SeedToolAsync("BQ-TOOL-BACKFILL");
        var created = await CreateTraceAsync("BQ-TOOL-BACKFILL");
        var traceId = created.Trace!.BqRepairTraceId;

        // OS MOVIMENTOS ANTIGOS do backfill: datas/horas reais de há ~2
        // meses (o occurredAt do form «Data e hora reais»), gravados
        // ANTES de qualquer produção existir.
        var retroLocal = DateTime.SpecifyKind(
            DmoBusinessTime.Now.AddDays(-60).Date.AddHours(9), DateTimeKind.Unspecified);
        var retroUtc = DmoBusinessTime.ToUtc(retroLocal);
        var saida = await _movementHandler.HandleAsync(
            new RegisterBqMovement(traceId, "saida", 3, null, retroUtc));
        var entrada = await _movementHandler.HandleAsync(
            new RegisterBqMovement(traceId, "entrada", 2, null, retroUtc.AddHours(4)));
        Assert.Equal(RegisterBqMovementResult.Success, saida.Result);
        Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);
        Assert.Null(entrada.Movement!.Discrepancy); // 2 explicadas sobre 3 fora

        // O JOB ON RETROATIVO criado DEPOIS pelo fluxo existente (o MF é
        // obrigatório na casa; as datas do passado gravam como vêm —
        // CreateJobOnHandler.cs:133-134) com a BQ do MESMO tool_id do
        // trace.
        var mfTool = new Tool
        {
            ToolId = "MF-BACKFILL",
            Type = ToolType.Mf,
            Reference = "5447T194",
            Lot = "1"
        };
        _db.Tools.Add(mfTool);
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();

        var inicioRetroativo = DateOnly.FromDateTime(retroLocal.Date);
        var createJobOn = new DMO.Alpha.Infrastructure.Production.CreateJobOnHandler(_db);
        var jobOn = await createJobOn.HandleAsync(new CreateJobOn(
            "202601",
            "5447T194",
            machine.Id,
            MfToolId: "MF-BACKFILL",
            BqToolId: "BQ-TOOL-BACKFILL",
            InicioEm: inicioRetroativo,
            FimEm: inicioRetroativo.AddDays(5)));
        Assert.Equal(CreateJobOnResult.Success, jobOn.Result);
        Assert.NotNull(jobOn.JobOn!.BqId);

        // A ASSOCIAÇÃO pelo bq_id resolvido: o handler preserva o trace e
        // os movimentos e limpa a âncora direta.
        var outcome = await AssociateAsync(traceId, jobOn.JobOn.BqId!.Value);
        Assert.Equal(AssociateBqRepairTraceToContextResult.Success, outcome.Result);

        var traceAfter = await _db.BqRepairTraces.AsNoTracking()
            .SingleAsync(t => t.Id == traceId);
        Assert.Equal(jobOn.JobOn.BqId.Value, traceAfter.BqContextId);
        Assert.Null(traceAfter.ToolId);

        // OS MOVIMENTOS E AS DATAS MANTÊM-SE: mesmos ids, mesmas
        // quantidades, as mesmas datas/horas reais — nada recriado,
        // nada copiado.
        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == traceId)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(saida.Movement!.MovementId, movements[0].Id);
        Assert.Equal(entrada.Movement!.MovementId, movements[1].Id);
        Assert.Equal(retroUtc, movements[0].OccurredAtUtc);
        Assert.Equal(retroUtc.AddHours(4), movements[1].OccurredAtUtc);
        Assert.Equal(3, movements[0].Quantity);
        Assert.Equal(2, movements[1].Quantity);

        // O FIM DO «EM BRANCO ATÉ SER ASSOCIADO»: o histórico do dia
        // retroativo mostra OS DOIS movimentos com o nº e a referência da
        // produção (a cadeia movimento → trace → bq_id → jobon), NA ORDEM
        // da data real (a entrada das 13:00 antes da saída das 09:00).
        var history = new BqToolHistoryQuery(_db);
        var retroDay = DateOnly.FromDateTime(
            DmoBusinessTime.ToLocal(retroUtc));
        var dayMovements = await history.ListMovementsAsync(retroDay, retroDay, null, null, null, null);
        Assert.Equal(2, dayMovements.Count);
        Assert.Equal(entrada.Movement!.MovementId, dayMovements[0].MovementId);
        Assert.Equal(saida.Movement!.MovementId, dayMovements[1].MovementId);
        Assert.All(dayMovements, item =>
        {
            Assert.Equal("202601", item.ProductionNumber);
            Assert.Equal("5447T194", item.Reference);
        });
    }
}
