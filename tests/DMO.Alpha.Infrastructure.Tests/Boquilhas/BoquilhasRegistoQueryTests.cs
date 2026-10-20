using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Prova a metade de LEITURA da regra 4: quando a âncora direta tool_id foi
/// limpa pela associação, <see cref="BoquilhasRegistoQuery.FindTraceAsync"/>
/// resolve a Tool canónica via bq_id → BqContext.ToolId (tool_id efetivo,
/// referência e lote), sem depender da âncora direta. O caminho de âncora
/// direta (trace pré-produção não resolvido) permanece coberto como contraste.
/// </summary>
public sealed class BoquilhasRegistoQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly BoquilhasRegistoQuery _query;

    public BoquilhasRegistoQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new BoquilhasRegistoQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task SeedToolAsync(string toolId, string reference, string lot)
    {
        _db.Tools.Add(new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = reference,
            Lot = lot
        });
        await _db.SaveChangesAsync();
    }

    private async Task<BqContext> SeedProductionBqContextAsync(
        string toolId,
        string reference,
        string productionNumber,
        string machineCode)
    {
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn
        {
            Reference = reference,
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
    public async Task AssociatedTrace_WithClearedAnchor_ResolvesToolViaBqContextChain()
    {
        // Estado canónico pós-associação: âncora direta limpa (ToolId = null),
        // bq_id resolvido. A Tool tem de ser alcançada via bq_id →
        // BqContext.ToolId.
        await SeedToolAsync("BQ-TOOL-9", "9389T194", "12");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "ZQREF-99", "PN-202609", "B1");

        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        // tool_id efetivo resolvido pela cadeia bq_id, não pela âncora direta.
        Assert.Equal("BQ-TOOL-9", result!.ToolId);
        Assert.Equal("9389T194", result.ToolReference);
        Assert.Equal("12", result.ToolLot);
        Assert.Equal(bq.Id, result.BqContextId);

        // Contexto de produção alcançável via bq_id.
        Assert.NotNull(result.Production);
        Assert.Equal("ZQREF-99", result.Production!.Reference);
        Assert.Equal("PN-202609", result.Production.ProductionNumber);
        Assert.Equal("B1", result.Production.MachineCode);
    }

    [Fact]
    public async Task UnresolvedTrace_ResolvesToolViaDirectAnchor()
    {
        // Contraste/regressão: um trace pré-produção não resolvido mantém a
        // âncora direta tool_id e resolve por ela (bq_id ainda null).
        await SeedToolAsync("BQ-TOOL-9", "9389T194", "12");

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9", BqContextId = null };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        Assert.Equal("BQ-TOOL-9", result!.ToolId);
        Assert.Equal("9389T194", result.ToolReference);
        Assert.Equal("12", result.ToolLot);
        Assert.Null(result.BqContextId);
        Assert.Null(result.Production);
    }

    // ------------------------------------------------------------------
    // A CONVENÇÃO DA DISCREPÂNCIA DO OWNER na leitura do Registo: o
    // histórico acumulado por trace SOMA OS EXCEDENTES anotados; as
    // faltantes nunca são anotadas (null) e o saldo fica no
    // legitimamente fora.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AccumulatedDiscrepancy_SumsTheAnnotatedExcesses()
    {
        // O HISTÓRICO ACUMULADO POR TRACE SOMA OS EXCEDENTES: dois ciclos
        // sai 3/entra 5 (cada entrada anota −2: as 2 a mais são peças de
        // fora do lote) → a discrepância acumulada do trace é −4.
        await SeedToolAsync("BQ-TOOL-9", "9389T194", "12");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "ZQREF-99", "PN-202609", "B1");

        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        _db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 3, Discrepancy = null },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 5, Discrepancy = -2 },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 2, Discrepancy = null },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 4, Discrepancy = -2 });
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        Assert.Equal(-4, result!.AccumulatedDiscrepancy);
        // Os retornos explicáveis ficam fora do saldo: 3 fora, 3 de volta;
        // 2 fora, 2 de volta — nada legitimamente fora no fim.
        Assert.Equal(0, result.QuantityLegitimatelyOut);
    }

    [Fact]
    public async Task MissingPiecesAreNotDiscrepancy_TheBalanceStaysLegitimatelyOut()
    {
        // SAI 5, ENTRA 3 (o lado oposto do Owner): SEM discrepância anotada
        // (a acumulada null — em branco, nunca zero como facto) e o saldo
        // +2 fica legitimamente fora (as 2 do lote em reparação).
        await SeedToolAsync("BQ-TOOL-9", "9389T194", "12");
        var bq = await SeedProductionBqContextAsync("BQ-TOOL-9", "ZQREF-99", "PN-202609", "B1");

        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        _db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 5, Discrepancy = null },
            new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 3, Discrepancy = null });
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        Assert.Null(result!.AccumulatedDiscrepancy);
        Assert.Equal(2, result.QuantityLegitimatelyOut);
    }

    [Fact]
    public async Task Movements_CarryTheRealDateAndTheFrozenRepairer()
    {
        // O facto humano do html («Cada movimento mantém o lote, a máquina,
        // a data e o reparador») no read model do Registo: a DATA/HORA REAL
        // (o occurredAt da A1) e o reparador CONGELADO da saída — as
        // colunas Data/hora e Reparador da tabela dos últimos movimentos
        // renderizam a partir daqui.
        await SeedToolAsync("BQ-TOOL-9", "9389T194", "12");
        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9", BqContextId = null };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var retroUtc = new DateTime(2026, 8, 14, 8, 0, 0, DateTimeKind.Utc);
        _db.BqMovements.AddRange(
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Saida,
                Quantity = 3,
                OccurredAtUtc = retroUtc,
                BqRepairerNameFrozen = "Vidroart"
            },
            new BqMovement
            {
                BqRepairTraceId = trace.Id,
                Type = BqMovementType.Entrada,
                Quantity = 3,
                OccurredAtUtc = retroUtc.AddHours(5)
            });
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Movements.Count);
        // OS ÚLTIMOS REGISTOS primeiro: a entrada (a mais recente) no topo.
        var entrada = result.Movements[0];
        var saida = result.Movements[1];
        Assert.Equal(retroUtc.AddHours(5), entrada.OccurredAtUtc);
        Assert.Null(entrada.BqRepairerNameFrozen);
        Assert.Equal(retroUtc, saida.OccurredAtUtc);
        Assert.Equal("Vidroart", saida.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Movements_AreListedByRealDate_NotByInsertionOrder()
    {
        // Uma saída antiga gravada depois (backfill) fica por baixo da
        // entrada mais recente: a lista segue a data real.
        await SeedToolAsync("BQ-TOOL-10", "9389T195", "13");
        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-10", BqContextId = null };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var todayUtc = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        _db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 3, OccurredAtUtc = todayUtc });
        await _db.SaveChangesAsync();
        _db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 3, OccurredAtUtc = todayUtc.AddDays(-7) });
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.NotNull(result);
        Assert.Equal(todayUtc, result!.Movements[0].OccurredAtUtc);
        Assert.Equal(todayUtc.AddDays(-7), result.Movements[1].OccurredAtUtc);
    }

    [Fact]
    public async Task FindByContext_WithAssociatedTrace_ReturnsTraceProductionAndTool()
    {
        // O bq_id aberto no rail: a produção, o trace associado (com os
        // movimentos) e a ferramenta do contexto, com a quantidade do lote.
        _db.Tools.Add(new Tool { ToolId = "BQ-CTX-1", Type = ToolType.Bq, Reference = "T194", Lot = "3", BqAccountedQuantity = 120 });
        await _db.SaveChangesAsync();
        var bq = await SeedProductionBqContextAsync("BQ-CTX-1", "5447", "PN-1", "B2");
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        _db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 4 });
        await _db.SaveChangesAsync();

        var result = await _query.FindByContextAsync(bq.Id);

        Assert.NotNull(result);
        Assert.Equal("BQ-CTX-1", result!.ToolId);
        Assert.Equal(new BoquilhasRegistoProductionContext("5447", "PN-1", "B2"), result.Production);
        Assert.Equal(new ToolLookupResult("BQ-CTX-1", ToolType.Bq, "T194", "3"), result.Tool);
        Assert.Equal(120, result.ToolAccountedQuantity);
        Assert.NotNull(result.Trace);
        Assert.Equal(trace.Id, result.Trace!.BqRepairTraceId);
        Assert.Equal("T194", result.Trace.ToolReference);
        Assert.Equal(result.Production, result.Trace.Production);
        Assert.Equal(120, result.Trace.ToolAccountedQuantity);
        Assert.Equal(4, result.Trace.QuantityLegitimatelyOut);
        Assert.Single(result.Trace.Movements);
    }

    [Fact]
    public async Task FindByContext_WithoutTrace_ReturnsToolAndNoTrace()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-CTX-2", Type = ToolType.Bq, Reference = "Y232", Lot = "1" });
        await _db.SaveChangesAsync();
        var bq = await SeedProductionBqContextAsync("BQ-CTX-2", "6001", "PN-2", "C1");

        var result = await _query.FindByContextAsync(bq.Id);

        Assert.NotNull(result);
        Assert.Null(result!.Trace);
        Assert.Equal("Y232", result.Tool?.Reference);
        Assert.Null(result.ToolAccountedQuantity);
        Assert.Equal("C1", result.Production.MachineCode);
    }

    [Fact]
    public async Task FindByContext_ToolNotRegisteredOrRemoved_HasNoTool()
    {
        // Sem a ferramenta no Ferramentas (ou a linha REMOVED): Tool null,
        // como a procura do Ferramentas.
        _db.Tools.Add(new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Bq, Reference = "REMOVIDA", Lot = "-" });
        await _db.SaveChangesAsync();
        var removed = await SeedProductionBqContextAsync(ToolRegistryConstants.RemovedToolId, "7001", "PN-3", "C2");
        var missing = await SeedProductionBqContextAsync("BQ-CTX-NAO-EXISTE", "7002", "PN-4", "C3");

        Assert.Null((await _query.FindByContextAsync(removed.Id))!.Tool);
        Assert.Null((await _query.FindByContextAsync(missing.Id))!.Tool);
        Assert.Null(await _query.FindByContextAsync(999_999));
    }

    [Fact]
    public async Task FindTrace_CarriesLotAccountedQuantity()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-CTX-3", Type = ToolType.Bq, Reference = "T300", Lot = "9", BqAccountedQuantity = 80 });
        await _db.SaveChangesAsync();
        var trace = new BqRepairTrace { ToolId = "BQ-CTX-3" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var result = await _query.FindTraceAsync(trace.Id);

        Assert.Equal(80, result!.ToolAccountedQuantity);
    }

    [Fact]
    public async Task FindTrace_ReadsOnlyThePageAsked_TheTotalsCountEveryMovement()
    {
        // 30 saídas de 1, uma por hora: a página 1 tem as 25 mais recentes,
        // a página 2 as 5 mais antigas; as contas somam as 30.
        _db.Tools.Add(new Tool { ToolId = "BQ-PAG-1", Type = ToolType.Bq, Reference = "T400", Lot = "1" });
        var trace = new BqRepairTrace { ToolId = "BQ-PAG-1" };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        var start = new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 30; i++)
        {
            _db.BqMovements.Add(new BqMovement { BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 1, OccurredAtUtc = start.AddHours(i) });
        }
        await _db.SaveChangesAsync();

        var first = (await _query.FindTraceAsync(trace.Id))!;
        Assert.Equal(30, first.MovementCount);
        Assert.Equal(1, first.MovementPageNumber);
        Assert.Equal(25, first.Movements.Count);
        Assert.Equal(start.AddHours(29), first.Movements[0].OccurredAtUtc);
        Assert.Equal(30, first.QuantityLegitimatelyOut);

        var second = (await _query.FindTraceAsync(trace.Id, movementPage: 2))!;
        Assert.Equal(2, second.MovementPageNumber);
        Assert.Equal(5, second.Movements.Count);
        Assert.Equal(start, second.Movements[^1].OccurredAtUtc);
        Assert.Equal(30, second.QuantityLegitimatelyOut);

        // Uma página que não existe fica na última.
        Assert.Equal(2, (await _query.FindTraceAsync(trace.Id, movementPage: 9))!.MovementPageNumber);


        // O movimento a corrigir encontra-se noutra página, mas só no seu registo.
        var oldest = second.Movements[^1];
        Assert.Equal(oldest, await _query.FindMovementAsync(trace.Id, oldest.MovementId));
        Assert.Null(await _query.FindMovementAsync(trace.Id + 1, oldest.MovementId));
    }
}
