using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// As MUDANÇAS DE LOTE durante a produção (o «Duplicar → Lote»; o
/// proprietário, 16/10/2026: «não mudava a produção, deixava apenas uma
/// nota»). Registar grava só a nota: o Job On, os contextos CM/MF/BQ e o
/// Controlo ficam como estão. O lote novo tem de ser um lote registado no
/// Ferramentas com a mesma referência e tipo de uma ferramenta da produção,
/// para a nota ficar ligada ao registo e não ser só texto.
/// </summary>
public sealed class JobOnLotNoteHandler
{
    private readonly DmoDbContext _db;

    public JobOnLotNoteHandler(DmoDbContext db)
    {
        _db = db;
    }

    private sealed record SlotTool(string Slot, string ToolId, string Reference, string Lot);

    /// <summary>As ferramentas que a produção tem, por slot (a do contexto, sem as mudanças).</summary>
    private async Task<IReadOnlyList<SlotTool>> ContextToolsAsync(int jobOnId, CancellationToken cancellationToken)
    {
        var ids = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new
            {
                Cm = j.CmContext != null ? j.CmContext.ToolId : null,
                Mf = j.MfContext != null ? j.MfContext.ToolId : null,
                Bq = j.BqContext != null ? j.BqContext.ToolId : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (ids is null)
        {
            return Array.Empty<SlotTool>();
        }

        var bySlot = new[] { ("CM", ids.Cm), ("MF", ids.Mf), ("BQ", ids.Bq) }
            .Where(x => x.Item2 is not null)
            .ToList();
        var toolIds = bySlot.Select(x => x.Item2!).ToList();
        var tools = await _db.Tools
            .AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.Reference, t.Lot })
            .ToListAsync(cancellationToken);
        return bySlot
            .Select(x => tools.FirstOrDefault(t => t.ToolId == x.Item2) is { } t
                ? new SlotTool(x.Item1, t.ToolId, t.Reference, t.Lot)
                : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    /// <summary>O lote em uso num dia: a última mudança até esse dia, ou o lote do contexto.</summary>
    private async Task<(string ToolId, string Lot)> InForceAsync(
        int jobOnId, SlotTool context, DateOnly dia, CancellationToken cancellationToken)
    {
        var last = await _db.JobOnLotNotes
            .AsNoTracking()
            .Where(n => n.JobOnId == jobOnId && n.Slot == context.Slot && n.Dia <= dia)
            .OrderByDescending(n => n.Dia)
            .ThenByDescending(n => n.Id)
            .Select(n => new { n.NewToolId, n.NewLotFrozen })
            .FirstOrDefaultAsync(cancellationToken);
        return last is null ? (context.ToolId, context.Lot) : (last.NewToolId, last.NewLotFrozen);
    }

    /// <summary>
    /// Os lotes que a produção pode passar a usar no dia: os outros lotes
    /// registados de cada ferramenta dela (o que está em uso nesse dia fica
    /// de fora), por CM, MF e BQ.
    /// </summary>
    public async Task<IReadOnlyList<LotCandidate>> CandidatesAsync(
        int jobOnId, DateOnly dia, CancellationToken cancellationToken = default)
    {
        var result = new List<LotCandidate>();
        foreach (var context in await ContextToolsAsync(jobOnId, cancellationToken))
        {
            var type = SlotType(context.Slot);
            var inForce = await InForceAsync(jobOnId, context, dia, cancellationToken);
            var lots = await _db.Tools
                .AsNoTracking()
                .Where(t => t.Type == type
                    && t.Reference == context.Reference
                    && t.ToolId != ToolRegistryConstants.RemovedToolId
                    && t.ToolId != inForce.ToolId)
                .OrderBy(t => t.Lot)
                .Select(t => new LotCandidate(context.Slot, t.ToolId, t.Reference, t.Lot))
                .ToListAsync(cancellationToken);
            result.AddRange(lots);
        }

        return result;
    }

    /// <summary>
    /// Regista a mudança de lote. Recusas tipadas antes de gravar; nada na
    /// produção muda.
    /// </summary>
    public async Task<AddLotNoteResult> AddAsync(
        int jobOnId,
        DateOnly? dia,
        string? newToolId,
        string? note,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var jobOn = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new { j.InicioEm, j.FimEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (jobOn is null)
        {
            return AddLotNoteResult.JobOnNotFound;
        }

        if (dia is not { } day)
        {
            return AddLotNoteResult.MissingDay;
        }

        if ((jobOn.InicioEm is { } inicio && day < inicio) || (jobOn.FimEm is { } fim && day > fim))
        {
            return AddLotNoteResult.DayOutsideProduction;
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > JobOnLotNote.NoteMaxLength })
        {
            return AddLotNoteResult.NoteTooLong;
        }

        var newTool = newToolId is { Length: > 0 }
            ? await _db.Tools
                .AsNoTracking()
                .Where(t => t.ToolId == newToolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
                .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var context = newTool is null
            ? null
            : (await ContextToolsAsync(jobOnId, cancellationToken))
                .FirstOrDefault(c => SlotType(c.Slot) == newTool.Type && c.Reference == newTool.Reference);
        if (newTool is null || context is null)
        {
            return AddLotNoteResult.LotNotFound;
        }

        var inForce = await InForceAsync(jobOnId, context, day, cancellationToken);
        if (inForce.ToolId == newTool.ToolId)
        {
            return AddLotNoteResult.SameLot;
        }

        _db.JobOnLotNotes.Add(new JobOnLotNote
        {
            JobOnId = jobOnId,
            Dia = day,
            Slot = context.Slot,
            NewToolId = newTool.ToolId,
            ToolReferenceFrozen = newTool.Reference,
            PreviousLotFrozen = inForce.Lot,
            NewLotFrozen = newTool.Lot,
            Note = trimmedNote,
            CreatedBy = createdBy is { Length: > 254 } ? createdBy[..254] : createdBy,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return AddLotNoteResult.Success;
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
            .Select(n => new NextLotChange(n.JobOnId, n.Dia, n.Slot, n.PreviousLotFrozen, n.NewLotFrozen))
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

    private static ToolType SlotType(string slot) => slot switch
    {
        "CM" => ToolType.Cm,
        "MF" => ToolType.Mf,
        _ => ToolType.Bq
    };
}
