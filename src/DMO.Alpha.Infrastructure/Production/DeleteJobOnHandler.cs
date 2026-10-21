using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Elimina um Job On (Diogo, 19/10/2026, D2 / AV-JO-04) numa só transação,
/// com o mesmo trinco do Editar: uma edição ao mesmo tempo espera, e um
/// segundo envio (duplo clique) já não encontra a produção. Apaga tudo o que
/// é só desta produção, por ordem das chaves estrangeiras (as RESTRICT da
/// base não deixam apagar uma produção com controlo ou contextos trocados).
/// Os movimentos das Boquilhas dos bq_id da produção deixam rasto em
/// BqMovementDeletions, como no «Apagar» das Boquilhas. As ferramentas
/// ficam; um Peso de outra produção que a tinha como «produção anterior»
/// fica sem ela.
/// </summary>
public sealed class DeleteJobOnHandler : IDeleteJobOnHandler
{
    private readonly DmoDbContext _db;
    private readonly TimeProvider _clock;

    public DeleteJobOnHandler(DmoDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<JobOnDeletionPreview?> PreviewAsync(
        int jobOnId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var jobOn = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new { j.Reference, j.ProductionNumber, j.InicioEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (jobOn is null)
        {
            return null;
        }

        var controlo = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => new { c.Id, Revisto = c.ApprovedAtUtc != null || c.NotApprovedAtUtc != null })
            .FirstOrDefaultAsync(cancellationToken);
        var controloId = controlo?.Id;
        var bqIds = BqContextIdsOf(jobOnId);
        var traceIds = _db.BqRepairTraces.Where(t => t.BqContextId != null && bqIds.Contains(t.BqContextId.Value)).Select(t => t.Id);

        return new JobOnDeletionPreview(
            jobOnId,
            jobOn.Reference,
            jobOn.ProductionNumber,
            jobOn.InicioEm,
            jobOn.InicioEm is { } inicio && today >= inicio,
            controloId is null ? 0 : await _db.Pesos.CountAsync(p => p.ControloId == controloId, cancellationToken),
            controloId is null ? 0 : await _db.PesoComparacoes.CountAsync(c => c.ControloId == controloId, cancellationToken),
            controloId is null ? 0 : await _db.Pegamentos.CountAsync(p => p.ControloId == controloId, cancellationToken),
            controloId is null ? 0 : await _db.Puncoes.CountAsync(p => p.ControloId == controloId, cancellationToken),
            await _db.BqMovements.CountAsync(m => traceIds.Contains(m.BqRepairTraceId), cancellationToken),
            controlo is not null
                && (controlo.Revisto || await _db.ControloDecisoes.AnyAsync(d => d.ControloId == controloId, cancellationToken)));
    }

    public Task<DeleteJobOnResult> HandleAsync(
        DeleteJobOn command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"jobon:editar:{command.JobOnId}", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<DeleteJobOnResult> HandleLockedAsync(
        DeleteJobOn command,
        CancellationToken cancellationToken)
    {
        var jobOn = await _db.JobOns.FirstOrDefaultAsync(j => j.Id == command.JobOnId, cancellationToken);
        if (jobOn is null)
        {
            return DeleteJobOnResult.JobOnNotFound;
        }

        var jobOnId = jobOn.Id;

        // As Boquilhas: os registos dos bq_id da produção (o atual e os das
        // BQs trocadas), com o trinco de cada registo (o mesmo do registar,
        // corrigir e apagar), e os movimentos com rasto.
        var bqContexts = await _db.BqContexts
            .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
            .ToListAsync(cancellationToken);
        var bqIds = bqContexts.Select(c => c.Id).ToList();
        var traces = await _db.BqRepairTraces
            .Where(t => t.BqContextId != null && bqIds.Contains(t.BqContextId.Value))
            .ToListAsync(cancellationToken);
        if (_db.Database.IsRelational())
        {
            foreach (var trace in traces)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT 1 FROM "BqRepairTraces" WHERE "Id" = {trace.Id} FOR UPDATE""",
                    cancellationToken);
            }
        }

        var traceIds = traces.Select(t => t.Id).ToList();
        var movements = await _db.BqMovements
            .Where(m => traceIds.Contains(m.BqRepairTraceId))
            .ToListAsync(cancellationToken);
        var deletedAt = _clock.GetUtcNow().UtcDateTime;
        var deletedBy = BqActorName.Freeze(command.DeletedBy);
        foreach (var movement in movements)
        {
            _db.BqMovementDeletions.Add(new BqMovementDeletion
            {
                BqMovementId = movement.Id,
                BqRepairTraceId = movement.BqRepairTraceId,
                Type = movement.Type,
                Quantity = movement.Quantity,
                OccurredAtUtc = movement.OccurredAtUtc,
                MachineCodeFrozen = movement.MachineCodeFrozen,
                DeletedByNameFrozen = deletedBy,
                DeletedAtUtc = deletedAt
            });
        }

        _db.BqMovements.RemoveRange(movements);
        _db.BqRepairTraces.RemoveRange(traces);

        // O controlo da produção e o que é dele.
        var controlo = await _db.Controlos.FirstOrDefaultAsync(c => c.JobOnId == jobOnId, cancellationToken);
        if (controlo is not null)
        {
            var controloId = controlo.Id;
            var pesoIds = _db.Pesos.Where(p => p.ControloId == controloId).Select(p => p.Id);
            var comparacaoIds = _db.PesoComparacoes.Where(c => c.ControloId == controloId).Select(c => c.Id);
            var pegamentosIds = _db.Pegamentos.Where(p => p.ControloId == controloId).Select(p => p.Id);
            var puncoesIds = _db.Puncoes.Where(p => p.ControloId == controloId).Select(p => p.Id);

            // Um Peso de outra produção que tinha esta como anterior fica sem
            // ela (a base faz o mesmo: ON DELETE SET NULL).
            foreach (var peso in await _db.Pesos
                .Where(p => p.ProducaoAnteriorControloId == controloId && p.ControloId != controloId)
                .ToListAsync(cancellationToken))
            {
                peso.ProducaoAnteriorControloId = null;
            }

            _db.PesoComparacaoMeasurements.RemoveRange(
                await _db.PesoComparacaoMeasurements.Where(m => comparacaoIds.Contains(m.ComparacaoId)).ToListAsync(cancellationToken));
            _db.PesoComparacoes.RemoveRange(
                await _db.PesoComparacoes.Where(c => c.ControloId == controloId).ToListAsync(cancellationToken));
            _db.PesoMeasurements.RemoveRange(
                await _db.PesoMeasurements.Where(m => pesoIds.Contains(m.PesoId)).ToListAsync(cancellationToken));
            _db.Pesos.RemoveRange(
                await _db.Pesos.Where(p => p.ControloId == controloId).ToListAsync(cancellationToken));
            _db.PegamentosMeasurements.RemoveRange(
                await _db.PegamentosMeasurements.Where(m => pegamentosIds.Contains(m.PegamentosId)).ToListAsync(cancellationToken));
            _db.PegamentosMeasurementHistorico.RemoveRange(
                await _db.PegamentosMeasurementHistorico.Where(h => pegamentosIds.Contains(h.PegamentosId)).ToListAsync(cancellationToken));
            _db.Pegamentos.RemoveRange(
                await _db.Pegamentos.Where(p => p.ControloId == controloId).ToListAsync(cancellationToken));
            _db.PuncaoInspecoes.RemoveRange(
                await _db.PuncaoInspecoes.Where(i => puncoesIds.Contains(i.PuncoesId)).ToListAsync(cancellationToken));
            _db.Puncoes.RemoveRange(
                await _db.Puncoes.Where(p => p.ControloId == controloId).ToListAsync(cancellationToken));
            _db.ControloDecisoes.RemoveRange(
                await _db.ControloDecisoes.Where(d => d.ControloId == controloId).ToListAsync(cancellationToken));
            _db.ControloViewings.RemoveRange(
                await _db.ControloViewings.Where(v => v.ControloId == controloId).ToListAsync(cancellationToken));
            _db.ControloAreaStates.RemoveRange(
                await _db.ControloAreaStates.Where(s => s.ControloId == controloId).ToListAsync(cancellationToken));
            _db.Controlos.Remove(controlo);
        }

        // Os cm_id, mf_id e bq_id (os atuais e os das ferramentas trocadas), as
        // alterações registadas e as notas de lote.
        _db.CmContexts.RemoveRange(
            await _db.CmContexts.Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId).ToListAsync(cancellationToken));
        _db.MfContexts.RemoveRange(
            await _db.MfContexts.Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId).ToListAsync(cancellationToken));
        _db.BqContexts.RemoveRange(bqContexts);
        _db.JobOnChanges.RemoveRange(
            await _db.JobOnChanges.Where(c => c.JobOnId == jobOnId).ToListAsync(cancellationToken));
        _db.JobOnLotNotes.RemoveRange(
            await _db.JobOnLotNotes.Where(n => n.JobOnId == jobOnId).ToListAsync(cancellationToken));
        // A folha da fábrica (na base apagam em cascata; aqui também em memória).
        _db.JobOnFolhas.RemoveRange(
            await _db.JobOnFolhas.Where(f => f.JobOnId == jobOnId).ToListAsync(cancellationToken));
        _db.JobOnFolhaCalLinhas.RemoveRange(
            await _db.JobOnFolhaCalLinhas.Where(l => l.JobOnId == jobOnId).ToListAsync(cancellationToken));
        _db.JobOns.Remove(jobOn);

        await _db.SaveChangesAsync(cancellationToken);
        return DeleteJobOnResult.Success;
    }

    private IQueryable<int> BqContextIdsOf(int jobOnId) =>
        _db.BqContexts
            .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId)
            .Select(c => c.Id);
}
