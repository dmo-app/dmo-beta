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
/// Prova o write path de registo de movimento de Boquilhas: escritas com
/// sucesso (saida sem discrepância; entrada e entrada_sem_reparacao com a
/// regra idêntica de matched/unmatched — entrada_sem_reparacao reduz o
/// outstanding exatamente como entrada e o token distinto é preservado) e
/// recusas tipadas (trace inexistente, Tool não registada no registo
/// canónico, tipo não autorizado). O registo não depende do Tool.Type e
/// nenhuma escrita altera Job On, BqContext ou o trace.
/// A CONVENÇÃO DA DISCREPÂNCIA do Owner (2026-10-12: «sai 3 entra 5
/// deveria aparecer -2»): DISCREPÂNCIA = SAÍDA − ENTRADA — o EXCEDENTE é
/// anotado (negativo) no movimento de ENTRADA, NUNCA somado ao lote
/// contabilístico; as QUE FALTAM não são discrepância (o saldo
/// legitimamente fora); movimentos normais: null, nunca zero como facto.
/// A ROTA DO REPARADOR (o bloco dos reparadores): a saída RESOLVE o
/// reparador pela rota (a máquina do contexto de produção; o avulso pela
/// máquina associada da Tool → a config Boquilhas:Repairer<código>), com
/// o NOME CONGELADO no movimento; SEM config grava na mesma sem
/// reparador; a config mudada depois NUNCA reescreve o movimento antigo.
/// </summary>
public sealed class RegisterBqMovementHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly RegisterBqMovementHandler _handler;

    /// <summary>
    /// O stub das definições por módulo: um dicionário configurável (a
    /// config das chaves de rota Boquilhas:RepairerB1..C3 nos testes; sem
    /// chaves = a máquina sem config, o estado truthful).
    /// </summary>
    private sealed class StubSettings : IModuleSettingsQuery
    {
        private readonly Dictionary<string, string> _values = new();

        public void Configure(string settingKey, string settingValue) =>
            _values[settingKey] = settingValue;

        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.TryGetValue(settingKey, out var value) ? value : null);
    }

    private readonly StubSettings _settings = new();

    public RegisterBqMovementHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RegisterBqMovementHandler(_db, _settings);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Tool> SeedToolAsync(string toolId, ToolType type)
    {
        var tool = new Tool
        {
            ToolId = toolId,
            Type = type,
            Reference = "9389T194",
            Lot = "12"
        };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqRepairTrace> SeedTraceAsync(string toolId)
    {
        var trace = new BqRepairTrace { ToolId = toolId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    private Task<RegisterBqMovementOutcome> RegisterAsync(
        BqRepairTrace trace,
        string type,
        int quantity) =>
        _handler.HandleAsync(new RegisterBqMovement(trace.Id, type, quantity));

    [Fact]
    public async Task Saida_RegistersMovementWithoutDiscrepancy()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var outcome = await RegisterAsync(trace, "saida", 5);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.NotNull(outcome.Movement);
        Assert.Equal(trace.Id, outcome.Movement!.BqRepairTraceId);
        Assert.Equal("saida", outcome.Movement.MovementType);
        Assert.Equal(5, outcome.Movement.Quantity);
        Assert.Null(outcome.Movement.Discrepancy);

        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement.MovementId);
        Assert.Equal(trace.Id, persisted.BqRepairTraceId);
        Assert.Equal(BqMovementType.Saida, persisted.Type);
        Assert.Equal(5, persisted.Quantity);
        Assert.Null(persisted.Discrepancy);
    }

    [Fact]
    public async Task Entrada_FullyMatched_HasNoDiscrepancy()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        var outcome = await RegisterAsync(trace, "entrada", 5);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(5, outcome.Movement!.Quantity);
        Assert.Null(outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task Entrada_WithUnexplainedQuantity_RecordsFullQuantityAndNegativeDiscrepancy()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        var outcome = await RegisterAsync(trace, "entrada", 7);

        // A Entrada observada é registada na totalidade (7, não truncada
        // para 5) e produz a discrepância -2 (movimento real aceite).
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(7, outcome.Movement!.Quantity);
        Assert.Equal(-2, outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task Entrada_WithoutAnySaida_IsAcceptedInFull()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        // Proibido rejeitar uma Entrada por exceder a quantidade explicável.
        var outcome = await RegisterAsync(trace, "entrada", 3);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(3, outcome.Movement!.Quantity);
        Assert.Equal(-3, outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task Entrada_FollowsTraceHistorySequence()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        var first = await RegisterAsync(trace, "entrada", 7);
        var second = await RegisterAsync(trace, "entrada", 5);

        // Cada Entrada produz a sua própria discrepância histórica contra o
        // estado precedente do trace; a segunda não compensa a primeira.
        Assert.Equal(-2, first.Movement!.Discrepancy);
        Assert.Equal(-5, second.Movement!.Discrepancy);

        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.Equal(3, movements.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task EntradaSemReparacao_AfterSaida_ReducesOutstanding()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        var retorno = await RegisterAsync(trace, "entrada_sem_reparacao", 3);

        // Reduz o outstanding exatamente como entrada: 3 explicados, sem
        // discrepância.
        Assert.Equal(RegisterBqMovementResult.Success, retorno.Result);
        Assert.Equal("entrada_sem_reparacao", retorno.Movement!.MovementType);
        Assert.Equal(3, retorno.Movement.Quantity);
        Assert.Null(retorno.Movement.Discrepancy);

        // Sobram 2 legitimamente fora: uma Entrada de 2 é totalmente
        // explicável.
        var later = await RegisterAsync(trace, "entrada", 2);
        Assert.Equal(RegisterBqMovementResult.Success, later.Result);
        Assert.Equal(2, later.Movement!.Quantity);
        Assert.Null(later.Movement.Discrepancy);
    }

    [Fact]
    public async Task EntradaSemReparacao_Excess_RecordsFullQuantityAndNegativeDiscrepancy()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        var outcome = await RegisterAsync(trace, "entrada_sem_reparacao", 7);

        // Quantidade observada na totalidade (7) e a mesma discrepância
        // negativa de entrada (matched 5, unmatched 2).
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(7, outcome.Movement!.Quantity);
        Assert.Equal(-2, outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task Entrada_AfterEntradaSemReparacao_UsesRemainingOutstanding()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        await RegisterAsync(trace, "saida", 5);
        await RegisterAsync(trace, "entrada_sem_reparacao", 3);

        // Outstanding restante: 2. Entrada de 3 → matched 2, unmatched 1.
        var outcome = await RegisterAsync(trace, "entrada", 3);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal(3, outcome.Movement!.Quantity);
        Assert.Equal(-1, outcome.Movement.Discrepancy);
    }

    [Fact]
    public async Task MovementRegistration_DoesNotDependOnToolType()
    {
        // O trace já transporta o tool_id relevante; o Tool.Type não é um
        // gate comportamental — um trace ancorado a uma Tool CM regista
        // movimentos normalmente.
        await SeedToolAsync("CM-TOOL-1", ToolType.Cm);
        var trace = await SeedTraceAsync("CM-TOOL-1");

        var outcome = await RegisterAsync(trace, "saida", 5);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.Equal("saida", outcome.Movement!.MovementType);

        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement.MovementId);
        Assert.Equal(BqMovementType.Saida, persisted.Type);
        Assert.Equal(5, persisted.Quantity);
    }

    [Fact]
    public async Task AssociatedTrace_WithClearedToolId_ResolvesToolViaBqContext()
    {
        // Após a associação a âncora direta tool_id é limpa (ToolId = null);
        // o registo de movimento resolve a Tool via bq_id → BqContext.ToolId
        // e NÃO dispara TraceToolNotRegistered num trace associado.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);

        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = "PN-202609",
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();

        // Trace já associado: âncora direta limpa.
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var outcome = await _handler.HandleAsync(
            new RegisterBqMovement(trace.Id, "saida", 5));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.NotNull(outcome.Movement);
        Assert.Equal(trace.Id, outcome.Movement!.BqRepairTraceId);
        Assert.Equal(5, outcome.Movement.Quantity);
    }

    [Fact]
    public async Task DistinctMovementToken_PreservedInHistory()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var saida = await RegisterAsync(trace, "saida", 5);
        var entrada = await RegisterAsync(trace, "entrada", 5);
        var semReparacao = await RegisterAsync(trace, "entrada_sem_reparacao", 2);

        Assert.Equal(RegisterBqMovementResult.Success, saida.Result);
        Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);
        Assert.Equal(RegisterBqMovementResult.Success, semReparacao.Result);

        // O token de entrada_sem_reparacao permanece distinto no histórico
        // persistido — não é colapsado em entrada.
        var persisted = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        Assert.Equal(
            new[] { BqMovementType.Saida, BqMovementType.Entrada, BqMovementType.EntradaSemReparacao },
            persisted.Select(m => m.Type).ToArray());
        Assert.Equal("entrada_sem_reparacao", semReparacao.Movement!.MovementType);
        Assert.Equal(BqMovementType.EntradaSemReparacao,
            persisted.Single(m => m.Id == semReparacao.Movement.MovementId).Type);
    }

    [Fact]
    public async Task UnknownTrace_RefusedTraceNotFound()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        await SeedTraceAsync("BQ-TOOL-9");

        var outcome = await _handler.HandleAsync(new RegisterBqMovement(9999, "saida", 5));

        Assert.Equal(RegisterBqMovementResult.TraceNotFound, outcome.Result);
        Assert.Null(outcome.Movement);
        Assert.Equal(0, await _db.BqMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task TraceToolMissingFromRegistry_Refused()
    {
        var trace = await SeedTraceAsync("BQ-TOOL-UNREGISTERED");

        var outcome = await RegisterAsync(trace, "saida", 5);

        Assert.Equal(RegisterBqMovementResult.TraceToolNotRegistered, outcome.Result);
        Assert.Null(outcome.Movement);
        Assert.Equal(0, await _db.BqMovements.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("Irreparável")]
    [InlineData("SAIDA")]
    [InlineData("Saída")]
    [InlineData("")]
    public async Task UnauthorizedTypeTokens_Refused(string token)
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var outcome = await RegisterAsync(trace, token, 5);

        Assert.Equal(RegisterBqMovementResult.UnknownMovementType, outcome.Result);
        Assert.Null(outcome.Movement);
        Assert.Equal(0, await _db.BqMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RegisteringMovements_DoesNotModifyJobOnBqContextOrTrace()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);

        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = "PN-202609",
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);

        var trace = new BqRepairTrace { ToolId = "BQ-TOOL-9", BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var jobOnId = jobOn.Id;
        var machineId = jobOn.MachineId;

        // Registar movimentos (pré e pós-produção irrelevante: as datas de
        // produção não controlam o registo) não altera nada no Job On, no
        // BqContext ou no trace.
        await RegisterAsync(trace, "saida", 5);
        await RegisterAsync(trace, "entrada_sem_reparacao", 7);

        var jobOnAfter = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId);
        Assert.Equal("9389T194", jobOnAfter.Reference);
        Assert.Equal("PN-202609", jobOnAfter.ProductionNumber);
        Assert.Equal(machineId, jobOnAfter.MachineId);

        var contextAfter = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == bq.Id);
        Assert.Equal(jobOnId, contextAfter.JobOnId);
        Assert.Equal("BQ-TOOL-9", contextAfter.ToolId);

        var traceAfter = await _db.BqRepairTraces.AsNoTracking().SingleAsync(t => t.Id == trace.Id);
        Assert.Equal("BQ-TOOL-9", traceAfter.ToolId);
        Assert.Equal(bq.Id, traceAfter.BqContextId);

        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(2, movements.Select(m => m.Id).Distinct().Count());
    }

    // ------------------------------------------------------------------
    // A CONVENÇÃO DA DISCREPÂNCIA DO OWNER (o guardião do lote):
    // DISCREPÂNCIA = SAÍDA − ENTRADA. O EXCEDENTE (entra mais do que saiu)
    // é anotado negativo NO MOVIMENTO DA ENTRADA e NUNCA somado ao lote
    // contabilístico; as QUE FALTAM NÃO SÃO discrepância (o saldo
    // legitimamente fora); movimento normal: null, nunca zero como facto.
    // ------------------------------------------------------------------

    [Fact]
    public async Task SaiTresEntraCinco_OwnerConvention_EntradaAnnotatesMinusTwo_LoteNeverGrows()
    {
        // O EXEMPLO CANÓNICO do Owner (2026-10-12: «sai 3 entra 5 deveria
        // aparecer -2» + «se um lote tem 120 bqs no total, se sair 3 e
        // entrar 5, e nao metermos o saldo, aumenta o total do lote»):
        // as 2 a mais são peças DE FORA DO LOTE — ANOTADAS (−2) no
        // movimento da ENTRADA, NUNCA somadas ao lote contabilístico (a
        // Tool BqAccountedQuantity intocada).
        var tool = await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        tool.BqAccountedQuantity = 120;
        await _db.SaveChangesAsync();
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var saida = await RegisterAsync(trace, "saida", 3);
        var entrada = await RegisterAsync(trace, "entrada", 5);

        // A entrada regista a quantidade observada na totalidade (5, nunca
        // truncada a 3) com a discrepância −2 anotada nela.
        Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);
        Assert.Equal(5, entrada.Movement!.Quantity);
        Assert.Equal(-2, entrada.Movement.Discrepancy);
        Assert.Null(saida.Movement!.Discrepancy);

        // O LOTE CONTABILÍSTICO NÃO CRESCU: as 2 a mais são de fora do
        // lote — o movimento NUNCA muta o BqAccountedQuantity da Tool.
        var lotAfter = await _db.Tools.AsNoTracking()
            .Where(t => t.ToolId == "BQ-TOOL-9")
            .Select(t => t.BqAccountedQuantity)
            .SingleAsync();
        Assert.Equal(120, lotAfter);
    }

    [Fact]
    public async Task SaiCincoEntraTres_MissingPiecesAreNotDiscrepancy_TheBalanceIsLegitimate()
    {
        // O LADO OPOSTO do Owner: sai 5, entra 3 → +2 é o SALDO LEGÍTIMO
        // (as 2 do lote fora em reparação) — SEM anotação de discrepância
        // (null na entrada); o retorno final das 2 em falta também não
        // produz discrepância (movimento normal).
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var saida = await RegisterAsync(trace, "saida", 5);
        var parcial = await RegisterAsync(trace, "entrada", 3);
        var final = await RegisterAsync(trace, "entrada", 2);

        Assert.Null(saida.Movement!.Discrepancy);
        Assert.Null(parcial.Movement!.Discrepancy);
        Assert.Null(final.Movement!.Discrepancy);
    }

    // ------------------------------------------------------------------
    // A ROTA DO REPARADOR NA SAÍDA (o bloco dos reparadores): a saída
    // NUNCA pergunta o reparador — o handler resolve pela rota
    // (a máquina do contexto de produção; o avulso pela máquina associada
    // da Tool) → a config Boquilhas:Repairer<código> → o reparador
    // CONGELADO no movimento. SEM config: grava na mesma, sem reparador
    // (truthful, a app nunca bloqueia).
    // ------------------------------------------------------------------

    private async Task<BqRepairer> SeedRepairerAsync(string name)
    {
        var repairer = new BqRepairer { Name = name };
        _db.BqRepairers.Add(repairer);
        await _db.SaveChangesAsync();
        return repairer;
    }

    private async Task<BqRepairTrace> SeedAssociatedTraceAsync(string toolId, string machineCode)
    {
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn
        {
            Reference = "9389T194",
            ProductionNumber = "PN-" + Guid.NewGuid().ToString("N")[..8],
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = toolId };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();

        // O trace JÁ ASSOCIADO (o estado canónico pós-produção: a âncora
        // direta limpa, a Tool alcança-se via bq_id).
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        return trace;
    }

    [Fact]
    public async Task Saida_MachineRouteConfigured_FreezesTheRepairerOnTheMovement()
    {
        // A ROTA COMPLETA: o contexto de produção do trace (o BqContext →
        // o JobOn → a Machine B1) → a config Boquilhas:RepairerB1 → O
        // REPARADOR usado pela Saída, gravado com o NOME CONGELADO.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-TOOL-9", "B1");
        var repairer = await SeedRepairerAsync("Vidroart");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("B1"), repairer.Id.ToString());

        var outcome = await RegisterAsync(trace, "saida", 2);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);
        Assert.Equal(repairer.Id, persisted.BqRepairerId);
        Assert.Equal("Vidroart", persisted.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Saida_MachineWithoutRoute_SavesWithoutRepairer_NeverBlocks()
    {
        // SEM config para a máquina: o movimento grava na mesma SEM
        // reparador (os campos null — truthful; o manifesto: a app nunca
        // bloqueia).
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-TOOL-9", "B1");

        var outcome = await RegisterAsync(trace, "saida", 2);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);
        Assert.Null(persisted.BqRepairerId);
        Assert.Null(persisted.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Saida_RouteConfigChangedLater_OldMovementKeepsItsFrozenRepairer()
    {
        // O CONGELADO (blueprint L372-374: «The repairer that applied
        // when the real movement occurred remains part of that movement's
        // historical truth»): a config mudada DEPOIS não reescreve o
        // movimento antigo — a saída antiga mantém o reparador A, a nova
        // congela o B.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-TOOL-9", "B1");
        var first = await SeedRepairerAsync("Vidroart");
        var second = await SeedRepairerAsync("Boquilharia Central");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("B1"), first.Id.ToString());

        var firstSaida = await RegisterAsync(trace, "saida", 2);

        // A config MUDA (a máquina passa a outro reparador).
        _settings.Configure(BqRepairerRouting.SettingKeyFor("B1"), second.Id.ToString());
        var secondSaida = await RegisterAsync(trace, "saida", 1);

        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        // O MOVIMENTO ANTIGO mantém o A (o congelado); o novo congela o B.
        Assert.Equal(first.Id, movements[0].BqRepairerId);
        Assert.Equal("Vidroart", movements[0].BqRepairerNameFrozen);
        Assert.Equal(second.Id, movements[1].BqRepairerId);
        Assert.Equal("Boquilharia Central", movements[1].BqRepairerNameFrozen);
        Assert.Equal(2, movements.Count);
        Assert.Equal(firstSaida.Movement!.MovementId, movements[0].Id);
        Assert.Equal(secondSaida.Movement!.MovementId, movements[1].Id);
    }

    [Fact]
    public async Task Saida_AvulsoTrace_RoutesViaTheToolAssociatedMachine()
    {
        // O TRACE AVULSO (pré-produção, âncora direta tool_id, sem bq_id):
        // a rota usa a MÁQUINA ASSOCIADA da ficha da Tool (a primeira do
        // ToolMachineLink — a ordem dos MachineIds; a BQ tem no máximo
        // uma, imposta tipadamente no Ferramentas).
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var machine = new Machine { Code = "C2" };
        _db.Machines.Add(machine);
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-TOOL-9", MachineId = machine.Id });
        await _db.SaveChangesAsync();
        var trace = await SeedTraceAsync("BQ-TOOL-9");
        var repairer = await SeedRepairerAsync("Vidroart");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("C2"), repairer.Id.ToString());

        var outcome = await RegisterAsync(trace, "saida", 4);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);
        Assert.Equal(repairer.Id, persisted.BqRepairerId);
        Assert.Equal("Vidroart", persisted.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Saida_AvulsoToolWithoutMachineLink_SavesWithoutRepairer()
    {
        // A Tool sem máquina associada e sem contexto: a rota não resolve
        // máquina nenhuma — a saída grava na mesma, sem reparador.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var outcome = await RegisterAsync(trace, "saida", 4);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);
        Assert.Null(persisted.BqRepairerId);
        Assert.Null(persisted.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Saida_RoutePointingToMissingRepairer_SavesWithoutRepairer()
    {
        // A config aponta a um Id que já não resolve a nenhuma entidade
        // (a entidade desapareceu entre a config e a saída): o movimento
        // grava na mesma, SEM reparador — nunca bloqueia, nunca inventa.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-TOOL-9", "B1");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("B1"), "9999");

        var outcome = await RegisterAsync(trace, "saida", 2);

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);
        Assert.Null(persisted.BqRepairerId);
        Assert.Null(persisted.BqRepairerNameFrozen);
    }

    [Fact]
    public async Task Entrada_NeverCarriesRepairer_EvenWithTheRouteConfigured()
    {
        // Os movimentos entrada/entrada_sem_reparacao não transportam
        // reparador: SÓ A SAÍDA o define (a entrada devolve ao lote, não
        // reencaminha).
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-TOOL-9", "B1");
        var repairer = await SeedRepairerAsync("Vidroart");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("B1"), repairer.Id.ToString());

        await RegisterAsync(trace, "saida", 2);
        var entrada = await RegisterAsync(trace, "entrada", 2);
        var semReparacao = await RegisterAsync(trace, "entrada_sem_reparacao", 1);

        var movements = await _db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        // A saída congela o reparador; as entradas ficam sem reparador.
        Assert.Equal(repairer.Id, movements[0].BqRepairerId);
        Assert.Null(movements[1].BqRepairerId);
        Assert.Null(movements[1].BqRepairerNameFrozen);
        Assert.Null(movements[2].BqRepairerId);
        Assert.Null(movements[2].BqRepairerNameFrozen);
        Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);
        Assert.Equal(RegisterBqMovementResult.Success, semReparacao.Result);
    }

    // ------------------------------------------------------------------
    // A DATA/HORA REAL DO MOVIMENTO (o backfill dos ~2 meses — o Owner
    // 2026-10-14): o comando transporta o occurredAt do form «Data e hora
    // reais» e o movimento grava com ESSA data (a verdade do registo
    // antigo); o CreatedAtUtc continua o AGORA TÉCNICO do registo (a
    // auditoria, sempre do backend, nunca reescrita pela data real). Sem
    // data fornecida: o agora do backend — o uso diário intacto (o
    // comportamento antigo das linhas do handler mantém-se).
    // ------------------------------------------------------------------

    /// <summary>A data/hora retroativa em hora Europe/Lisbon (o formato
    /// que o form datetime-local entrega), convertida a UTC como a folha
    /// faz — o instante do movimento real de há ~2 meses.</summary>
    private static (DateTime RetroUtc, DateTime RetroLocal) RetroOccurrence()
    {
        var localNow = DmoBusinessTime.Now;
        var retroLocal = DateTime.SpecifyKind(
            new DateTime(localNow.Year, localNow.Month, localNow.Day, 10, 30, 0).AddDays(-60),
            DateTimeKind.Unspecified);
        return (DmoBusinessTime.ToUtc(retroLocal), retroLocal);
    }

    [Fact]
    public async Task MovimentoComDataRetroativa_PersisteADataReal_EAuditoriaFicaOAgora()
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var beforeUtc = DateTime.UtcNow;
        var (retroUtc, _) = RetroOccurrence();

        var outcome = await _handler.HandleAsync(
            new RegisterBqMovement(trace.Id, "saida", 3, null, retroUtc));
        var afterUtc = DateTime.UtcNow;

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);

        // A DATA REAL do movimento persiste tal como o form a transportou.
        Assert.Equal(retroUtc, persisted.OccurredAtUtc);

        // O CreatedAtUtc é o AGORA TÉCNICO do registo — a auditoria nunca é
        // reescrita pela data retroativa (o valor antigo mantém-se; ~60
        // dias separam o registo do movimento real).
        Assert.InRange(persisted.CreatedAtUtc, beforeUtc, afterUtc);
        Assert.True(
            (persisted.CreatedAtUtc - persisted.OccurredAtUtc).TotalDays > 59,
            "O CreatedAtUtc deve ficar no agora do registo, não na data real retroativa.");
    }

    [Fact]
    public async Task MovimentoSemData_GravaComOAgoraDoBackend_UsoDiarioIntacto()
    {
        // O USO DIÁRIO intacto: sem occurredAt no comando, a data real do
        // movimento É o agora do backend — o mesmo instante do registo
        // técnico (o comportamento antigo das linhas do handler).
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var beforeUtc = DateTime.UtcNow;
        var outcome = await RegisterAsync(trace, "saida", 2);
        var afterUtc = DateTime.UtcNow;

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var persisted = await _db.BqMovements.AsNoTracking()
            .SingleAsync(m => m.Id == outcome.Movement!.MovementId);

        Assert.InRange(persisted.OccurredAtUtc, beforeUtc, afterUtc);
        Assert.Equal(persisted.CreatedAtUtc, persisted.OccurredAtUtc);
    }

    [Fact]
    public async Task MovimentoComDataRetroativa_ApareceNoHistoricoEContadoresPelaDataReal()
    {
        // O CONTRATO NOVO: um movimento gravado com data/hora passada
        // aparece no HISTÓRICO e nos CONTADORES NA ORDEM da data real do
        // movimento — o dia retroativo fica com o movimento (o calendário
        // conta-o no dia antigo) e o dia de hoje não.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");

        var (retroUtc, retroLocal) = RetroOccurrence();
        var retroDay = DateOnly.FromDateTime(retroLocal);
        var localNow = DmoBusinessTime.Now;
        var today = DateOnly.FromDateTime(localNow);
        Assert.NotEqual(retroDay, today);
        Assert.NotEqual(retroLocal.Month, localNow.Month);

        // O movimento antigo (a data real de há ~2 meses) e o movimento de
        // hoje (o uso diário, sem data no comando).
        await _handler.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 3, null, retroUtc));
        await RegisterAsync(trace, "entrada", 3);

        var history = new BqToolHistoryQuery(_db);

        // O HISTÓRICO do dia retroativo mostra EXATAMENTE o movimento
        // antigo, com a data real; o dia de hoje mostra exatamente o de
        // hoje.
        var retroMovements = await history.ListMovementsAsync(retroDay, retroDay, null, null, null, null);
        var retroItem = Assert.Single(retroMovements);
        Assert.Equal(BqMovementType.Saida, Enum.Parse<BqMovementType>(retroItem.MovementType));
        Assert.Equal(3, retroItem.Quantity);
        Assert.Equal(retroUtc, retroItem.OccurredAtUtc);

        var todayMovements = await history.ListMovementsAsync(today, today, null, null, null, null);
        var todayItem = Assert.Single(todayMovements);
        Assert.Equal(BqMovementType.Entrada, Enum.Parse<BqMovementType>(todayItem.MovementType));

        // Os CONTADORES do calendário: o mês retroativo conta 1 movimento
        // no dia antigo; o mês corrente conta 1 no dia de hoje.
        var retroCounts = await history.ListMonthCountsAsync(retroLocal.Year, retroLocal.Month);
        var retroCount = Assert.Single(retroCounts);
        Assert.Equal(retroDay, retroCount.Day);
        Assert.Equal(1, retroCount.MovementCount);

        var todayCounts = await history.ListMonthCountsAsync(localNow.Year, localNow.Month);
        var todayCount = Assert.Single(todayCounts);
        Assert.Equal(today, todayCount.Day);
        Assert.Equal(1, todayCount.MovementCount);
    }
    [Fact]
    public async Task Saida_ChosenMachineControlsRepairer_AndHistoryKeepsMachineAfterCompatibilityChanges()
    {
        await SeedToolAsync("BQ-MULTI", ToolType.Bq);
        var trace = await SeedAssociatedTraceAsync("BQ-MULTI", "C2");
        var c3 = new Machine { Code = "C3" };
        _db.Machines.Add(c3);
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-MULTI", Machine = c3 });
        await _db.SaveChangesAsync();
        var repairer = await SeedRepairerAsync("Reparador C3");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("C3"), repairer.Id.ToString());
        var outcome = await _handler.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 4, MachineId: c3.Id));
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var movement = await _db.BqMovements.SingleAsync();
        Assert.Equal("C3", movement.MachineCodeFrozen);
        Assert.Equal(repairer.Id, movement.BqRepairerId);
        _db.ToolMachineLinks.RemoveRange(_db.ToolMachineLinks);
        c3.Code = "C3-alterada";
        await _db.SaveChangesAsync();
        var history = await new BqToolHistoryQuery(_db).ListMovementsAsync(null, null, "C3", null, null, null);
        var saved = Assert.Single(history);
        Assert.Equal("C3", saved.MachineCode);
    }

    [Fact]
    public async Task Saida_MultipleCompatibilitiesWithoutChoice_DoesNotPickArbitraryRepairer()
    {
        await SeedToolAsync("BQ-AMBIGUOUS", ToolType.Bq);
        var c2 = new Machine { Code = "C2" };
        var c3 = new Machine { Code = "C3" };
        _db.Machines.AddRange(c2, c3);
        _db.ToolMachineLinks.AddRange(new ToolMachineLink { ToolId = "BQ-AMBIGUOUS", Machine = c2 }, new ToolMachineLink { ToolId = "BQ-AMBIGUOUS", Machine = c3 });
        await _db.SaveChangesAsync();
        var repairer = await SeedRepairerAsync("Reparador C2");
        _settings.Configure(BqRepairerRouting.SettingKeyFor("C2"), repairer.Id.ToString());
        var trace = await SeedTraceAsync("BQ-AMBIGUOUS");
        var outcome = await RegisterAsync(trace, "saida", 4);
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        var movement = await _db.BqMovements.SingleAsync();
        Assert.Null(movement.MachineCodeFrozen);
        Assert.Null(movement.BqRepairerId);
    }

    // ------------------------------------------------------------------
    // A data real no futuro (Diogo, 19/10/2026: «as datas têm de ser
    // livres»; na beta.241, BD-8, era recusada): grava e o outcome diz que
    // está no futuro, para a página avisar. A folga de 10 minutos cobre o
    // relógio do posto adiantado e não avisa; as datas passadas (as guias
    // lançadas com atraso) gravam sem aviso.
    // ------------------------------------------------------------------

    private static readonly DateTime ClockNow = new(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private RegisterBqMovementHandler HandlerAt(DateTime utcNow) => new(_db, _settings, new FixedClock(utcNow));

    [Theory]
    [InlineData(BqMovementOccurrence.FutureToleranceMinutes + 1)] // um minuto para lá da folga
    [InlineData(60 * 24)]                                          // amanhã
    [InlineData(60 * 24 * 365)]                                    // daqui a um ano
    public async Task Register_RealDateInTheFutureBeyondTheTolerance_IsSaved_AndSaysSo(int minutesAhead)
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");
        var occurredAtUtc = ClockNow.AddMinutes(minutesAhead);

        var outcome = await HandlerAt(ClockNow).HandleAsync(
            new RegisterBqMovement(trace.Id, "saida", 3, null, occurredAtUtc));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.True(outcome.OccurredInTheFuture);
        var persisted = await _db.BqMovements.AsNoTracking().SingleAsync();
        Assert.Equal(occurredAtUtc, persisted.OccurredAtUtc);
        Assert.Equal(ClockNow, persisted.CreatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(BqMovementOccurrence.FutureToleranceMinutes)] // o limite da folga ainda grava
    public async Task Register_RealDateWithinTheTolerance_IsSaved(int minutesAhead)
    {
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");
        var occurredAtUtc = ClockNow.AddMinutes(minutesAhead);

        var outcome = await HandlerAt(ClockNow).HandleAsync(
            new RegisterBqMovement(trace.Id, "saida", 3, null, occurredAtUtc));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.False(outcome.OccurredInTheFuture);
        var persisted = await _db.BqMovements.AsNoTracking().SingleAsync();
        Assert.Equal(occurredAtUtc, persisted.OccurredAtUtc);
        Assert.Equal(ClockNow, persisted.CreatedAtUtc);
    }

    [Fact]
    public async Task Register_RealDateInThePast_IsSaved_LatePaperworkStillGoesIn()
    {
        // As guias lançadas com atraso (um minuto, dias, mais de um ano): todas gravam com a data real.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");
        var handler = HandlerAt(ClockNow);
        var pastDates = new[] { ClockNow.AddMinutes(-1), ClockNow.AddDays(-3), ClockNow.AddDays(-400) };

        foreach (var occurredAtUtc in pastDates)
        {
            var outcome = await handler.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 1, null, occurredAtUtc));
            Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
            Assert.False(outcome.OccurredInTheFuture);
        }

        var persisted = await _db.BqMovements.AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => m.OccurredAtUtc)
            .ToListAsync();
        Assert.Equal(pastDates, persisted);
    }

    [Fact]
    public async Task Register_TheRepeatedHourOfTheClockChange_TheNowIsNotTheFuture()
    {
        // 25/10/2026 em Lisboa: as 02:00 de verão voltam a 01:00 e a hora
        // 01:00–01:59 passa duas vezes. Às 01:30 da primeira passagem (00:30
        // UTC) o form envia 01:30, que a conversão grava como a segunda
        // passagem (01:30 UTC, uma hora depois). Não é um movimento no futuro.
        await SeedToolAsync("BQ-TOOL-9", ToolType.Bq);
        var trace = await SeedTraceAsync("BQ-TOOL-9");
        var now = new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc);
        var typedLocal = new DateTime(2026, 10, 25, 1, 30, 0);
        Assert.Equal(typedLocal, DmoBusinessTime.ToLocal(now));
        var occurredAtUtc = DmoBusinessTime.ToUtc(typedLocal);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc), occurredAtUtc);

        var outcome = await HandlerAt(now).HandleAsync(new RegisterBqMovement(trace.Id, "saida", 1, null, occurredAtUtc));

        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.False(outcome.OccurredInTheFuture);

        // Uma hora depois da hora repetida continua a ser futuro: grava e avisa.
        var later = await HandlerAt(now).HandleAsync(new RegisterBqMovement(
            trace.Id, "saida", 1, null, DmoBusinessTime.ToUtc(new DateTime(2026, 10, 25, 2, 30, 0))));
        Assert.Equal(RegisterBqMovementResult.Success, later.Result);
        Assert.True(later.OccurredInTheFuture);
    }
}
