using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova o write path da EDIÇÃO de um Job On — a produção re-planeada:
/// criar → editar (a janela temporal, a linha, as tools) grava no MESMO
/// jobon (o nº de produção não duplica, o controlo_id idem), as datas
/// novas no read model, trocar a CM, o MF ou a BQ cria um cm_id, mf_id ou
/// bq_id novo e o anterior fica no registo da produção (Diogo, 18/10/2026
/// e 19/10/2026), um contexto que não existia nasce na edição e um slot
/// null PRESERVA o contexto existente (a edição nunca destrói um
/// facto). A regra do MF OBRIGATÓRIO herda na edição (Owner
/// 2026-10-10: «jobon necessita sempre de MF, senao nao faz garrafas ou
/// frascos» — MissingMfTool), a máquina âncora tem de existir
/// (MachineNotFound — a mesma recusa do create) e um jobon_id que não
/// resolve é recusa tipada JobOnNotFound — nenhuma linha inventada.
/// Todas as recusas são verificadas antes de qualquer escrita: a BD
/// fica INALTERADA (zero escritas parciais — a atomicidade).
/// </summary>
public sealed class UpdateJobOnHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _create;
    private readonly UpdateJobOnHandler _handler;

    public UpdateJobOnHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _create = new CreateJobOnHandler(_db);
        _handler = new UpdateJobOnHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Machine> SeedMachineAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<Tool> SeedToolAsync(string toolId, ToolType type, string reference, string lot)
    {
        var tool = new Tool { ToolId = toolId, Type = type, Reference = reference, Lot = lot };
        _db.Tools.Add(tool);
        await _db.SaveChangesAsync();
        return tool;
    }

    [Fact]
    public async Task Update_ReplansExistingJobOn_SameIdentitySameContextsSameControl()
    {
        // O happy path COMPLETO: criar → editar mudando a janela, a
        // linha e as três tools — a produção re-planeada é o MESMO
        // jobon (o nº de produção e a referência imutáveis), a CM, o MF e
        // a BQ novos têm cm_id, mf_id e bq_id novos e o controlo_id é o
        // mesmo.
        var b1 = await SeedMachineAsync("B1");
        var b2 = await SeedMachineAsync("B2");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T2", ToolType.Cm, "5447", "3");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedToolAsync("T-MF2", ToolType.Mf, "5811", "2");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "T194", "3");
        await SeedToolAsync("T-BQ2", ToolType.Bq, "T195", "4");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-100",
            "5810",
            b1.Id,
            "T1",
            "T-MF",
            "T-BQ1",
            InicioEm: new DateOnly(2026, 3, 2),
            FimEm: new DateOnly(2026, 3, 9)));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        var originalCm = await _db.CmContexts.AsNoTracking().SingleAsync();
        var originalMf = await _db.MfContexts.AsNoTracking().SingleAsync();
        var originalBq = await _db.BqContexts.AsNoTracking().SingleAsync();

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId,
            b2.Id,
            "T2",
            "T-MF2",
            "T-BQ2",
            InicioEm: new DateOnly(2026, 4, 12),
            FimEm: new DateOnly(2026, 4, 18)));

        // O read model da edição: o MESMO jobon com os factos novos —
        // as datas novas, a linha nova, as tools novas.
        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn);
        Assert.Equal(created.JobOn.JobOnId, outcome.JobOn!.JobOnId);
        Assert.Equal("PN-2025-100", outcome.JobOn.ProductionNumber);
        Assert.Equal("5810", outcome.JobOn.Reference);
        Assert.Equal("B2", outcome.JobOn.Machine);
        Assert.Equal(new DateOnly(2026, 4, 12), outcome.JobOn.InicioEm);
        Assert.Equal(new DateOnly(2026, 4, 18), outcome.JobOn.FimEm);

        // CM, MF e BQ: outra ferramenta é outro cm_id / mf_id / bq_id.
        Assert.NotEqual(originalCm.Id, outcome.JobOn.CmId);
        Assert.Equal("T2", outcome.JobOn.CmToolId);
        Assert.NotEqual(originalMf.Id, outcome.JobOn.MfId);
        Assert.Equal("T-MF2", outcome.JobOn.MfToolId);
        Assert.NotEqual(originalBq.Id, outcome.JobOn.BqId);
        Assert.Equal("T-BQ2", outcome.JobOn.BqToolId);

        // Verdade persistida: UM jobon (o nº não duplica) com a janela e
        // a linha novas; os contextos pelas tabelas próprias, os mesmos
        // ids com os ToolIds novos.
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(created.JobOn.JobOnId, persisted.Id);
        Assert.Equal("PN-2025-100", persisted.ProductionNumber);
        Assert.Equal("5810", persisted.Reference);
        Assert.Equal(b2.Id, persisted.MachineId);
        Assert.Equal(new DateOnly(2026, 4, 12), persisted.InicioEm);
        Assert.Equal(new DateOnly(2026, 4, 18), persisted.FimEm);

        var cm = await _db.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == persisted.Id);
        Assert.Equal(outcome.JobOn.CmId, cm.Id);
        Assert.Equal("T2", cm.ToolId);
        // O cm_id anterior fica no registo da produção, com a CM antiga.
        var replacedCm = await _db.CmContexts.AsNoTracking().SingleAsync(c => c.Id == originalCm.Id);
        Assert.Null(replacedCm.JobOnId);
        Assert.Equal(persisted.Id, replacedCm.ReplacedJobOnId);
        Assert.NotNull(replacedCm.ReplacedAtUtc);
        Assert.Equal("T1", replacedCm.ToolId);
        var mf = await _db.MfContexts.AsNoTracking().SingleAsync(c => c.JobOnId == persisted.Id);
        Assert.Equal(outcome.JobOn.MfId, mf.Id);
        Assert.Equal("T-MF2", mf.ToolId);
        // O mf_id anterior fica no registo da produção, com o MF antigo.
        var replacedMf = await _db.MfContexts.AsNoTracking().SingleAsync(c => c.Id == originalMf.Id);
        Assert.Null(replacedMf.JobOnId);
        Assert.Equal(persisted.Id, replacedMf.ReplacedJobOnId);
        Assert.NotNull(replacedMf.ReplacedAtUtc);
        Assert.Equal("T-MF", replacedMf.ToolId);
        var bq = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.JobOnId == persisted.Id);
        Assert.Equal(outcome.JobOn.BqId, bq.Id);
        Assert.Equal("T-BQ2", bq.ToolId);
        // O bq_id anterior fica com a BQ antiga, ligado à produção.
        var replaced = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == originalBq.Id);
        Assert.Null(replaced.JobOnId);
        Assert.Equal(persisted.Id, replaced.ReplacedJobOnId);
        Assert.NotNull(replaced.ReplacedAtUtc);
        Assert.Equal("T-BQ1", replaced.ToolId);

        // O controlo_id idem: UM controlo, o MESMO de sempre — a
        // edição não cria outro (nem outro jobon).
        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(2, await _db.BqContexts.AsNoTracking().CountAsync());
        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        Assert.Equal(persisted.Id, controlo.JobOnId);
    }

    [Fact]
    public async Task CreateAndUpdate_EndBeforeStart_IsSaved()
    {
        // Diogo, 19/10/2026: «as datas têm de ser livres». O fim antes do
        // início grava (a página avisa); na beta.228 era recusado.
        var machine = await SeedMachineAsync("C2");
        await SeedToolAsync("T-MF", ToolType.Mf, "6018", "1");

        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", machine.Id, null, "T-MF", null,
            InicioEm: new DateOnly(2026, 10, 20), FimEm: new DateOnly(2026, 10, 19)));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 20), persisted.InicioEm);
        Assert.Equal(new DateOnly(2026, 10, 19), persisted.FimEm);

        var update = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId, machine.Id, null, "T-MF", null,
            InicioEm: new DateOnly(2026, 10, 22), FimEm: new DateOnly(2026, 10, 21)));
        Assert.Equal(UpdateJobOnResult.Success, update.Result);
        persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 22), persisted.InicioEm);
        Assert.Equal(new DateOnly(2026, 10, 21), persisted.FimEm);
    }

    [Fact]
    public async Task Update_SwapBq_MovementsStayOnTheOldBq()
    {
        // Diogo, 18/10/2026: são boquilhas diferentes. O registo e os
        // movimentos da BQ antiga ficam nela; a BQ nova começa sem nada, e
        // as duas mostram a produção no histórico.
        var b1 = await SeedMachineAsync("C2");
        await SeedToolAsync("T-MF", ToolType.Mf, "6018", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "Y232", "1");
        await SeedToolAsync("T-BQ2", ToolType.Bq, "Y232", "2");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, null, "T-MF", "T-BQ1"));
        var oldBqId = created.JobOn!.BqId!.Value;
        var trace = new DMO.Alpha.Core.Boquilhas.BqRepairTrace { BqContextId = oldBqId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId, b1.Id, null, "T-MF", "T-BQ2"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.NotEqual(oldBqId, outcome.JobOn!.BqId);
        var persistedTrace = await _db.BqRepairTraces.AsNoTracking().SingleAsync();
        Assert.Equal(oldBqId, persistedTrace.BqContextId);
        Assert.Equal("T-BQ1", (await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == oldBqId)).ToolId);

        var history = new DMO.Alpha.Infrastructure.Boquilhas.BqToolHistoryQuery(_db);
        var oldHistory = await history.ListJobHistoryAsync("T-BQ1", null);
        var newHistory = await history.ListJobHistoryAsync("T-BQ2", null);
        Assert.Equal("N1", Assert.Single(oldHistory.Items).ProductionNumber);
        Assert.Equal("N1", Assert.Single(newHistory.Items).ProductionNumber);

        // Gravar outra vez a mesma BQ não cria mais nenhum bq_id.
        await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId, b1.Id, null, "T-MF", "T-BQ2"));
        Assert.Equal(2, await _db.BqContexts.AsNoTracking().CountAsync());

        // Voltar à BQ antiga retoma o bq_id dela, com o mesmo registo: a
        // produção aparece uma vez no histórico de cada BQ (avaliação dos
        // testes do CI, 18/10/2026).
        var back = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId, b1.Id, null, "T-MF", "T-BQ1"));
        Assert.Equal(oldBqId, back.JobOn!.BqId);
        Assert.Equal(2, await _db.BqContexts.AsNoTracking().CountAsync());
        Assert.Single((await history.ListJobHistoryAsync("T-BQ1", null)).Items);
        Assert.Single((await history.ListJobHistoryAsync("T-BQ2", null)).Items);
        var reactivated = await _db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == oldBqId);
        Assert.Equal(created.JobOn.JobOnId, reactivated.JobOnId);
        Assert.Null(reactivated.ReplacedJobOnId);
    }

    [Fact]
    public async Task Update_SwapCm_TheOldCmStaysInTheProduction_AndGoingBackReusesItsCmId()
    {
        // Diogo, 18/10/2026: o cm_id é o retrato de um tool_id naquele
        // momento; a CM que estava primeiro não desaparece do registo da
        // produção.
        var b1 = await SeedMachineAsync("C2");
        await SeedToolAsync("T-CM1", ToolType.Cm, "6018", "1");
        await SeedToolAsync("T-CM2", ToolType.Cm, "6018", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "6018", "1");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, "T-CM1", "T-MF"));
        var jobOnId = created.JobOn!.JobOnId;
        var oldCmId = created.JobOn.CmId!.Value;

        var outcome = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM2", "T-MF"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.NotEqual(oldCmId, outcome.JobOn!.CmId);
        Assert.Equal("T-CM2", outcome.JobOn.CmToolId);
        var old = await _db.CmContexts.AsNoTracking().SingleAsync(c => c.Id == oldCmId);
        Assert.Equal("T-CM1", old.ToolId);
        Assert.Null(old.JobOnId);
        Assert.Equal(jobOnId, old.ReplacedJobOnId);

        // Gravar outra vez a mesma CM não cria mais nenhum cm_id; voltar à
        // CM antiga retoma o cm_id dela.
        await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM2", "T-MF"));
        Assert.Equal(2, await _db.CmContexts.AsNoTracking().CountAsync());
        var back = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM1", "T-MF"));
        Assert.Equal(oldCmId, back.JobOn!.CmId);
        Assert.Equal(2, await _db.CmContexts.AsNoTracking().CountAsync());
        var reactivated = await _db.CmContexts.AsNoTracking().SingleAsync(c => c.Id == oldCmId);
        Assert.Equal(jobOnId, reactivated.JobOnId);
        Assert.Null(reactivated.ReplacedJobOnId);
        Assert.Null(reactivated.ReplacedAtUtc);
    }

    [Fact]
    public async Task Update_SwapMf_TheOldMfStaysInTheProduction_AndGoingBackReusesItsMfId()
    {
        // Diogo, 19/10/2026: «mf_id snapshot do tool_id usado para jobon_id,
        // então alterar tool_id deve alterar mf_id; depois o que associa a
        // produção é o jobon_id».
        var b1 = await SeedMachineAsync("M2");
        await SeedToolAsync("T-CM", ToolType.Cm, "6018", "1");
        await SeedToolAsync("T-MF1", ToolType.Mf, "6018", "1");
        await SeedToolAsync("T-MF2", ToolType.Mf, "6018", "2");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, "T-CM", "T-MF1"));
        var jobOnId = created.JobOn!.JobOnId;
        var oldMfId = created.JobOn.MfId!.Value;

        var outcome = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF2"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.NotEqual(oldMfId, outcome.JobOn!.MfId);
        Assert.Equal("T-MF2", outcome.JobOn.MfToolId);
        var old = await _db.MfContexts.AsNoTracking().SingleAsync(c => c.Id == oldMfId);
        Assert.Equal("T-MF1", old.ToolId);
        Assert.Null(old.JobOnId);
        Assert.Equal(jobOnId, old.ReplacedJobOnId);
        Assert.NotNull(old.ReplacedAtUtc);

        // Gravar outra vez o mesmo MF não cria mais nenhum mf_id; voltar ao
        // MF antigo retoma o mf_id dele.
        await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF2"));
        Assert.Equal(2, await _db.MfContexts.AsNoTracking().CountAsync());
        var back = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF1"));
        Assert.Equal(oldMfId, back.JobOn!.MfId);
        Assert.Equal(2, await _db.MfContexts.AsNoTracking().CountAsync());
        var reactivated = await _db.MfContexts.AsNoTracking().SingleAsync(c => c.Id == oldMfId);
        Assert.Equal(jobOnId, reactivated.JobOnId);
        Assert.Null(reactivated.ReplacedJobOnId);
        Assert.Null(reactivated.ReplacedAtUtc);
        // O CM não foi tocado: um só cm_id.
        Assert.Equal(1, await _db.CmContexts.AsNoTracking().CountAsync());
    }

    // Um movimento num registo da BQ deste bq_id; devolve o id do movimento.
    private async Task<int> SeedBqMovementAsync(int bqContextId, int quantity)
    {
        var trace = new BqRepairTrace { BqContextId = bqContextId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        var movement = new BqMovement
        {
            BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = quantity,
            OccurredAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow
        };
        _db.BqMovements.Add(movement);
        await _db.SaveChangesAsync();
        return movement.Id;
    }

    [Fact]
    public async Task Update_TresBqsSeguidas_CadaUmaFicaNoSeuBqIdComOsSeusMovimentos_EVoltarAUmaRetomaOBqIdDela()
    {
        // Diogo, 18/10/2026: «um tool_id nunca pode ter o mesmo bq_id». Com
        // duas BQs já trocadas nesta produção, a terceira nasce num bq_id
        // novo; não retoma o bq_id de outra BQ (e os movimentos dela).
        var b1 = await SeedMachineAsync("C2");
        await SeedToolAsync("T-MF", ToolType.Mf, "6018", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "Y232", "1");
        await SeedToolAsync("T-BQ2", ToolType.Bq, "Y232", "2");
        await SeedToolAsync("T-BQ3", ToolType.Bq, "Y232", "3");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, null, "T-MF", "T-BQ1"));
        var jobOnId = created.JobOn!.JobOnId;
        var bq1Id = created.JobOn.BqId!.Value;
        var movement1 = await SeedBqMovementAsync(bq1Id, 10);
        var second = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, null, "T-MF", "T-BQ2"));
        var bq2Id = second.JobOn!.BqId!.Value;
        var movement2 = await SeedBqMovementAsync(bq2Id, 20);

        var third = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, null, "T-MF", "T-BQ3"));

        Assert.Equal(UpdateJobOnResult.Success, third.Result);
        Assert.Equal("T-BQ3", third.JobOn!.BqToolId);
        var bq3Id = third.JobOn.BqId!.Value;
        // Três bq_ids, cada um com a sua BQ; só o da BQ3 é o da produção.
        var bqs = await _db.BqContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { bq1Id, bq2Id, bq3Id }, bqs.Select(c => c.Id));
        Assert.Equal(new[] { "T-BQ1", "T-BQ2", "T-BQ3" }, bqs.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, null, jobOnId }, bqs.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, jobOnId, null }, bqs.Select(c => c.ReplacedJobOnId));
        // Os movimentos ficam no bq_id em que foram registados; a BQ3
        // começa sem nenhum.
        Assert.Equal(bq1Id, await _db.BqMovements.AsNoTracking().Where(m => m.Id == movement1).Select(m => m.Trace.BqContextId).SingleAsync());
        Assert.Equal(bq2Id, await _db.BqMovements.AsNoTracking().Where(m => m.Id == movement2).Select(m => m.Trace.BqContextId).SingleAsync());
        Assert.False(await _db.BqRepairTraces.AsNoTracking().AnyAsync(t => t.BqContextId == bq3Id));

        // Voltar à BQ2, com a BQ1 e a BQ3 trocadas, retoma o bq_id da BQ2.
        var back = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, null, "T-MF", "T-BQ2"));

        Assert.Equal(UpdateJobOnResult.Success, back.Result);
        Assert.Equal(bq2Id, back.JobOn!.BqId);
        Assert.Equal("T-BQ2", back.JobOn.BqToolId);
        bqs = await _db.BqContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { bq1Id, bq2Id, bq3Id }, bqs.Select(c => c.Id));
        Assert.Equal(new[] { "T-BQ1", "T-BQ2", "T-BQ3" }, bqs.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, jobOnId, null }, bqs.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, null, jobOnId }, bqs.Select(c => c.ReplacedJobOnId));
    }

    [Fact]
    public async Task Update_TresCmsSeguidas_CadaUmaFicaNoSeuCmId_EVoltarAUmaRetomaOCmIdDela()
    {
        // Diogo, 18/10/2026: o cm_id é o retrato de um tool_id. Com duas CMs
        // já trocadas nesta produção, a terceira nasce num cm_id novo; não
        // retoma o cm_id de outra CM.
        var b1 = await SeedMachineAsync("C2");
        await SeedToolAsync("T-CM1", ToolType.Cm, "6018", "1");
        await SeedToolAsync("T-CM2", ToolType.Cm, "6018", "2");
        await SeedToolAsync("T-CM3", ToolType.Cm, "6018", "3");
        await SeedToolAsync("T-MF", ToolType.Mf, "6018", "1");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, "T-CM1", "T-MF"));
        var jobOnId = created.JobOn!.JobOnId;
        var cm1Id = created.JobOn.CmId!.Value;
        var second = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM2", "T-MF"));
        var cm2Id = second.JobOn!.CmId!.Value;

        var third = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM3", "T-MF"));

        Assert.Equal(UpdateJobOnResult.Success, third.Result);
        Assert.Equal("T-CM3", third.JobOn!.CmToolId);
        var cm3Id = third.JobOn.CmId!.Value;
        // Três cm_ids, cada um com a sua CM; só o da CM3 é o da produção.
        var cms = await _db.CmContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { cm1Id, cm2Id, cm3Id }, cms.Select(c => c.Id));
        Assert.Equal(new[] { "T-CM1", "T-CM2", "T-CM3" }, cms.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, null, jobOnId }, cms.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, jobOnId, null }, cms.Select(c => c.ReplacedJobOnId));

        // Voltar à CM2, com a CM1 e a CM3 trocadas, retoma o cm_id da CM2.
        var back = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM2", "T-MF"));

        Assert.Equal(UpdateJobOnResult.Success, back.Result);
        Assert.Equal(cm2Id, back.JobOn!.CmId);
        Assert.Equal("T-CM2", back.JobOn.CmToolId);
        cms = await _db.CmContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { cm1Id, cm2Id, cm3Id }, cms.Select(c => c.Id));
        Assert.Equal(new[] { "T-CM1", "T-CM2", "T-CM3" }, cms.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, jobOnId, null }, cms.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, null, jobOnId }, cms.Select(c => c.ReplacedJobOnId));
    }

    [Fact]
    public async Task Update_TresMfsSeguidos_CadaUmFicaNoSeuMfId_EVoltarAUmRetomaOMfIdDele()
    {
        // Diogo, 19/10/2026: «alterar tool_id deve alterar mf_id». Com dois
        // MFs já trocados nesta produção, o terceiro nasce num mf_id novo;
        // não retoma o mf_id de outro MF.
        var b1 = await SeedMachineAsync("M2");
        await SeedToolAsync("T-CM", ToolType.Cm, "6018", "1");
        await SeedToolAsync("T-MF1", ToolType.Mf, "6018", "1");
        await SeedToolAsync("T-MF2", ToolType.Mf, "6018", "2");
        await SeedToolAsync("T-MF3", ToolType.Mf, "6018", "3");
        var created = await _create.HandleAsync(new CreateJobOn(
            "N1", "6018Y232", b1.Id, "T-CM", "T-MF1"));
        var jobOnId = created.JobOn!.JobOnId;
        var mf1Id = created.JobOn.MfId!.Value;
        var second = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF2"));
        var mf2Id = second.JobOn!.MfId!.Value;

        var third = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF3"));

        Assert.Equal(UpdateJobOnResult.Success, third.Result);
        Assert.Equal("T-MF3", third.JobOn!.MfToolId);
        var mf3Id = third.JobOn.MfId!.Value;
        // Três mf_ids, cada um com o seu MF; só o do MF3 é o da produção.
        var mfs = await _db.MfContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { mf1Id, mf2Id, mf3Id }, mfs.Select(c => c.Id));
        Assert.Equal(new[] { "T-MF1", "T-MF2", "T-MF3" }, mfs.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, null, jobOnId }, mfs.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, jobOnId, null }, mfs.Select(c => c.ReplacedJobOnId));

        // Voltar ao MF2, com o MF1 e o MF3 trocados, retoma o mf_id do MF2.
        var back = await _handler.HandleAsync(new UpdateJobOn(jobOnId, b1.Id, "T-CM", "T-MF2"));

        Assert.Equal(UpdateJobOnResult.Success, back.Result);
        Assert.Equal(mf2Id, back.JobOn!.MfId);
        Assert.Equal("T-MF2", back.JobOn.MfToolId);
        mfs = await _db.MfContexts.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(new[] { mf1Id, mf2Id, mf3Id }, mfs.Select(c => c.Id));
        Assert.Equal(new[] { "T-MF1", "T-MF2", "T-MF3" }, mfs.Select(c => c.ToolId));
        Assert.Equal(new int?[] { null, jobOnId, null }, mfs.Select(c => c.JobOnId));
        Assert.Equal(new int?[] { jobOnId, null, jobOnId }, mfs.Select(c => c.ReplacedJobOnId));
    }

    [Fact]
    public async Task Update_ClearsTemporalWindow_OpenProduction()
    {
        // A janela em aberto: a edição com as datas a null limpa a
        // associação de dias (o mesmo efeito canónico do [Retirar do
        // dia], pela folha) — o JobOn, os contextos e o controlo ficam.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-101",
            "5810",
            b1.Id,
            CmToolId: "T1",
            MfToolId: "T-MF",
            InicioEm: new DateOnly(2026, 3, 2),
            FimEm: new DateOnly(2026, 3, 9)));

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId,
            b1.Id,
            CmToolId: "T1",
            MfToolId: "T-MF"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.Null(outcome.JobOn!.InicioEm);
        Assert.Null(outcome.JobOn.FimEm);

        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Null(persisted.InicioEm);
        Assert.Null(persisted.FimEm);
    }

    [Fact]
    public async Task Update_AddsContextThatDidNotExist_EnsuresApplicableContext()
    {
        // O ensure do create herda na edição: uma produção criada SEM
        // contexto BQ (o slot não era aplicável) ganha-o quando a
        // edição o traz — o contexto nasce na MESMA escrita atómica da
        // edição, vinculado a ESTE jobon_id.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "T194", "3");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-102", "5810", b1.Id, CmToolId: "T1", MfToolId: "T-MF"));
        Assert.Null(created.JobOn!.BqId);

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId,
            b1.Id,
            CmToolId: "T1",
            MfToolId: "T-MF",
            BqToolId: "T-BQ1"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn!.BqId);
        Assert.Equal("T-BQ1", outcome.JobOn.BqToolId);

        var bq = await _db.BqContexts.AsNoTracking().SingleAsync();
        Assert.Equal(created.JobOn.JobOnId, bq.JobOnId);
        Assert.Equal("T-BQ1", bq.ToolId);
        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Update_NullSlots_PreserveExistingContexts()
    {
        // Um slot null na edição PRESERVA o contexto existente: a
        // edição nunca destrói um facto (os traces/movimentos ancorados
        // ficam). O null é o estado «não mexer» do handler, não um
        // remover: por isso o Editar não oferece «Retirar» (deep sweep,
        // 18/10/2026, A1-M3).
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "T194", "3");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-103", "5810", b1.Id, "T1", "T-MF", "T-BQ1"));

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId,
            b1.Id,
            CmToolId: null,
            MfToolId: "T-MF",
            BqToolId: null));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.Equal("T1", outcome.JobOn!.CmToolId);
        Assert.Equal("T-BQ1", outcome.JobOn.BqToolId);

        Assert.Equal(1, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.BqContexts.AsNoTracking().CountAsync());
        var cm = await _db.CmContexts.AsNoTracking().SingleAsync();
        Assert.Equal("T1", cm.ToolId);
    }

    [Fact]
    public async Task Update_UnknownJobOn_RefusedClean()
    {
        // O jobon_id que não resolve é recusa tipada JobOnNotFound —
        // nenhuma linha é inventada, nunca uma exceção.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-110", "5810", b1.Id, MfToolId: "T-MF"));

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            999999,
            b1.Id,
            CmToolId: "T1",
            MfToolId: "T-MF"));

        Assert.Equal(UpdateJobOnResult.JobOnNotFound, outcome.Result);
        Assert.Null(outcome.JobOn);

        // A BD INALTERADA: o jobon existente fica exatamente como está
        // (zero escritas parciais — a atomicidade).
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(created.JobOn!.JobOnId, persisted.Id);
        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Update_MissingMfTool_RefusedBdUnchanged(string? mfToolId)
    {
        // A regra do Owner HERDA na edição: sem MF a produção
        // re-planeada não se grava — o facto persistido fica
        // exatamente como estava (a janela, a linha, as tools).
        var b1 = await SeedMachineAsync("B1");
        var b2 = await SeedMachineAsync("B2");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-111",
            "5810",
            b1.Id,
            "T1",
            "T-MF",
            InicioEm: new DateOnly(2026, 3, 2),
            FimEm: new DateOnly(2026, 3, 9)));

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId,
            b2.Id,
            "T1",
            mfToolId,
            InicioEm: new DateOnly(2026, 4, 12)));

        Assert.Equal(UpdateJobOnResult.MissingMfTool, outcome.Result);
        Assert.Null(outcome.JobOn);

        // A BD INALTERADA quando recusa: a máquina, as datas e as
        // tools são as de ANTES da edição recusada.
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(b1.Id, persisted.MachineId);
        Assert.Equal(new DateOnly(2026, 3, 2), persisted.InicioEm);
        Assert.Equal(new DateOnly(2026, 3, 9), persisted.FimEm);
        var cm = await _db.CmContexts.AsNoTracking().SingleAsync();
        Assert.Equal("T1", cm.ToolId);
    }

    [Fact]
    public async Task Update_UnknownMachine_RefusedBdUnchanged()
    {
        // A MESMA recusa do create: a máquina âncora tem de existir —
        // nunca é auto-selecionada outra máquina, e a BD fica
        // inalterada.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-112", "5810", b1.Id, "T1", "T-MF",
            InicioEm: new DateOnly(2026, 3, 2)));

        var outcome = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId,
            999,
            "T1",
            "T-MF",
            InicioEm: new DateOnly(2026, 4, 12)));

        Assert.Equal(UpdateJobOnResult.MachineNotFound, outcome.Result);

        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(b1.Id, persisted.MachineId);
        Assert.Equal(new DateOnly(2026, 3, 2), persisted.InicioEm);
    }

    [Fact]
    public async Task Update_UnknownTools_RefusedBdUnchanged()
    {
        // As recusas de tools do create herdam na edição: um tool_id
        // que não resolve no registo Ferramentas recusa a edição
        // inteira — a BD inalterada (zero escritas parciais).
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "T194", "3");

        var created = await _create.HandleAsync(new CreateJobOn(
            "PN-2025-113", "5810", b1.Id, "T1", "T-MF", "T-BQ1"));

        var refusedCm = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn!.JobOnId, b1.Id, "T-DESCONHECIDA", "T-MF", "T-BQ1"));
        Assert.Equal(UpdateJobOnResult.CmToolNotFound, refusedCm.Result);

        var refusedMf = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId, b1.Id, "T1", "T-DESCONHECIDA", "T-BQ1"));
        Assert.Equal(UpdateJobOnResult.MfToolNotFound, refusedMf.Result);

        var refusedBq = await _handler.HandleAsync(new UpdateJobOn(
            created.JobOn.JobOnId, b1.Id, "T1", "T-MF", "T-DESCONHECIDA"));
        Assert.Equal(UpdateJobOnResult.BqToolNotFound, refusedBq.Result);

        // A BD INALTERADA pelas três recusas: o jobon e os contextos
        // exatamente como o create os deixou.
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal("PN-2025-113", persisted.ProductionNumber);
        Assert.Equal(1, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal("T1", (await _db.CmContexts.AsNoTracking().SingleAsync()).ToolId);
        Assert.Equal("T-BQ1", (await _db.BqContexts.AsNoTracking().SingleAsync()).ToolId);
    }
}
