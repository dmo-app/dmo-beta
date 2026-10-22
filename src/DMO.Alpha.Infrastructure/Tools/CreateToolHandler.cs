using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Regista uma Tool real (Ferramentas — o corte A, a RAIZ da cascata
/// tool_id → jobon_id → controlo_id → pegamentos/peso). O tool_id é
/// SEMPRE mintado pelo backend («T» + 10 hex minúsculos, verificação de
/// existência + re-mint; nunca sequencial, nunca derivado dos factos) — a
/// ameaça real evitada: ids internos ao operador e identidade de
/// atributos visíveis (nada na UI deriva o tool_id da referência/lote).
/// As recusas são tipadas (enum fechado) e verificadas ANTES de qualquer
/// escrita; a referência é a toda, como no desenho, e o formato NUNCA é
/// recusado (função antes de proteção). GET-OR-CREATE (o Owner
/// 2026-10-12): a chave de unicidade é (tipo, referência, lote) — os
/// mesmos factos eram a Tool EXISTENTE; desde 18/10/2026 são recusados, e
/// desde 19/10/2026 só quando partilham uma máquina (ToolDuplicateWarnings);
/// um lote diferente é outra ferramenta legítima.
/// A Tool nasce com as ligações de máquina aplicáveis: a BQ carrega no
/// máximo UMA (0 é permitido — completa depois), CM/MF o conjunto.
/// Gravação numa única escrita atómica.
/// </summary>
public sealed class CreateToolHandler : ICreateToolHandler
{
    private readonly DmoDbContext _db;

    private readonly IToolLotQuantitySettings _lotQuantities;

    public CreateToolHandler(DmoDbContext db, IToolLotQuantitySettings lotQuantities)
    {
        _db = db;
        _lotQuantities = lotQuantities;
    }

    // GET-OR-CREATE ao mesmo tempo (duplo envio): o segundo pedido não via a
    // ferramenta do primeiro e criava outra com a mesma referência e lote
    // (18/10/2026). O trinco faz o segundo esperar e abrir a do primeiro.
    public Task<CreateToolOutcome> HandleAsync(
        CreateTool command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, "ferramentas:criar", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<CreateToolOutcome> HandleLockedAsync(
        CreateTool command,
        CancellationToken cancellationToken)
    {
        // 1. Token do tipo: um dos canónicos (CM/MF/BQ) — recusa tipada,
        //    nunca exceção.
        if (command.TypeToken is not ("CM" or "MF" or "BQ"))
        {
            return Refused(CreateToolResult.UnknownTypeToken);
        }
        var type = command.TypeToken == "CM"
            ? ToolType.Cm
            : command.TypeToken == "MF" ? ToolType.Mf
            : ToolType.Bq;

        // 2. Factos humanos obrigatórios (colunas NOT NULL): a referência é
        //    a identidade HUMANA do registo (decisão aprovada: em branco é
        //    recusa MissingReference, nunca texto inventado); o lote
        //    distingue Tools da mesma referência. Os valores persistem SEM
        //    espaços envolventes (facto limpo, como no CreateJobOn).
        var reference = command.Reference?.Trim();
        if (string.IsNullOrEmpty(reference))
        {
            return Refused(CreateToolResult.MissingReference);
        }

        // beta.63 (regra do Owner «tentar sempre warning»): o lote vazio
        // grava «s/lote» e a ficha avisa. A chave (tipo, referência, lote)
        // não muda: se já existe a mesma ferramenta «s/lote», o
        // get-or-create do passo 5 abre-a em vez de criar outra.
        var typedLot = command.Lot?.Trim();
        var lotDefaulted = string.IsNullOrEmpty(typedLot);
        // A BQ sem lote é recusada (Diogo, 22/10/2026: «bloquear o registo
        // sem lote. Precisamos do lote para tool_id»): o lote é o que separa
        // as boquilhas da mesma referência.
        if (type == ToolType.Bq && (lotDefaulted || string.Equals(typedLot, ToolRegistryConstants.NoLot, StringComparison.OrdinalIgnoreCase)))
        {
            return Refused(CreateToolResult.MissingLot);
        }
        var lot = lotDefaulted ? ToolRegistryConstants.NoLot : typedLot!;

        if (command.BqAccountedQuantity is < 0) return Refused(CreateToolResult.InvalidLotQuantity);

        // 3. Associação explícita de referência MF (descoberta, opcional):
        //    trim, vazio = null — nunca substitui a referência canónica.
        var associatedMfReference = TrimToNull(command.AssociatedMfReference);

        // Compatibilidade: CM, MF e BQ aceitam várias máquinas persistidas.
        // Duplicados do formulário colapsam num único par Tool–máquina.
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
                return Refused(CreateToolResult.MachineNotFound);
            }
        }

        // 5. FERRAMENTA REPETIDA (o proprietário, 18/10/2026): o mesmo tipo,
        //    referência e lote é recusado e a existente fica com um aviso,
        //    porque a errada pode ser ela. Desde 19/10/2026 só numa máquina
        //    em comum: sem máquinas em comum é outra ferramenta legítima,
        //    como um lote diferente (passo 6).
        var existing = await ToolDuplicateWarnings.FindRepeatedAsync(
            _db, type, reference, lot, machineIds, exceptToolId: null, cancellationToken);

        if (existing is not null)
        {
            // O duplo clique: o resultado do primeiro clique, sem recusa nem aviso.
            if (ToolDuplicateWarnings.IsRepeatedSubmit(existing.RegisteredAtUtc))
            {
                return new CreateToolOutcome(
                    CreateToolResult.Success,
                    new CreatedTool(
                        existing.ToolId,
                        type,
                        reference,
                        lot,
                        existing.AssociatedMfReference,
                        existing.BqAccountedQuantity,
                        LotDefaulted: lotDefaulted));
            }

            await ToolDuplicateWarnings.MarkAsync(_db, existing.ToolId, command.RequestedBy, cancellationToken);
            return new CreateToolOutcome(CreateToolResult.ToolAlreadyExists, ExistingToolId: existing.ToolId);
        }

        // 6. Mint do tool_id: verificação de existência + re-mint — a
        //    identidade é emitida pelo backend, nunca sequencial, nunca
        //    derivada dos factos.
        var toolId = await ToolIdMinter.MintUnusedAsync(
            id => _db.Tools.AsNoTracking().AnyAsync(t => t.ToolId == id, cancellationToken),
            cancellationToken);

        // 7. Escrita atómica única: a Tool + as ligações de máquina. A
        //    quantidade contabilística persiste verbatim (facto do lote
        //    BQ; a UI oferece-o apenas para BQ — o handler não é polícia
        //    de formato, registo truthful).
        var quantity = command.BqAccountedQuantity;
        if (quantity is null && command.UseLotQuantityDefault)
            quantity = (await _lotQuantities.SuggestAsync(type, machineIds, cancellationToken)).Quantity;

        var tool = new Tool
        {
            ToolId = toolId,
            Type = type,
            Reference = reference,
            Lot = lot,
            AssociatedMfReference = associatedMfReference,
            BqAccountedQuantity = quantity,
            // O nome técnico (o código do desenho do SAP) guardado como o
            // operador escreveu — o decode é PROPOSTA na ficha.
            TechnicalName = TrimToNull(command.TechnicalName),
            RegisteredAtUtc = DateTime.UtcNow
        };
        _db.Tools.Add(tool);

        foreach (var machineId in machineIds)
        {
            _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = toolId, MachineId = machineId });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CreateToolOutcome(
            CreateToolResult.Success,
            new CreatedTool(
                toolId,
                type,
                reference,
                lot,
                associatedMfReference,
                tool.BqAccountedQuantity,
                AlreadyExisted: false,
                LotDefaulted: lotDefaulted));
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CreateToolOutcome Refused(CreateToolResult result) => new(result);
}
