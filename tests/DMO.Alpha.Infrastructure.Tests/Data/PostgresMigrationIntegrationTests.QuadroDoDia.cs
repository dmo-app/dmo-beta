using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // The day board moved out of JobOn/Index.cshtml.cs keeps the reads it made
    // when the page composed it: lot notes of the day, changes after start and,
    // with a day selected, the next production per machine and the next lot
    // change. The window of productions is loaded by the page beforehand.
    [SkippableTheory]
    [InlineData(true, 4)]
    [InlineData(false, 2)]
    public async Task JobOnQuadroDoDia_CadaPedidoFazSoAsConsultasDeLeitura(bool diaEscolhido, int consultas)
    {
        SkipWithoutConnection();
        var commands = new PesoFolhaCommands();
        await using var db = CreateContextWith(commands);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (machines, janela, dia) = await SemearQuadroDoDiaAsync(db);

        var query = new JobOnQuadroDoDiaQuery(
            new JobOnListQuery(db), new JobOnLotNoteHandler(db), new JobOnChangeQuery(db));
        commands.Reading = true;
        var rows = await query.QuadroAsync(machines, janela, dia.Year, dia.Month, dia.Day, diaEscolhido);
        commands.Reading = false;

        Assert.Equal(consultas, commands.Reads);
        var linha = Assert.Single(rows);
        var producao = Assert.Single(linha.Productions);
        Assert.Single(producao.LotNotes);
        Assert.Equal(1, producao.ChangeCount);
        Assert.Equal(diaEscolhido, linha.Next is not null);
        Assert.Empty(db.ChangeTracker.Entries());
        // Disposing the transaction rolls back this test's fixture only.
    }

    // One machine, one production covering the day with a lot note that day,
    // a change after its start, a lot swap after the day and the next
    // production on the same line.
    private static async Task<(IReadOnlyList<MachineOption> Machines, IReadOnlyList<ProductionContextReadModel> Janela, DateOnly Dia)>
        SemearQuadroDoDiaAsync(DMO.Alpha.Infrastructure.Data.DmoDbContext db)
    {
        var marker = "QD-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var dia = new DateOnly(2026, 10, 20);
        var machine = new Machine { Code = marker };
        var atual = new JobOn
        {
            Machine = machine, Reference = marker, ProductionNumber = "1",
            InicioEm = new DateOnly(2026, 10, 10), FimEm = null
        };
        var seguinte = new JobOn
        {
            Machine = machine, Reference = marker + "-N", ProductionNumber = "2",
            InicioEm = new DateOnly(2026, 10, 28), FimEm = new DateOnly(2026, 11, 5)
        };
        db.JobOns.AddRange(atual, seguinte);
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-a", Type = ToolType.Bq, Reference = "T1", Lot = "7" },
            new Tool { ToolId = marker + "-b", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        await db.SaveChangesAsync();
        db.JobOnLotNotes.AddRange(
            new JobOnLotNote
            {
                JobOnId = atual.Id, Dia = dia, Slot = "BQ", NewToolId = marker + "-a", ToolReferenceFrozen = "T1",
                PreviousLotFrozen = "6", NewLotFrozen = "7"
            },
            new JobOnLotNote
            {
                JobOnId = atual.Id, Dia = dia.AddDays(3), Slot = "CM", NewToolId = marker + "-b", ToolReferenceFrozen = "5447",
                PreviousLotFrozen = "1", NewLotFrozen = "2"
            });
        db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = atual.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "9", NewValueFrozen = "10",
            ChangedAtUtc = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        var janela = await new JobOnListQuery(db).ListWindowAsync(dia.AddDays(-10), dia.AddDays(10));
        db.ChangeTracker.Clear();
        return ([new MachineOption(machine.Id, marker)], janela.Where(j => j.Machine == marker).ToList(), dia);
    }
}
