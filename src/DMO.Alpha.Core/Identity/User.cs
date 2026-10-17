using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Core.Identity;

public sealed class User
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    /// <summary>
    /// Operator / BA Glass ID. Up to 4 digits. Leading zeros are preserved.
    /// </summary>
    public required string OperatorId { get; set; }

    /// <summary>
    /// The provider-owned technical authentication identity for this User.
    /// </summary>
    public required string ProviderUserId { get; set; }

    /// <summary>
    /// O EMAIL VISÍVEL (o Owner 2026-10-13: «o email sempre existiu, vê o
    /// front end design» — o mockup canónico 13_ADMIN tem a COLUNA Email na
    /// tabela, a procura «Nome ou email» e o campo no editar): o rótulo de
    /// identificação humana que o admin digitou na criação. A SENHA continua
    /// a viver SÓ no provedor (o login é pass-through pelo ProviderUserId) —
    /// este campo é o QUE SE MOSTRA E PROCURA, nunca uma credencial.
    /// </summary>
    public string? Email { get; set; }

    public bool RequiresPasswordChange { get; set; }

    public bool IsStandBy { get; set; }

    /// <summary>
    /// Human-facing profile/access label from the associated Access Template.
    /// May be null when no Template is assigned or when Templates are not yet configured.
    /// </summary>
    public string? TemplateName { get; set; }

    /// <summary>
    /// The User's current Access Template assignment — 0..1 (TEMPLATES.md
    /// §"Access model": a User has at most one current Template, enforced
    /// by the persisted relationship's unique constraint). This
    /// relationship is the CANONICAL access anchor: the effective-access
    /// resolution goes through it and the Template's stable identity —
    /// never through <see cref="TemplateName"/>, which stays a denormalized
    /// legacy display label. Null when the User has no current Template.
    /// </summary>
    public TemplateAssignment? CurrentTemplateAssignment { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
