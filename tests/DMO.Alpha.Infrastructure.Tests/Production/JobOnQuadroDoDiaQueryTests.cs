using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The Job On day board that used to be composed inside the page: same rows,
/// same order, same next change, and nothing is written.
/// </summary>
public sealed class JobOnQuadroDoDiaQueryTests : IDisposable
{
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly JobOnQuadroDoDiaQuery _query;

    public JobOnQuadroDoDiaQueryTests() =>
        _query = new JobOnQuadroDoDiaQuery(new JobOnListQuery(_db), new JobOnLotNoteHandler(_db), new JobOnChangeQuery(_db));

    public void Dispose() => _db.Dispose();

    private static JobOn Producao(Machine machine, string reference, string numero, DateOnly? inicio, DateOnly? fim) =>
        new() { Machine = machine, Reference = reference, ProductionNumber = numero, InicioEm = inicio, FimEm = fim };

    private async Task<IReadOnlyList<DayMachineRow>> QuadroAsync(bool diaEscolhido = true)
    {
        var maquinas = await _db.Machines.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => new MachineOption(m.Id, m.Code)).ToListAsync();
        var janela = await new JobOnListQuery(_db).ListWindowAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31));
        return await _query.QuadroAsync(maquinas, janela, Dia.Year, Dia.Month, Dia.Day, diaEscolhido);
    }

    private void NotaDeLote(JobOn producao, DateOnly dia, string slot, string de, string para, string toolId) =>
        _db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOnId = producao.Id, Dia = dia, Slot = slot, NewToolId = toolId, ToolReferenceFrozen = "T1",
            PreviousLotFrozen = de, NewLotFrozen = para
        });

    private async Task<(Machine B1, Machine B2)> DuasMaquinasAsync()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        _db.Machines.AddRange(b1, b2);
        await _db.SaveChangesAsync();
        return (b1, b2);
    }

    [Fact]
    public async Task UmaLinhaPorMaquina_NaOrdemDasMaquinas_ComAsProducoesDoDiaPorReferencia()
    {
        var (b1, b2) = await DuasMaquinasAsync();
        var b3 = new Machine { Code = "B3" };
        _db.Machines.Add(b3);
        _db.JobOns.AddRange(
            Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30)),
            Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 25)),
            Producao(b1, "1111A111", "3", new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 30)),
            Producao(b2, "2222B222", "4", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 19)));
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        Assert.Equal(["B1", "B2", "B3"], linhas.Select(l => l.MachineCode));
        Assert.Equal(["5447T173", "6018Y232"], linhas[0].Productions.Select(p => p.Reference));
        Assert.False(linhas[1].HasProductions);
        Assert.False(linhas[2].HasProductions);
    }

    [Fact]
    public async Task ProducaoAteAviso_CobreOsDiasDepoisDoInicio_MasNaoOsAntes()
    {
        var (b1, _) = await DuasMaquinasAsync();
        _db.JobOns.AddRange(
            Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null),
            Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 20), null),
            Producao(b1, "7777N777", "3", new DateOnly(2026, 10, 21), null));
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        Assert.Equal(["5447T173", "6018Y232"], linhas[0].Productions.Select(p => p.Reference));
    }

    [Fact]
    public async Task ProducaoCobreOPrimeiroEOUltimoDia()
    {
        var (b1, b2) = await DuasMaquinasAsync();
        _db.JobOns.AddRange(
            Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), Dia),
            Producao(b2, "6018Y232", "2", Dia, new DateOnly(2026, 10, 30)));
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        Assert.Equal("5447T173", Assert.Single(linhas[0].Productions).Reference);
        Assert.Equal("6018Y232", Assert.Single(linhas[1].Productions).Reference);
    }

    [Fact]
    public async Task DuasProducoesDaMesmaLinhaQueSeCruzam_NaoMarcamSobreposicao()
    {
        var (b1, _) = await DuasMaquinasAsync();
        _db.JobOns.AddRange(
            Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 25)),
            Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 30)));
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        // Diogo, 25/10/2026: «so se aplica as bqs»; as duas cobrem o dia e a
        // linha não fica marcada.
        Assert.Equal(2, linhas[0].Productions.Count);
        Assert.All(linhas, l => Assert.False(l.HasOverlap));
    }

    [Fact]
    public async Task NotasDeLoteDoDiaEResumoDasAlteracoes_ficamNaProducao()
    {
        var (b1, _) = await DuasMaquinasAsync();
        var com = Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null);
        var sem = Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 12), null);
        _db.JobOns.AddRange(com, sem);
        _db.Tools.Add(new Tool { ToolId = "BQ-7", Type = ToolType.Bq, Reference = "T1", Lot = "7" });
        await _db.SaveChangesAsync();
        NotaDeLote(com, Dia, "BQ", "6", "7", "BQ-7");
        NotaDeLote(com, Dia.AddDays(1), "BQ", "7", "8", "BQ-7");
        _db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = com.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "9", NewValueFrozen = "10",
            ChangedAtUtc = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        var daCom = linhas[0].Productions.Single(p => p.JobOnId == com.Id);
        var daSem = linhas[0].Productions.Single(p => p.JobOnId == sem.Id);
        Assert.Equal(["Muda BQ lote 6 para 7"], daCom.LotNotes);
        Assert.Equal(1, daCom.ChangeCount);
        Assert.False(string.IsNullOrWhiteSpace(daCom.ChangeSummary));
        Assert.Single(daCom.ChangeDetails);
        Assert.Empty(daSem.LotNotes);
        Assert.Equal(0, daSem.ChangeCount);
        Assert.Null(daSem.ChangeSummary);
    }

    [Fact]
    public async Task ProximaMudanca_EscolheAMaisCedoEntreAProducaoSeguinteEATrocaDeComponente()
    {
        var (b1, b2) = await DuasMaquinasAsync();
        var atual1 = Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null);
        var seguinte1 = Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 5));
        var atual2 = Producao(b2, "1199C142", "3", new DateOnly(2026, 10, 1), null);
        var seguinte2 = Producao(b2, "7777N777", "4", new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 5));
        _db.JobOns.AddRange(atual1, seguinte1, atual2, seguinte2);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-2", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = "BQ-9", Type = ToolType.Bq, Reference = "T1", Lot = "9" });
        await _db.SaveChangesAsync();
        // B1: a troca (24/10) vem antes da produção seguinte (28/10).
        NotaDeLote(atual1, new DateOnly(2026, 10, 24), "CM", "1", "2", "CM-2");
        // B2: a troca e a produção seguinte no mesmo dia (25/10): a produção nova.
        NotaDeLote(atual2, new DateOnly(2026, 10, 25), "BQ", "8", "9", "BQ-9");
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        var daB1 = linhas[0].Next!;
        Assert.False(daB1.EntraProducao);
        Assert.Equal(new DateOnly(2026, 10, 24), daB1.Dia);
        Assert.Equal("CM lote 1 para 2", daB1.Texto);
        Assert.Equal("CM-2", daB1.ToolId);
        var daB2 = linhas[1].Next!;
        Assert.True(daB2.EntraProducao);
        Assert.Equal(new DateOnly(2026, 10, 25), daB2.Dia);
        Assert.Equal("7777N777 4", daB2.Texto);
        Assert.Equal(seguinte2.Id, daB2.JobOnId);
    }

    [Fact]
    public async Task ProximaMudanca_ASeguinteMaisCedoQueATroca_GanhaAProducao()
    {
        var (b1, _) = await DuasMaquinasAsync();
        var atual = Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null);
        var seguinte = Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 22), new DateOnly(2026, 11, 5));
        _db.JobOns.AddRange(atual, seguinte);
        _db.Tools.Add(new Tool { ToolId = "CM-2", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        await _db.SaveChangesAsync();
        NotaDeLote(atual, new DateOnly(2026, 10, 27), "CM", "1", "2", "CM-2");
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync();

        Assert.True(linhas[0].Next!.EntraProducao);
        Assert.Equal(new DateOnly(2026, 10, 22), linhas[0].Next!.Dia);
    }

    [Fact]
    public async Task SemDiaEscolhido_NaoHaProximaMudanca_MasHaProducoesDoDiaInicial()
    {
        var (b1, _) = await DuasMaquinasAsync();
        _db.JobOns.AddRange(
            Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null),
            Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 5)));
        await _db.SaveChangesAsync();

        var linhas = await QuadroAsync(diaEscolhido: false);

        Assert.Single(linhas[0].Productions);
        Assert.All(linhas, l => Assert.Null(l.Next));
    }

    [Fact]
    public async Task OPedido_NaoGravaNada()
    {
        var (b1, _) = await DuasMaquinasAsync();
        var atual = Producao(b1, "5447T173", "1", new DateOnly(2026, 10, 10), null);
        _db.JobOns.AddRange(atual, Producao(b1, "6018Y232", "2", new DateOnly(2026, 10, 28), null));
        _db.Tools.Add(new Tool { ToolId = "CM-2", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        await _db.SaveChangesAsync();
        NotaDeLote(atual, Dia, "CM", "1", "2", "CM-2");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await QuadroAsync();
        await QuadroAsync(diaEscolhido: false);

        Assert.Empty(_db.ChangeTracker.Entries());
    }
}
