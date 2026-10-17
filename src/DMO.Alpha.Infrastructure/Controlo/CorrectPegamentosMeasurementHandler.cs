using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

public sealed class CorrectPegamentosMeasurementHandler : ICorrectPegamentosMeasurementHandler
{
    private readonly DmoDbContext _db;

    public CorrectPegamentosMeasurementHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<CorrectPegamentosMeasurementResult> EditAsync(
        EditPegamentosMeasurement command,
        CancellationToken cancellationToken = default)
    {
        var measurement = await FindAsync(command.Anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPegamentosMeasurementResult.MeasurementNotFound;
        }

        measurement.CosturaMm = command.CosturaMm;
        measurement.ContraCosturaMm = command.ContraCosturaMm;

        // O nominal congelado na leitura não muda. Uma leitura que ficou sem
        // nominal passa a usar o da ferramenta, se entretanto foi preenchido.
        if (measurement.NominalGargaloMm is null)
        {
            var toolId = measurement.Component switch
            {
                PegamentosComponent.Cm => measurement.Pegamentos.CmToolId,
                PegamentosComponent.Bq => measurement.Pegamentos.BqToolId,
                _ => measurement.Pegamentos.MfToolId
            };
            measurement.NominalGargaloMm = await _db.ToolTechnicalContexts
                .AsNoTracking()
                .Where(t => t.ToolId == toolId)
                .Select(t => t.DiametroGargaloMm)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CorrectPegamentosMeasurementResult.Success;
    }

    public async Task<CorrectPegamentosMeasurementResult> DeleteAsync(
        PegamentosMeasurementAnchor anchor,
        CancellationToken cancellationToken = default)
    {
        var measurement = await FindAsync(anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPegamentosMeasurementResult.MeasurementNotFound;
        }

        var pegamentosId = measurement.PegamentosId;
        _db.PegamentosMeasurements.Remove(measurement);

        var remaining = await _db.PegamentosMeasurements
            .Where(m => m.PegamentosId == pegamentosId && m.Id != measurement.Id)
            .OrderBy(m => m.RowPosition)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);
        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].RowPosition = i + 1;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CorrectPegamentosMeasurementResult.Success;
    }

    private Task<PegamentosMeasurement?> FindAsync(PegamentosMeasurementAnchor anchor, CancellationToken cancellationToken) =>
        _db.PegamentosMeasurements
            .Include(m => m.Pegamentos)
            .Where(m => m.Id == anchor.MeasurementId)
            .Where(m => anchor.PegamentosId != null
                ? m.PegamentosId == anchor.PegamentosId
                : m.Pegamentos.ControloId == anchor.ControloId)
            .FirstOrDefaultAsync(cancellationToken);
}
