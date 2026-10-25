using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// The Job On page lookups that used to read the DbContext from the page
/// itself. Each method is the same single read-only query as before.
/// </summary>
public sealed class JobOnPaginaQuery(DmoDbContext db) : IJobOnPaginaQuery
{
    public async Task<JobOnProductionOption?> ProducaoDoHistoricoAsync(
        string reference,
        string productionNumber,
        string? exactReference,
        int? machineId,
        CancellationToken cancellationToken = default)
    {
        var antigas = db.JobOns.AsNoTracking()
            .Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, reference))
            .Where(j => j.ProductionNumber == productionNumber);
        if (exactReference is not null)
        {
            antigas = antigas.Where(j => j.Reference == exactReference);
        }
        if (machineId is int maquinaPedida)
        {
            antigas = antigas.Where(j => j.MachineId == maquinaPedida);
        }
        return await antigas
            .OrderByDescending(j => j.Id)
            .Select(j => new JobOnProductionOption(j.ProductionNumber, j.Reference))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OverlapWarning?> AvisoDeSobreposicaoAsync(
        int jobOnId,
        string machineCode,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken = default)
    {
        var machineId = await db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => j.MachineId)
            .FirstOrDefaultAsync(cancellationToken);
        return machineId != 0
            ? await JobOnOverlapQuery.EvaluateAsync(
                db, jobOnId, machineId, machineCode, inicioEm, fimEm, cancellationToken)
            : null;
    }

    public Task<string?> ReferenciaDaMfAsync(string mfToolId, CancellationToken cancellationToken = default) =>
        db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId == mfToolId && tool.Type == ToolType.Mf)
            .Select(tool => tool.Reference)
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<string?> ReferenciaMfDaCmAsync(string cmToolId, CancellationToken cancellationToken = default) =>
        db.Tools.AsNoTracking()
            .Where(tool => tool.ToolId == cmToolId && tool.Type == ToolType.Cm)
            .Select(tool => tool.AssociatedMfReference ?? tool.Reference)
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<JobOnExistente?> ProducaoComReferenciaENumeroAsync(
        string reference,
        string productionNumber,
        CancellationToken cancellationToken = default) =>
        db.JobOns
            .AsNoTracking()
            .Where(j => j.ProductionNumber == productionNumber && j.Reference.ToUpper() == reference)
            .Select(j => new JobOnExistente(
                j.Id, j.MachineId, j.InicioEm, j.FimEm, j.CaloteMm,
                j.CmContext == null ? null : j.CmContext.ToolId,
                j.MfContext == null ? null : j.MfContext.ToolId,
                j.BqContext == null ? null : j.BqContext.ToolId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<decimal?> CaloteGuardadaAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        db.JobOns
            .AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => j.CaloteMm)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> CodigoDaMaquinaAsync(int machineId, CancellationToken cancellationToken = default) =>
        db.Machines
            .AsNoTracking()
            .Where(m => m.Id == machineId)
            .Select(m => (string?)m.Code)
            .FirstOrDefaultAsync(cancellationToken);
}
