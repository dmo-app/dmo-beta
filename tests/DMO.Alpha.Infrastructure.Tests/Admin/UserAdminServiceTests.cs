using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// Prova a leitura de fundação da vista Admin → Users (Step B) sobre os
/// factos de aplicação do User normal (USERS.md §1): nome, identificador
/// operador/BA Glass (zeros à esquerda preservados), Template corrente ou
/// nenhum, estado de disponibilidade. O facto "Template corrente" resolve
/// SEMPRE pela relação persistida User↔Template — nunca pela coluna
/// denormalizada legada User.TemplateName (que pode estar desatualizada) —
/// e o marcador ADMIN-associated projeta o boundary canónico para a vista.
/// </summary>
public sealed class UserAdminServiceTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly InMemoryAuthenticationProvider _authProvider;
    private readonly UserAdminService _service;

    public UserAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // O seam determinístico de provisionamento sobre o MESMO conjunto de
        // contas do seam de autenticação (Step D: o serviço estendeu o
        // construtor da fundação com o provisionamento e a autorização).
        _authProvider = new InMemoryAuthenticationProvider();
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _service = new UserAdminService(
            _db,
            new InMemoryUserProvisioningProvider(_authProvider),
            new AdminAuthorizationService(_db, new AdminEmailSettings(_db)),
            new AdminEmailSettings(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<User> SeedUserAsync(
        string operatorId,
        string providerUserId,
        bool isStandBy = false,
        string? legacyTemplateName = null,
        string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = $"Operador {operatorId}",
            ProviderUserId = providerUserId,
            IsStandBy = isStandBy,
            TemplateName = legacyTemplateName,
            Email = email
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    /// <summary>Semeia o email do ADMIN nas Definições (o ADMIN é o email).</summary>
    private async Task SeedAdminEmailAsync(string adminEmail)
    {
        _db.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = adminEmail
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListUsers_EmptyPersistence_ReturnsEmptyList()
    {
        Assert.Empty(await _service.ListUsersAsync());
    }

    [Fact]
    public async Task ListUsers_ProjectsApplicationFactsOrderedByOperatorId()
    {
        await SeedUserAsync("0042", "provider-user-0042");
        await SeedUserAsync("0100", "provider-user-0100");
        await SeedUserAsync("1234", "provider-user-1234", isStandBy: true);

        var users = await _service.ListUsersAsync();

        // Ordenação determinística pelo identificador de operador (string,
        // zeros à esquerda preservados).
        Assert.Equal(["0042", "0100", "1234"], users.Select(u => u.OperatorId).ToArray());
        Assert.Equal("Operador 1234", users.Single(u => u.OperatorId == "1234").Name);
        Assert.True(users.Single(u => u.OperatorId == "1234").IsStandBy);
        Assert.False(users.Single(u => u.OperatorId == "0042").IsStandBy);
        Assert.Equal("provider-user-0100", users.Single(u => u.OperatorId == "0100").ProviderUserId);
    }

    [Fact]
    public async Task ListUsers_CurrentTemplateResolvesThroughTheRelationship_NotTheLegacyLabel()
    {
        var user = await SeedUserAsync("2345", "provider-user-2345", legacyTemplateName: "Rótulo Legado Desatualizado");

        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = "tpl-responsavel-operacional",
            Name = "Responsável Operacional"
        };
        _db.Templates.Add(template);
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        await _db.SaveChangesAsync();

        var projected = (await _service.ListUsersAsync()).Single();

        // A relação persistida é a autoridade — o rótulo legado
        // denormalizado NÃO é a fonte do facto "Template corrente".
        Assert.Equal("tpl-responsavel-operacional", projected.CurrentTemplateKey);
        Assert.Equal("Responsável Operacional", projected.CurrentTemplateName);
        Assert.NotEqual("Rótulo Legado Desatualizado", projected.CurrentTemplateName);
    }

    [Fact]
    public async Task ListUsers_NoAssignment_CurrentTemplateIsNull()
    {
        await SeedUserAsync("3456", "provider-user-3456", legacyTemplateName: "Rótulo Órfão");

        var projected = (await _service.ListUsersAsync()).Single();

        Assert.Null(projected.CurrentTemplateKey);
        Assert.Null(projected.CurrentTemplateName);
    }

    [Fact]
    public async Task ListManagedUsersPage_ExcludesAdminEmailAndPagesInOperatorOrder()
    {
        await SeedAdminEmailAsync("admin@example.test");
        await SeedUserAsync("0000", "provider-admin", email: " ADMIN@example.test ");
        for (var i = 1; i <= 30; i++)
        {
            await SeedUserAsync(i.ToString("0000"), $"provider-user-{i}");
        }

        var page1 = await _service.ListManagedUsersPageAsync(1);
        var page2 = await _service.ListManagedUsersPageAsync(2);

        // O ADMIN não conta nem aparece; 30 geridos = 25 + 5.
        Assert.Equal(30, page1.TotalCount);
        Assert.Equal(25, page1.Items.Count);
        Assert.Equal("0001", page1.Items[0].OperatorId);
        Assert.Equal(["0026", "0027", "0028", "0029", "0030"], page2.Items.Select(u => u.OperatorId).ToArray());
        Assert.DoesNotContain(page1.Items.Concat(page2.Items), u => u.IsAdmin);
    }

    [Fact]
    public async Task ListUsers_AdminEmail_FlaggedWithoutTemplateFacts()
    {
        // O ADMIN é o email das Definições (o Owner 2026-10-16): a linha
        // do User cujo email é o do ADMIN projeta o marcador de boundary —
        // sem Template, sem factos geríveis.
        await SeedAdminEmailAsync("admin@example.test");
        await SeedUserAsync("9000", "provider-admin-1", email: "admin@example.test");
        await SeedUserAsync("5678", "provider-user-5678");

        var users = await _service.ListUsersAsync();

        var adminUser = users.Single(u => u.OperatorId == "9000");
        Assert.True(adminUser.IsAdmin);
        Assert.Null(adminUser.CurrentTemplateKey);
        Assert.Null(adminUser.CurrentTemplateName);

        var normalUser = users.Single(u => u.OperatorId == "5678");
        Assert.False(normalUser.IsAdmin);
    }

    // ------------------------------------------------------------------
    // CreateDmoUser (USERS.md §2/§3): invariáveis tipadas antes de
    // qualquer escrita, password temporária mintada pelo backend,
    // provisionamento no provider e persistência atómica.
    // ------------------------------------------------------------------

    private async Task<Template> SeedTemplateAsync(string templateKey, string name, params string[] capabilities)
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateKey,
            Name = name,
            Capabilities = capabilities
        };

        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    /// <summary>Provisionamento scripted: devolve resultados escolhidos pelo teste.</summary>
    private sealed class ScriptedProvisioningProvider : IUserProvisioningProvider
    {
        public UserProvisioningCreateResult CreateResult { get; set; } =
            UserProvisioningCreateResult.ProviderFailure();

        public UserProvisioningResetResult ResetResult { get; set; } =
            UserProvisioningResetResult.ProviderFailure();

        public int CreateCalls { get; private set; }

        public Task<UserProvisioningCreateResult> CreateAsync(
            string email, string temporaryPassword, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(CreateResult);
        }

        public Task<UserProvisioningResetResult> ResetAsync(
            string providerUserId, string temporaryPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(ResetResult);

        public UserProvisioningDeleteResult DeleteResult { get; set; } =
            UserProvisioningDeleteResult.ProviderFailure();

        public Task<UserProvisioningDeleteResult> DeleteAsync(
            string providerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(DeleteResult);
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("test-persistence-failure");
        }
    }

    private sealed class ToggleSaveFailureInterceptor : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("test-recovery-save-failure");
            return ValueTask.FromResult(result);
        }
    }

    private static CreateDmoUser ValidCreateCommand(
        string operatorId = "1234",
        string email = "novo.utilizador@empresa.pt",
        string? templateKey = null) =>
        new("Novo Utilizador", operatorId, email, templateKey);

    [Fact]
    public async Task CreateDmoUser_ValidCommand_ProvisionsPersistsAtomicallyAndReturnsTheBootstrapToken()
    {
        var template = await SeedTemplateAsync(
            "tpl-responsavel-operacional",
            "Responsável Operacional",
            DmoCapabilities.JobOnCreate,
            DmoCapabilities.ControloApprove);

        var outcome = await _service.CreateDmoUserAsync(ValidCreateCommand(templateKey: template.TemplateKey));

        // Sucesso: o outcome devolve o par de entrega do ADMIN — o email
        // (nunca persistido) e o PIN temporário mintado (4 números).
        Assert.Equal(CreateDmoUserResult.Success, outcome.Result);
        Assert.NotNull(outcome.User);
        Assert.Equal("novo.utilizador@empresa.pt", outcome.User!.Email);
        Assert.Equal("1234", outcome.User.OperatorId);
        Assert.Equal(template.TemplateKey, outcome.User.TemplateKey);
        Assert.Equal("Responsável Operacional", outcome.User.TemplateName);
        Assert.Matches("^[0-9]{4}$", outcome.User.TemporaryPassword);

        // Persistência atómica: o User + a atribuição numa única escrita.
        var user = _db.Users.Include(u => u.CurrentTemplateAssignment).ThenInclude(a => a!.Template)
            .Single(u => u.OperatorId == "1234");
        Assert.Equal("Novo Utilizador", user.Name);
        Assert.False(user.IsStandBy);

        // O bootstrap (USERS.md §3): o User nasce em estado de mudança
        // obrigatória — o 1.º login força a password privada.
        Assert.True(user.RequiresPasswordChange);

        // A identidade é a provisionada (ProviderUserId do provider) — e
        // o EMAIL não é gravado em lado nenhum (nenhuma coluna de email
        // existe; a única coluna de identidade é a do provider).
        var provisionedIdentity = _authProvider.AuthenticateAsync(
            "novo.utilizador@empresa.pt",
            outcome.User.TemporaryPassword);
        var authResult = await provisionedIdentity;
        Assert.True(authResult.Success);
        Assert.True(authResult.IsTemporaryPassword);
        Assert.Equal(user.ProviderUserId, authResult.ProviderUserId);

        // Atribuição pela relação persistida — SEM TemplateId em Users.
        Assert.Equal(template.Id, user.CurrentTemplateAssignment!.TemplateId);
        Assert.Equal("tpl-responsavel-operacional", user.CurrentTemplateAssignment.Template!.TemplateKey);
    }

    [Fact]
    public async Task CreateDmoUser_WithoutTemplate_PersistsUserWithoutAssignment()
    {
        var outcome = await _service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.Success, outcome.Result);
        Assert.Null(outcome.User!.TemplateKey);

        var user = _db.Users.Single(u => u.OperatorId == "1234");
        Assert.Empty(_db.TemplateAssignments.ToList());
    }

    [Theory]
    [InlineData("", "1234", "a@b.pt", CreateDmoUserResult.MissingName)]
    [InlineData("   ", "1234", "a@b.pt", CreateDmoUserResult.MissingName)]
    [InlineData("Nome", "", "a@b.pt", CreateDmoUserResult.MissingOperator)]
    [InlineData("Nome", "12a4", "a@b.pt", CreateDmoUserResult.InvalidOperator)]
    [InlineData("Nome", "12 4", "a@b.pt", CreateDmoUserResult.InvalidOperator)]
    [InlineData("Nome", "12345", "a@b.pt", CreateDmoUserResult.OperatorTooLong)]
    [InlineData("Nome", "1234", "", CreateDmoUserResult.MissingEmail)]
    [InlineData("Nome", "1234", "   ", CreateDmoUserResult.MissingEmail)]
    public async Task CreateDmoUser_InvariantViolations_AreTypedRefusalsBeforeAnyWrite(
        string name, string operatorId, string email, CreateDmoUserResult expected)
    {
        var outcome = await _service.CreateDmoUserAsync(new CreateDmoUser(name, operatorId, email));

        // Recusa tipada ANTES de qualquer escrita: nem User persistido,
        // nem identidade provisionada no seam.
        Assert.Equal(expected, outcome.Result);
        Assert.Null(outcome.User);
        Assert.Empty(_db.Users.ToList());
        Assert.False(await _authProvider.IdentityExistsAsync("a@b.pt"));
    }

    [Fact]
    public async Task CreateDmoUser_DuplicateOperator_IsRefusedBeforeProvisioning()
    {
        await SeedUserAsync("1234", "provider-user-existing");

        var outcome = await _service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.DuplicateOperator, outcome.Result);

        // A unicidade do identificador humano é verificada ANTES do
        // provisionamento: nenhuma identidade foi criada no provider.
        Assert.False(await _authProvider.IdentityExistsAsync("novo.utilizador@empresa.pt"));
        Assert.Single(_db.Users.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_UnknownTemplateKey_IsRefusedBeforeProvisioning()
    {
        var outcome = await _service.CreateDmoUserAsync(ValidCreateCommand(templateKey: "tpl-inexistente"));

        Assert.Equal(CreateDmoUserResult.UnknownTemplate, outcome.Result);
        Assert.Empty(_db.Users.ToList());
        Assert.False(await _authProvider.IdentityExistsAsync("novo.utilizador@empresa.pt"));
    }

    [Fact]
    public async Task CreateDmoUser_EmailAlreadyAProviderIdentity_EmailExistsPersistsNothing()
    {
        // O provider já conhece o email (identidade existente): recusa
        // tipada do provisionamento — NENHUM User é persistido.
        _authProvider.AddAccount(
            "novo.utilizador@empresa.pt",
            "provider-existing-identity",
            "JaExiste1!",
            "novo.utilizador@empresa.pt");

        var outcome = await _service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.EmailExists, outcome.Result);
        Assert.Empty(_db.Users.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_EmailOfAnotherUser_RefusedBeforeProvisioningWithOwner()
    {
        // O email já é de um User da app (com outras maiúsculas): recusa
        // antes do provider, a dizer de quem é.
        await SeedUserAsync("2165", "provider-chefe", email: "Novo.Utilizador@Empresa.pt");
        var scripted = new ScriptedProvisioningProvider
        {
            CreateResult = UserProvisioningCreateResult.Success("nunca-usado")
        };
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));

        var outcome = await service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.EmailInUseByUser, outcome.Result);
        Assert.Equal(new ExistingEmailOwner("Operador 2165", "2165"), outcome.EmailOwner);
        Assert.Equal(0, scripted.CreateCalls);
        Assert.Single(_db.Users.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_ProviderFailure_PersistsNothing()
    {
        var scripted = new ScriptedProvisioningProvider
        {
            CreateResult = UserProvisioningCreateResult.ProviderFailure()
        };
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));

        var outcome = await service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.ProviderFailure, outcome.Result);
        Assert.Empty(_db.Users.ToList());
        Assert.Empty(_db.TemplateAssignments.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_EmailIsTheAdmins_RefusedBeforeProvisioning_PersistsNothing()
    {
        // O email do comando É o email do ADMIN das Definições (por
        // exemplo, guardado antes de a pessoa ter identidade no provider):
        // a recusa acontece ANTES do provisionamento, por isso o provider
        // nem é chamado e nenhuma identidade órfã fica criada.
        await SeedAdminEmailAsync("novo.utilizador@empresa.pt");

        var scripted = new ScriptedProvisioningProvider
        {
            CreateResult = UserProvisioningCreateResult.Success("provider-admin-current")
        };
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));

        var outcome = await service.CreateDmoUserAsync(ValidCreateCommand());

        Assert.Equal(CreateDmoUserResult.AdminUserRefused, outcome.Result);
        Assert.Equal(0, scripted.CreateCalls);
        Assert.Empty(_db.Users.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_EmailIsTheAdmins_NoIdentityCreatedInTheProvider()
    {
        // O mesmo caso pelo seam em memória: a identidade do futuro ADMIN
        // não é criada com uma password temporária que ninguém conhece.
        await SeedAdminEmailAsync("Futuro.Admin@empresa.pt");

        var outcome = await _service.CreateDmoUserAsync(
            ValidCreateCommand(email: " futuro.admin@empresa.pt "));

        Assert.Equal(CreateDmoUserResult.AdminUserRefused, outcome.Result);
        Assert.False(await _authProvider.IdentityExistsAsync("futuro.admin@empresa.pt"));
        Assert.Empty(_db.Users.ToList());
    }

    [Fact]
    public async Task CreateDmoUser_PersistenceFailureAfterProvisioning_PersistsNothing()
    {
        // Falha parcial REAL: o provisionamento sucede (a identidade fica
        // no provider — facto do provider, não do DMO) e a escrita DMO
        // falha: NENHUM User é persistido. A recuperação é operacional
        // (repetir com outro email), nunca um User órfão na app.
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new ThrowingSaveChangesInterceptor())
            .Options;

        await using var db = new DmoDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new UserAdminService(db, new InMemoryUserProvisioningProvider(_authProvider), new AdminAuthorizationService(db, new AdminEmailSettings(db)), new AdminEmailSettings(db));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDmoUserAsync(ValidCreateCommand()));

        Assert.Empty(db.Users.ToList());
        Assert.Empty(db.TemplateAssignments.ToList());
    }

    [Fact]
    public async Task DeleteUser_RemovesAuthAndAssignment_AllowsSameEmailAndNumberAgain()
    {
        var template = await SeedTemplateAsync("tpl-delete", "Template", DmoCapabilities.JobOnCreate);
        var command = new CreateDmoUser("Recriável", "0042", "recriar@empresa.pt", template.TemplateKey);
        var created = await _service.CreateDmoUserAsync(command);
        Assert.Equal(CreateDmoUserResult.Success, created.Result);
        var firstProviderId = _db.Users.Single(u => u.OperatorId == "0042").ProviderUserId;
        Assert.Single(_db.TemplateAssignments);

        var deleted = await _service.DeleteUserAsync(new DeleteUser("0042"));
        Assert.Equal(DeleteUserResult.Success, deleted.Result);
        Assert.Empty(_db.Users);
        Assert.Empty(_db.TemplateAssignments);
        Assert.False(await _authProvider.IdentityExistsAsync("recriar@empresa.pt"));

        var recreated = await _service.CreateDmoUserAsync(command);
        Assert.Equal(CreateDmoUserResult.Success, recreated.Result);
        Assert.NotEqual(firstProviderId, _db.Users.Single().ProviderUserId);
        Assert.Single(_db.TemplateAssignments);
    }

    [Fact]
    public async Task DeleteUser_ProviderFailure_KeepsDmoUserAndAssignment()
    {
        var user = await SeedUserAsync("0077", "provider-failing-delete");
        var tpl = await SeedTemplateAsync("tpl-keep", "Keep", DmoCapabilities.JobOnCreate);
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = tpl.Id
        });
        await _db.SaveChangesAsync();
        var scripted = new ScriptedProvisioningProvider();
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
        var result = await service.DeleteUserAsync(new DeleteUser("0077"));
        Assert.Equal(DeleteUserResult.ProviderFailure, result.Result);
        Assert.Single(_db.Users);
        Assert.Single(_db.TemplateAssignments);
    }

    [Fact]
    public async Task DeleteUser_AlreadyMissingAuthIdentity_IsRetryable()
    {
        await SeedUserAsync("0078", "missing-provider-identity");
        var result = await _service.DeleteUserAsync(new DeleteUser("0078"));
        // H22-01: «já não existia» distingue-se de «apagada no Supabase».
        Assert.Equal(DeleteUserResult.SuccessIdentityAlreadyMissing, result.Result);
        Assert.Empty(_db.Users);
    }

    [Fact]
    public async Task DeleteUser_EmptyProviderUserId_RemovesTheRowWithoutCallingTheProvider()
    {
        await SeedUserAsync("0079", "");
        var scripted = new ScriptedProvisioningProvider();
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
        var result = await service.DeleteUserAsync(new DeleteUser("0079"));
        Assert.Equal(DeleteUserResult.SuccessIdentityAlreadyMissing, result.Result);
        Assert.Empty(_db.Users);
    }

    // ------------------------------------------------------------------
    // ResetUserAccess (USERS.md §5): nova password temporária, mudança
    // obrigatória re-armada, histórico/configuração intocados.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResetUserAccess_GeneratesNewTemporaryPasswordAndReArmsTheForcedChange()
    {
        await SeedUserAsync("1234", "provider-user-reset");
        _authProvider.AddAccount("reset.user@empresa.pt", "provider-user-reset", "PasswordAntiga1!");

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("1234"));

        Assert.Equal(ResetUserAccessResult.Success, outcome.Result);
        Assert.Equal("1234", outcome.OperatorId);
        Assert.NotNull(outcome.TemporaryPassword);
        Assert.Matches("^[0-9]{4}$", outcome.TemporaryPassword!);
        Assert.NotEqual("PasswordAntiga1!", outcome.TemporaryPassword);

        // O estado bootstrap volta a verdadeiro.
        Assert.True(_db.Users.Single(u => u.OperatorId == "1234").RequiresPasswordChange);

        // A password ANTERIOR deixa de valer; a nova temporária autentica
        // com a marca de temporária (mudança obrigatória no login seguinte).
        var oldCredential = await _authProvider.AuthenticateAsync("reset.user@empresa.pt", "PasswordAntiga1!");
        Assert.False(oldCredential.Success);

        var newCredential = await _authProvider.AuthenticateAsync("reset.user@empresa.pt", outcome.TemporaryPassword);
        Assert.True(newCredential.Success);
        Assert.True(newCredential.IsTemporaryPassword);
    }

    [Fact]
    public async Task ResetUserAccess_DoesNotChangeHistoryOrAccessConfiguration()
    {
        var user = await SeedUserAsync("2345", "provider-user-reset-2");
        var template = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        await _db.SaveChangesAsync();
        _authProvider.AddAccount("reset2.user@empresa.pt", "provider-user-reset-2", "PasswordAntiga2!");

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("2345"));

        // USERS.md L103: o reset NÃO muda histórico nem configuração —
        // nome, operador, template corrente e stand-by ficam exatamente
        // como estão; apenas a password temporária muda.
        Assert.Equal(ResetUserAccessResult.Success, outcome.Result);

        var after = _db.Users
            .Include(u => u.CurrentTemplateAssignment)
            .Single(u => u.OperatorId == "2345");
        Assert.Equal("Operador 2345", after.Name);
        Assert.Equal(template.Id, after.CurrentTemplateAssignment!.TemplateId);
        Assert.False(after.IsStandBy);
    }

    [Fact]
    public async Task ResetUserAccess_UnknownOperator_UserNotFound()
    {
        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("9999"));

        Assert.Equal(ResetUserAccessResult.UserNotFound, outcome.Result);
        Assert.Null(outcome.TemporaryPassword);
    }

    [Fact]
    public async Task ResetUserAccess_AdminAssociatedIdentity_AdminRefused()
    {
        await SeedAdminEmailAsync("admin.reset@empresa.pt");
        await SeedUserAsync("9000", "provider-admin-reset", email: "admin.reset@empresa.pt");
        _authProvider.AddAccount("admin.reset@empresa.pt", "provider-admin-reset", "AdminPassword1!");

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("9000"));

        // O ADMIN corrente nunca é alvo de reset (APROVADO) — e a sua
        // credencial fica intocada.
        Assert.Equal(ResetUserAccessResult.AdminRefused, outcome.Result);
        var credential = await _authProvider.AuthenticateAsync("admin.reset@empresa.pt", "AdminPassword1!");
        Assert.True(credential.Success);
    }

    [Fact]
    public async Task ResetUserAccess_MissingProviderIdentity_IdentityNotFoundLeavesUserUntouched()
    {
        // O User existe na app mas a identidade já não existe no provider:
        // recusa tipada, estado do User intocado.
        await SeedUserAsync("3456", "provider-user-ghost");

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("3456"));

        Assert.Equal(ResetUserAccessResult.IdentityNotFound, outcome.Result);
        Assert.False(_db.Users.Single(u => u.OperatorId == "3456").RequiresPasswordChange);
    }

    [Fact]
    public async Task ResetUserAccess_MissingIdentityWithEmail_RecoversSameUserAndTemplate()
    {
        var user = await SeedUserAsync("0061", "missing-auth-id");
        user.Email = "recuperar@empresa.pt";
        var template = await SeedTemplateAsync("tpl-recuperar", "Operacional", DmoCapabilities.ControloCreate);
        _db.TemplateAssignments.Add(new TemplateAssignment { Id=Guid.NewGuid(), UserId=user.Id, TemplateId=template.Id });
        await _db.SaveChangesAsync();
        var id = user.Id;
        var result = await _service.ResetUserAccessAsync(new ResetUserAccess("0061"));
        Assert.Equal(ResetUserAccessResult.Success, result.Result);
        var saved = Assert.Single(_db.Users);
        Assert.Equal(id, saved.Id);
        Assert.NotEqual("missing-auth-id", saved.ProviderUserId);
        Assert.Equal(template.Id, Assert.Single(_db.TemplateAssignments).TemplateId);
        Assert.Equal("Operador 0061", saved.Name);
        Assert.False(saved.IsStandBy);
        Assert.True(saved.RequiresPasswordChange);
        var auth = await _authProvider.AuthenticateAsync(user.Email, result.TemporaryPassword!);
        Assert.True(auth.Success);
        Assert.True(auth.IsTemporaryPassword);
        Assert.Equal(saved.ProviderUserId, auth.ProviderUserId);
        var second = await _service.ResetUserAccessAsync(new ResetUserAccess("0061"));
        Assert.Equal(ResetUserAccessResult.Success, second.Result);
        Assert.Equal(saved.ProviderUserId, Assert.Single(_db.Users).ProviderUserId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetUserAccess_RecoveryCreateFailure_DoesNotRebindUser(bool duplicate)
    {
        var user = await SeedUserAsync("0062", "missing-auth-id");
        user.Email = "conflito@empresa.pt";
        await _db.SaveChangesAsync();
        var provider = new ScriptedProvisioningProvider {
            ResetResult = UserProvisioningResetResult.IdentityNotFound(),
            CreateResult = duplicate ? UserProvisioningCreateResult.EmailExists() : UserProvisioningCreateResult.ProviderFailure()
        };
        var service = new UserAdminService(_db, provider, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
        var result = await service.ResetUserAccessAsync(new ResetUserAccess("0062"));
        Assert.Equal(duplicate ? "EmailAlreadyInUse" : "ProviderFailure", result.Result.ToString());
        Assert.Equal("missing-auth-id", user.ProviderUserId);
        Assert.False(user.RequiresPasswordChange);
        Assert.Null(result.TemporaryPassword);
    }

    [Fact]
    public async Task ResetUserAccess_RecoveryPersistenceFailure_DeletesOnlyNewIdentityAndKeepsOriginalUser()
    {
        var failure = new ToggleSaveFailureInterceptor();
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(failure).Options;
        await using var db = new DmoDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var user = new User { Id=Guid.NewGuid(), Name="Recuperar", OperatorId="0063",
            Email="falha.recuperacao@empresa.pt", ProviderUserId="previous-missing-id" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var id = user.Id;
        failure.Fail = true;
        var service = new UserAdminService(db, new InMemoryUserProvisioningProvider(_authProvider), new AdminAuthorizationService(db, new AdminEmailSettings(db)), new AdminEmailSettings(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetUserAccessAsync(new ResetUserAccess("0063")));
        Assert.False(await _authProvider.IdentityExistsAsync(user.Email));
        db.ChangeTracker.Clear();
        var saved = Assert.Single(db.Users);
        Assert.Equal(id, saved.Id);
        Assert.Equal("previous-missing-id", saved.ProviderUserId);
        Assert.False(saved.RequiresPasswordChange);
    }

    [Fact]
    public async Task ResetUserAccess_ProviderFailure_TypedRefusalLeavesUserUntouched()
    {
        var user = await SeedUserAsync("4567", "provider-user-providerfail");

        var scripted = new ScriptedProvisioningProvider
        {
            ResetResult = UserProvisioningResetResult.ProviderFailure()
        };
        var service = new UserAdminService(_db, scripted, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));

        var outcome = await service.ResetUserAccessAsync(new ResetUserAccess("4567"));

        Assert.Equal(ResetUserAccessResult.ProviderFailure, outcome.Result);
        Assert.False(_db.Users.Single(u => u.Id == user.Id).RequiresPasswordChange);
    }

    // ------------------------------------------------------------------
    // SetUserStandBy (USERS.md §1): desliga o acesso corrente,
    // preserva o User, a atribuição e o histórico.
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetUserStandBy_TogglesAccessState_PreservingUserAndAssignment()
    {
        var user = await SeedUserAsync("5678", "provider-user-standby");
        var template = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        await _db.SaveChangesAsync();

        var standBy = await _service.SetUserStandByAsync(new SetUserStandBy("5678", IsStandBy: true));

        Assert.Equal(SetUserStandByResult.Success, standBy.Result);
        var afterStandBy = _db.Users.Include(u => u.CurrentTemplateAssignment)
            .Single(u => u.OperatorId == "5678");
        Assert.True(afterStandBy.IsStandBy);

        // A atribuição corrente fica PRESERVADA (acesso desligado, não
        // desatribuído) e o histórico nunca é reescrito.
        Assert.Equal(template.Id, afterStandBy.CurrentTemplateAssignment!.TemplateId);

        // E volta ao ativo: a mesma relação, o mesmo User.
        var reactivated = await _service.SetUserStandByAsync(new SetUserStandBy("5678", IsStandBy: false));
        Assert.Equal(SetUserStandByResult.Success, reactivated.Result);
        Assert.False(_db.Users.Single(u => u.OperatorId == "5678").IsStandBy);
    }

    [Fact]
    public async Task SetUserStandBy_AdminAssociatedIdentity_AdminRefused()
    {
        await SeedAdminEmailAsync("admin.standby@example.test");
        await SeedUserAsync("9000", "provider-admin-standby", email: "admin.standby@example.test");

        var outcome = await _service.SetUserStandByAsync(new SetUserStandBy("9000", IsStandBy: true));

        Assert.Equal(SetUserStandByResult.AdminRefused, outcome.Result);
        Assert.False(_db.Users.Single(u => u.OperatorId == "9000").IsStandBy);
    }

    [Fact]
    public async Task SetUserStandBy_UnknownOperator_UserNotFound()
    {
        var outcome = await _service.SetUserStandByAsync(new SetUserStandBy("9999", IsStandBy: true));

        Assert.Equal(SetUserStandByResult.UserNotFound, outcome.Result);
    }

    // ------------------------------------------------------------------
    // SetCurrentUserTemplate (TEMPLATES.md L18): UMA relação persistida,
    // duas vistas — a edição da folha Utilizadores edita a MESMA linha.
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetCurrentUserTemplate_AssignsByStableKeyAndReplacesTheSingleRelationRow()
    {
        var user = await SeedUserAsync("6789", "provider-user-assign");
        var first = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);
        var second = await SeedTemplateAsync("tpl-responsavel-operacional", "Responsável Operacional", DmoCapabilities.ControloApprove);

        // Atribui a primeira (pela key ESTÁVEL, nunca pelo rótulo).
        var assign = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("6789", first.TemplateKey));
        Assert.Equal(SetCurrentUserTemplateResult.Success, assign.Result);
        Assert.Equal("tpl-operador-beta", assign.TemplateKey);
        Assert.Equal("Operador Beta", assign.TemplateName);
        var assignmentRow = _db.TemplateAssignments.Single(a => a.UserId == user.Id);
        Assert.Equal(first.Id, assignmentRow.TemplateId);

        // Substitui pela segunda: a MESMA linha é editada in-place — nunca
        // uma segunda atribuição concorrente (o índice único de UserId).
        var replace = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("6789", second.TemplateKey));
        Assert.Equal(SetCurrentUserTemplateResult.Success, replace.Result);

        var singleRow = _db.TemplateAssignments.Where(a => a.UserId == user.Id).ToList();
        Assert.Single(singleRow);
        Assert.Equal(second.Id, singleRow[0].TemplateId);

        // A leitura da vista Users reflete a relação.
        var projected = (await _service.ListUsersAsync()).Single(u => u.OperatorId == "6789");
        Assert.Equal("tpl-responsavel-operacional", projected.CurrentTemplateKey);
    }

    [Fact]
    public async Task SetCurrentUserTemplate_NullKeyRemovesTheAssignment()
    {
        var user = await SeedUserAsync("7890", "provider-user-remove");
        var template = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);
        _db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TemplateId = template.Id
        });
        await _db.SaveChangesAsync();

        // «Sem template» = remover a atribuição corrente.
        var outcome = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("7890", null));

        Assert.Equal(SetCurrentUserTemplateResult.Success, outcome.Result);
        Assert.Empty(_db.TemplateAssignments.Where(a => a.UserId == user.Id).ToList());

        var projected = (await _service.ListUsersAsync()).Single(u => u.OperatorId == "7890");
        Assert.Null(projected.CurrentTemplateKey);
        Assert.Null(projected.CurrentTemplateName);
    }

    [Fact]
    public async Task SetCurrentUserTemplate_AdminAssociatedIdentity_AdminRefusedNeverHoldsATemplate()
    {
        await SeedAdminEmailAsync("admin.assign@example.test");
        await SeedUserAsync("9000", "provider-admin-assign", email: "admin.assign@example.test");
        var template = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);

        var outcome = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("9000", template.TemplateKey));

        Assert.Equal(SetCurrentUserTemplateResult.AdminRefused, outcome.Result);
        Assert.Empty(_db.TemplateAssignments.ToList());
    }

    [Fact]
    public async Task TemplateWithBothControlos_CanBeUsedForCreationAndAssignment()
    {
        var template = await SeedTemplateAsync("tpl-both", "Ambos",
            DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove);
        var created = await _service.CreateDmoUserAsync(
            ValidCreateCommand(templateKey: template.TemplateKey));
        Assert.Equal(CreateDmoUserResult.Success, created.Result);

        await SeedUserAsync("1234", "provider-preexisting");
        var assigned = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("1234", template.TemplateKey));
        Assert.Equal(SetCurrentUserTemplateResult.Success, assigned.Result);
    }

    [Fact]
    public async Task SetCurrentUserTemplate_UnknownUserOrTemplate_TypedRefusals()
    {
        var user = await SeedUserAsync("8901", "provider-user-unknowns");
        var template = await SeedTemplateAsync("tpl-operador-beta", "Operador Beta", DmoCapabilities.JobOnCreate);

        var unknownUser = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("9999", template.TemplateKey));
        Assert.Equal(SetCurrentUserTemplateResult.UserNotFound, unknownUser.Result);

        var unknownTemplate = await _service.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate("8901", "tpl-inexistente"));
        Assert.Equal(SetCurrentUserTemplateResult.UnknownTemplate, unknownTemplate.Result);
        Assert.Empty(_db.TemplateAssignments.ToList());
    }
}
