namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Read model purpose-specific da descoberta de candidatos de Tool para
/// seleção humana explícita (picker Ferramentas/Boquilhas). Expõe apenas
/// os factos atualmente persistidos no registo canónico que uma pessoa
/// precisa para distinguir candidatos: o tool_id (ÚNICA identidade
/// canónica, emitida pelo backend) e os factos descritivos
/// type/reference/lot, projetados exatamente como estão persistidos. Um
/// lote diferente é uma Tool diferente e portanto um tool_id diferente:
/// candidatos com a mesma referência e lotes diferentes nunca colapsam.
/// O candidato nunca transporta qualquer noção de seleção, pré-seleção ou
/// preferência — a seleção é sempre um ato humano explícito sobre o
/// tool_id persistido, mesmo quando só resta um candidato.
/// A QUANTIDADE e as MÁQUINAS COMPATÍVEIS (o Owner 2026-10-12: «no jobon
/// deviamos meter alguma informaçao ao selecionar a ferramenta para nao
/// ficar tao bare, e a quantidade deveria ser um desses campos» + «a
/// quantidade existe sempre mas nem sempre é usada»): os factos de
/// contexto que dão corpo à escolha — a quantidade (o lote: nas BQ é o
/// total que alimenta o saldo; nas outras fica disponível, «—» quando
/// não aplicável) e as máquinas do link n-n (as linhas onde a
/// ferramenta pode trabalhar).
/// </summary>
public sealed record ToolCandidate(
    string ToolId,
    ToolType Type,
    string Reference,
    string Lot,
    int? AccountedQuantity = null,
    string? MachineCodes = null);
