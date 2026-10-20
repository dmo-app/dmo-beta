using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// As mudanças de lote durante a produção (o antigo «Duplicar → Lote»; o
/// proprietário, 16/10/2026): as notas já não se criam, mas as que ficaram
/// gravadas continuam a aparecer na ficha do Job On e nas produções do dia.
/// </summary>
public sealed class JobOnLotNoteHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly JobOnLotNoteHandler _handler;
    private readonly int _jobOnId;
    private readonly int _outraProducaoId;

    public JobOnLotNoteHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new JobOnLotNoteHandler(_db);

        var machine = new Machine { Code = "C2" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-6", Type = ToolType.Bq, Reference = "Y232", Lot = "6" },
            new Tool { ToolId = "BQ-7", Type = ToolType.Bq, Reference = "Y232", Lot = "7" },
            new Tool { ToolId = "BQ-8", Type = ToolType.Bq, Reference = "Y232", Lot = "8" });
        var jobOn = new JobOn
        {
            Reference = "6018Y232",
            ProductionNumber = "202616",
            Machine = machine,
            InicioEm = new DateOnly(2026, 9, 9),
            FimEm = new DateOnly(2026, 12, 31)
        };
        var outra = new JobOn
        {
            Reference = "6018Y232",
            ProductionNumber = "202617",
            Machine = machine,
            InicioEm = new DateOnly(2026, 9, 9),
            FimEm = new DateOnly(2026, 12, 31)
        };
        _db.JobOns.AddRange(jobOn, outra);
        _db.SaveChanges();
        _jobOnId = jobOn.Id;
        _outraProducaoId = outra.Id;

        // As notas gravadas antes de se deixarem de criar: a BQ passou do
        // lote 6 ao 7 a 16/10 e do 7 ao 8 a 20/10; a outra produção tem uma
        // nota no mesmo dia 16/10.
        _db.JobOnLotNotes.AddRange(
            Nota(_jobOnId, new DateOnly(2026, 10, 20), "BQ-8", "7", "8", null),
            Nota(_jobOnId, new DateOnly(2026, 10, 16), "BQ-7", "6", "7", "desgaste"),
            Nota(_outraProducaoId, new DateOnly(2026, 10, 16), "BQ-8", "6", "8", null));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose() => _db.Dispose();

    private static JobOnLotNote Nota(int jobOnId, DateOnly dia, string toolId, string anterior, string novo, string? texto) => new()
    {
        JobOnId = jobOnId,
        Dia = dia,
        Slot = "BQ",
        NewToolId = toolId,
        ToolReferenceFrozen = "Y232",
        PreviousLotFrozen = anterior,
        NewLotFrozen = novo,
        Note = texto,
        CreatedBy = "4591",
        CreatedAtUtc = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task ForDay_SoAsNotasDoDiaDasProducoesIndicadas_ComOTextoDaMudanca()
    {
        var day = Assert.Single(await _handler.ForDayAsync(new DateOnly(2026, 10, 16), [_jobOnId]));
        Assert.Equal("Muda BQ lote 6 para 7 · desgaste", day.Text);
        Assert.Equal("4591", day.CreatedBy);

        Assert.Empty(await _handler.ForDayAsync(new DateOnly(2026, 10, 17), [_jobOnId]));
        Assert.Equal(2, (await _handler.ForDayAsync(new DateOnly(2026, 10, 16), [_jobOnId, _outraProducaoId])).Count);
        Assert.Empty(await _handler.ForDayAsync(new DateOnly(2026, 10, 16), []));
    }

    [Fact]
    public async Task ForJobOn_TodasAsNotasDaProducao_PorDia()
    {
        var notes = await _handler.ForJobOnAsync(_jobOnId);

        Assert.Equal(["Muda BQ lote 6 para 7 · desgaste", "Muda BQ lote 7 para 8"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task NextAfter_APrimeiraTrocaDepoisDoDia()
    {
        var next = Assert.Single(await _handler.NextAfterAsync(new DateOnly(2026, 10, 16), [_jobOnId]));
        Assert.Equal((new DateOnly(2026, 10, 20), "BQ lote 7 para 8"), (next.Dia, next.Texto));
        Assert.Equal("BQ lote 6 para 7", Assert.Single(await _handler.NextAfterAsync(new DateOnly(2026, 10, 15), [_jobOnId])).Texto);
        Assert.Empty(await _handler.NextAfterAsync(new DateOnly(2026, 10, 20), [_jobOnId]));
    }
}
