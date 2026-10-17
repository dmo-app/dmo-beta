namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: criar uns Pegamentos sem Job On, com as ferramentas escolhidas
/// à mão (tool_id de CM, BQ e MF; pelo menos uma). O registo nasce sem
/// controlo_id, com as ferramentas e a tolerância congeladas como nos
/// Pegamentos de uma produção, e pode ser associado a um Job On mais tarde.
/// </summary>
public sealed record CreateToolScopedPegamentos(string? CmToolId, string? BqToolId, string? MfToolId);

public enum CreateToolScopedPegamentosResult
{
    Success,

    /// <summary>Falta a CM, a BQ ou o MF (os Pegamentos medem sempre os três): nada foi criado.</summary>
    NoTools,

    CmToolNotFound,

    BqToolNotFound,

    MfToolNotFound
}

public sealed record CreateToolScopedPegamentosOutcome(
    CreateToolScopedPegamentosResult Result,
    int? PegamentosId = null);

public interface ICreateToolScopedPegamentosHandler
{
    Task<CreateToolScopedPegamentosOutcome> HandleAsync(
        CreateToolScopedPegamentos command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Comando: associar uns Pegamentos sem Job On ao controlo_id de uma
/// produção. O MESMO pegamentos_id passa a pertencer à produção, com as
/// leituras e os valores congelados intactos.
/// </summary>
public sealed record AssociatePegamentosToControlo(int ControloId, int PegamentosId);

public enum AssociatePegamentosToControloResult
{
    Success,

    ControloNotFound,

    PegamentosNotFound,

    /// <summary>Os Pegamentos já pertencem a outra produção.</summary>
    PegamentosAlreadyAssociated,

    /// <summary>A produção já tem os seus Pegamentos (o normal é um por produção).</summary>
    ControloAlreadyHasPegamentos,

    /// <summary>Uma ferramenta dos Pegamentos não é a do Job On (ou o Job On não tem esse componente).</summary>
    ToolMismatch
}

public sealed record AssociatePegamentosToControloOutcome(
    AssociatePegamentosToControloResult Result,
    IReadOnlyList<PegamentosComponent>? MismatchedComponents = null);

public interface IAssociatePegamentosToControloHandler
{
    Task<AssociatePegamentosToControloOutcome> HandleAsync(
        AssociatePegamentosToControlo command,
        CancellationToken cancellationToken = default);
}
