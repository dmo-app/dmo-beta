namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Server-side authorization semantics of the Admin foundation — the
/// canonical operational access model of Blueprint
/// <c>modules/admin/TEMPLATES.md</c> (§"Access model", §"Effective
/// authorization", §"ADMIN boundary"):
///
/// <code>
/// User → current Access Template or none → assigned capabilities →
/// backend-authorized operation
/// </code>
///
/// Presentation never authorizes: UI visibility, URLs, page names, labels
/// and legacy job titles are not authorization authority. Every protected
/// operation must validate the current effective capability through this
/// service (the request-surface ADMIN authority itself — the
/// <c>RequireAdmin</c> policy over the <c>DmoClaimTypes.IsAdmin</c> claim —
/// is owned by the Web authorization wiring, not by this contract).
///
/// The functional management workflows (Templates CRUD, assignment
/// editing, Users CRUD and provisioning, App Definições editing) arrive
/// with the functional steps of the Admin plan (C/D/E) and extend this
/// foundation without changing these semantics.
/// </summary>
public interface IAdminAuthorizationService
{
    /// <summary>
    /// Resolves the CURRENT effective operational access of a User,
    /// server-side, from the persisted User↔Template relationship:
    ///
    /// - a User with a current Template resolves the capabilities that
    ///   Template assigns, anchored on the Template's STABLE identity —
    ///   renaming the Template's label never changes the resolution;
    /// - a User with no current Template has no Template-derived
    ///   operational capabilities;
    /// - a stand-by User has no current operational access (the User, the
    ///   assignment and historical attribution are preserved — access is
    ///   disabled, not unassigned);
    /// - the ADMIN account NEVER resolves a Template or any
    ///   capability (canonical ADMIN boundary: resolution always treats
    ///   ADMIN as having no Template, even against stale/persisted
    ///   assignment data).
    ///
    /// Returns null when the User does not exist.
    /// </summary>
    Task<EffectiveAccess?> ResolveEffectiveAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the canonical ADMIN boundary allows assigning a Template
    /// to this User. Template assignment REFUSES the ADMIN account (the
    /// User whose email is the ADMIN email of the Definições): ADMIN never
    /// holds an Access Template, in any form.
    /// The assignment write operations of the functional steps (C/D) must
    /// consult this authorization before persisting anything.
    /// </summary>
    Task<TemplateAssignmentAuthorization> AuthorizeTemplateAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The resolved current effective operational access of a User.
/// <see cref="Capabilities"/> is the authorization authority for protected
/// operations; the Template facts are resolution output (display facts),
/// never an authorization input.
/// </summary>
public sealed record EffectiveAccess
{
    public required Guid UserId { get; init; }

    /// <summary>
    /// True when this User is the ADMIN account — such a User is always
    /// resolved without a Template and without capabilities (canonical
    /// ADMIN boundary).
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>Availability state (USERS.md §1): stand-by disables current operational access.</summary>
    public bool IsStandBy { get; init; }

    /// <summary>The Auth identity the session must still match (read by the session validator).</summary>
    public string? ProviderUserId { get; init; }

    /// <summary>Whether an Admin password reset is pending (read by the session validator).</summary>
    public bool RequiresPasswordChange { get; init; }

    /// <summary>The User's session version: a cookie issued with an older one is revoked (read by the session validator).</summary>
    public int SessionVersion { get; init; }

    /// <summary>The current Template's stable row id, or null (no current Template / ADMIN boundary).</summary>
    public Guid? TemplateId { get; init; }

    /// <summary>The current Template's stable key (e.g. <c>tpl-responsavel-operacional</c>), or null.</summary>
    public string? TemplateKey { get; init; }

    /// <summary>The current Template's label at resolution time — display only, never an authorization anchor.</summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// The current effective operational capabilities. Empty whenever there
    /// is no current Template, the User is stand-by, or the identity is
    /// ADMIN-associated.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Whether the current effective access carries a capability (exact
    /// ordinal match on the effective set — the server-side check every
    /// protected operation performs).
    /// </summary>
    public bool HasCapability(string capability) =>
        Capabilities.Contains(capability, StringComparer.Ordinal);
}

/// <summary>
/// The ADMIN-boundary authorization outcome for a Template assignment.
/// </summary>
public enum TemplateAssignmentAuthorization
{
    /// <summary>The User may hold a current Template (normal operational User).</summary>
    Allowed,

    /// <summary>
    /// Refused: the User is an ADMIN-associated identity — an ADMIN never
    /// holds an Access Template (canonical boundary).
    /// </summary>
    RefusedAdminBoundary,

    /// <summary>No such User.</summary>
    UserNotFound
}
