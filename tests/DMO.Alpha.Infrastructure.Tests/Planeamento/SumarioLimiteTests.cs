using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Planeamento;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Planeamento;

/// <summary>
/// O Sumário de um mês com mais de 500 linhas de um tipo (relatório do
/// dmo-Improvements de 28/10/2026, S07): 500 produções a começar no dia 1
/// não escondem a que começa no dia 24, nem a marca dela no calendário.
/// </summary>
public sealed class SumarioLimiteTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly SumarioQuery _query;

    public SumarioLimiteTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new SumarioQuery(_db, new ControloRevisaoQuery(_db));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task QuinhentasProducoesNoDia1_NaoEscondemADoDia24()
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        var dia1 = new DateOnly(2026, 10, 1);
        var dia24 = new DateOnly(2026, 10, 24);
        for (var i = 0; i < 500; i++)
        {
            _db.JobOns.Add(new JobOn { Reference = "5447T173", ProductionNumber = $"2026{i:D4}", Machine = machine, InicioEm = dia1 });
        }

        var ultima = new JobOn { Reference = "5447T173", ProductionNumber = "2026999", Machine = machine, InicioEm = dia24 };
        _db.JobOns.Add(ultima);
        await _db.SaveChangesAsync();

        var items = await _query.PeriodAsync(dia1, dia1.AddMonths(1), [SumarioTipo.ProducaoComeca]);

        Assert.Equal(501, items.Count);
        Assert.Equal(ultima.Id, Assert.Single(items, i => i.Dia == dia24).JobOnId);
        Assert.Equal(500, items.Count(i => i.Dia == dia1));
    }

    [Fact]
    public async Task PoucasLinhas_UmaLeituraComoAntes()
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        var dia = new DateOnly(2026, 10, 5);
        _db.JobOns.Add(new JobOn { Reference = "5447T173", ProductionNumber = "2026001", Machine = machine, InicioEm = dia });
        _db.JobOns.Add(new JobOn { Reference = "5447T173", ProductionNumber = "2026002", Machine = machine, InicioEm = dia.AddDays(3) });
        await _db.SaveChangesAsync();

        var items = await _query.PeriodAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), [SumarioTipo.ProducaoComeca, SumarioTipo.JobOnCriado, SumarioTipo.JobOnPorVer]);

        Assert.Equal(2, items.Count(i => i.Tipo == SumarioTipo.ProducaoComeca));
        Assert.Equal(2, items.Count(i => i.Tipo == SumarioTipo.JobOnPorVer));
        Assert.Equal([dia.AddDays(3), dia.AddDays(3), dia, dia], items.Select(i => i.Dia));
    }
}
