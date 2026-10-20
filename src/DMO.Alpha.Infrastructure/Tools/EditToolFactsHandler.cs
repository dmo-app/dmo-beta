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

        // Outra pessoa gravou depois de a ficha ser aberta: nada se grava por
        // cima do que ela gravou (varredura de 18/10/2026, B4).
        if (command.ExpectedVersion is { } expected
            && _db.Entry(tool).Property<uint>(DmoDbContext.RowVersion).CurrentValue != expected)
        {
            return Refused(EditToolFactsResult.ChangedByAnotherUser);
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

        // A colisão com OUTRA ferramenta (tipo, referência, lote, numa máquina
        // em comum desde 19/10/2026) continua a ser recusa: gravar partiria a
        // chave. A recusa leva o tool_id da existente para a ficha a oferecer
        // abrir, e a outra fica com o aviso de ferramenta repetida.
        if (await ToolDuplicateWarnings.FindRepeatedAsync(_db, type, reference, lot, machineIds, tool.ToolId, cancellationToken) is { } existing)
        {
            await ToolDuplicateWarnings.MarkAsync(_db, existing.ToolId, command.RequestedBy, cancellationToken);
            return new EditToolFactsOutcome(EditToolFactsResult.LotAlreadyExists, ExistingToolId: existing.ToolId);
        }

        // A referência MF associada só se aplica à CM.
        var associatedMfReference = type == ToolType.Cm ? TrimToNull(command.AssociatedMfReference) : null;

        // Mudar o tipo de uma ferramenta já usada grava (o Editar corrige uma
        // ferramenta registada errada), mas a ficha avisa com as produções
        // que a usaram com o tipo anterior: passam a mostrá-la com o tipo
        // novo, e uma BQ sai das Boquilhas. Voltar ao tipo anterior repõe
        // tudo, porque o tool_id não muda (sweep de 19/10/2026, F-1).
        var previousType = tool.Type;
        var previousTypeProductions = previousType == type
            ? 0
            : await ProductionsUsingAsync(tool.ToolId, previousType, cancellationToken);

        // 4. Full-replace no MESMO tool_id + substituição atómica do
        //    conjunto de ligações (remove as antigas, acrescenta as novas —
        //    uma única escrita). A identidade canónica NUNCA muda.
        tool.Type = type;
        tool.Reference = reference;
        tool.Lot = lot;
        tool.AssociatedMfReference = associatedMfReference;
        tool.BqAccountedQuantity = command.BqAccountedQuantity;
        if (command.TechnicalName is not null)
        {
            tool.TechnicalName = TrimToNull(command.TechnicalName);
        }

        var existingLinks = await _db.ToolMachineLinks
            .Where(link => link.ToolId == tool.ToolId)
            .ToListAsync(cancellationToken);
        _db.ToolMachineLinks.RemoveRange(existingLinks);

        foreach (var machineId in machineIds)
        {
            _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = tool.ToolId, MachineId = machineId });
        }

        // Mudar só as máquinas também muda a versão da Tool, para a ficha
        // aberta por outra pessoa ficar desatualizada.
        if (!existingLinks.Select(link => link.MachineId).ToHashSet().SetEquals(machineIds))
        {
            _db.Entry(tool).Property(t => t.Reference).IsModified = true;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra pessoa gravou entre a leitura e esta escrita.
            return Refused(EditToolFactsResult.ChangedByAnotherUser);
        }

        return new EditToolFactsOutcome(
            EditToolFactsResult.Success,
            new EditedToolFacts(
                tool.ToolId,
                type,
                reference,
                lot,
                associatedMfReference,
                tool.BqAccountedQuantity,
                LotDefaulted: lotDefaulted,
                PreviousType: previousType == type ? null : previousType,
                PreviousTypeProductions: previousTypeProductions));
    }

    // As produções (atuais ou trocadas) em que a ferramenta foi a CM, a BQ ou o MF.
    private Task<int> ProductionsUsingAsync(string toolId, ToolType type, CancellationToken cancellationToken) => type switch
    {
        ToolType.Cm => _db.CmContexts.Where(c => c.ToolId == toolId)
            .Select(c => c.JobOnId ?? c.ReplacedJobOnId).Distinct().CountAsync(cancellationToken),
        ToolType.Bq => _db.BqContexts.Where(c => c.ToolId == toolId)
            .Select(c => c.JobOnId ?? c.ReplacedJobOnId).Distinct().CountAsync(cancellationToken),
        _ => _db.MfContexts.Where(c => c.ToolId == toolId)
            .Select(c => c.JobOnId ?? c.ReplacedJobOnId).Distinct().CountAsync(cancellationToken)
    };

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static EditToolFactsOutcome Refused(EditToolFactsResult result) => new(result);
}
