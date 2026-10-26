using System.Net;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using Microsoft.AspNetCore.Mvc.Testing;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException("No antiforgery token found on page.");
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password)
    {
        var token = await GetVerificationTokenAsync(client, "/login");
        var content = HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token));
        return await client.PostAsync("/login", content);
    }

    /// <summary>
    /// Seeds the ADMIN: the email of the Definições (o ADMIN é o email —
    /// o Owner 2026-10-16: «usa uma solução apenas para o mail») plus the
    /// Auth account that authenticates with it.
    /// </summary>
    private void SeedAdmin(string email, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        dbContext.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = email
        });
        dbContext.SaveChanges();
    }

    /// <summary>Muda o email do ADMIN nas Definições (a sessão anterior morre).</summary>
    private void ChangeAdminEmailSetting(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var setting = dbContext.ModuleSettings.Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
        setting.SettingValue = email;
        dbContext.SaveChanges();
    }

    private void SeedUser(string operatorId, string name, string providerUserId, string password, bool requiresPasswordChange = false, string? profile = null)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail, requiresPasswordChange);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId,
            RequiresPasswordChange = requiresPasswordChange,
            TemplateName = profile
        });
        dbContext.SaveChanges();
    }

    [Fact]
    public async Task AdminLogin_Success_RedirectsToAdminArea()
    {
        const string email = "admin@example.com";
        const string password = "AdminPass123!";
        SeedAdmin(email, "provider-admin-1", password);

        using var client = CreateClient();
        var response = await LoginAsync(client, email, password);

        // A REGRA ABSOLUTA DO OWNER (2026-10-13, repetida por ele):
        // «admin so tem pagina de admin» — o ADMIN aterra SEMPRE na sua
        // área, NEM POR returnUrl (o challenge de /JobOn traz ReturnUrl=
        // %2FJobOn e o admin caía lá dentro — o bug G1-final).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Users", response.Headers.Location?.OriginalString);

        var admin = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var adminHtml = await admin.Content.ReadAsStringAsync();
        Assert.DoesNotContain(">Setup</a>", adminHtml);
        Assert.Contains("data-dmo-nav-definicoes", adminHtml);
    }

    [Fact]
    public async Task AdminLogin_Failure_WrongPassword()
    {
        const string email = "admin@example.com";
        SeedAdmin(email, "provider-admin-2", "correct");

        using var client = CreateClient();
        var response = await LoginAsync(client, email, "wrong");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // P2-6 da auditoria: a mensagem em PORTUGUÊS (antes «Invalid
        // credentials.» em inglês).
        Assert.Contains("Credenciais inv", html);
    }

    [Fact]
    public async Task AdminLogin_Failure_NotAssociated()
    {
        // Com o ADMIN definido nas Definições, um email que autentica mas
        // não é o do campo nem um User registado: a recusa tipada. (Com o
        // campo VAZIO o primeiro login por email assume-o — o bootstrap
        // tem os seus próprios testes.)
        SeedAdmin("definido@example.com", "provider-definido", "DefinidoPass1!");
        const string email = "orphan@example.com";
        _factory.AuthProvider.AddAccount(email, "provider-orphan", "password", email);

        using var client = CreateClient();
        var response = await LoginAsync(client, email, "password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("tem acesso", html);
    }

    [Fact]
    public async Task UserLogin_Success_RedirectsToHome()
    {
        SeedUser("1234", "Joao Silva", "provider-user-1234", "UserPass1!");

        using var client = CreateClient();
        var response = await LoginAsync(client, "1234", "UserPass1!");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Modules/Home", response.Headers.Location?.OriginalString);

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        // Sem template não há módulos: a raiz leva ao início, que o explica.
        Assert.Equal("/Modules/Home", home.Headers.Location?.OriginalString);
        var inicio = await client.GetAsync("/Modules/Home");
        inicio.EnsureSuccessStatusCode();
        var html = await inicio.Content.ReadAsStringAsync();
        Assert.Contains("ainda não inclui módulos", html);
    }

    [Fact]
    public async Task UserLogin_PreservesLeadingZeros()
    {
        const string operatorId = "0042";
        SeedUser(operatorId, "Maria Souza", "provider-user-0042", "UserPass2!");

        using var client = CreateClient();
        var response = await LoginAsync(client, operatorId, "UserPass2!");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Modules/Home", response.Headers.Location?.OriginalString);

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        // Sem template não há módulos: a raiz leva ao início, que o explica.
        Assert.Equal("/Modules/Home", home.Headers.Location?.OriginalString);
        var inicio = await client.GetAsync("/Modules/Home");
        inicio.EnsureSuccessStatusCode();
        var html = await inicio.Content.ReadAsStringAsync();
        Assert.Contains("ainda não inclui módulos", html);
    }

    [Fact]
    public async Task AdminLogin_NeedsOnlyTheEmailSetting()
    {
        // O ADMIN é o email das Definições — sem qualquer registo de
        // instalação/associação (o setup/wizard foi removido por decisão
        // do proprietário): o login por email funciona com o campo.
        SeedAdmin("no-setup-admin@example.test", "no-setup-admin", "AdminPass123!");

        using var client = CreateClient();
        var login = await LoginAsync(client, "no-setup-admin@example.test", "AdminPass123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Admin/Users", login.Headers.Location?.OriginalString);
        var page = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task AdminBootstrap_EmptySetting_FirstEmailLoginBecomesAdmin()
    {
        // Ambiente novo, campo do ADMIN vazio: o primeiro login por email
        // com sucesso que não é um User registado ASSUME o campo e fica
        // ADMIN — sem setup, sem wizard (o Owner 2026-10-16: «nao inserir
        // setup/wizard usa uma soluçao apenas para o mail»).
        _factory.AuthProvider.AddAccount("first@example.test", "provider-first-admin", "FirstPass123!", "first@example.test");

        using var client = CreateClient();
        var login = await LoginAsync(client, "first@example.test", "FirstPass123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Admin/Users", login.Headers.Location?.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var setting = db.ModuleSettings.Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
            Assert.Equal("first@example.test", setting.SettingValue);
        }

        // O login seguinte do mesmo email continua ADMIN pelo campo.
        using var second = CreateClient();
        var again = await LoginAsync(second, "first@example.test", "FirstPass123!");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Equal("/Admin/Users", again.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminBootstrap_EmptySetting_UserEmailLoginDoesNotClaimAdmin()
    {
        // As contas operacionais (Users criados pelo ADMIN) NUNCA
        // reivindicam o campo vazio: o login por email delas é sessão de
        // utilizador normal e o campo continua vazio.
        SeedUser("5555", "Operador Existente", "provider-user-5555", "OperadorPass123!");

        using var client = CreateClient();
        var login = await LoginAsync(client, "5555@dmo.local", "OperadorPass123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.NotEqual("/Admin/Users", login.Headers.Location?.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.False(db.ModuleSettings.Any(s => s.SettingKey == ModuleSettingKeys.AdminEmail));
        }
    }

    [Theory]
    [InlineData(" Operador.Mail@Empresa.pt ")]
    [InlineData("operador.mail@empresa.pt\t")]
    public async Task UserEmailLogin_IgnoresSpacesAndCapitals(string typed)
    {
        SeedUser("5556", "Operador Mail", "provider-user-5556", "OperadorPass123!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Single(u => u.OperatorId == "5556").Email = "operador.mail@empresa.pt";
            db.SaveChanges();
        }
        _factory.AuthProvider.AddAccount("operador.mail@empresa.pt", "provider-user-5556", "OperadorPass123!", "operador.mail@empresa.pt");

        using var client = CreateClient();
        var login = await LoginAsync(client, typed, "OperadorPass123!");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.NotEqual("/Admin/Users", login.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("jobon.create", "/JobOn")]
    [InlineData("controlo.create", "/Controlo/Resumo")]
    [InlineData("controlo.approve", "/Controlo/Approve")]
    public async Task OperatorLogin_EntersAreaOfAssignedTemplate(string capability, string expectedPath)
    {
        SeedUser("8417", "Operador Beta", "beta-user-8417", "BetaPass123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = db.Users.Single(u => u.OperatorId == "8417");
            var template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "beta-tpl-8417",
                Name = "Template Beta",
                Capabilities = new[] { capability }
            };
            db.Templates.Add(template);
            db.TemplateAssignments.Add(new TemplateAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TemplateId = template.Id
            });
            db.SaveChanges();
        }

        using var client = CreateClient();
        var login = await LoginAsync(client, "8417", "BetaPass123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(expectedPath, login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UserLogin_Failure_WrongPassword()
    {
        SeedUser("9999", "Carlos", "provider-user-9999", "right");

        using var client = CreateClient();
        var response = await LoginAsync(client, "9999", "wrong");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // P2-6 da auditoria: a mensagem em PORTUGUÊS (antes «Invalid
        // credentials.» em inglês).
        Assert.Contains("Credenciais inv", html);
    }

    [Fact]
    public async Task UserLogin_RepeatedFailures_NeverLockTheUser()
    {
        // Diogo, 18/10/2026: «não tranques nada; a última coisa de que
        // preciso é ficar sem aceder a uma ferramenta de trabalho». Falhas
        // seguidas não travam o utilizador: a palavra-passe certa entra.
        // Diogo, 26/10/2026: da 5.ª em diante manda consultar o ADMIN.
        SeedUser("9881", "Carlos", "provider-user-9881", "right");

        using var client = CreateClient();
        for (var i = 1; i <= 6; i++)
        {
            var failed = await LoginAsync(client, "9881", "wrong");
            Assert.Contains(i < 5 ? "Credenciais inv" : "Consulte o ADMIN para rever a palavra-passe", await failed.Content.ReadAsStringAsync());
        }

        var ok = await LoginAsync(client, "9881", "right");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
    }

    [Fact]
    public async Task Login_InASecondTab_AfterTheFirstOneEntered_GoesIn_InsteadOfError400()
    {
        // B-admin-4 do deep sweep: dois separadores no login; entra-se no
        // primeiro e, no segundo, o envio levava o token de antes da sessão
        // e dava «HTTP ERROR 400» (e o F5 repetia-o). Agora segue para a
        // aplicação com a sessão que já existe.
        SeedUser("9883", "Ana", "provider-user-9883", "right");

        using var client = CreateClient();
        var staleToken = await GetVerificationTokenAsync(client, "/login");
        var first = await LoginAsync(client, "9883", "right");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        var second = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "9883"),
            ("password", "right"),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", staleToken)));

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
    }

    [Fact]
    public async Task Login_WithoutAValidToken_ShowsTheLoginAgain_InsteadOfError400()
    {
        SeedUser("9884", "Rui", "provider-user-9884", "right");

        using var client = CreateClient();
        await client.GetAsync("/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "9884"),
            ("password", "right"),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", "token-que-nao-serve")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A página de entrada expirou", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith("DmoAuth=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AfterSair_TheBrowserDoesNotKeepThePages_SoBackShowsNothing()
    {
        // B-admin-5 do deep sweep: depois de «Sair», o Voltar mostrava as
        // páginas anteriores, guardadas pelo browser. As páginas da
        // aplicação vão com «Cache-Control: no-store».
        SeedUser("9885", "Rita", "provider-user-9885", "right");

        using var client = CreateClient();
        var login = await LoginAsync(client, "9885", "right");
        var page = await client.GetAsync(login.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Login_BehindTheHttpsProxy_SetsTheSessionCookieAsSecure()
    {
        // O Railway termina o HTTPS e passa X-Forwarded-Proto: https. A app
        // lê o cabeçalho, por isso o cookie da sessão sai com Secure.
        SeedUser("9883", "Proxy", "provider-user-9883", "right");

        using var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/login")
        {
            Content = HtmlFormHelpers.BuildFormPost(
                ("identifier", "9883"),
                ("password", "right"),
                ("returnUrl", "/"),
                ("__RequestVerificationToken", token))
        };
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("DmoAuth=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserLogin_Failure_StandBy()
    {
        var generatedEmail = "0001@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, "provider-user-0001", "pass", generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = "0001",
            Name = "StandBy User",
            ProviderUserId = "provider-user-0001",
            IsStandBy = true
        });
        dbContext.SaveChanges();

        using var client = CreateClient();
        var response = await LoginAsync(client, "0001", "pass");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // Com a palavra-passe certa, diz que a conta está arquivada.
        Assert.Contains("arquivada", html);
    }

    [Fact]
    public async Task UserLogin_Failure_UnknownOperator()
    {
        // The provider knows the identity, but DMO has no User record for it.
        _factory.AuthProvider.AddAccount("5555@dmo.local", "provider-user-5555", "pass", "5555@dmo.local");

        using var client = CreateClient();
        var response = await LoginAsync(client, "5555", "pass");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // A mesma frase de uma palavra-passe errada: o ecrã de entrada não
        // revela que números de operador existem.
        Assert.Contains("Credenciais inv", html);
        Assert.DoesNotContain("Unknown operator", html);
    }

    [Fact]
    public async Task ForcedPasswordChange_UserRequiresChange_BlocksOperationalPages()
    {
        SeedUser("1000", "Temp User", "provider-user-1000", "OldPass1!", requiresPasswordChange: true);

        using var client = CreateClient();
        var response = await LoginAsync(client, "1000", "OldPass1!");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/change-password", response.Headers.Location?.OriginalString);

        var blocked = await client.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Equal("/change-password", blocked.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ForcedPasswordChange_TooShortPassword_ShowsExactRequirement_WithoutChangingPassword()
    {
        SeedUser("1999", "New Operator", "provider-user-1999", "OldPass1!", requiresPasswordChange: true);

        using var client = CreateClient();
        var login = await LoginAsync(client, "1999", "OldPass1!");
        Assert.Equal("/change-password", login.Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var content = HtmlFormHelpers.BuildFormPost(
            ("newPassword", "123"),
            ("confirmPassword", "123"),
            ("__RequestVerificationToken", token));

        var response = await client.PostAsync("/change-password", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("pelo menos 4 caracteres", html);
        Assert.Contains("minlength=\"4\"", html);

        var stillRequired = await client.GetAsync("/JobOn");
        Assert.Equal(HttpStatusCode.Redirect, stillRequired.StatusCode);
        Assert.Equal("/change-password", stillRequired.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ForcedPasswordChange_StaleFormAfterLogin_ReopensAndAllowsFreshSubmission()
    {
        SeedUser("2099", "Reset User", "provider-user-2099", "TemporaryPass1!", requiresPasswordChange: true);
        using var client = CreateClient();
        // A form token from the previous anonymous session is invalid after login.
        var staleToken = await GetVerificationTokenAsync(client, "/login");
        var login = await LoginAsync(client, "2099", "TemporaryPass1!");
        Assert.Equal("/change-password", login.Headers.Location?.OriginalString);

        var expired = await client.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
            ("newPassword", "NewPrivatePass1!"), ("confirmPassword", "NewPrivatePass1!"),
            ("__RequestVerificationToken", staleToken)));
        Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
        Assert.Equal("/change-password?form_expired=1", expired.Headers.Location?.OriginalString);
        using (var scope = _factory.Services.CreateScope())
        {
            var user = scope.ServiceProvider.GetRequiredService<DmoDbContext>().Users.Single(u => u.OperatorId == "2099");
            Assert.True(user.RequiresPasswordChange);
        }
        using var freshLogin = CreateClient();
        var oldPassword = await LoginAsync(freshLogin, "2099", "TemporaryPass1!");
        Assert.Equal("/change-password", oldPassword.Headers.Location?.OriginalString);

        var form = await client.GetAsync(expired.Headers.Location);
        form.EnsureSuccessStatusCode();
        var html = await form.Content.ReadAsStringAsync();
        Assert.Contains("O formulário expirou ou a sessão mudou", html);
        Assert.DoesNotContain("NewPrivatePass1!", html);
        Assert.True(form.Headers.CacheControl?.NoStore);
        var freshToken = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var saved = await client.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
            ("newPassword", "NewPrivatePass1!"), ("confirmPassword", "NewPrivatePass1!"),
            ("__RequestVerificationToken", freshToken)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/Modules/Home", saved.Headers.Location?.OriginalString);
        using var newLogin = CreateClient();
        var newPassword = await LoginAsync(newLogin, "2099", "NewPrivatePass1!");
        Assert.Equal("/Modules/Home", newPassword.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ResetPassword_OldSessionCookie_StaysRevokedAfterNewPasswordIsChosen()
    {
        // Encontrado pelo Codex a 18/10/2026: depois de o ADMIN repor a
        // palavra-passe e de o utilizador escolher uma nova, o cookie da
        // sessão antiga voltava a dar acesso.
        SeedUser("2050", "Old Session", "provider-user-2050", "FirstPass1!");

        using var oldDevice = CreateClient();
        var firstLogin = await LoginAsync(oldDevice, "2050", "FirstPass1!");
        Assert.Equal("/Modules/Home", firstLogin.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await oldDevice.GetAsync("/Modules/Home")).StatusCode);

        string temporaryPassword;
        using (var scope = _factory.Services.CreateScope())
        {
            var reset = await scope.ServiceProvider.GetRequiredService<IUserAdminService>()
                .ResetUserAccessAsync(new ResetUserAccess("2050"));
            Assert.Equal(ResetUserAccessResult.Success, reset.Result);
            temporaryPassword = reset.TemporaryPassword!;
        }

        var duringReset = await oldDevice.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.Redirect, duringReset.StatusCode);
        Assert.Contains("/login", duringReset.Headers.Location?.OriginalString);

        using var newDevice = CreateClient();
        var resetLogin = await LoginAsync(newDevice, "2050", temporaryPassword);
        Assert.Equal("/change-password", resetLogin.Headers.Location?.OriginalString);
        var token = await GetVerificationTokenAsync(newDevice, "/change-password");
        var changed = await newDevice.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
            ("newPassword", "SecondPass1!"), ("confirmPassword", "SecondPass1!"),
            ("__RequestVerificationToken", token)));
        Assert.Equal("/Modules/Home", changed.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await newDevice.GetAsync("/Modules/Home")).StatusCode);

        // O cookie de antes do reset não volta a valer.
        var afterChange = await oldDevice.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.Redirect, afterChange.StatusCode);
        Assert.Contains("/login", afterChange.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminPasswordChange_EndsTheOtherAdminSessions()
    {
        // Deep sweep (18/10/2026): o ADMIN não tem linha em Users e a sessão
        // dele só comparava o email; mudar a palavra-passe deixava abertas
        // as sessões do ADMIN noutros computadores.
        SeedAdmin("sessoes-admin@example.test", "provider-admin-sessoes", "AdminOld123!");
        using var otherDevice = CreateClient();
        Assert.Equal("/Admin/Users", (await LoginAsync(otherDevice, "sessoes-admin@example.test", "AdminOld123!")).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/Admin/Users")).StatusCode);
        using var owner = CreateClient();
        Assert.Equal("/Admin/Users", (await LoginAsync(owner, "sessoes-admin@example.test", "AdminOld123!")).Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(owner, "/change-password");
        var changed = await owner.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
            ("currentPassword", "AdminOld123!"), ("newPassword", "AdminNew456!"), ("confirmPassword", "AdminNew456!"),
            ("__RequestVerificationToken", token)));
        Assert.Equal("/Admin/Users", changed.Headers.Location?.OriginalString);

        // Quem mudou continua dentro; a outra sessão vai para o login.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/Admin/Users")).StatusCode);
        var after = await otherDevice.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/login", after.Headers.Location?.OriginalString);

        // Uma sessão nova com a palavra-passe nova entra.
        using var fresh = CreateClient();
        Assert.Equal("/Admin/Users", (await LoginAsync(fresh, "sessoes-admin@example.test", "AdminNew456!")).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/Admin/Users")).StatusCode);
    }

    [Fact]
    public async Task ForcedPasswordChange_FourDigitPin_IsAccepted()
    {
        // Pedido do Diogo de 17/10/2026: sai o mínimo de 6; 4 números servem.
        SeedUser("2001", "Pin User", "provider-user-2001", "4821", requiresPasswordChange: true);

        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, "2001", "4821");
        Assert.Equal("/change-password", loginResponse.Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var content = HtmlFormHelpers.BuildFormPost(
            ("newPassword", "7305"),
            ("confirmPassword", "7305"),
            ("__RequestVerificationToken", token));
        var changeResponse = await client.PostAsync("/change-password", content);

        Assert.Equal(HttpStatusCode.Redirect, changeResponse.StatusCode);
        Assert.Equal("/Modules/Home", changeResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ForcedPasswordChange_UserSetsNewPassword_CanAccessApp()
    {
        SeedUser("2000", "Temp User", "provider-user-2000", "OldPass2!", requiresPasswordChange: true);

        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, "2000", "OldPass2!");
        Assert.Equal("/change-password", loginResponse.Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var content = HtmlFormHelpers.BuildFormPost(
            ("newPassword", "NewSecurePass1!"),
            ("confirmPassword", "NewSecurePass1!"),
            ("__RequestVerificationToken", token));
        var changeResponse = await client.PostAsync("/change-password", content);

        Assert.Equal(HttpStatusCode.Redirect, changeResponse.StatusCode);
        // A MUDANCA DE SENHA ATERRA NO PLANEAMENTO (a home morreu — o Owner
        // 2026-10-12: «entrar logo com o user no planeamento»).
        Assert.Equal("/Modules/Home", changeResponse.Headers.Location?.OriginalString);

        var home = await client.GetAsync("/Modules/Home");
        home.EnsureSuccessStatusCode();
        var html = await home.Content.ReadAsStringAsync();
        Assert.Contains("Temp User", html);

        var module = await client.GetAsync("/Modules/Home");
        module.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ForcedPasswordChange_SecondSubmitOfSameForm_LandsInAppInsteadOfReopeningForm()
    {
        // Bug 16/10: o segundo «Guardar» (duplo clique, ou clique enquanto a
        // página seguinte carrega) chegava com o token antigo e a sessão já
        // renovada; a app reabria o formulário (antes da beta.12: HTTP 400).
        SeedUser("2100", "Double Submit", "provider-user-2100", "TempPass21!", requiresPasswordChange: true);

        using var client = CreateClient();
        var login = await LoginAsync(client, "2100", "TempPass21!");
        Assert.Equal("/change-password", login.Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(client, "/change-password");
        HttpContent Form() => HtmlFormHelpers.BuildFormPost(
            ("newPassword", "ChosenPass21!"),
            ("confirmPassword", "ChosenPass21!"),
            ("__RequestVerificationToken", token));

        var first = await client.PostAsync("/change-password", Form());
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal("/Modules/Home", first.Headers.Location?.OriginalString);

        var second = await client.PostAsync("/change-password", Form());
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal("/Modules/Home", second.Headers.Location?.OriginalString);

        var landing = await client.GetAsync(second.Headers.Location);
        landing.EnsureSuccessStatusCode();
        Assert.Contains("Palavra-passe alterada com sucesso.", await landing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForcedPasswordChange_ProviderTemporaryPassword_CanBeCleared()
    {
        SeedUser("3000", "New Hire", "provider-user-3000", "TempPass1!", requiresPasswordChange: false);
        // Simulate the provider flagging the password as temporary by seeding the account with the flag.
        _factory.AuthProvider.AddAccount("3000@dmo.local", "provider-user-3000", "TempPass1!", isTemporaryPassword: true);

        using var client = CreateClient();
        var loginResponse = await LoginAsync(client, "3000", "TempPass1!");
        Assert.Equal("/change-password", loginResponse.Headers.Location?.OriginalString);

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var content = HtmlFormHelpers.BuildFormPost(
            ("newPassword", "RealPass1!"),
            ("confirmPassword", "RealPass1!"),
            ("__RequestVerificationToken", token));
        var changeResponse = await client.PostAsync("/change-password", content);

        Assert.Equal(HttpStatusCode.Redirect, changeResponse.StatusCode);
        // A mudança aterra no Planeamento (a home morreu).
        Assert.Equal("/Modules/Home", changeResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Unauthenticated_DeepLink_RedirectsToLoginWithReturnUrl()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/Modules/Home");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/login", location);
        Assert.Contains("ReturnUrl=%2FModules%2FHome", location);
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        SeedUser("7777", "Logout User", "provider-user-7777", "Pass1!");

        using var client = CreateClient();
        var login = await LoginAsync(client, "7777", "Pass1!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var homeBefore = await client.GetAsync("/");
        // A raiz leva o autenticado ao seu primeiro módulo (a home morreu — o
        // redirect 302 é a prova da sessão viva).
        Assert.Equal(HttpStatusCode.Redirect, homeBefore.StatusCode);
        Assert.Equal("/Modules/Home", homeBefore.Headers.Location?.OriginalString);

        // Sair = sair (2026-10-11, Owner: «quando tento sair ou voltar leva
        // me para uma pagina intermedia, que nao precisa de existir»): the
        // GET terminates the session and returns to the login immediately —
        // no intermediate page, no extra click.
        var logout = await client.GetAsync("/logout");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/login", logout.Headers.Location?.OriginalString);

        var homeAfter = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeAfter.StatusCode);
        var loginRedirect = homeAfter.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/login", loginRedirect);
    }

    [Fact]
    public async Task AuthenticatedActorContext_ExposesProfileAndAdminFlag()
    {
        SeedUser("6000", "Profile User", "provider-user-6000", "Pass2!", profile: "Operador");

        using var client = CreateClient();
        var login = await LoginAsync(client, "6000", "Pass2!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var module = await client.GetAsync("/Modules/Home");
        module.EnsureSuccessStatusCode();
        var html = await module.Content.ReadAsStringAsync();
        Assert.Contains("Profile User", html);
        // C-4 (decisão do Owner): vocabulário pt-PT no shell — «Perfil:».
        Assert.Contains("Perfil: Operador", html);
        Assert.DoesNotContain("ADMIN", html);
    }

    [Fact]
    public async Task ModuleRoute_Home_IsReachableWhenAuthenticated()
    {
        SeedUser("8000", "Module User", "provider-user-8000", "Pass3!");

        using var client = CreateClient();
        var login = await LoginAsync(client, "8000", "Pass3!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var module = await client.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.OK, module.StatusCode);
        var html = await module.Content.ReadAsStringAsync();
        // C-4: «Home Module» → «Módulo Início» (pt-PT no shell, o hub H-3).
        Assert.Contains("Módulo Início", html);
    }

    // ------------------------------------------------------------------
    // Anchored ADMIN resolution + ADMIN-only epoch session validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminLogin_OnlyTheDefinicoesEmailBecomesAdmin()
    {
        // O ADMIN é o email do campo (o Owner 2026-10-16): apenas quem
        // autentica com ESSE email entra como ADMIN. O cenário antigo de
        // «colisão de email com identidade diferente» não existe no
        // Supabase (um email é UMA identidade — ninguém se autentica como
        // o email do ADMIN sem ser o dono da password); a proteção
        // equivalente é esta: um impostor com o SEU email diferente, mesmo
        // autenticando com a password certa dele, nunca resolve a ADMIN
        // nem a User: a recusa tipada.
        const string email = "collision@example.com";
        SeedAdmin(email, "provider-admin-anchor", "CorrectPass1!");
        _factory.AuthProvider.AddAccount(
            "impostor@example.com", "provider-impostor", "ImpostorPass1!", "impostor@example.com");

        using var client = CreateClient();
        var response = await LoginAsync(client, "impostor@example.com", "ImpostorPass1!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("tem acesso", html);
    }

    [Fact]
    public async Task AdminLogin_EmailChangedInDefinicoes_OldEmailIsNoLongerAdmin()
    {
        // O email do ADMIN mudou nas Definições: a identidade anterior
        // ainda autentica com o provider, mas já não é o email do campo —
        // e não é um User registado: a recusa tipada.
        const string email = "replaced@example.com";
        SeedAdmin(email, "provider-admin-replaced", "ReplacedPass1!");

        ChangeAdminEmailSetting("novo-admin@example.com");

        using var client = CreateClient();
        var response = await LoginAsync(client, email, "ReplacedPass1!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("tem acesso", html);
    }

    [Fact]
    public async Task AdminCookie_EmailChangedRejected_And_OperatorCookieUnaffected()
    {
        // O email do ADMIN mudou nas Definições: o cookie deslizante de 8h
        // do ADMIN anterior morre no pedido seguinte (a validação compara
        // o email da sessão com o campo); os cookies operacionais não
        // participam e ficam.
        SeedAdmin("epoch-admin@example.com", "provider-admin-epoch", "EpochPass1!");
        SeedUser("4242", "Epoch Operator", "provider-user-4242", "OperatorPass1!");

        using var adminClient = CreateClient();
        var adminLogin = await LoginAsync(adminClient, "epoch-admin@example.com", "EpochPass1!");
        Assert.Equal(HttpStatusCode.Redirect, adminLogin.StatusCode);
        // A regra do Owner («admin so tem pagina de admin»): a sonda da
        // sessão do ADMIN corre na SUA área — nunca nas páginas
        // operacionais (o antigo probe do /JobOn afirmava o furo G1: o
        // admin a abrir superfícies de produção).
        var adminArea = await adminClient.GetAsync("/Admin/Users");
        adminArea.EnsureSuccessStatusCode();

        using var operatorClient = CreateClient();
        var operatorLogin = await LoginAsync(operatorClient, "4242", "OperatorPass1!");
        Assert.Equal(HttpStatusCode.Redirect, operatorLogin.StatusCode);
        var operatorHome = await operatorClient.GetAsync("/Modules/Home");
        operatorHome.EnsureSuccessStatusCode();

        // O email do ADMIN muda nas Definições (o ADMIN autenticado troca
        // o campo): a sessão anterior morre.
        ChangeAdminEmailSetting("replacement@example.test");

        var adminAfter = await adminClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, adminAfter.StatusCode);
        Assert.Contains("/login", adminAfter.Headers.Location?.ToString());

        var operatorAfter = await operatorClient.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.OK, operatorAfter.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OperatorCookie_DeletedOrStandBy_IsRejected(bool deleted)
    {
        SeedUser("6381", "Sessao Antiga", "provider-stale-6381", "OperatorPass123!");
        using var client = CreateClient();
        var login = await LoginAsync(client, "6381", "OperatorPass123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        (await client.GetAsync("/Modules/Home")).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = db.Users.Single(u => u.OperatorId == "6381");
            if (deleted)
                db.Users.Remove(user);
            else
                user.IsStandBy = true;
            db.SaveChanges();
        }

        var response = await client.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_Page_HasNoRecoveryNotice()
    {
        // O Owner mandou tirar o aviso da recuperação ADMIN do login
        // (2026-10-12: «podes tirar isto do login»): a página fica limpa —
        // a recuperação continua acessível pela URL direta, o login nunca
        // a anuncia.
        using var client = CreateClient();
        var response = await client.GetAsync("/login");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Recuperação de acesso ADMIN", html);
        Assert.DoesNotContain("recovery-note", html);
    }

    [Fact]
    public async Task AdminHierarchy_AfterActivation_RequiresAdmin()
    {
        const string adminEmail = "hierarchy-admin@example.test";
        SeedAdmin(adminEmail, "hierarchy-admin-provider", "OfflineAdmin123!");
        SeedUser("7711", "Operador", "hierarchy-operator-provider", "OfflineOperator123!");

        using var adminClient = CreateClient();
        using var operatorClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "OfflineAdmin123!");
        await LoginAsync(operatorClient, "7711", "OfflineOperator123!");

        foreach (var path in new[] { "/Admin", "/Admin/Definicoes" })
        {
            var allowed = await adminClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, allowed.StatusCode);
            Assert.Equal(path == "/Admin" ? "/Admin/Users" : "/Definicoes?secao=admin&sub=app", allowed.Headers.Location?.OriginalString);
            var refused = await operatorClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/login", refused.Headers.Location?.AbsolutePath);
        }
        var removedSetup = await adminClient.GetAsync("/Admin/Definicoes/Sistema");
        Assert.Equal(HttpStatusCode.NotFound, removedSetup.StatusCode);
    }

    [Fact]
    public async Task ShellUserMenu_ContainsChangePasswordLink_DefinicoesForAdminOnly()
    {
        // The shell header (2026-10-11, Owner: «no topo nao deveria ter opçao
        // para mudr password»): NO change-password entry at the top — the
        // access paths are the forced change (corte I flow) and the direct
        // URL /change-password. The System Settings entry appears for ADMIN
        // principals only; Sair terminates directly.
        const string adminEmail = "shell-admin@example.com";
        SeedAdmin(adminEmail, "shell-admin-provider-id", "AdminPass123!");
        SeedUser("7700", "Operador Shell", "shell-operator-provider-id", "OperatorPass123!");

        using (var adminClient = CreateClient())
        {
            await LoginAsync(adminClient, adminEmail, "AdminPass123!");
            // Admin has no access to the operational Modules/Home route.
            var home = await adminClient.GetAsync("/Admin/Users");
            home.EnsureSuccessStatusCode();
            var html = await home.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Mudar palavra-passe", html);
            // 2026-10-13, Owner: «existem dois botoes de definiçoes o que esta
            // ao pe do Sair nao deveria existir» — o atalho do header era
            // DUPLICADO da barra das funções da admin; o header fica SÓ com
            // a sessão (nome · papel · Sair).
            Assert.DoesNotContain("dmo-shell-settings", html);
            Assert.Contains("Sair", html);
            Assert.Contains($"data-dmo-version=\"{DMO.Alpha.Web.Services.AppBuildInfo.Current.Version}\"", html);
            Assert.Contains($"data-dmo-build=\"{DMO.Alpha.Web.Services.AppBuildInfo.Current.BuildId}\"", html);
            Assert.Contains("href=\"/versao\"", html);
        }

        using (var operatorClient = CreateClient())
        {
            await LoginAsync(operatorClient, "7700", "OperatorPass123!");
            var home = await operatorClient.GetAsync("/Modules/Home");
            var html = await home.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Mudar palavra-passe", html);
            Assert.DoesNotContain("/change-password", html);
            // The System Settings are administration-only: the operator's
            // shell carries no entry to the surface.
            Assert.DoesNotContain("/Admin/Definicoes", html);
        }
    }
}
