using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// CM, BQ e MF por tool_id, com o contexto técnico e a densidade do vidro
/// do processo da CM, numa só consulta. Os mesmos valores que
/// <see cref="Tools.ToolLookupQuery"/>, <see cref="Tools.ToolTechnicalQuery"/>
/// e <see cref="GlassDensityConfiguration"/> devolvem um a um.
/// </summary>
public sealed class PesoToolFactsQuery
{
    private readonly DmoDbContext _db;

    public PesoToolFactsQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<PesoToolFacts> GetAsync(
        string? cmToolId,
        string? bqToolId,
        string? mfToolId,
        CancellationToken cancellationToken = default)
    {
        var ids = new[] { cmToolId, bqToolId, mfToolId }
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return new PesoToolFacts(null, null, null, null, null, null);
        }

        var rows = await (
            from t in _db.Tools.AsNoTracking()
            where ids.Contains(t.ToolId)
            join tc in _db.ToolTechnicalContexts.AsNoTracking() on t.ToolId equals tc.ToolId into technical
            from tc in technical.DefaultIfEmpty()
            select new
            {
                t.ToolId,
                t.Type,
                t.Reference,
                t.Lot,
                HasTechnical = tc != null,
                Process = tc != null ? tc.Process : null,
                NominalWeightGrams = tc != null ? tc.NominalWeightGrams : null,
                VolumePuncaoCm3 = tc != null ? tc.VolumePuncaoCm3 : null,
                VolumeMarisaCm3 = tc != null ? tc.VolumeMarisaCm3 : null,
                DiametroGargaloMm = tc != null ? tc.DiametroGargaloMm : null,
                TpDiametroMm = tc != null ? tc.TpDiametroMm : null,
                TpRaioMm = tc != null ? tc.TpRaioMm : null,
                TpSagittaMm = tc != null ? tc.TpSagittaMm : null,
                TpRaioBaseMm = tc != null ? tc.TpRaioBaseMm : null,
                TpRaioEsferaMm = tc != null ? tc.TpRaioEsferaMm : null,
                TpVolumeMm3 = tc != null ? tc.TpVolumeMm3 : null,
                GlassDensity = _db.GlassDensities
                    .Where(g => tc != null && tc.Process != null && g.Process == tc.Process)
                    .Select(g => (decimal?)g.DensityGramsPerCm3)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var byId = rows.ToDictionary(r => r.ToolId, StringComparer.Ordinal);

        ToolLookupResult? Tool(string? toolId) =>
            toolId is not null
            && toolId != ToolRegistryConstants.RemovedToolId
            && byId.TryGetValue(toolId, out var row)
                ? new ToolLookupResult(row.ToolId, row.Type, row.Reference, row.Lot)
                : null;

        ToolTechnicalReadModel? Technical(string? toolId) =>
            !string.IsNullOrWhiteSpace(toolId)
            && byId.TryGetValue(toolId, out var row)
            && row.HasTechnical
                ? new ToolTechnicalReadModel(
                    row.ToolId,
                    row.Process,
                    row.NominalWeightGrams,
                    row.VolumePuncaoCm3,
                    row.VolumeMarisaCm3,
                    row.DiametroGargaloMm,
                    row.TpDiametroMm,
                    row.TpRaioMm,
                    row.TpSagittaMm,
                    row.TpRaioBaseMm,
                    row.TpRaioEsferaMm,
                    row.TpVolumeMm3)
                : null;

        var cmTechnical = Technical(cmToolId);
        var cmGlassDensity = cmTechnical?.Process is not null && byId.TryGetValue(cmToolId!, out var cmRow)
            ? cmRow.GlassDensity
            : null;

        return new PesoToolFacts(
            Tool(cmToolId),
            cmTechnical,
            cmGlassDensity,
            Tool(bqToolId),
            Technical(bqToolId),
            Tool(mfToolId));
    }
}
