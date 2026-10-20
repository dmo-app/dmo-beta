namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Admin → Users administration view over the persisted normal-User facts
/// (Blueprint <c>modules/admin/USERS.md</c> §1): name, operator / BA Glass
/// identifier, current Access Template or none, availability state.
///
/// The foundation read (Step B) projects the persisted facts; the
/// functional Users workflows (Step D — this build) EXTEND the same
/// contract: creation with Auth-provider provisioning (§2), the
/// temporary-password bootstrap (§3), the access reset (§5) and the
/// stand-by editing (§1). The current-Template fact always resolves
/// through the persisted User↔Template relationship (see
/// <see cref="ITemplateAdminService"/>) — the Users view never maintains
/// a competing assignment, and the Template editing entry of the Users
/// view is the assignment select of the SAME relation.
/// </summary>
public interface IUserAdminService
{
    /// <summary>
    /// The persisted normal Users with their application-side facts,
    /// ordered by operator identifier (leading zeros preserved — the
    /// identifier is application User data, exactly as stored).
    /// </summary>
    Task<IReadOnlyList<UserAdminSummary>> ListUsersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the managed Users list (the ADMIN account
    /// excluded), ordered by operator identifier; paged in the query.
    /// </summary>
    Task<DMO.Alpha.Core.Paging.PagedList<UserAdminSummary>> ListManagedUsersPageAsync(
        int? pageNumber,
        int pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a normal DMO User through the application (USERS.md §2):
    /// typed invariant refusals BEFORE any write, the temporary bootstrap
    /// password minted by the backend, the Auth-provider provisioning of
    /// the new identity, then ONE atomic persistence of the User row and
    /// the optional current-Template assignment (the persisted
    /// relationship — never a competing column).
    /// </summary>
    Task<CreateDmoUserOutcome> CreateDmoUserAsync(
        CreateDmoUser command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a User's access (USERS.md §5): a new temporary password is
    /// minted and established through the Auth provider, the forced
    /// private-password choice is re-armed on the next login, and nothing
    /// else about the User changes — no operational history, no access
    /// configuration (the current Template assignment stays as-is).
    /// </summary>
    Task<ResetUserAccessOutcome> ResetUserAccessAsync(
        ResetUserAccess command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the availability state of a User (USERS.md §1): stand-by
    /// disables the current operational access while preserving the User,
    /// the current-Template assignment and all historical attribution;
    /// un-stand-by restores the current access. Neither direction
    /// rewrites any historical fact.
    /// </summary>
    Task<SetUserStandByOutcome> SetUserStandByAsync(
        SetUserStandBy command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits the User's current Access Template through the SAME persisted
    /// User↔Template relationship the Templates view edits (TEMPLATES.md
    /// §"Access model": one relation, two administration views). A null
    /// template key REMOVES the current assignment («Sem template»); a
    /// template key resolves by the Template's STABLE identity (never by
    /// label). The ADMIN boundary is authorized before anything is
    /// persisted (an ADMIN-associated identity never holds a Template).
    /// </summary>
    Task<SetCurrentUserTemplateOutcome> SetCurrentUserTemplateAsync(
        SetCurrentUserTemplate command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O DELETE REAL do User (o Owner 2026-10-13: «sim mas eu preciso de
    /// apagar os que estao la» — o mesmo princípio das Tools/DUMMY: o delete
    /// físico com OS FATOS CONGELADOS nos registos passados): a linha sai da
    /// lista e a atribuição corrente morre com ela (CASCADE da relação); os
    /// registos históricos que citam o nº dele mantêm o TEXTO congelado do
    /// momento (o histórico nunca mente, nunca parte). A conta ADMIN NUNCA é
    /// alvo (recusa tipada).
    /// </summary>
    Task<DeleteUserOutcome> DeleteUserAsync(
        DeleteUser command,
        CancellationToken cancellationToken = default);
}

/// <summary>O comando do delete: o nº de operador (o facto humano único).</summary>
public sealed record DeleteUser(string OperatorId);

/// <summary>O resultado tipado do delete do User.</summary>
public sealed record DeleteUserOutcome(
    DeleteUserResult Result,
    string? OperatorId = null)
{
    public static DeleteUserOutcome Refused(DeleteUserResult result) => new(result);
}

public enum DeleteUserResult
{
    /// <summary>Identidade Auth apagada e User DMO removido.</summary>
    Success,

    /// <summary>
    /// User DMO removido; o provider respondeu que a identidade Auth já não
    /// existia (nada para apagar lá).
    /// </summary>
    SuccessIdentityAlreadyMissing,

    /// <summary>
    /// A identidade Auth foi apagada mas a remoção do User DMO falhou. Repetir
    /// é seguro: o provider responde que a identidade já não existe.
    /// </summary>
    PersistenceFailure,

    /// <summary>O nº indicado não resolve a nenhum User.</summary>
    UserNotFound,

    /// <summary>A conta ADMIN nunca é apagada aqui.</summary>
    AdminRefused,

    /// <summary>Auth provider failed deletion; DMO row remains intact.</summary>
    ProviderFailure
}

/// <summary>
/// Read-side projection of a normal User's application-side facts. The
/// current Template projects the STABLE key plus the current label; the
/// legacy <c>User.TemplateName</c> denormalized column is not the source —
/// the persisted relationship is.
/// </summary>
public sealed record UserAdminSummary
{
    /// <summary>The User's row identity.</summary>
    public required Guid Id { get; init; }

    /// <summary>The User's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Operator / BA Glass identifier: at most 4 digits, leading zeros
    /// preserved — application User data, not a login credential.
    /// </summary>
    public required string OperatorId { get; init; }

    /// <summary>
    /// The provider-owned technical authentication identity of this User
    /// (Supabase Auth owns the credential itself; DMO never stores
    /// passwords).
    /// </summary>
    public required string ProviderUserId { get; init; }

    /// <summary>
    /// O email associado ao User (o Owner 2026-10-13: «o email tem de ficar
    /// associado ao user» — o rótulo visível do mockup 13_ADMIN: a coluna, a
    /// procura «Nome ou email», o editar). NUNCA uma credencial: a senha
    /// vive no provedor; o vínculo de login é o ProviderUserId.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>Availability state: stand-by preserves the User and disables current operational access.</summary>
    public bool IsStandBy { get; init; }

    /// <summary>The temporary-password bootstrap state (USERS.md §3).</summary>
    public bool RequiresPasswordChange { get; init; }

    /// <summary>The current Template's stable key, or null when the User has no current Template.</summary>
    public string? CurrentTemplateKey { get; init; }

    /// <summary>The current Template's label, or null — display only, resolved through the relationship.</summary>
    public string? CurrentTemplateName { get; init; }

    /// <summary>
    /// True when this User's email is the ADMIN email of the Definições
    /// (the canonical boundary marker for the Users view: such a User
    /// never receives a Template).
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// The User's creation moment in the app (a fact of the ficha do
    /// utilizador — when the user nasceu na app; always UTC).
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Command: create a normal DMO User inside the application (USERS.md §2)
/// — the ADMIN's act of the weekend condition («o user do chefe nasce NA
/// APP»). The email is the Auth-provider identity to provision (DMO
/// stores it NOWHERE — it is handed to the provider and shown to the
/// ADMIN once in the outcome); the optional Template key is the STABLE
/// identity of the current assignment created atomically with the User.
/// </summary>
public sealed record CreateDmoUser(
    string Name,
    string OperatorId,
    string Email,
    string? TemplateKey = null);

/// <summary>
/// Resultado tipado da criação de um User na app. Vocabulário FECHADO:
/// as recusas de invariante voltam ANTES de qualquer escrita (nem a
/// identidade é provisionada); as recusas do provider voltam tipadas sem
/// qualquer persistência DMO.
/// </summary>
public enum CreateDmoUserResult
{
    Success,

    /// <summary>O nome está em branco — facto humano obrigatório do User.</summary>
    MissingName,

    /// <summary>O nº de operador está em branco (USERS.md §1: até 4 dígitos).</summary>
    MissingOperator,

    /// <summary>O nº de operador contém caracteres que não são dígitos.</summary>
    InvalidOperator,

    /// <summary>O nº de operador tem mais de 4 dígitos.</summary>
    OperatorTooLong,

    /// <summary>
    /// O nº de operador já identifica outro User persistido (a unicidade
    /// do identificador é facto humano, verificada antes de qualquer
    /// escrita/provisionamento).
    /// </summary>
    DuplicateOperator,

    /// <summary>
    /// A TemplateKey indicada não resolve nenhuma Template persistida
    /// (a atribuição cria-se pela identidade ESTÁVEL, nunca pelo rótulo).
    /// </summary>
    UnknownTemplate,

    /// <summary>
    /// O email da identidade a provisionar está em branco — a identidade
    /// Supabase Auth é obrigatória (USERS.md §2); nenhuma escrita acontece.
    /// </summary>
    MissingEmail,

    /// <summary>
    /// O provider já conhece esse email (identidade existente) — recusa
    /// tipada do provisionamento; NENHUM User é persistido.
    /// </summary>
    EmailExists,

    /// <summary>
    /// O email já é o de um User da app (comparação sem maiúsculas). Recusado
    /// antes do provisionamento; o outcome diz de que User é o email.
    /// </summary>
    EmailInUseByUser,

    /// <summary>O provider recusou/falhou o provisionamento; NENHUM User é persistido.</summary>
    ProviderFailure,

    /// <summary>
    /// O email indicado é o email do ADMIN guardado nas Definições: o ADMIN
    /// nunca nasce como User normal. Recusado antes do provisionamento, por
    /// isso nenhuma identidade é criada no provider.
    /// </summary>
    AdminUserRefused
}

/// <summary>
/// Outcome da criação: o resultado tipado e, em caso de sucesso, o
/// registo de display do ADMIN — o email (nunca persistido; mostrado UMA
/// vez) e a password temporária mintada pelo backend (o token de entrega
/// ao próprio utilizador, USERS.md §3). A password NÃO é a definitiva: o
/// 1.º login força a escolha da password privada através do provider.
/// </summary>
public sealed record CreateDmoUserOutcome(
    CreateDmoUserResult Result,
    CreatedDmoUser? User = null,
    ExistingEmailOwner? EmailOwner = null);

/// <summary>O User que já tem o email pedido (só em EmailInUseByUser).</summary>
public sealed record ExistingEmailOwner(string Name, string OperatorId);

/// <summary>Registo de display da criação bem-sucedida (gestão do ADMIN, mostrado UMA vez).</summary>
public sealed record CreatedDmoUser(
    string Name,
    string OperatorId,
    string Email,
    string TemporaryPassword,
    string? TemplateKey,
    string? TemplateName);

/// <summary>
/// Command: reset the access of a persisted User (USERS.md §5),
/// identificado pelo seu nº de operador (facto humano único — os ids do
/// backend nunca saem nas folhas).
/// </summary>
public sealed record ResetUserAccess(string OperatorId);

/// <summary>
/// Resultado tipado do reset de acesso. Vocabulário FECHADO: o histórico
/// e a configuração de acesso NUNCA mudam por um reset (USERS.md L103).
/// </summary>
public enum ResetUserAccessResult
{
    Success,

    /// <summary>Nenhum User persistido com esse nº de operador.</summary>
    UserNotFound,

    /// <summary>
    /// Recusa tipada: o alvo é a identidade associada ao ADMIN corrente —
    /// o ADMIN nunca é alvo de stand-by/reset (boundary canónico).
    /// </summary>
    AdminRefused,

    /// <summary>A identidade não existe no provider (refusa tipada do provider).</summary>
    IdentityNotFound,

    /// <summary>O provider recusou/falhou — o estado do User fica intocado.</summary>
    ProviderFailure,

    /// <summary>O email já pertence a uma identidade; não associar silenciosamente.</summary>
    EmailAlreadyInUse
}

/// <summary>
/// Outcome do reset: em sucesso, a NOVA password temporária (token de
/// entrega do ADMIN ao utilizador; mostrado UMA vez) e o facto de que a
/// password anterior deixa de valer — a mudança para a password privada
/// volta a ser obrigatória no login seguinte (USERS.md §5).
/// </summary>
public sealed record ResetUserAccessOutcome(
    ResetUserAccessResult Result,
    string? OperatorId = null,
    string? TemporaryPassword = null);

/// <summary>
/// Command: set the availability state of a persisted User (USERS.md §1),
/// identificado pelo nº de operador (facto humano único).
/// </summary>
public sealed record SetUserStandBy(string OperatorId, bool IsStandBy);

/// <summary>
/// Resultado tipado do estado de disponibilidade. Vocabulário FECHADO:
/// o stand-by preserva o User, a atribuição corrente e todo o histórico.
/// </summary>
public enum SetUserStandByResult
{
    Success,

    /// <summary>Nenhum User persistido com esse nº de operador.</summary>
    UserNotFound,

    /// <summary>
    /// Recusa tipada: o alvo é a identidade associada ao ADMIN corrente —
    /// o ADMIN nunca é alvo de stand-by/reset (boundary canónico).
    /// </summary>
    AdminRefused
}

/// <summary>Outcome do estado de disponibilidade (resultado tipado, sem read model).</summary>
public sealed record SetUserStandByOutcome(SetUserStandByResult Result);

/// <summary>
/// Command: edit the current Access Template of a persisted User through
/// the SAME persisted relationship (TEMPLATES.md §"Access model").
/// TemplateKey null = REMOVER a atribuição corrente («Sem template»).
/// </summary>
public sealed record SetCurrentUserTemplate(string OperatorId, string? TemplateKey);

/// <summary>Resultado tipado da edição da atribuição corrente. Vocabulário FECHADO.</summary>
public enum SetCurrentUserTemplateResult
{
    Success,

    /// <summary>Nenhum User persistido com esse nº de operador.</summary>
    UserNotFound,

    /// <summary>
    /// Recusa tipada: a atribuição de Template RECUSA a identidade
    /// associada ao ADMIN corrente — o ADMIN nunca tem Template (D1).
    /// </summary>
    AdminRefused,

    /// <summary>A TemplateKey indicada não resolve nenhuma Template persistida.</summary>
    UnknownTemplate
}

/// <summary>
/// Outcome da edição da atribuição: o resultado tipado e, em sucesso, o
/// facto corrente da relação (TemplateKey/TemplateKey nula + rótulo) —
/// resolvido pela relação persistida, a única fonte.
/// </summary>
public sealed record SetCurrentUserTemplateOutcome(
    SetCurrentUserTemplateResult Result,
    string? TemplateKey = null,
    string? TemplateName = null);
