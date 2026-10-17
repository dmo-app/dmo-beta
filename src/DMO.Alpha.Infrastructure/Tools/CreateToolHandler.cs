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
/// escrita; os formatos de referência CM/BQ são HINT + nota avisadora
/// (ReferenceFormatAdvisory no outcome — a página transporta-a por PRG),
/// NUNCA recusa (função antes de proteção). GET-OR-CREATE (o Owner
/// 2026-10-12): a chave de unicidade é (tipo, referência, lote) — os
/// mesmos factos devolvem a Tool EXISTENTE com as máquinas do form
/// ACRESCENTADAS ao ToolMachineLink dela (nunca uma segunda linha, nunca
/// uma row por máquina); um lote diferente é outra ferramenta legítima.
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

    public async Task<CreateToolOutcome> HandleAsync(
        CreateTool command,
        CancellationToken cancellationToken = default)
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

        // 5. GET-OR-CREATE (o Owner 2026-10-12: «ferramentas duplicadas
        //    sao os lotes diferentes, ou maquinas diferentes» + «certas
        //    referencias podem trabalhar em mais que uma maquina»): a
        //    chave de unicidade é (tipo, referência, lote) — os MESMOS
        //    três factos devolvem a Tool EXISTENTE (nunca uma segunda
        //    linha; a linha DUMMY nunca conta); as máquinas do form
        //    ACRESCENTAM ao ToolMachineLink dela (a multi-máquina da
        //    MESMA ferramenta — o link a crescer, nunca uma row por
        //    máquina). Um lote diferente é outra ferramenta legítima: o
        //    fluxo de criação nova (passo 6) mantém-se.
        var existing = await _db.Tools
            .AsNoTracking()
            .Where(t => t.Type == type
                && t.Reference == reference
                && t.Lot == lot
                && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.AssociatedMfReference, t.BqAccountedQuantity })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // O mesmo tipo/referência/lote conserva a identidade; novas compatibilidades acrescentam-se.
            var linkedMachineIds = await _db.ToolMachineLinks
                .AsNoTracking()
                .Where(l => l.ToolId == existing.ToolId)
                .Select(l => l.MachineId)
                .ToListAsync(cancellationToken);
            var machinesToAdd = machineIds.Where(id => !linkedMachineIds.Contains(id)).ToList();

            foreach (var machineId in machinesToAdd)
            {
                _db.ToolMachineLinks.Add(new ToolMachineLink
                {
                    ToolId = existing.ToolId,
                    MachineId = machineId
                });
            }

            if (machinesToAdd.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new CreateToolOutcome(
                CreateToolResult.Success,
                new CreatedTool(
                    existing.ToolId,
                    type,
                    reference,
                    lot,
                    existing.AssociatedMfReference,
                    existing.BqAccountedQuantity,
                    ReferenceFormatAdvisoryFor(type, reference),
                    AlreadyExisted: true,
                    LotDefaulted: lotDefaulted));
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
            TechnicalName = TrimToNull(command.TechnicalName)
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
                ReferenceFormatAdvisoryFor(type, reference),
                AlreadyExisted: false,
                LotDefaulted: lotDefaulted));
    }

    /// <summary>
    /// Nota avisadora de formato (NUNCA recusa): CM = dígitos (5447);
    /// BQ = 1 letra maiúscula + 3 dígitos (T194/C142/Y232). TRUE quando o
    /// valor registado se desvia do formato canónico — o valor fica
    /// registado na mesma (registo truthful) e a página mostra a nota.
    /// </summary>
    internal static bool ReferenceFormatAdvisoryFor(ToolType type, string reference) => type switch
    {
        ToolType.Cm => !System.Text.RegularExpressions.Regex.IsMatch(reference, @"^\d+$"),
        ToolType.Bq => !System.Text.RegularExpressions.Regex.IsMatch(reference, @"^[A-Z]\d{3}$"),
        _ => false
    };

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CreateToolOutcome Refused(CreateToolResult result) => new(result);
}
