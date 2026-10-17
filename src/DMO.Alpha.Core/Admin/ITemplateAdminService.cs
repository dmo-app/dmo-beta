namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Admin → Templates administration view over the persisted Template
/// rows and the User↔Template relationship (Blueprint
/// <c>modules/admin/TEMPLATES.md</c>).
///
/// The foundation read (Step B) projects the persisted facts; the
/// functional Templates workflows (Step C — this build) EXTEND the same
/// contract: creation and editing of Templates with the Beta capability
/// vocabulary, the stable identity minted by the backend. The assignment
/// write operations authorize through <see cref="IAdminAuthorizationService"/>
/// (ADMIN boundary) and share the SAME persisted User↔Template
/// relationship as the Users view (never a competing assignment — the
/// assignment select itself lives in the Users view).
/// </summary>
public interface ITemplateAdminService
{
    /// <summary>
    /// The persisted Access Templates, ordered by stable key. Projects the
    /// stable identity, the (mutable) label and the assigned capabilities
    /// — the facts the effective-access resolution anchors on.
    /// </summary>
    Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an Access Template (TEMPLATES.md §"Template changes"): the
    /// stable <c>TemplateKey</c> is MINTED by the backend («tpl-» + 8
    /// hex, re-mint on collision — never derived from the label, never
    /// sequential) and the capabilities are the closed Beta vocabulary
    /// (<see cref="DmoCapabilities"/>); an unknown capability string is a
    /// typed refusal. Zero capabilities is a valid Template («Sem
    /// capabilities») — the Beta needs no invented symmetry.
    /// </summary>
    Task<CreateTemplateOutcome> CreateTemplateAsync(
        CreateTemplate command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a persisted Access Template, resolved by its STABLE key:
    /// the label may be renamed freely (it never owns access) and the
    /// capability set may be edited (the same closed Beta vocabulary);
    /// the stable key is NEVER rewritten. Editing a Template changes the
    /// current effective access of the Users currently assigned to it —
    /// and never rewrites any historical action (TEMPLATES.md §"Template
    /// changes").
    /// </summary>
    Task<EditTemplateOutcome> EditTemplateAsync(
        EditTemplate command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina uma Template, resolvida pela key estável (Diogo, 17/10/2026:
    /// «não existe forma de apagar os templates»). Uma Template ainda
    /// atribuída a utilizadores NÃO se apaga (recusa tipada InUse, com a
    /// contagem): primeiro muda-se a template desses utilizadores, para
    /// que ninguém perca o acesso sem dar por isso.
    /// </summary>
    Task<DeleteTemplateOutcome> DeleteTemplateAsync(
        string templateKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-side projection of a persisted Access Template: the stable
/// identity, the human-readable label and the assigned capability strings.
/// </summary>
public sealed record TemplateSummary
{
    /// <summary>The stable row identity.</summary>
    public required Guid Id { get; init; }

    /// <summary>The stable Template key (e.g. <c>tpl-responsavel-operacional</c>) — the access anchor.</summary>
    public required string TemplateKey { get; init; }

    /// <summary>The current human-readable label — display only.</summary>
    public required string Name { get; init; }

    /// <summary>The capabilities this Template assigns (module-owned strings; see <see cref="DmoCapabilities"/>).</summary>
    public required IReadOnlyList<string> Capabilities { get; init; }
}

/// <summary>
/// Command: criar uma Access Template. A TemplateKey NÃO é transportada —
/// é SEMPRE mintada pelo backend («tpl-» + 8 hex, re-mint), tal como o
/// tool_id do registo Ferramentas: nenhuma identidade é derivada dos
/// factos e o rótulo nunca é a âncora de acesso.
/// </summary>
public sealed record CreateTemplate(
    string Name,
    IReadOnlyList<string> Capabilities);

/// <summary>Resultado tipado da criação de uma Template. Vocabulário FECHADO.</summary>
public enum CreateTemplateResult
{
    Success,

    /// <summary>O rótulo está em branco — o Name é obrigatório (apenas rótulo, nunca âncora).</summary>
    MissingName,

    /// <summary>
    /// Uma das capabilities não pertence ao vocabulário Beta fechado
    /// (DmoCapabilities.BetaSet) — o matrix de permissões por módulo do
    /// mockup NÃO é vocabulário Beta.
    /// </summary>
    UnknownCapability
}

/// <summary>Outcome da criação: resultado tipado + a projeção da Template criada (com a key mintada).</summary>
public sealed record CreateTemplateOutcome(
    CreateTemplateResult Result,
    TemplateSummary? Template = null);

/// <summary>
/// Command: editar uma Template persistida, resolvida pela identidade
/// ESTÁVEL (TemplateKey — nunca pelo rótulo, que pode ter sido renomeado).
/// </summary>
public sealed record EditTemplate(
    string TemplateKey,
    string Name,
    IReadOnlyList<string> Capabilities);

/// <summary>Resultado tipado da edição de uma Template. Vocabulário FECHADO.</summary>
public enum EditTemplateResult
{
    Success,

    /// <summary>A TemplateKey indicada não resolve nenhuma Template persistida.</summary>
    UnknownTemplate,

    /// <summary>O rótulo está em branco — o Name é obrigatório.</summary>
    MissingName,

    /// <summary>Uma das capabilities não pertence ao vocabulário Beta fechado (DmoCapabilities.BetaSet).</summary>
    UnknownCapability
}

/// <summary>Outcome da edição: resultado tipado + a projeção da Template editada (key estável intocada).</summary>
public sealed record EditTemplateOutcome(
    EditTemplateResult Result,
    TemplateSummary? Template = null);

/// <summary>Resultado tipado da eliminação de uma Template. Vocabulário FECHADO.</summary>
public enum DeleteTemplateResult
{
    Success,

    /// <summary>A TemplateKey indicada não resolve nenhuma Template persistida.</summary>
    UnknownTemplate,

    /// <summary>A Template ainda está atribuída a utilizadores — nada foi apagado.</summary>
    InUse
}

/// <summary>Outcome da eliminação: resultado tipado + quantos utilizadores a usam (na recusa InUse).</summary>
public sealed record DeleteTemplateOutcome(
    DeleteTemplateResult Result,
    int AssignedUsers = 0);
