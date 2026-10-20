namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// A ASSOCIAÇÃO POSTERIOR de um Peso pré-produção (tool-scoped) a uma
/// produção (spec PESO.md «Later association to production»; o Owner
/// 2026-10-16: «quando existir um jobon com CM_ID que usa o tool_id
/// perguntar se quero associar»). Decisão humana explícita: o MESMO
/// peso_id passa a ser o Peso dessa produção — nenhum Peso substituto é
/// criado — e deixa de estar disponível para outra produção. A CM e a BQ do
/// Peso têm de ser cada uma a da produção agora ou uma que ela teve antes de
/// uma troca, e a produção ainda não pode ter Peso para essa CM+BQ (resposta
/// R16-6 do especialista, 18/10/2026): o Peso passa a ser o dessa
/// combinação.
/// </summary>
public sealed record AssociatePesoToControlo(int ControloId, int PesoId);

public enum AssociatePesoToControloResult
{
    Success,

    ControloNotFound,

    PesoNotFound,

    /// <summary>O Job On do controlo não tem CM: não há tool_id a comparar.</summary>
    ContextMissingCm,

    /// <summary>A CM pesada não é a CM (tool_id) do Job On, nem uma que ele teve antes de uma troca.</summary>
    CmMismatch,

    /// <summary>O Peso já pertence a OUTRA produção (cardinalidade 1).</summary>
    PesoAlreadyAssociated,

    /// <summary>A produção já tem um Peso com esta CM e esta BQ (um Peso por controlo para cada CM+BQ).</summary>
    ControloAlreadyHasPeso,

    /// <summary>O Job On do controlo não tem BQ (nem agora nem antes de uma troca): não há tool_id a comparar.</summary>
    ContextMissingBq,

    /// <summary>
    /// A BQ (marisa) do Peso não é a BQ do Job On, nem uma que ele teve antes
    /// de uma troca: o volume marisa entra no cálculo (resposta R16-6 do
    /// especialista, 18/10/2026).
    /// </summary>
    BqMismatch
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

/// <summary>
/// Uma produção (Job On) cuja CM e cuja BQ são as do Peso (agora ou antes de
/// uma troca) e que ainda não tem Peso para essa CM+BQ.
/// </summary>
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
    /// <summary>
    /// Pesos sem produção que se podem associar a este controlo, quando ainda
    /// não tem o Peso da CM e da BQ atuais: a CM e a BQ de cada um são as do
    /// Job On ou umas que ele teve antes de uma troca, e a produção ainda não
    /// tem Peso para essa CM+BQ (resposta R16-6 do especialista).
    /// </summary>
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

    /// <summary>
    /// Produções a que este Peso sem produção se pode associar: a CM e a BQ
    /// dele são as do Job On ou umas que ele teve antes de uma troca, e a
    /// produção ainda não tem Peso para essa CM+BQ.
    /// </summary>
    Task<IReadOnlyList<PesoProductionCandidate>> FindProductionsForPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default);
}
