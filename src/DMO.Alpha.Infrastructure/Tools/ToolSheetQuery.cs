using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação purpose-specific da FICHA de uma Tool (Ferramentas):
/// composição no momento da leitura dos factos persistidos, das ligações
/// de máquina (ordenadas por código — FATOS HUMANOS: «CM · 5447 · lote 3 ·
/// B1») e do contexto técnico Tool-owned quando existe (a ausência é
/// estado válido). Resolução cirúrgica por tool_id canónico exato com
/// AsNoTracking; a DUMMY (REMOVED) é excluída por CONSTANTE — nunca
/// resolve a ficha. Não cria Tools, não muta estado, não deriva
/// identidade.
/// </summary>
public sealed class ToolSheetQuery : IToolSheetQuery
{
    private readonly DmoDbContext _db;

    public ToolSheetQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<ToolSheetReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        // Uma só consulta: factos, máquinas compatíveis e contexto técnico.
        // A DUMMY nunca é endereçável como uma Tool real.
        var facts = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new
            {
                t.ToolId,
                t.Type,
                t.Reference,
                t.Lot,
                t.AssociatedMfReference,
                t.BqAccountedQuantity,
                t.AnQuantity,
                t.B2Modo,
                t.TechnicalName,
                t.FabricaOrigem,
                t.DuplicateWarningAtUtc,
                t.DuplicateWarningByNameFrozen,
                Version = EF.Property<uint>(t, DmoDbContext.RowVersion),
                TechnicalVersion = _db.ToolTechnicalContexts
                    .Where(c => c.ToolId == t.ToolId)
                    .Select(c => (uint?)EF.Property<uint>(c, DmoDbContext.RowVersion))
                    .FirstOrDefault(),
                Machines = (
                    from link in _db.ToolMachineLinks
                    join machine in _db.Machines on link.MachineId equals machine.Id
                    where link.ToolId == t.ToolId
                    orderby machine.Code, machine.Id
                    select new MachineOption(machine.Id, machine.Code))
                    .ToList(),
                Technical = _db.ToolTechnicalContexts
                    .Where(c => c.ToolId == t.ToolId)
                    .Select(c => new ToolTechnicalReadModel(
                        c.ToolId,
                        c.Process,
                        c.NominalWeightGrams,
                        c.VolumePuncaoCm3,
                        c.VolumeMarisaCm3,
                        c.DiametroGargaloMm,
                        c.TpDiametroMm,
                        c.TpRaioMm,
                        c.TpSagittaMm,
                        c.TpRaioBaseMm,
                        c.TpRaioEsferaMm,
                        c.TpVolumeMm3))
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (facts is null)
        {
            return null;
        }

        return new ToolSheetReadModel(
            facts.ToolId,
            facts.Type,
            facts.Reference,
            facts.Lot,
            facts.AssociatedMfReference,
            facts.BqAccountedQuantity,
            facts.Machines,
            facts.Technical,
            facts.TechnicalName,
            facts.Version,
            facts.TechnicalVersion ?? 0,
            facts.DuplicateWarningAtUtc,
            facts.DuplicateWarningByNameFrozen,
            facts.FabricaOrigem,
            facts.AnQuantity,
            facts.B2Modo);
    }
}
