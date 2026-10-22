using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// As MUDANÇAS DE LOTE durante a produção (o antigo «Duplicar → Lote»; o
/// proprietário, 16/10/2026: «não mudava a produção, deixava apenas uma
/// nota»). As notas já não se criam: só se leem as que ficaram gravadas,
/// para a ficha do Job On e as produções do dia.
/// </summary>
public sealed class JobOnLotNoteHandler
{
    private readonly DmoDbContext _db;

    public JobOnLotNoteHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>As mudanças de lote de um dia, das produções indicadas, pela ordem em que foram registadas.</summary>
    public async Task<IReadOnlyList<LotNoteView>> ForDayAsync(
        DateOnly dia, IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        if (jobOnIds.Count == 0)
        {
            return Array.Empty<LotNoteView>();
        }

        return await ListAsync(_db.JobOnLotNotes.Where(n => n.Dia == dia && jobOnIds.Contains(n.JobOnId)), cancellationToken);
    }

    /// <summary>A primeira troca de lote marcada depois do dia, por produção (só das indicadas).</summary>
    public async Task<IReadOnlyList<NextLotChange>> NextAfterAsync(
        DateOnly dia, IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        if (jobOnIds.Count == 0)
        {
            return Array.Empty<NextLotChange>();
        }

        var rows = await _db.JobOnLotNotes
            .AsNoTracking()
            .Where(n => n.Dia > dia && jobOnIds.Contains(n.JobOnId))
            .OrderBy(n => n.Dia)
            .ThenBy(n => n.Id)
            .Select(n => new NextLotChange(n.JobOnId, n.Dia, n.Slot, n.PreviousLotFrozen, n.NewLotFrozen, n.NewToolId))
            .ToListAsync(cancellationToken);
        return rows.GroupBy(n => n.JobOnId).Select(g => g.First()).ToList();
    }

    /// <summary>Todas as mudanças de lote de uma produção, por dia.</summary>
    public Task<IReadOnlyList<LotNoteView>> ForJobOnAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        ListAsync(_db.JobOnLotNotes.Where(n => n.JobOnId == jobOnId), cancellationToken);

    private static async Task<IReadOnlyList<LotNoteView>> ListAsync(
        IQueryable<JobOnLotNote> notes, CancellationToken cancellationToken)
    {
        var rows = await notes
            .AsNoTracking()
            .OrderBy(n => n.Dia)
            .ThenBy(n => n.Id)
            .Select(n => new { n.JobOnId, n.Dia, n.Slot, n.PreviousLotFrozen, n.NewLotFrozen, n.Note, n.CreatedBy })
            .ToListAsync(cancellationToken);
        return rows
            .Select(n => new LotNoteView(
                n.JobOnId, n.Dia, n.Slot,
                JobOnLotNote.Describe(n.Slot, n.PreviousLotFrozen, n.NewLotFrozen, n.Note),
                n.CreatedBy))
            .ToList();
    }
}
