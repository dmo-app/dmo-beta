using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Edita os FACTOS de uma Tool (full-replace no MESMO tool_id — a correção
/// normal quando a identidade física não mudou; um lote diferente é uma
/// Tool diferente: duplicar, não editar). Substituição integral de
/// Type/Reference/Lot/AssociatedMfReference/BqAccountedQuantity/máquinas —
/// a BQ substitui a ÚNICA máquina (0 ou 1; &gt;1 é recusa tipada), CM/MF
/// substituem o conjunto. A DUMMY (REMOVED) nunca é endereçável
/// (ToolNotFound): nenhum caminho a edita. Recusas tipadas verificadas
/// ANTES de qualquer escrita; a substituição de factos e de máquinas é
/// atómica (uma única escrita). A ameaça real evitada: um registo mestre
/// que muda de identidade em silêncio — o tool_id é imutável na edição.
/// </summary>
public sealed class EditToolFactsHandler : IEditToolFactsHandler
{
    private readonly DmoDbContext _db;

    public EditToolFactsHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<EditToolFactsOutcome> HandleAsync(
        EditToolFacts command,
        CancellationToken cancellationToken = default)
    {
        // 1. A Tool tem de existir (e não ser a DUMMY): a ficha editável é
        //    a da Tool real registada — a tombstone REMOVED nunca é
        //    endereçável.
        if (string.IsNullOrEmpty(command.ToolId)
            || command.ToolId == ToolRegistryConstants.RemovedToolId)
        {
            return Refused(EditToolFactsResult.ToolNotFound);
        }

        var tool = await _db.Tools
            .FirstOrDefaultAsync(t => t.ToolId == command.ToolId, cancellationToken);
        if (tool is null)
        {
            return Refused(EditToolFactsResult.ToolNotFound);
        }

        // 2. Validação dos factos substitutos (a mesma regra da criação).
        if (command.TypeToken is not ("CM" or "MF" or "BQ"))
        {
            return Refused(EditToolFactsResult.UnknownTypeToken);
        }
        var type = command.TypeToken == "CM"
            ? ToolType.Cm
            : command.TypeToken == "MF" ? ToolType.Mf
            : ToolType.Bq;

        var reference = command.Reference?.Trim();
        if (string.IsNullOrEmpty(reference))
        {
            return Refused(EditToolFactsResult.MissingReference);
        }

        // beta.63: o lote vazio grava «s/lote» e a ficha avisa (nunca recusa).
        var typedLot = command.Lot?.Trim();
        var lotDefaulted = string.IsNullOrEmpty(typedLot);
        var lot = lotDefaulted ? ToolRegistryConstants.NoLot : typedLot!;

        if (command.BqAccountedQuantity is < 0)
        {
            return Refused(EditToolFactsResult.InvalidLotQuantity);
        }

        // A colisão com OUTRA ferramenta (tipo, referência, lote) continua a
        // ser recusa: gravar partiria a chave. A recusa leva o tool_id da
        // existente para a ficha a oferecer abrir.
        if (await DuplicateToolHandler.FindToolWithFacts(_db, type, reference, lot, tool.ToolId, cancellationToken) is { } existingToolId)
        {
            return new EditToolFactsOutcome(EditToolFactsResult.LotAlreadyExists, ExistingToolId: existingToolId);
        }

        // A referência MF associada só se aplica à CM.
        var associatedMfReference = type == ToolType.Cm ? TrimToNull(command.AssociatedMfReference) : null;

        // Substituição do conjunto de compatibilidades, em todos os tipos; cada id resolve.
        var machineIds = (command.MachineIds ?? Array.Empty<int>()).Distinct().ToList();
        if (machineIds.Count > 0)
        {
            var persistedMachineIds = await _db.Machines
                .AsNoTracking()
                .Where(m => machineIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);
            if (persistedMachineIds.Count != machineIds.Count)
            {
                return Refused(EditToolFactsResult.MachineNotFound);
            }
        }

        // 4. Full-replace no MESMO tool_id + substituição atómica do
        //    conjunto de ligações (remove as antigas, acrescenta as novas —
        //    uma única escrita). A identidade canónica NUNCA muda.
        tool.Type = type;
        tool.Reference = reference;
        tool.Lot = lot;
        tool.AssociatedMfReference = associatedMfReference;
        tool.BqAccountedQuantity = command.BqAccountedQuantity;

        var existingLinks = await _db.ToolMachineLinks
            .Where(link => link.ToolId == tool.ToolId)
            .ToListAsync(cancellationToken);
        _db.ToolMachineLinks.RemoveRange(existingLinks);

        foreach (var machineId in machineIds)
        {
            _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = tool.ToolId, MachineId = machineId });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new EditToolFactsOutcome(
            EditToolFactsResult.Success,
            new EditedToolFacts(
                tool.ToolId,
                type,
                reference,
                lot,
                associatedMfReference,
                tool.BqAccountedQuantity,
                CreateToolHandler.ReferenceFormatAdvisoryFor(type, reference),
                LotDefaulted: lotDefaulted));
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static EditToolFactsOutcome Refused(EditToolFactsResult result) => new(result);
}
