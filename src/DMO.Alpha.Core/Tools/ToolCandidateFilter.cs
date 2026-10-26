namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Filtros explícitos e fechados da descoberta de candidatos de Tool:
/// as dimensões suportadas pela persistência canónica corrente do
/// registo Alpha — type, reference, lot, machine/line (via ligações
/// Tool–máquina) e a referência MF/produção (match: Reference == valor OR
/// AssociatedMfReference == valor, qualquer tipo — worked example
/// blueprint L70-93: a CM cuja referência canónica difere da referência
/// MF em que encaixa é candidata PELA associação explícita). Factos que
/// ajudam a distinguir candidatos e nunca formam chave de identidade
/// derivada. Cada dimensão é opcional: null/vazio significa que a
/// dimensão não é aplicada (sem exceção de argumento, ao estilo do
/// <see cref="IToolLookupQuery"/>).
/// A correspondência de reference/lot é a igualdade exata ao valor
/// persistido — sem trimming, sem case-insensitivity, sem
/// contains/semântica difusa. O filtro de máquina corresponde às
/// ligações persistidas (par Tool–máquina existente); o filtro de
/// referência MF corresponde à referência canónica OU à associação
/// explícita de descoberta — qualquer tipo de Tool.
/// Os filtros reduzem candidatos — nunca determinam nem inferem
/// identidade: a identidade final é sempre o tool_id persistido da Tool
/// explicitamente selecionada pelo humano. A linha DUMMY (REMOVED) é
/// excluída por constante em TODAS as queries de descoberta.
/// </summary>
public sealed record ToolCandidateFilter(
    ToolType? Type = null,
    string? Reference = null,
    string? Lot = null,
    int? MachineId = null,
    string? MfReference = null,
    // Job On picker: exact reference/explicit association or recorded production context.
    // Case-insensitive; never guesses a CM/BQ reference from a substring.
    string? ProductionReference = null,
    // Catalog search only: start of the reference or of its BQ part (the ReferenceMatch rule),
    // or part of the lot or of a compatible machine code.
    string? Search = null,
    // Only these tool_ids (the tools a page already shows), never the whole catalog.
    IReadOnlyCollection<string>? ToolIds = null,
    // Ferramentas page: start of the tool reference, of its BQ part or of the associated
    // MF reference («Y2» finds Y232 and 6018Y232; «0» finds 0132T173, not 1075), case-insensitive.
    string? ReferenceContains = null,
    // Job On picker: with MachineId, also the tools with no line linked at all (imported
    // without links), so a lot never hides behind the line filter (Diogo, 23/10/2026).
    bool IncluirSemLinha = false);
