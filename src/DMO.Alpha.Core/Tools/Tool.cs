namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Ferramenta canónica registada no Ferramentas. A identidade é o tool_id
/// emitido pelo backend; nunca é derivado de referência, lote, máquina ou
/// qualquer outro facto de descoberta de candidatos.
/// Um lote diferente é uma Tool diferente e portanto um tool_id diferente.
/// Entidade do registo canónico: factos humanos de identificação
/// (referência, lote), a associação explícita de referência MF
/// (descoberta) e a quantidade contabilística do lote BQ; a compatibilidade
/// de máquinas/linhas vive na entidade de ligação ToolMachineLink
/// (conjunto por Tool, uma linha por par Tool–máquina); os valores técnicos
/// vivem no ToolTechnicalContext 1:1. A regra de duplicação é o
/// GET-OR-CREATE do registo (o Owner 2026-10-12: «ferramentas duplicadas
/// sao os lotes diferentes, ou maquinas diferentes»): os MESMOS (tipo,
/// referência, lote) são a MESMA ferramenta — as máquinas acrescentam ao
/// ToolMachineLink dela; um lote diferente persiste como Tool distinta.
/// </summary>
public sealed class Tool
{
    public string ToolId { get; set; } = null!;

    public ToolType Type { get; set; }

    /// <summary>
    /// A referência canónica da Tool (a identidade HUMANA do registo:
    /// «CM · 5447 · lote 3»): dígitos para CM (5447), 1 letra maiúscula +
    /// 3 dígitos para BQ (T194/C142/Y232). O formato é HINT + nota
    /// avisadora — nunca recusa (função antes de proteção). A referência é
    /// facto de descoberta, não chave de identidade: nunca deriva o
    /// tool_id nem cria unicidade.
    /// </summary>
    public string Reference { get; set; } = null!;

    public string Lot { get; set; } = null!;

    /// <summary>
    /// Associação explícita de referência MF/produção para descoberta
    /// (regra Ferramentas «CM reference assistance»): a CM cuja referência
    /// canónica difere da referência MF/produção em que encaixa leva aqui
    /// essa referência. Contexto de descoberta APENAS — não substitui a
    /// referência canónica, não muda o tool_id e não é usado para
    /// selecionar/fundir Tools em silêncio (a busca por referência MF
    /// apresenta as Tools cuja Reference OU AssociatedMfReference
    /// correspondem, qualquer tipo, e a seleção continua humana explícita
    /// sobre o tool_id — worked example blueprint L70-93). Null quando não
    /// aplicável.
    /// </summary>
    public string? AssociatedMfReference { get; set; }

    /// <summary>
    /// Quantidade contabilística do lote BQ («A BQ Tool/lote carries the
    /// accounted lot quantity used by Boquilhas»): o facto pertence à Tool
    /// — o Boquilhas DERIVA quantidades operacionais dele e dos movimentos
    /// de reparação, mas os movimentos NUNCA mutam este valor. Null nas
    /// Tools CM/MF e enquanto a BQ ainda não o tiver registado.
    /// </summary>
    public int? BqAccountedQuantity { get; set; }

    /// <summary>
    /// O NOME TÉCNICO (o código do desenho — o que está no SAP; o Owner
    /// 2026-10-11: «se introduzirmos o nome tecnico, ou seja o que esta no
    /// sap. apresenta logo tudo»): guardado como o operador escreveu (a
    /// app de registo não verifica); o PARSER canónico decodifica os factos
    /// (o tipo, a referência, a dimensão, o processo, a ventilação — a
    /// gramática OP 99 PMD 02). Null quando a Tool nasce pelo formato
    /// simples (tipo + referência + lote).
    /// </summary>
    public string? TechnicalName { get; set; }
}
