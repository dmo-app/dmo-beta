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
    CreatedTool? Tool = null,
    // Na recusa ToolAlreadyExists: a ferramenta que já tem esses factos.
    string? ExistingToolId = null);

/// <summary>
/// Read model purpose-specific da Tool criada: o tool_id canónico mintado
/// e os factos tal como persistidos. A referência é a toda, como no
/// desenho (1075C142, 5447T173 ou ST100), e nunca se recusa pelo formato
/// (a nota de formato «CM = dígitos» saiu a 17/10/2026). AlreadyExisted:
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
    bool AlreadyExisted = false,
    bool LotDefaulted = false);
