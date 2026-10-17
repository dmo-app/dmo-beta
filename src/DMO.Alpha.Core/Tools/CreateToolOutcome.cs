namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Resultado da criação: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific da Tool criada (tool_id mintado pelo backend +
/// factos registados) e a nota avisadora de formato de referência (a
/// página transporta-a por PRG — o formato NUNCA recusa).
/// GET-OR-CREATE (o Owner 2026-10-12: «ferramentas duplicadas sao os
/// lotes diferentes, ou maquinas diferentes» + «certas referencias
/// podem trabalhar em mais que uma maquina»): a chave de unicidade é
/// (tipo, referência, lote) — os MESMOS três factos devolvem a Tool
/// EXISTENTE (nunca uma segunda linha: <see cref="CreatedTool.AlreadyExisted"/>
/// true, as máquinas do form acrescentam ao link dela); um lote
/// diferente é outra ferramenta legítima (a Tool distinta de sempre).
/// </summary>
public sealed record CreateToolOutcome(
    CreateToolResult Result,
    CreatedTool? Tool = null);

/// <summary>
/// Read model purpose-specific da Tool criada: o tool_id canónico mintado
/// e os factos tal como persistidos. O ReferenceFormatAdvisory é a nota
/// avisadora (CM = dígitos; BQ = 1 letra maiúscula + 3 dígitos): TRUE
/// quando o valor registado se desvia do formato canónico — o valor foi
/// registado na mesma (registo truthful, nunca recusa). AlreadyExisted:
/// TRUE quando o get-or-create devolveu a Tool que JÁ existia com os
/// mesmos (tipo, referência, lote) — nada foi duplicado e as máquinas do
/// form foram acrescentadas às ligações dela. LotDefaulted: TRUE quando o
/// lote chegou vazio e ficou «s/lote» (beta.63 — aviso, nunca recusa).
/// </summary>
public sealed record CreatedTool(
    string ToolId,
    ToolType Type,
    string Reference,
    string Lot,
    string? AssociatedMfReference,
    int? BqAccountedQuantity,
    bool ReferenceFormatAdvisory,
    bool AlreadyExisted = false,
    bool LotDefaulted = false);
