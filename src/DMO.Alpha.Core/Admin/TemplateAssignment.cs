using DMO.Alpha.Core.Identity;

namespace DMO.Alpha.Core.Admin;

/// <summary>
/// The persisted User↔Template relationship: one row IS the User's current
/// Template assignment (row present = current; row absent = no current
/// Template). A User has at most one current Template — enforced by a
/// unique index on <see cref="UserId"/> — and a Template may be assigned
/// to many Users (Blueprint <c>modules/admin/TEMPLATES.md</c> §"Access
/// model": Users and Templates are two administration views over this
/// same persisted relationship; they must not create competing access
/// assignments).
///
/// ADMIN boundary (canonical): an ADMIN identity never holds an Access
/// Template — template assignment refuses a User with ADMIN status and
/// effective-access resolution always treats ADMIN as having no Template.
/// This row therefore never belongs to an ADMIN-associated identity.
/// </summary>
public sealed class TemplateAssignment
{
    public Guid Id { get; init; }

    /// <summary>
    /// FK → <see cref="User"/>. Unique: the single current assignment of
    /// that User (1 User : 0..1 current Template).
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// FK → <see cref="Template"/> (the assigned Template's stable row).
    /// The row IS the User's single current assignment: the assignment
    /// operations of the Users view edit this FK in place (never a second
    /// competing row — the unique index on <see cref="UserId"/> keeps the
    /// relation 0..1).
    /// </summary>
    public required Guid TemplateId { get; set; }

    /// <summary>
    /// Assignment time of the CURRENT assignment (the row IS the current
    /// assignment: editing the User's Template updates the row and this
    /// timestamp); historical actions are never rewritten by later changes.
    /// </summary>
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The assigned Template (resolved by the stable <see cref="TemplateId"/>, never by label).</summary>
    public Template? Template { get; set; }

    /// <summary>The assigned User.</summary>
    public User? User { get; set; }
}
