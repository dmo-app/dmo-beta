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

    // DUPLICAR AO MESMO TEMPO (duplo clique na janela, que envia um POST
    // nativo, ou outro posto): o segundo pedido não via a ferramenta do
    // primeiro e criava outra com o mesmo tipo, referência e lote (deep
    // sweep, 18/10/2026). O mesmo trinco do «Criar»: o segundo espera e abre
    // a do primeiro, e o Criar e o Duplicar também não correm um contra o
    // outro.
    public Task<DuplicateToolOutcome> HandleAsync(
        DuplicateTool command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, "ferramentas:criar", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<DuplicateToolOutcome> HandleLockedAsync(
        DuplicateTool command,
        CancellationToken cancellationToken)
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

        // Só o mesmo tipo + referência + lote já registado é recusado (Diogo,
        // 26/10/2026: as linhas não entram na regra do Duplicar) e a existente
        // fica com o aviso de ferramenta repetida. A cópia leva as linhas da
        // fonte. A fonte fica intacta.
        var sourceMachineIds = await _db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => link.ToolId == source.ToolId)
            .Select(link => link.MachineId)
            .ToListAsync(cancellationToken);
        var existing = await ToolDuplicateWarnings.FindRepeatedAsync(
            _db, source.Type, source.Reference, newLot, Array.Empty<int>(), exceptToolId: null, cancellationToken);
        if (existing is not null)
        {
            // O duplo clique: o resultado do primeiro clique, sem recusa nem aviso.
            if (ToolDuplicateWarnings.IsRepeatedSubmit(existing.RegisteredAtUtc))
            {
                return new DuplicateToolOutcome(
                    DuplicateToolResult.Success,
                    new CreatedTool(
                        existing.ToolId,
                        source.Type,
                        source.Reference,
                        newLot,
                        existing.AssociatedMfReference,
                        existing.BqAccountedQuantity));
            }

            await ToolDuplicateWarnings.MarkAsync(_db, existing.ToolId, command.RequestedBy, cancellationToken);
            return new DuplicateToolOutcome(DuplicateToolResult.LotAlreadyExists, ExistingToolId: existing.ToolId);
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
            TechnicalName = source.TechnicalName,
            FabricaOrigem = source.FabricaOrigem,
            RegisteredAtUtc = DateTime.UtcNow
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
                TpVolumeMm3 = sourceTechnical.TpVolumeMm3,
                McaliperUrl = sourceTechnical.McaliperUrl
            });
        }

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
                source.BqAccountedQuantity));
    }

    private static DuplicateToolOutcome Refused(DuplicateToolResult result) => new(result);
}
