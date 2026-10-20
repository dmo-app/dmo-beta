using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O aviso de sobreposição lê na base só as produções que podem cruzar a
/// janela (varredura de 18/10/2026, C2). Para todas as combinações de
/// janelas, o aviso tem de ser o mesmo que a calculadora dá com todas as
/// produções da máquina.
/// </summary>
public sealed class JobOnOverlapQueryTests
{
    [Fact]
    public async Task EveryWindow_GivesTheSameWarningAsComparingEveryProduction_InMemory()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new DmoDbContext(options);
        await JobOnOverlapScenario.AssertSameAsCalculatorAsync(db, "OVL");
    }
}

public static class JobOnOverlapScenario
{
    private static readonly DateOnly?[] Days =
    [
        null,
        new DateOnly(2026, 10, 1),
        new DateOnly(2026, 10, 5),
        new DateOnly(2026, 10, 10),
        new DateOnly(2026, 10, 15)
    ];

    private static IEnumerable<(DateOnly? Inicio, DateOnly? Fim)> Windows() =>
        from inicio in Days
        from fim in Days
        where inicio is null || fim is null || inicio <= fim
        select (inicio, fim);

    /// <summary>
    /// Semeia uma máquina com uma produção por janela possível (mais uma
    /// noutra máquina) e compara, janela a janela, o aviso da consulta com
    /// o da calculadora sobre todas as produções da máquina. Usado também
    /// contra o PostgreSQL.
    /// </summary>
    public static async Task AssertSameAsCalculatorAsync(DmoDbContext db, string prefix)
    {
        var machine = new Machine { Code = prefix + "-M1" };
        var other = new Machine { Code = prefix + "-M2" };
        db.Machines.AddRange(machine, other);
        var number = 0;
        foreach (var (inicio, fim) in Windows())
        {
            db.JobOns.Add(new JobOn { Reference = prefix + "REF", ProductionNumber = $"{++number}", Machine = machine, InicioEm = inicio, FimEm = fim });
        }

        db.JobOns.Add(new JobOn { Reference = prefix + "REF", ProductionNumber = "OUTRA", Machine = other, InicioEm = Days[1], FimEm = Days[4] });
        await db.SaveChangesAsync();

        var all = await db.JobOns.AsNoTracking()
            .Where(j => j.MachineId == machine.Id)
            .OrderBy(j => j.Id)
            .Select(j => new OverlapCandidate(j.Id, j.Reference, j.ProductionNumber, j.InicioEm, j.FimEm))
            .ToListAsync();
        var self = all[0];
        var others = all.Where(c => c.JobOnId != self.JobOnId).ToList();

        foreach (var (inicio, fim) in Windows())
        {
            var expected = JobOnOverlap.Evaluate(inicio, fim, machine.Code, others);
            var actual = await JobOnOverlapQuery.EvaluateAsync(db, self.JobOnId, machine.Id, machine.Code, inicio, fim, CancellationToken.None);
            Assert.Equal(expected?.Message, actual?.Message);
            Assert.Equal(
                (expected?.Conflicts ?? []).Select(c => c.JobOnId).Order(),
                (actual?.Conflicts ?? []).Select(c => c.JobOnId).Order());
        }
    }
}
