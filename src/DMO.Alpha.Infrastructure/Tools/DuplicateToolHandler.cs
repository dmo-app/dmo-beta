using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Duplica uma Tool — criação assistida de OUTRA Tool real (blueprint
/// «General Tool duplication»): copia os factos (tipo, referência,
/// referência MF associada, quantidade contabilística BQ, ligações de
/// máquina) e os valores técnicos (incl. a geometria TP de referência)
/// como DEFAULTS EDITÁVEIS (herança de conveniência, nunca imutável), o
/// LOTE NOVO é OBRIGATÓRIO (recusa MissingNewLot — um lote diferente é uma
/// Tool diferente) e o novo tool_id é MINTADO pelo backend. A fonte fica
/// intacta (a ameaça real evitada: a duplicação que transformava a Tool
/// antiga no novo lote). Gravação numa única escrita atómica.
/// </summary>
public sealed class DuplicateToolHandler : IDuplicateToolHandler
{
    private readonly DmoDbContext _db;

    public DuplicateToolHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<DuplicateToolOutcome> HandleAsync(
        DuplicateTool command,
        CancellationToken cancellationToken = default)
    {
        // 1. A fonte tem de existir (e não ser a DUMMY — a tombstone não
        //    é duplicável).
        if (string.IsNullOrEmpty(command.SourceToolId)
            || command.SourceToolId == ToolRegistryConstants.RemovedToolId)
        {
            return Refused(DuplicateToolResult.ToolNotFound);
        }

        var source = await _db.Tools
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ToolId == command.SourceToolId, cancellationToken);
        if (source is null)
        {
            return Refused(DuplicateToolResult.ToolNotFound);
        }

        // 2. O lote novo é OBRIGATÓRIO: a duplicação cria a Tool do lote
        //    real novo — nunca muta a fonte nem o seu lote.
        var newLot = command.NewLot?.Trim();
        if (string.IsNullOrEmpty(newLot))
        {
            return Refused(DuplicateToolResult.MissingNewLot);
        }

        // beta.63 (A10): o lote já registado para esta referência abre a
        // ferramenta EXISTENTE (como o «Criar» já faz) em vez de recusar.
        // Nada é copiado nem alterado nela; a fonte fica intacta.
        var existing = await _db.Tools
            .AsNoTracking()
            .Where(t => t.Type == source.Type
                && t.Reference == source.Reference
                && t.Lot == newLot
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.AssociatedMfReference, t.BqAccountedQuantity })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new DuplicateToolOutcome(
                DuplicateToolResult.Success,
                new CreatedTool(
                    existing.ToolId,
                    source.Type,
                    source.Reference,
                    newLot,
                    existing.AssociatedMfReference,
                    existing.BqAccountedQuantity,
                    CreateToolHandler.ReferenceFormatAdvisoryFor(source.Type, source.Reference),
                    AlreadyExisted: true));
        }

        // 3. Mint do tool_id novo (verificação + re-mint): a identidade da
        //    nova Tool é emitida pelo backend, nunca herdada da fonte.
        var newToolId = await ToolIdMinter.MintUnusedAsync(
            id => _db.Tools.AsNoTracking().AnyAsync(t => t.ToolId == id, cancellationToken),
            cancellationToken);

        // 4. Defaults editáveis copiados da fonte: factos + valores
        //    técnicos (incl. TP) + ligações de máquina. A fonte fica
        //    intacta; o lote é o NOVO (a cópia nunca vira a fonte).
        var tool = new Tool
        {
            ToolId = newToolId,
            Type = source.Type,
            Reference = source.Reference,
            Lot = newLot,
            AssociatedMfReference = source.AssociatedMfReference,
            BqAccountedQuantity = source.BqAccountedQuantity,
            TechnicalName = source.TechnicalName
        };
        _db.Tools.Add(tool);

        var sourceTechnical = await _db.ToolTechnicalContexts
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ToolId == source.ToolId, cancellationToken);
        if (sourceTechnical is not null)
        {
            _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
            {
                ToolId = newToolId,
                Process = sourceTechnical.Process,
                NominalWeightGrams = sourceTechnical.NominalWeightGrams,
                VolumePuncaoCm3 = sourceTechnical.VolumePuncaoCm3,
                VolumeMarisaCm3 = sourceTechnical.VolumeMarisaCm3,
                DiametroGargaloMm = sourceTechnical.DiametroGargaloMm,
                TpDiametroMm = sourceTechnical.TpDiametroMm,
                TpRaioMm = sourceTechnical.TpRaioMm,
                TpSagittaMm = sourceTechnical.TpSagittaMm,
                TpRaioBaseMm = sourceTechnical.TpRaioBaseMm,
                TpRaioEsferaMm = sourceTechnical.TpRaioEsferaMm,
                TpVolumeMm3 = sourceTechnical.TpVolumeMm3
            });
        }

        var sourceMachineIds = await _db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => link.ToolId == source.ToolId)
            .Select(link => link.MachineId)
            .ToListAsync(cancellationToken);
        foreach (var machineId in sourceMachineIds)
        {
            _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = newToolId, MachineId = machineId });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new DuplicateToolOutcome(
            DuplicateToolResult.Success,
            new CreatedTool(
                newToolId,
                source.Type,
                source.Reference,
                newLot,
                source.AssociatedMfReference,
                source.BqAccountedQuantity,
                // Nota avisadora do formato recalculada para o valor copiado
                // (a mesma regra da criação: TRUE quando o valor se desvia do
                // formato canónico — o valor fica registado na mesma).
                CreateToolHandler.ReferenceFormatAdvisoryFor(source.Type, source.Reference)));
    }

    /// <summary>
    /// (tipo, referência, lote) é a chave da ferramenta; a linha DUMMY não
    /// conta. Devolve o tool_id de outra ferramenta com esses factos, ou null.
    /// </summary>
    internal static Task<string?> FindToolWithFacts(
        DmoDbContext db, ToolType type, string reference, string lot, string? exceptToolId, CancellationToken cancellationToken) =>
        db.Tools.AsNoTracking()
            .Where(t => t.Type == type
                && t.Reference == reference
                && t.Lot == lot
                && t.ToolId != ToolRegistryConstants.RemovedToolId
                && t.ToolId != exceptToolId)
            .Select(t => (string?)t.ToolId)
            .FirstOrDefaultAsync(cancellationToken);

    private static DuplicateToolOutcome Refused(DuplicateToolResult result) => new(result);
}
