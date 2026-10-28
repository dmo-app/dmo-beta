using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// The Admin → Users administration service over the persisted normal-User
/// facts (see <see cref="IUserAdminService"/>). The current-Template fact
/// always resolves through the persisted User↔Template relationship —
/// never through the legacy <c>TemplateName</c> denormalized label. The
/// functional Users workflows (this build, USERS.md §1-§5): creation with
/// Auth-provider provisioning and the temporary-password bootstrap, the
/// access reset, the stand-by editing and the current-Template assignment
/// (the SAME relation the Templates view edits, authorized through
/// <see cref="IAdminAuthorizationService"/>).
///
/// Ownership boundary (USERS.md §6): DMO creates/manages the application
/// User and its access configuration and ASKS the Auth provider for the
/// provisioning/reset operations — the provider owns every credential;
/// DMO never becomes a password store.
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly DmoDbContext _dbContext;
    private readonly IUserProvisioningProvider _provisioningProvider;
    private readonly IAdminAuthorizationService _authorizationService;
    private readonly IAdminEmailSettings _adminEmailSettings;

    public UserAdminService(
        DmoDbContext dbContext,
        IUserProvisioningProvider provisioningProvider,
        IAdminAuthorizationService authorizationService,
        IAdminEmailSettings adminEmailSettings)
    {
        _dbContext = dbContext;
        _provisioningProvider = provisioningProvider;
        _authorizationService = authorizationService;
        _adminEmailSettings = adminEmailSettings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAdminSummary>> ListUsersAsync(
        CancellationToken cancellationToken = default)
    {
        // O ADMIN é o email guardado nas Definições: a marca de boundary
        // de cada linha é a comparação do email do User com esse valor.
        var adminEmail = await _adminEmailSettings.GetAdminEmailAsync(cancellationToken);

        var users = await OrderedUsers().ToListAsync(cancellationToken);

        return users.Select(u => Summarize(u, adminEmail)).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedList<UserAdminSummary>> ListManagedUsersPageAsync(
        int? pageNumber,
        int pageSize = ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        // A LISTA SÓ TEM UTILIZADORES GERIDOS (o Owner 2026-10-16: «admin
        // nao deve aparecer na lista»): o corte do email do ADMIN é feito
        // na query, para a contagem e as páginas baterem certo.
        var adminEmail = await _adminEmailSettings.GetAdminEmailAsync(cancellationToken);
        var query = OrderedUsers();
        if (adminEmail is not null)
        {
            var adminEmailLower = adminEmail.ToLowerInvariant();
            query = query.Where(u => u.Email == null || u.Email.Trim().ToLower() != adminEmailLower);
        }

        var page = await query.ToPagedListAsync(pageNumber, pageSize, cancellationToken);
        return new PagedList<UserAdminSummary>(
            page.Items.Select(u => Summarize(u, adminEmail)).ToList(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }

    private IQueryable<User> OrderedUsers() => _dbContext.Users
        .AsNoTracking()
        .Include(u => u.CurrentTemplateAssignment)
        .ThenInclude(a => a!.Template)
        .OrderBy(u => u.OperatorId);

    private static UserAdminSummary Summarize(User u, string? adminEmail)
    {
        var isAdmin = AdminEmailRule.IsAdminEmail(u.Email, adminEmail);

        return new UserAdminSummary
        {
            Id = u.Id,
            Name = u.Name,
            OperatorId = u.OperatorId,
            ProviderUserId = u.ProviderUserId,
            Email = u.Email,
            IsStandBy = u.IsStandBy,
            RequiresPasswordChange = u.RequiresPasswordChange,
            CurrentTemplateKey = isAdmin
                ? null
                : u.CurrentTemplateAssignment?.Template?.TemplateKey,
            CurrentTemplateName = isAdmin
                ? null
                : u.CurrentTemplateAssignment?.Template?.Name,
            IsAdmin = isAdmin,
            CreatedAt = u.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<CreateDmoUserOutcome> CreateDmoUserAsync(
        CreateDmoUser command,
        CancellationToken cancellationToken = default)
    {
        // 1. Invariáveis tipadas ANTES de qualquer escrita (nem a
        //    identidade é provisionada enquanto um facto humano falha).
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Refused(CreateDmoUserResult.MissingName);
        }

        var operatorId = command.OperatorId?.Trim();
        if (string.IsNullOrEmpty(operatorId))
        {
            return Refused(CreateDmoUserResult.MissingOperator);
        }

        // Nº de operador: até 4 DÍGITOS, zeros à esquerda preservados
        // (USERS.md §1) — facto humano do User, não credencial de login.
        if (operatorId.Any(c => !char.IsAsciiDigit(c)))
        {
            return Refused(CreateDmoUserResult.InvalidOperator);
        }

        if (operatorId.Length > 4)
        {
            return Refused(CreateDmoUserResult.OperatorTooLong);
        }

        // A unicidade do nº de operador é a última linha da verdade
        // persistida; a verificação prévia devolve a recusa tipada.
        if (await _dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.OperatorId == operatorId, cancellationToken))
        {
            return Refused(CreateDmoUserResult.DuplicateOperator);
        }

        // O EMAIL É OPCIONAL (Diogo, 26/10/2026,
        // cmsg_01K75FTs9bQnbhhH7qttpgraEMxzGkVktKoK7yyAw6RnoU: «uma conta
        // não precisa de email para login a não ser o admin»): a conta entra
        // pelo nº de operador. Sem email, a identidade no Auth provider nasce
        // com o endereço técnico <nº>@dmo.local (o mesmo que o login por nº
        // já usa nas contas sem email) e User.Email fica vazio.
        var email = TrimToNull(command.Email);
        if (email is not null)
        {
            // O email do ADMIN (Definições) nunca nasce como User normal. A
            // verificação corre ANTES do provisionamento: assim nenhuma
            // identidade fica criada no provider sem User no DMO.
            if (await IsAdminEmailAddressAsync(email, cancellationToken))
            {
                return Refused(CreateDmoUserResult.AdminUserRefused);
            }

            // O email já é de um User da app: recusa antes do provider, com o
            // dono, para a mensagem dizer qual é (a lista pode estar paginada).
            var normalizedEmail = email.ToLowerInvariant();
            var owner = await _dbContext.Users.AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == normalizedEmail)
                .Select(u => new ExistingEmailOwner(u.Name, u.OperatorId))
                .FirstOrDefaultAsync(cancellationToken);
            if (owner is not null)
            {
                return new CreateDmoUserOutcome(CreateDmoUserResult.EmailInUseByUser, EmailOwner: owner);
            }
        }

        // Atribuição opcional: resolve SEMPRE pela identidade estável
        // (TemplateKey), nunca pelo rótulo.
        var templateKey = TrimToNull(command.TemplateKey);
        Template? template = null;
        if (templateKey is not null)
        {
            template = await _dbContext.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateKey == templateKey, cancellationToken);

            if (template is null)
            {
                return Refused(CreateDmoUserResult.UnknownTemplate);
            }
        }

        // 2. Password temporária de bootstrap: mintada pelo backend (12
        //    caracteres), DEVOLVIDA no outcome — é o token de entrega do
        //    ADMIN ao próprio utilizador (USERS.md §3). O DMO nunca a
        //    persiste: o provider é dono da credencial.
        var temporaryPassword = TemporaryPasswordMinter.Mint();

        // 3. Provisionamento: o provider cria a identidade com a password
        //    temporária e a marca de mudança obrigatória. Recusas tipadas
        //    (EmailExists/ProviderFailure) voltam SEM qualquer User
        //    persistido. A partir daqui o pedido acaba mesmo que o browser
        //    saia: cortado durante a ida ao provider, a identidade ficava
        //    criada sem User e repetir dava «email já existe» (deep sweep,
        //    18/10/2026).
        var provisioning = await _provisioningProvider.CreateAsync(
            email ?? OperatorProviderEmail(operatorId), temporaryPassword, CancellationToken.None);
        if (provisioning.Status != UserProvisioningCreateStatus.Success
            || provisioning.ProviderUserId is not { } providerUserId)
        {
            return Refused(provisioning.Status switch
            {
                UserProvisioningCreateStatus.EmailExists => CreateDmoUserResult.EmailExists,
                _ => CreateDmoUserResult.ProviderFailure
            });
        }

        // 4. Persistência atómica ÚNICA: o User (factos de aplicação +
        //    estado bootstrap de password temporária) e a atribuição
        //    opcional de Template — a MESMA relação persistida que as duas
        //    vistas editam.
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperatorId = operatorId,
            ProviderUserId = providerUserId,
            // O EMAIL FICA ASSOCIADO AO USER (o Owner 2026-10-13: [eu disse
            // que nao guardava senhas, mas o email tem de ficar associado ao
            // user] - o rotulo visivel do mockup 13_ADMIN: a coluna, a
            // procura, a edicao; NUNCA credencial - a SENHA so no provider).
            Email = email,
            RequiresPasswordChange = true,
            IsStandBy = false
        };
        _dbContext.Users.Add(user);

        if (template is not null)
        {
            _dbContext.TemplateAssignments.Add(new TemplateAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TemplateId = template.Id
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // A failed DMO insert must not leave a new Auth identity behind.
            // Compensate by canonical provider id, not by email.
            await _provisioningProvider.DeleteAsync(providerUserId, CancellationToken.None);
            throw;
        }

        return new CreateDmoUserOutcome(
            CreateDmoUserResult.Success,
            new CreatedDmoUser(
                name,
                operatorId,
                email ?? string.Empty,
                temporaryPassword,
                template?.TemplateKey,
                template?.Name));
    }

    /// <inheritdoc />
    public async Task<ResetUserAccessOutcome> ResetUserAccessAsync(
        ResetUserAccess command,
        CancellationToken cancellationToken = default)
    {
        var operatorId = command.OperatorId?.Trim();
        if (string.IsNullOrEmpty(operatorId))
        {
            return new ResetUserAccessOutcome(ResetUserAccessResult.UserNotFound);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.OperatorId == operatorId, cancellationToken);

        if (user is null)
        {
            return new ResetUserAccessOutcome(ResetUserAccessResult.UserNotFound);
        }

        // O ADMIN corrente nunca é alvo de reset (boundary canónico).
        if (await IsAdminEmailAddressAsync(user.Email, cancellationToken))
        {
            return new ResetUserAccessOutcome(ResetUserAccessResult.AdminRefused);
        }

        // Nova password temporária mintada pelo backend — o token de
        // entrega do ADMIN (mostrado UMA vez; USERS.md §5).
        var temporaryPassword = TemporaryPasswordMinter.Mint();

        // PRIMEIRO A APP, DEPOIS O PROVIDER (issue #1 do dmo-Improvements e
        // relatório de backend de 28/10/2026, BE12; «sim» do Diogo, cartão
        // cmsg_01K75FTs9bQnbhhH7qttpgraXb8tBQWVyJXk8hiyb37wF1). As sessões
        // abertas antes do reset deixam de valer, também depois de o User
        // escolher a nova palavra-passe, e a mudança para a password privada
        // volta a ser obrigatória: isto grava-se ANTES de mudar a credencial.
        // Antes era ao contrário e uma gravação falhada depois do provider
        // deixava a palavra-passe mudada, a sessão antiga aberta e ninguém
        // via o PIN. Se a gravação falha, o provider não é chamado.
        var previousRequiresChange = user.RequiresPasswordChange;
        var previousSessionVersion = user.SessionVersion;
        user.RequiresPasswordChange = true;
        user.SessionVersion++;
        try
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            user.RequiresPasswordChange = previousRequiresChange;
            user.SessionVersion = previousSessionVersion;
            throw;
        }

        // O provider re-establisha a credencial: a anterior deixa de valer.
        // Uma recusa tipada do provider diz que nada mudou lá: o User volta
        // ao que estava. Uma falha sem resposta (exceção) pode ter mudado a
        // credencial: fica a sessão fechada e a mudança obrigatória, e o
        // ADMIN repõe outra vez. A partir daqui o pedido acaba mesmo que o
        // browser saia (deep sweep, 18/10/2026).
        async Task<ResetUserAccessOutcome> RecusaAsync(ResetUserAccessResult result)
        {
            user.RequiresPasswordChange = previousRequiresChange;
            user.SessionVersion = previousSessionVersion;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            return new ResetUserAccessOutcome(result);
        }

        var reset = await _provisioningProvider.ResetAsync(user.ProviderUserId, temporaryPassword, CancellationToken.None);
        if (reset.Status == UserProvisioningResetStatus.IdentityNotFound)
        {
            // Recover the existing DMO user, not a second application account.
            // Sem email não há como recriar a identidade aqui.
            var email = TrimToNull(user.Email);
            if (email is null)
                return await RecusaAsync(ResetUserAccessResult.IdentityNotFound);

            var created = await _provisioningProvider.CreateAsync(email, temporaryPassword, CancellationToken.None);
            if (created.Status != UserProvisioningCreateStatus.Success
                || string.IsNullOrWhiteSpace(created.ProviderUserId))
            {
                return await RecusaAsync(created.Status == UserProvisioningCreateStatus.EmailExists
                    ? ResetUserAccessResult.EmailAlreadyInUse
                    : ResetUserAccessResult.ProviderFailure);
            }

            var previousProviderId = user.ProviderUserId;
            user.ProviderUserId = created.ProviderUserId;
            try
            {
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                user.ProviderUserId = previousProviderId;
                // Compensate only the identity just created by this recovery.
                // Never delete an existing identity on an ordinary reset failure.
                await _provisioningProvider.DeleteAsync(created.ProviderUserId, CancellationToken.None);
                throw;
            }
        }
        else if (reset.Status != UserProvisioningResetStatus.Success)
        {
            return await RecusaAsync(ResetUserAccessResult.ProviderFailure);
        }

        return new ResetUserAccessOutcome(
            ResetUserAccessResult.Success,
            user.OperatorId,
            temporaryPassword);
    }

    /// <inheritdoc />
    public async Task<SetUserStandByOutcome> SetUserStandByAsync(
        SetUserStandBy command,
        CancellationToken cancellationToken = default)
    {
        var operatorId = command.OperatorId?.Trim();
        if (string.IsNullOrEmpty(operatorId))
        {
            return new SetUserStandByOutcome(SetUserStandByResult.UserNotFound);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.OperatorId == operatorId, cancellationToken);

        if (user is null)
        {
            return new SetUserStandByOutcome(SetUserStandByResult.UserNotFound);
        }

        // O ADMIN corrente nunca é alvo de stand-by (boundary canónico).
        if (await IsAdminEmailAddressAsync(user.Email, cancellationToken))
        {
            return new SetUserStandByOutcome(SetUserStandByResult.AdminRefused);
        }

        // Stand-by preserva o User E a atribuição corrente (USERS.md §1) —
        // desliga o acesso operacional corrente, nunca apaga configuração
        // nem história. A resolução de acesso efectivo já lê o estado.
        user.IsStandBy = command.IsStandBy;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetUserStandByOutcome(SetUserStandByResult.Success);
    }

    /// <inheritdoc />
    public async Task<DeleteUserOutcome> DeleteUserAsync(
        DeleteUser command,
        CancellationToken cancellationToken = default)
    {
        var operatorId = command.OperatorId?.Trim();
        if (string.IsNullOrEmpty(operatorId))
        {
            return DeleteUserOutcome.Refused(DeleteUserResult.UserNotFound);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.OperatorId == operatorId, cancellationToken);

        if (user is null)
        {
            return DeleteUserOutcome.Refused(DeleteUserResult.UserNotFound);
        }

        // O ADMIN corrente nunca é alvo (o boundary canónico — o mesmo do
        // stand-by e do reset).
        if (await IsAdminEmailAddressAsync(user.Email, cancellationToken))
        {
            return DeleteUserOutcome.Refused(DeleteUserResult.AdminRefused);
        }

        // The Auth delete is first: on provider failure DMO remains intact.
        // An already-missing identity (or no stored identity at all) is
        // reported separately so the ADMIN knows nothing was deleted there.
        // A partir daqui o pedido acaba mesmo que o browser saia: cortado a
        // meio, a identidade desaparecia e o User ficava na lista (deep
        // sweep, 18/10/2026).
        var identityStatus = string.IsNullOrWhiteSpace(user.ProviderUserId)
            ? UserProvisioningDeleteStatus.IdentityNotFound
            : (await _provisioningProvider.DeleteAsync(user.ProviderUserId, CancellationToken.None)).Status;
        if (identityStatus == UserProvisioningDeleteStatus.ProviderFailure)
            return DeleteUserOutcome.Refused(DeleteUserResult.ProviderFailure);

        // Assignments cascade. Historical operator strings are not rewritten.
        // If SaveChanges fails, retry is safe (provider will return not found).
        _dbContext.Users.Remove(user);
        try
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return DeleteUserOutcome.Refused(DeleteUserResult.PersistenceFailure);
        }

        return new DeleteUserOutcome(
            identityStatus == UserProvisioningDeleteStatus.IdentityNotFound
                ? DeleteUserResult.SuccessIdentityAlreadyMissing
                : DeleteUserResult.Success,
            user.OperatorId);
    }

    /// <inheritdoc />
    public async Task<SetCurrentUserTemplateOutcome> SetCurrentUserTemplateAsync(
        SetCurrentUserTemplate command,
        CancellationToken cancellationToken = default)
    {
        var operatorId = command.OperatorId?.Trim();
        if (string.IsNullOrEmpty(operatorId))
        {
            return new SetCurrentUserTemplateOutcome(SetCurrentUserTemplateResult.UserNotFound);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.OperatorId == operatorId, cancellationToken);

        if (user is null)
        {
            return new SetCurrentUserTemplateOutcome(SetCurrentUserTemplateResult.UserNotFound);
        }

        // Autorização canónica ANTES de qualquer escrita: a atribuição de
        // Template RECUSA a identidade ADMIN-associated (o ADMIN nunca tem
        // Template — D1, IAdminAuthorizationService).
        var decision = await _authorizationService.AuthorizeTemplateAssignmentAsync(user.Id, cancellationToken);
        if (decision != TemplateAssignmentAuthorization.Allowed)
        {
            return new SetCurrentUserTemplateOutcome(
                decision == TemplateAssignmentAuthorization.RefusedAdminBoundary
                    ? SetCurrentUserTemplateResult.AdminRefused
                    : SetCurrentUserTemplateResult.UserNotFound);
        }

        // «Sem template» = REMOVER a atribuição corrente (linha ausente =
        // sem Template; USERS.md §1 "current Access Template or none").
        var templateKey = TrimToNull(command.TemplateKey);
        if (templateKey is null)
        {
            var current = await _dbContext.TemplateAssignments
                .FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);

            if (current is not null)
            {
                _dbContext.TemplateAssignments.Remove(current);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new SetCurrentUserTemplateOutcome(SetCurrentUserTemplateResult.Success);
        }

        // A Template resolve SEMPRE pela identidade estável (TemplateKey).
        var template = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey, cancellationToken);

        if (template is null)
        {
            return new SetCurrentUserTemplateOutcome(SetCurrentUserTemplateResult.UnknownTemplate);
        }

        // UMA relação, duas vistas: a MESMA linha de atribuição corrente é
        // editada in-place (atualiza o TemplateId — nunca uma segunda
        // atribuição concorrente; o índice único de UserId mantém 0..1).
        // Sem linha existente, a relação nasce.
        var assignment = await _dbContext.TemplateAssignments
            .FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);

        if (assignment is null)
        {
            _dbContext.TemplateAssignments.Add(new TemplateAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TemplateId = template.Id
            });
        }
        else
        {
            assignment.TemplateId = template.Id;
            assignment.AssignedAtUtc = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetCurrentUserTemplateOutcome(
            SetCurrentUserTemplateResult.Success,
            template.TemplateKey,
            template.Name);
    }

    /// <summary>
    /// O endereço técnico da identidade de uma conta sem email: o mesmo que
    /// o login por nº de operador usa (DmoSignInService).
    /// </summary>
    private static string OperatorProviderEmail(string operatorId) => $"{operatorId}@dmo.local";

    /// <summary>
    /// Whether an email IS the ADMIN's email — the value kept in the
    /// Definições (chave canónica ModuleSettingKeys.AdminEmail; o Owner
    /// 2026-10-16: o ADMIN é apenas um email). No setting/no User email →
    /// not the ADMIN. Same value the login and the session validator
    /// compare.
    /// </summary>
    private async Task<bool> IsAdminEmailAddressAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        var adminEmail = await _adminEmailSettings.GetAdminEmailAsync(cancellationToken);
        return AdminEmailRule.IsAdminEmail(email, adminEmail);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CreateDmoUserOutcome Refused(CreateDmoUserResult result) => new(result);
}
