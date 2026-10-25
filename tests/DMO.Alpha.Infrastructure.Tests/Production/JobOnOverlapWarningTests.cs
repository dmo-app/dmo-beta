using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O AVISO INFORMATIVO do overlap da máquina (o Owner 2026-10-12: «overlap
/// da maquina deve dar um warning, nao deve bloquear, o warning deve dizer
/// detalhadamente o que esta mal como resolver»): a criação e a edição
/// GRAVAM SEMPRE (o mantra — o aviso nunca é recusa) e o outcome
/// transporta o OverlapWarning DETALHADO — as produções em conflito (a
/// referência total, o nº, AS JANELAS de cada), o período exato da
/// sobreposição e a sugestão concreta de resolução no texto; SEM
/// sobreposição, ZERO warning. A janela inteiramente em aberto (ambos
/// os dias null) não afirma período nenhum — nada a comparar.
/// </summary>
public sealed class JobOnOverlapWarningTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _createHandler;
    private readonly UpdateJobOnHandler _updateHandler;

    public JobOnOverlapWarningTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _createHandler = new CreateJobOnHandler(_db);
        _updateHandler = new UpdateJobOnHandler(_db);
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

    private async Task SeedToolAsync(string toolId, ToolType type, string reference, string lot)
    {
        _db.Tools.Add(new Tool { ToolId = toolId, Type = type, Reference = reference, Lot = lot });
        await _db.SaveChangesAsync();
    }

    private async Task<int> SeedJobOnAsync(
        Machine machine,
        string productionNumber,
        string reference,
        DateOnly? inicioEm,
        DateOnly? fimEm)
    {
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            MachineId = machine.Id,
            InicioEm = inicioEm,
            FimEm = fimEm
        };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndatedExistingProduction_DoesNotInventOverlap_OnCreateOrUpdate(bool editing)
    {
        var machine = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        var undatedId = await SeedJobOnAsync(machine, "PN-SEM-DATAS", "1075C142", null, null);
        var from = new DateOnly(2026, 4, 12);
        var until = new DateOnly(2026, 4, 20);
        if (editing)
        {
            var id = await SeedJobOnAsync(machine, "PN-EDITADA", "1075C142", null, null);
            var result = await _updateHandler.HandleAsync(new UpdateJobOn(id, machine.Id, MfToolId: "T-MF", InicioEm: from, FimEm: until));
            Assert.Equal(UpdateJobOnResult.Success, result.Result);
            Assert.Null(result.OverlapWarning);
            Assert.Equal(id, result.JobOn!.JobOnId);
        }
        else
        {
            var result = await _createHandler.HandleAsync(new CreateJobOn("PN-NOVA", "1075C142", machine.Id,
                MfToolId: "T-MF", InicioEm: from, FimEm: until));
            Assert.Equal(CreateJobOnResult.Success, result.Result);
            Assert.Null(result.OverlapWarning);
        }
        var saved = await _db.JobOns.SingleAsync(j => j.Id != undatedId);
        Assert.Equal(from, saved.InicioEm);
        Assert.Equal(until, saved.FimEm);
        Assert.Null((await _db.JobOns.SingleAsync(j => j.Id == undatedId)).InicioEm);
    }

    [Fact]
    public void UndatedCandidate_DoesNotHideTheRealConflict()
    {
        var warning = JobOnOverlap.Evaluate(new(2026, 4, 12), new(2026, 4, 20), "B1", new[]
        {
            new OverlapCandidate(1, "1075C142", "SEM-DATAS", null, null),
            new OverlapCandidate(2, "1075C142", "CONFLITO-REAL", new(2026, 4, 10), new(2026, 4, 15))
        });
        Assert.Equal("CONFLITO-REAL", Assert.Single(warning!.Conflicts).ProductionNumber);
        Assert.DoesNotContain("SEM-DATAS", warning.Message);
        Assert.Contains("12/04/2026–15/04/2026", warning.Message);
    }

    [Fact]
    public void MixedOpenAndClosedConflicts_DoesNotSuggestThatTheMachineIsFreeAfterTheClosedEnd()
    {
        var warning = JobOnOverlap.Evaluate(new(2026, 4, 12), new(2026, 4, 20), "B1", new[]
        {
            new OverlapCandidate(1, "1075C142", "ATE-AVISO", new(2026, 4, 10), null),
            new OverlapCandidate(2, "1075C142", "COM-FIM", new(2026, 4, 11), new(2026, 4, 15))
        });
        Assert.Equal(2, warning!.Conflicts.Count);
        Assert.Contains("ainda está em aberto", warning.Message);
        Assert.DoesNotContain("Ajusta o início da nova para depois de 15/04/2026", warning.Message);
    }

    [Fact]
    public async Task Create_WithOverlapOnSameMachine_SavesWithoutWarning()
    {
        // A produção existente na B1: 10/04–15/04. A nova na MESMA
        // máquina: 12/04–20/04 — sobrepõe 12/04–15/04.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T1", ToolType.Cm, "5447", "2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedJobOnAsync(b1, "PN-EXISTENTE", "1075C142", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 15));

        var outcome = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-NOVA",
            "1075C142",
            b1.Id,
            "T1",
            "T-MF",
            null,
            new DateOnly(2026, 4, 12),
            new DateOnly(2026, 4, 20)));

        // O MANTRA: a produção está GRAVADA — o overlap nunca bloqueia.
        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.NotNull(outcome.JobOn);
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "PN-NOVA");
        Assert.Equal(new DateOnly(2026, 4, 12), persisted.InicioEm);

        // Sem aviso entre produções da mesma linha (Diogo, 25/10/2026:
        // «so se aplica as bqs»): a sobreposição fica para as BQ.
        Assert.Null(outcome.OverlapWarning);
    }

    [Fact]
    public async Task Update_WithOverlapOnSameMachine_SavesWithoutWarning()
    {
        // Duas produções na B1 em janelas DISJUNTAS (a edição original
        // sem aviso); a edição RE-PLANEIA a segunda para cima da
        // primeira: grava + avisa.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedJobOnAsync(b1, "PN-UM", "1075C142", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
        var editado = await SeedJobOnAsync(b1, "PN-DOIS", "1075C142", new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));

        var outcome = await _updateHandler.HandleAsync(new UpdateJobOn(
            editado,
            b1.Id,
            MfToolId: "T-MF",
            InicioEm: new DateOnly(2026, 5, 3),
            FimEm: new DateOnly(2026, 5, 12)));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == editado);
        Assert.Equal(new DateOnly(2026, 5, 3), persisted.InicioEm);
        Assert.Equal(new DateOnly(2026, 5, 12), persisted.FimEm);

        Assert.Null(outcome.OverlapWarning);
    }

    [Fact]
    public async Task Create_WithoutOverlap_ZeroWarning()
    {
        var b1 = await SeedMachineAsync("B1");
        var b2 = await SeedMachineAsync("B2");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");

        // Janelas DISJUNTAS na MESMA máquina (depois do fim da
        // existente): sem sobreposição.
        await SeedJobOnAsync(b1, "PN-EXISTENTE", "1075C142", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 10));

        var after = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-DEPOIS", "1075C142", b1.Id, MfToolId: "T-MF",
            InicioEm: new DateOnly(2026, 4, 11), FimEm: new DateOnly(2026, 4, 20)));
        Assert.Equal(CreateJobOnResult.Success, after.Result);
        Assert.Null(after.OverlapWarning);

        // A MESMA janela noutra MÁQUINA: máquinas diferentes nunca
        // entram em conflito.
        var otherMachine = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-OUTRA-LINHA", "1075C142", b2.Id, MfToolId: "T-MF",
            InicioEm: new DateOnly(2026, 4, 2), FimEm: new DateOnly(2026, 4, 8)));
        Assert.Equal(CreateJobOnResult.Success, otherMachine.Result);
        Assert.Null(otherMachine.OverlapWarning);
    }

    [Fact]
    public async Task Create_EntirelyOpenWindow_NothingToCompare_ZeroWarning()
    {
        // A janela da nova produção INTEIRAMENTE em aberto (ambos os
        // dias null) não afirma período nenhum: nada a comparar — o
        // registo truthful continua, o aviso nasce quando houver dias
        // (a decisão registada do contrato).
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedJobOnAsync(b1, "PN-EXISTENTE", "1075C142", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 15));

        var outcome = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-SEM-DIAS", "1075C142", b1.Id, MfToolId: "T-MF"));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.Null(outcome.OverlapWarning);
    }

    [Fact]
    public async Task Create_OpenEndAgainstOpenStart_SavesWithoutWarning()
    {
        // A existente «até aviso» (fim null) e a nova com início dentro:
        // a janela DELA diz-se «até aviso», a sobreposição é o período
        // concreto (o fim da nova limita-a) e a sugestão manda definir o
        // fim da anterior na folha.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        await SeedJobOnAsync(b1, "PN-EM-MAQUINA", "1075C142", new DateOnly(2026, 4, 10), null);

        var outcome = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-NOVA", "1075C142", b1.Id, MfToolId: "T-MF",
            InicioEm: new DateOnly(2026, 4, 12), FimEm: new DateOnly(2026, 4, 20)));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.Null(outcome.OverlapWarning);
    }

    [Theory]
    [InlineData(-1, true)]  // começa na véspera do fim da existente
    [InlineData(0, false)]  // começa no dia em que a existente acaba: o dia da troca
    [InlineData(1, false)]  // começa no dia seguinte
    public async Task Create_ComecaNoDiaEmQueAOutraAcaba_GravaSemAviso_NoDiaSeguinteNaoCruza(int inicioDaNova, bool avisa)
    {
        // O dia da troca não conta (Diogo, 21/10/2026: «uma producao sai
        // dia 20 outra entra dia 20»): a nova que entra no dia em que a
        // existente sai da B1 (15/04) não a cruza; a que entra na véspera sim.
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        var fimDaExistente = new DateOnly(2026, 4, 15);
        await SeedJobOnAsync(b1, "PN-EXISTENTE", "1075C142", new DateOnly(2026, 4, 10), fimDaExistente);
        var inicio = fimDaExistente.AddDays(inicioDaNova);
        var fim = new DateOnly(2026, 4, 20);

        var outcome = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-NOVA", "1075C142", b1.Id, MfToolId: "T-MF", InicioEm: inicio, FimEm: fim));

        await AssertGravadaSemAvisoAsync(outcome, inicio, fim, avisa, overlapFrom: inicio, overlapUntil: fimDaExistente);
    }

    [Theory]
    [InlineData(1, true)]   // acaba no dia seguinte ao início da existente
    [InlineData(0, false)]  // acaba no dia em que a existente começa: o dia da troca
    [InlineData(-1, false)] // acaba na véspera
    public async Task Create_AcabaNoDiaEmQueAOutraComeca_GravaSemAviso_NaVesperaNaoCruza(int fimDaNova, bool avisa)
    {
        var b1 = await SeedMachineAsync("B1");
        await SeedToolAsync("T-MF", ToolType.Mf, "5810", "1");
        var inicioDaExistente = new DateOnly(2026, 4, 10);
        await SeedJobOnAsync(b1, "PN-EXISTENTE", "1075C142", inicioDaExistente, new DateOnly(2026, 4, 15));
        var inicio = new DateOnly(2026, 4, 5);
        var fim = inicioDaExistente.AddDays(fimDaNova);

        var outcome = await _createHandler.HandleAsync(new CreateJobOn(
            "PN-NOVA", "1075C142", b1.Id, MfToolId: "T-MF", InicioEm: inicio, FimEm: fim));

        await AssertGravadaSemAvisoAsync(outcome, inicio, fim, avisa, overlapFrom: inicioDaExistente, overlapUntil: fim);
    }

    /// <summary>
    /// A nova fica sempre gravada (lida de novo da base); com aviso, o
    /// conflito é a PN-EXISTENTE no período indicado; sem aviso, nenhum.
    /// </summary>
    private async Task AssertGravadaSemAvisoAsync(
        CreateJobOnOutcome outcome, DateOnly inicio, DateOnly fim, bool avisa, DateOnly overlapFrom, DateOnly overlapUntil)
    {
        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        var persisted = await _db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "PN-NOVA");
        Assert.Equal(inicio, persisted.InicioEm);
        Assert.Equal(fim, persisted.FimEm);
        // A produção nunca avisa; o cruzamento das datas (a regra das BQ)
        // distingue a véspera do dia da troca.
        Assert.Null(outcome.OverlapWarning);
        Assert.Equal(avisa, JobOnOverlap.Crosses(inicio, fim, overlapFrom == inicio ? new DateOnly(2026, 4, 10) : overlapFrom, overlapFrom == inicio ? overlapUntil : new DateOnly(2026, 4, 15)));
    }

    [Theory]
    [InlineData(20, 20, false)] // sai dia 20, entra dia 20: o dia da troca
    [InlineData(20, 19, true)]  // entra na véspera da saída
    [InlineData(20, 21, false)]
    public void Crosses_ODiaDaTrocaNaoESobreposicao(int fimDaQueSai, int inicioDaQueEntra, bool cruza)
    {
        var sai = (Inicio: new DateOnly(2026, 10, 10), Fim: new DateOnly(2026, 10, fimDaQueSai));
        var entra = new DateOnly(2026, 10, inicioDaQueEntra);
        Assert.Equal(cruza, JobOnOverlap.Crosses(sai.Inicio, sai.Fim, entra, null));
        Assert.Equal(cruza, JobOnOverlap.Crosses(entra, null, sai.Inicio, sai.Fim));
    }

    [Fact]
    public void JobOnOverlap_PureCalculation_TwoConflicts_ListsEach()
    {
        // A calculadora pura (a mesma que os handlers e a folha /JobOn
        // usam): DOIS conflitos — cada produção com a SUA janela e o SEU
        // período exato; a sugestão segue a ÚLTIMA a sair da máquina.
        var warning = JobOnOverlap.Evaluate(
            new DateOnly(2026, 4, 15),
            new DateOnly(2026, 4, 20),
            "B1",
            new[]
            {
                new OverlapCandidate(1, "1075C142", "PN-1", new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 16)),
                new OverlapCandidate(2, "1075C142", "PN-2", new DateOnly(2026, 4, 18), new DateOnly(2026, 4, 25)),
                new OverlapCandidate(3, "1075C142", "PN-3", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 9))
            });

        Assert.NotNull(warning);
        Assert.Equal(2, warning!.Conflicts.Count);
        Assert.Contains("2 produções", warning.Message);
        Assert.Contains("PN-1", warning.Message);
        Assert.Contains("PN-2", warning.Message);
        Assert.DoesNotContain("PN-3", warning.Message);
        Assert.Contains("15/04/2026–16/04/2026", warning.Message);
        Assert.Contains("18/04/2026–20/04/2026", warning.Message);
        Assert.Contains("Ajusta o início da nova para depois de 25/04/2026", warning.Message);
    }
}
