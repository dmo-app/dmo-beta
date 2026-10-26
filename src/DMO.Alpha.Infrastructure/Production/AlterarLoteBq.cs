using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// O «Alterar lote» do Registo das Boquilhas: a troca do Editar do Job On
/// (<see cref="BqContextTroca"/>), só para a BQ, com a mesma linha em «O que
/// mudou» e o mesmo cadeado da produção.
/// </summary>
public sealed class AlterarLoteBq : IAlterarLoteBq
{
    private readonly DmoDbContext _db;

    public AlterarLoteBq(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<LotesDaBq?> LotesAsync(int bqContextId, CancellationToken cancellationToken = default)
    {
        // Uma só consulta (o Registo abre com poucas): o lote do bq_id e todas
        // as BQ da mesma referência, ele incluído.
        var linhas = await (
                from c in _db.BqContexts.AsNoTracking()
                join t in _db.Tools.AsNoTracking() on c.ToolId equals t.ToolId
                where c.Id == bqContextId && c.JobOnId != null
                from o in _db.Tools.AsNoTracking()
                where o.Type == ToolType.Bq && o.Reference.ToUpper() == t.Reference.ToUpper()
                select new { JobOnId = c.JobOnId!.Value, t.ToolId, t.Reference, t.Lot, OutroId = o.ToolId, OutroLot = o.Lot })
            .ToListAsync(cancellationToken);
        if (linhas.Count == 0)
        {
            return null;
        }

        var atual = linhas[0];
        var outros = linhas
            .Where(l => l.OutroId != atual.ToolId)
            .Select(l => new LoteDaBq(l.OutroId, l.OutroLot))
            // Lote 2 antes do lote 10.
            .OrderBy(l => LoteExibido.Mostrar(l.Lot).Length)
            .ThenBy(l => LoteExibido.Mostrar(l.Lot), StringComparer.Ordinal)
            .ToList();
        return new LotesDaBq(atual.JobOnId, atual.Reference, atual.Lot, outros);
    }

    public Task<AlterarLoteOutcome> AlterarAsync(AlterarLote command, CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            var jobOnId = await _db.BqContexts
                .Where(c => c.Id == command.BqContextId)
                .Select(c => c.JobOnId)
                .FirstOrDefaultAsync(cancellationToken);
            if (jobOnId is null)
            {
                return new AlterarLoteOutcome(AlterarLoteResultado.JaNaoEDaProducao);
            }

            // O mesmo cadeado do Editar do Job On: as duas trocas nunca se cruzam.
            await DbWrite.LockAsync(_db, $"jobon:editar:{jobOnId}", cancellationToken);
            var jobOn = await _db.JobOns
                .Include(j => j.BqContext)
                .FirstOrDefaultAsync(j => j.Id == jobOnId, cancellationToken);
            if (jobOn?.BqContext is not { } antes || antes.Id != command.BqContextId)
            {
                return new AlterarLoteOutcome(AlterarLoteResultado.JaNaoEDaProducao);
            }

            var ids = new[] { antes.ToolId, command.ToolId };
            var tools = await _db.Tools.AsNoTracking()
                .Where(t => ids.Contains(t.ToolId))
                .ToDictionaryAsync(t => t.ToolId, cancellationToken);
            if (!tools.TryGetValue(antes.ToolId, out var velha)
                || !tools.TryGetValue(command.ToolId, out var nova)
                || nova.Type != ToolType.Bq
                || nova.ToolId == velha.ToolId
                || !string.Equals(nova.Reference, velha.Reference, StringComparison.OrdinalIgnoreCase))
            {
                return new AlterarLoteOutcome(AlterarLoteResultado.LoteNaoServe);
            }

            var bq = await BqContextTroca.TrocarAsync(_db, jobOn, nova.ToolId, cancellationToken);
            _db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOn.Id,
                Field = JobOnChangeFields.Bq,
                OldValueFrozen = Truncate($"{velha.Reference} · lote {velha.Lot}", JobOnChange.ValueMaxLength),
                NewValueFrozen = Truncate($"{nova.Reference} · lote {nova.Lot}", JobOnChange.ValueMaxLength),
                ChangedByNameFrozen = Truncate(command.ChangedBy?.Trim(), 254),
                ChangedAtUtc = DateTime.UtcNow
            });

            // A ficha do Job On aberta por outra pessoa fica desatualizada.
            _db.Entry(jobOn).Property(j => j.MachineId).IsModified = true;
            await _db.SaveChangesAsync(cancellationToken);
            return new AlterarLoteOutcome(AlterarLoteResultado.Alterado, bq.Id, nova.Lot);
        }, cancellationToken);

    public async Task<IReadOnlyList<string>> LotesUsadosAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var usados = await (
                from c in _db.BqContexts.AsNoTracking()
                join t in _db.Tools.AsNoTracking() on c.ToolId equals t.ToolId
                where c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId
                select new { t.Lot, Atual = c.JobOnId == jobOnId, c.ReplacedAtUtc, c.Id })
            .ToListAsync(cancellationToken);
        // Os trocados pela ordem da troca; o atual no fim.
        return usados
            .OrderBy(u => u.Atual)
            .ThenBy(u => u.ReplacedAtUtc)
            .ThenBy(u => u.Id)
            .Select(u => u.Lot)
            .ToList();
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
