using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Prova o write path da criação de um Job On: criação com sucesso (jobon_id
/// emitido pelo backend, contextos aplicáveis cm_id/mf_id/bq_id ancorados às
/// Tools canónicas explicitamente selecionadas, controlo_id associado criado
/// na MESMA escrita atómica — spec Job On L33 —, read model do contexto
/// estabelecido devolvido), a regra «only applicable component contexts are
/// created» (slot sem seleção não cria contexto — o MF é OBRIGATÓRIO em
/// todo o Job On, regra do Owner 2026-10-10: «jobon necessita sempre de MF,
/// senao nao faz garrafas ou frascos» — o molde final dá a forma do produto
/// e a base 4-dígitos da referência; a sua falta é recusa tipada
/// MissingMfTool), a invariante da spec de que a MESMA Tool canónica pode
/// aparecer em várias produções — cada uma com o seu próprio contexto e o
/// seu próprio controlo_id — e as recusas tipadas (factos obrigatórios em
/// branco, máquina desconhecida, MF em falta, tool não registada, nº de
/// produção duplicado), todas verificadas antes de qualquer escrita: uma
/// recusa não deixa Job Ons, contextos NEM controles parcialmente criados.
/// </summary>
public sealed class CreateJobOnHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _handler;

    public CreateJobOnHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CreateJobOnHandler(_db);
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
    public async Task Create_AllToolsSelected_EstablishesProductionContext()
    {
        // O cenário completo: máquina real, as três Tools canónicas
        // selecionadas explicitamente — o contexto de produção fica
        // estabelecido com as identidades emitidas pelo backend.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedToolAsync("T-BQ1", ToolType.Bq, "T194", "3");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-050",
            "5810",
            machine.Id,
            "T1",
            "T-MF",
            "T-BQ1"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn);

        // O Job On persistido: factos da produção tal como submetidos.
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(outcome.JobOn!.JobOnId, persisted.Id);
        Assert.Equal("PN-2025-050", persisted.ProductionNumber);
        Assert.Equal("5810", persisted.Reference);
        Assert.Equal(machine.Id, persisted.MachineId);

        // Os três contextos aplicáveis, cada um vinculado a ESTE jobon_id.
        var cm = await _db.CmContexts.AsNoTracking().SingleAsync();
        var mf = await _db.MfContexts.AsNoTracking().SingleAsync();
        var bq = await _db.BqContexts.AsNoTracking().SingleAsync();
        Assert.Equal(persisted.Id, cm.JobOnId);
        Assert.Equal("T1", cm.ToolId);
        Assert.Equal(persisted.Id, mf.JobOnId);
        Assert.Equal("T-MF", mf.ToolId);
        Assert.Equal(persisted.Id, bq.JobOnId);
        Assert.Equal("T-BQ1", bq.ToolId);

        // O read model devolvido é o contexto estabelecido — o MESMO que a
        // query canónica serve aos consumidores: identidades + Tools.
        Assert.Equal(persisted.Id, outcome.JobOn.JobOnId);
        Assert.Equal("PN-2025-050", outcome.JobOn.ProductionNumber);
        Assert.Equal("5810", outcome.JobOn.Reference);
        Assert.Equal("B1", outcome.JobOn.Machine);
        Assert.Equal(cm.Id, outcome.JobOn.CmId);
        Assert.Equal("T1", outcome.JobOn.CmToolId);
        Assert.Equal(mf.Id, outcome.JobOn.MfId);
        Assert.Equal("T-MF", outcome.JobOn.MfToolId);
        Assert.Equal(bq.Id, outcome.JobOn.BqId);
        Assert.Equal("T-BQ1", outcome.JobOn.BqToolId);

        // O controlo_id associado nasce na MESMA escrita atómica (spec
        // Job On L33): UM controlo, vinculado a ESTE jobon_id — a produção
        // fica com o seu controlo_id garantido desde a criação.
        var controlo = await _db.Controlos.AsNoTracking().SingleAsync();
        Assert.Equal(persisted.Id, controlo.JobOnId);
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_OnlyApplicableContexts_AreCreated()
    {
        // Spec: «Only applicable component contexts are created». O MF é
        // OBRIGATÓRIO (regra do Owner 2026-10-10), pelo que o slot sem
        // seleção que resta é o BQ: uma produção com CM e MF selecionados
        // cria os contextos CM e MF e NENHUM BQ.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-051",
            "5810",
            machine.Id,
            CmToolId: "T1",
            MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn);

        Assert.Equal(1, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());

        Assert.NotNull(outcome.JobOn!.CmId);
        Assert.NotNull(outcome.JobOn.MfId);
        Assert.Null(outcome.JobOn.BqId);

        // O controlo_id existe mesmo quando só um subconjunto de
        // contextos é aplicável: é criado com a produção, não com os
        // contextos.
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
        Assert.Equal(outcome.JobOn!.JobOnId, (await _db.Controlos.AsNoTracking().SingleAsync()).JobOnId);
    }

    [Fact]
    public async Task Create_MfOnlySelected_MinimalProduction_SingleMfContext()
    {
        // A produção mínima válida: o MF é OBRIGATÓRIO (regra do Owner
        // 2026-10-10 — «jobon necessita sempre de MF, senao nao faz
        // garrafas ou frascos»; o MF dá a forma do produto e a base
        // 4-dígitos da referência), os slots CM/BQ sem seleção são não
        // aplicáveis e nenhum contexto deles é criado — o Job On existe e
        // o contexto pode ser consumido como tal.
        var machine = await SeedMachineAsync("B2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-052",
            "5810",
            machine.Id,
            MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn);

        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());

        Assert.Null(outcome.JobOn!.CmId);
        Assert.NotNull(outcome.JobOn.MfId);
        Assert.Null(outcome.JobOn.BqId);
        Assert.Equal("T-MF", outcome.JobOn.MfToolId);
        Assert.Equal("B2", outcome.JobOn.Machine);

        // O controlo_id existe mesmo sem contextos CM/BQ aplicáveis: a
        // produção nasce com o seu controlo_id (spec Job On L33) mesmo
        // quando só o MF obrigatório tem seleção.
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
        Assert.Equal(outcome.JobOn.JobOnId, (await _db.Controlos.AsNoTracking().SingleAsync()).JobOnId);
    }

    [Fact]
    public async Task Create_RecordsWhenTheJobOnWasSaved()
    {
        // O Sumário do Planeamento anuncia o Job On no dia em que foi gravado
        // (Diogo, 19/10/2026: «quando damos lock deveria guardar»).
        var machine = await SeedMachineAsync("B3");
        await SeedToolAsync("T-MF-CRIADO", ToolType.Mf, "5811", "1");
        var antes = DateTime.UtcNow;

        var outcome = await _handler.HandleAsync(new CreateJobOn("PN-CRIADO", "5811", machine.Id, MfToolId: "T-MF-CRIADO"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        var criado = (await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == outcome.JobOn!.JobOnId)).CriadoEmUtc;
        Assert.NotNull(criado);
        Assert.InRange(criado!.Value, antes, DateTime.UtcNow);
    }

    [Fact]
    public async Task Create_SameToolInTwoProductions_EachKeepsItsOwnContext()
    {
        // Invariante da spec: a MESMA Tool canónica pode aparecer em mais do
        // que uma produção — cada produção mantém o seu próprio contexto.
        // Dois Job Ons com a mesma CM T1 (e o mesmo MF obrigatório do
        // desenho) têm DOIS cm_id distintos, cada um vinculado ao seu
        // jobon_id.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var first = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-060", "5810", machine.Id, CmToolId: "T1", MfToolId: "T-MF"));
        var second = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-061", "5810", machine.Id, CmToolId: "T1", MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, first.Result);
        Assert.Equal(CreateJobOnResult.Success, second.Result);
        Assert.NotEqual(first.JobOn!.JobOnId, second.JobOn!.JobOnId);

        var cmContexts = await _db.CmContexts.AsNoTracking().ToListAsync();
        Assert.Equal(2, cmContexts.Count);
        Assert.All(cmContexts, c => Assert.Equal("T1", c.ToolId));
        Assert.All(cmContexts, c => Assert.Contains(
            new[] { first.JobOn.JobOnId, second.JobOn.JobOnId },
            id => id == c.JobOnId));
        Assert.NotEqual(first.JobOn.CmId, second.JobOn.CmId);

        // O mesmo vale para o MF obrigatório: cada produção mantém o SEU
        // mf_id vinculado ao seu jobon_id.
        var mfContexts = await _db.MfContexts.AsNoTracking().ToListAsync();
        Assert.Equal(2, mfContexts.Count);
        Assert.All(mfContexts, c => Assert.Equal("T-MF", c.ToolId));
        Assert.NotEqual(first.JobOn.MfId, second.JobOn.MfId);

        // Cada produção mantém o SEU controlo_id: dois jobons → dois
        // controles, um por jobon_id (spec Job On L33).
        var controlos = await _db.Controlos.AsNoTracking().ToListAsync();
        Assert.Equal(2, controlos.Count);
        Assert.Contains(controlos, c => c.JobOnId == first.JobOn.JobOnId);
        Assert.Contains(controlos, c => c.JobOnId == second.JobOn.JobOnId);
    }

    [Fact]
    public async Task Create_SameToolIdInTwoSlots_IsNotRefused()
    {
        // Zero gates inventados: a spec não proíbe a mesma Tool em dois
        // tipos de contexto da mesma produção — o Tool.Type nunca é um gate
        // (precedente Peso/Boquilhas). A recusa não é inventada aqui.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-062",
            "5810",
            machine.Id,
            CmToolId: "T1",
            MfToolId: "T1"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.Equal("T1", outcome.JobOn!.CmToolId);
        Assert.Equal("T1", outcome.JobOn.MfToolId);
    }

    [Fact]
    public async Task Create_TrimsSurroundingWhitespaceFromTextFacts()
    {
        // O facto persistido é limpo: espaços envolventes não fazem parte
        // do nº de produção nem da referência (e o nº apresentado a
        // duplicados é o facto limpo). O MF obrigatório do desenho está
        // selecionado — a produção é válida.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "  PN-2025-070  ",
            "  5810  ",
            machine.Id,
            MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.Equal("PN-2025-070", outcome.JobOn!.ProductionNumber);
        Assert.Equal("5810", outcome.JobOn.Reference);

        var persisted = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal("PN-2025-070", persisted.ProductionNumber);
        Assert.Equal("5810", persisted.Reference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_MissingProductionNumber_Refused(string? productionNumber)
    {
        var machine = await SeedMachineAsync("B1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            productionNumber!, "5810", machine.Id));

        Assert.Equal(CreateJobOnResult.MissingProductionNumber, outcome.Result);
        Assert.Null(outcome.JobOn);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_MissingReference_Refused(string? reference)
    {
        var machine = await SeedMachineAsync("B1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-080", reference!, machine.Id));

        Assert.Equal(CreateJobOnResult.MissingReference, outcome.Result);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownMachine_Refused()
    {
        // A máquina é âncora canónica persistida: MachineId que não
        // resolve é recusa tipada — nunca é auto-selecionada outra máquina.
        await SeedMachineAsync("B1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-081", "5810", MachineId: 999));

        Assert.Equal(CreateJobOnResult.MachineNotFound, outcome.Result);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_MissingMfTool_Refused(string? mfToolId)
    {
        // A regra do Owner 2026-10-10: «jobon necessita sempre de MF, senao
        // nao faz garrafas ou frascos» — o MF (molde final) dá a forma do
        // produto e a base 4-dígitos da referência (o desenho): sem ele não
        // há produção real. A recusa é tipada (MissingMfTool — a identidade
        // mínima do registo de produção, como MissingReference nas Tools)
        // e verificada ANTES de qualquer escrita: zero Job Ons, contextos
        // e controles.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-085", "5810", machine.Id, CmToolId: "T1", MfToolId: mfToolId));

        Assert.Equal(CreateJobOnResult.MissingMfTool, outcome.Result);
        Assert.Null(outcome.JobOn);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.BqContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownCmTool_Refused()
    {
        // O MF obrigatório está selecionado: a recusa é mesmo a da CM
        // desconhecida (o check do MF vem antes do lookup das tools).
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-082", "5810", machine.Id, CmToolId: "T-DESCONHECIDA", MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.CmToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownMfTool_Refused()
    {
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-083", "5810", machine.Id, CmToolId: "T1", MfToolId: "T-DESCONHECIDA"));

        Assert.Equal(CreateJobOnResult.MfToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_UnknownBqTool_Refused()
    {
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var outcome = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-084",
            "5810",
            machine.Id,
            CmToolId: "T1",
            MfToolId: "T-MF",
            BqToolId: "T-BQ-DESCONHECIDA"));

        Assert.Equal(CreateJobOnResult.BqToolNotFound, outcome.Result);
        Assert.Equal(0, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.MfContexts.AsNoTracking().CountAsync());
        Assert.Equal(0, await _db.Controlos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_DuplicateProductionNumber_RefusedWithoutPartialWrite()
    {
        // A referência + o nº identificam a ocorrência: um segundo Job On
        // da mesma referência com o mesmo nº é recusado tipadamente e não fica nada criado
        // (nenhum Job On, nenhum contexto, nenhum controlo novo). O MF
        // obrigatório está selecionado em ambos — a recusa é mesmo a do
        // duplicado.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-2", ToolType.Cm, "5447", "3");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var first = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-090", "5810", machine.Id, CmToolId: "T1", MfToolId: "T-MF"));
        var second = await _handler.HandleAsync(new CreateJobOn(
            "PN-2025-090", "5810", machine.Id, CmToolId: "T-2", MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, first.Result);
        Assert.Equal(CreateJobOnResult.DuplicateProductionNumber, second.Result);
        Assert.Null(second.JobOn);

        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.CmContexts.AsNoTracking().CountAsync());
        Assert.Equal(1, await _db.MfContexts.AsNoTracking().CountAsync());

        // O primeiro Job On criado tem o seu controlo_id; a recusa do
        // duplicado não cria um segundo controlo.
        Assert.Equal(1, await _db.Controlos.AsNoTracking().CountAsync());
        Assert.Equal(first.JobOn!.JobOnId, (await _db.Controlos.AsNoTracking().SingleAsync()).JobOnId);
    }

    [Fact]
    public async Task Create_SameProductionNumberOtherReference_Succeeds()
    {
        // Bug do Owner 17/10/2026: o 202601 de 9389T184 bloqueava o
        // 202601 de 1075C142. O nº só identifica a produção junto com a
        // referência.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        var first = await _handler.HandleAsync(new CreateJobOn(
            "202601", "9389T184", machine.Id, MfToolId: "T-MF"));
        var second = await _handler.HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, first.Result);
        Assert.Equal(CreateJobOnResult.Success, second.Result);
        Assert.Equal(2, await _db.JobOns.AsNoTracking().CountAsync(j => j.ProductionNumber == "202601"));
    }

    [Fact]
    public async Task Create_SameReferenceOtherCase_RefusedAsDuplicate()
    {
        // A mesma referência escrita em minúsculas é a mesma produção.
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        await _handler.HandleAsync(new CreateJobOn(
            "202608", "1075C194", machine.Id, MfToolId: "T-MF"));
        var second = await _handler.HandleAsync(new CreateJobOn(
            "202608", "1075c194", machine.Id, MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.DuplicateProductionNumber, second.Result);
        Assert.Equal(1, await _db.JobOns.AsNoTracking().CountAsync());
    }
}
