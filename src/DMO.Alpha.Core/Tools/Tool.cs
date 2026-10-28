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
    /// «CM · 5447T173 · lote 3»): a referência toda, como no desenho
    /// (1075C142, 5447T173 ou ST100), nunca o nome técnico. O formato
    /// nunca é recusado (função antes de proteção). A referência é
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
    /// A quantidade de AN (anéis) do lote da BQ (Diogo, 28/10/2026: «vamos
    /// passar a registar AN»; BQ+AN são um só componente). Fica ao lado da
    /// quantidade das BQ, que é outra (B1: 120 BQ, 180 AN). Null nas CM/MF e
    /// enquanto não for escrita; os movimentos nunca a mudam.
    /// </summary>
    public int? AnQuantity { get; set; }

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

    /// <summary>
    /// O CÓDIGO SAP à mão (Diogo, 28/10/2026: «se precisar de alterar algum
    /// nome de uma ferramenta para o script procurar»): quando o código no SAP
    /// não é o que a tabela de procura dá, o script procura este. Null usa a
    /// tabela (Admin › SAP).
    /// </summary>
    public string? SapCodigo { get; set; }

    /// <summary>
    /// A B2 em que a ferramenta trabalha (Diogo, 28/10/2026): uma da C3+B2
    /// fica sempre com B2 Tripla, uma da C1+B2 com B2 Dupla. Null fora da B2
    /// e, na B2, enquanto não for escolhida (aviso importante).
    /// </summary>
    public B2Modo? B2Modo { get; set; }

    /// <summary>
    /// A FÁBRICA DE ORIGEM (Diogo, 22/10/2026): a fábrica dona da ferramenta
    /// (ex.: «MG»), para separar depois as ferramentas emprestadas. Null = não indicada.
    /// </summary>
    public string? FabricaOrigem { get; set; }

    /// <summary>
    /// FERRAMENTA REPETIDA (o proprietário, 18/10/2026): quando alguém tenta
    /// registar outra ferramenta com o mesmo tipo, referência e lote, o
    /// registo é recusado e ESTA fica com um aviso, porque a errada pode ser
    /// a que já existia. Quem confirmar que está certa limpa o aviso (null).
    /// </summary>
    public DateTime? DuplicateWarningAtUtc { get; set; }

    /// <summary>
    /// Quando foi registada (null nas registadas antes de 18/10/2026). Serve
    /// para o duplo clique: o mesmo registo repetido logo a seguir devolve a
    /// que acabou de nascer, sem recusa nem aviso.
    /// </summary>
    public DateTime? RegisteredAtUtc { get; set; }

    /// <summary>Quem tentou registar a repetida (o nome, senão o nº de operador ou o email).</summary>
    public string? DuplicateWarningByNameFrozen { get; set; }

    /// <summary>O utilizador de <see cref="DuplicateWarningByNameFrozen"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? DuplicateWarningByUserId { get; set; }
}
