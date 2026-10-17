namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// A ASSOCIAÇÃO POSTERIOR de um Peso pré-produção (tool-scoped) a uma
/// produção (spec PESO.md «Later association to production»; o Owner
/// 2026-10-16: «quando existir um jobon com CM_ID que usa o tool_id
/// perguntar se quero associar»). Decisão humana explícita: o MESMO
/// peso_id passa a ser o Peso dessa produção — nenhum Peso substituto é
/// criado — e deixa de estar disponível para outra produção.
/// </summary>
public sealed record AssociatePesoToControlo(int ControloId, int PesoId);

public enum AssociatePesoToControloResult
{
    Success,

    ControloNotFound,

    PesoNotFound,

    /// <summary>O Job On do controlo não tem CM: não há tool_id a comparar.</summary>
    ContextMissingCm,

    /// <summary>A CM pesada não é a CM (tool_id) do Job On.</summary>
    CmMismatch,

    /// <summary>O Peso já pertence a OUTRA produção (cardinalidade 1).</summary>
    PesoAlreadyAssociated,

    /// <summary>A produção já tem o seu Peso (um Peso por controlo).</summary>
    ControloAlreadyHasPeso
}

public sealed record AssociatePesoToControloOutcome(AssociatePesoToControloResult Result);

public interface IAssociatePesoToControloHandler
{
    Task<AssociatePesoToControloOutcome> HandleAsync(
        AssociatePesoToControlo command,
        CancellationToken cancellationToken = default);
}

/// <summary>Um Peso sem produção cuja CM é a CM do Job On (candidato a associar).</summary>
public sealed record UnassociatedPesoCandidate(
    int PesoId,
    string? CmReference,
    string? CmLot,
    string? BqReference,
    string? BqLot,
    int MeasurementCount);

/// <summary>Uma produção (Job On) cuja CM usa o tool_id do Peso e que ainda não tem Peso.</summary>
public sealed record PesoProductionCandidate(
    int JobOnId,
    string ProductionNumber,
    string Reference,
    string Machine);

/// <summary>
/// As leituras da associação posterior — apenas consulta; associar é
/// sempre o POST explícito do <see cref="IAssociatePesoToControloHandler"/>.
/// </summary>
public interface IPesoAssociationQuery
{
    /// <summary>Pesos sem produção da CM (tool_id) do Job On deste controlo, quando o controlo ainda não tem Peso.</summary>
    Task<IReadOnlyList<UnassociatedPesoCandidate>> FindCandidatesForControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pesos sem produção desta CM (tool_id), para quem já sabe que o controlo
    /// não tem Peso e qual é a CM do Job On.
    /// </summary>
    Task<IReadOnlyList<UnassociatedPesoCandidate>> FindUnassociatedByCmToolAsync(
        string cmToolId,
        CancellationToken cancellationToken = default);

    /// <summary>Produções cuja CM usa o tool_id deste Peso sem produção e que ainda não têm Peso.</summary>
    Task<IReadOnlyList<PesoProductionCandidate>> FindProductionsForPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default);
}
