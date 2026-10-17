namespace DMO.Alpha.Core.Admin;

/// <summary>
/// An Access Template: a named collection of operational access
/// assignments (Blueprint <c>modules/admin/TEMPLATES.md</c> §"Template
/// meaning"). A Template may combine the module capabilities needed by one
/// kind of user; it is the only carrier of operational capability
/// assignment — ADMIN authority never derives from a Template.
/// </summary>
public sealed class Template
{
    public Guid Id { get; init; }

    /// <summary>
    /// The STABLE identity of the Template (for example
    /// <c>tpl-responsavel-operacional</c>): unique, assigned at creation
    /// and never rewritten. The stable key — never the <see cref="Name"/>
    /// label — is what the persisted User↔Template relationship and the
    /// effective-access resolution anchor on, so renaming a Template never
    /// changes anyone's access.
    /// </summary>
    public required string TemplateKey { get; init; }

    /// <summary>
    /// Human-readable label. Only a label: it may be renamed freely, and it
    /// is never interpreted as a hardcoded role or authorization rule
    /// (legacy labels such as Operador or Responsável are not a second
    /// access authority).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The operational capabilities this Template assigns, as module-owned
    /// capability strings (see <see cref="DmoCapabilities"/> for the Beta
    /// vocabulary). Persisted canonically (de-duplicated, sorted,
    /// comma-joined text) — the storage form is an implementation detail
    /// of the persistence layer.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; set; } = [];

    /// <summary>Creation time; never rewritten.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Last modification time (label/capability edits).</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
