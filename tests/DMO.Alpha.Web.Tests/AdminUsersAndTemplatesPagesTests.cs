using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As folhas do corte I na app real (mockup 13_ADMIN com os desvios por
/// blueprint: sem coluna Email, sem «Último acesso», auditoria fora do
/// corte): /Admin/Users e /Admin/Templates — criação de utilizadores NA
/// APP (a condição do fim de semana: «o user do chefe nasce NA APP»), os
/// worked examples de USERS.md L61-71 (criar → password temporária → 1.º
/// login força a mudança → acesso normal; reset → password velha falha →
/// nova temporária → mudança → normal), as recusas tipadas renderizadas
/// por PRG, o dressed shell da área Admin (placeholders ligados — D8) e
/// o gate da policy de pasta sobre as novas superfícies.
/// </summary>
public sealed partial class AdminUsersAndTemplatesPagesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminUsersAndTemplatesPagesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    // ------------------------------------------------------------------
    // Seeding helpers (o padrão provado do AdminAuthorizationPolicyTests)
    // ------------------------------------------------------------------

    /// <summary>Semeia o ADMIN: o email das Definições (o ADMIN é o email).</summary>
    private void SeedConfiguredAdmin(string email, string providerUserId, string password)
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

    private static async Task<string> GetTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token on {path}.");
    }

    /// <summary>
    /// O caminho de um Location de redirect, absoluto (challenge do cookie
    /// handler) ou relativo (redirects da app) — o padrão provado da suite.
    /// </summary>
    private static string? LocationPath(HttpResponseMessage response) =>
        response.Headers.Location is { } location
            ? location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString
            : null;

    /// <summary>
    /// O Location por extenso (caminho + query), absoluto ou relativo —
    /// para afirmar parâmetros PRG e extrair a TemplateKey mintada.
    /// </summary>
    private static string LocationText(HttpResponseMessage response) =>
        response.Headers.Location?.ToString() ?? string.Empty;

    /// <summary>O valor de um parâmetro do query do Location (PRG), ou null.</summary>
    private static string? LocationQueryValue(HttpResponseMessage response, string name)
    {
        var text = LocationText(response);
        var query = text.Contains('?') ? text[(text.IndexOf('?') + 1)..] : text;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (pair.StartsWith($"{name}=", StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(name.Length + 1)..]);
            }
        }

        return null;
    }

    /// <summary>
    /// O tag &lt;input&gt; completo do checkbox (os atributos podem virbrar
    /// linha no render — o tag inteiro é a âncora segura da asserção).
    /// </summary>
    private static string CheckboxTag(string html, string id)
    {
        var tag = Regex.Match(html, $@"<input[^>]*id=""{Regex.Escape(id)}""[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(tag.Success, $"a página não tem o checkbox #{id}");
        return tag.Value;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string path, params (string name, string value)[] fields)
    {
        // O token antiforgery vem da página base (o POST leva o handler no
        // query; a página GET é sempre a base sem handler).
        var tokenPath = path.Split('?')[0];
        var token = await GetTokenAsync(client, tokenPath);
        var content = HtmlFormHelpers.BuildFormPost(
            fields.Append(("__RequestVerificationToken", token)).ToArray());
        return await client.PostAsync(path, content);
    }

    private static async Task<HttpClient> LoginAdminAsync(CustomWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var login = await PostAsync(
            client, "/login",
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/Admin/Users"));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        return client;
    }

    /// <summary>Extrai o par de entrega (email + password temporária) do painel UMA-VEZ.</summary>
    private static async Task<(string Email, string TemporaryPassword)> ExtractBootstrapAsync(HttpClient client)
    {
        var page = await client.GetAsync("/Admin/Users");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();

        var email = Regex.Match(
            html,
            @"Email de autenticação</span>\s*<strong>([^<]+)</strong>",
            RegexOptions.IgnoreCase).Groups[1].Value;
        var password = Regex.Match(
            html,
            @"PIN temporário \(mostrado uma vez\)</span>\s*<strong>([^<]+)</strong>",
            RegexOptions.IgnoreCase).Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(email), "O painel de entrega não mostrou o email.");
        Assert.False(string.IsNullOrWhiteSpace(password), "O painel de entrega não mostrou a password temporária.");

        return (email, password);
    }

    private static async Task<(string Email, string TemporaryPassword)> CreateUserViaAppAsync(
        HttpClient adminClient,
        string name = "Chefe Teste",
        string operatorId = "4242",
        string email = "chefe.teste@empresa.pt")
    {
        var response = await PostAsync(
            adminClient, "/Admin/Users?handler=Create",
            ("name", name),
            ("operator_id", operatorId),
            ("email", email),
            ("template_key", ""));

        // PRG: sucesso volta à lista (o painel UMA-VEZ transporta o par de
        // entrega por TempData).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Users", LocationPath(response));

        return await ExtractBootstrapAsync(adminClient);
    }

    // ------------------------------------------------------------------
    // Worked example 1 (USERS.md L61-71): criar → temporária → 1.º login
    // força a mudança → acesso normal.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsers_CreateUserViaApp_FirstLoginForcesPrivateChange_ThenNormalAccess()
    {
        SeedConfiguredAdmin("pages-admin@example.test", "pages-admin-provider", "PagesAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "pages-admin@example.test", "PagesAdmin123!");

        // O user nasce NA APP: a identidade é provisionada no seam com a
        // password temporária mintada pelo backend.
        var (email, temporaryPassword) = await CreateUserViaAppAsync(adminClient);

        Assert.Equal("chefe.teste@empresa.pt", email);
        Assert.Matches("^[0-9]{4}$", temporaryPassword);

        // 1.º login COM A TEMPORÁRIA: a sessão nasce e é IMEDIATAMENTE
        // desviada para a mudança obrigatória (password definitiva — a
        // escolha privada do utilizador, via provider; o DMO nunca a vê).
        using var userClient = CreateClient();
        var firstLogin = await PostAsync(
            userClient, "/login",
            ("identifier", email),
            ("password", temporaryPassword),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, firstLogin.StatusCode);
        Assert.Equal("/change-password", LocationPath(firstLogin));

        // A mudança é obrigatória: qualquer outra superfície desvia para
        // o ecrã de mudança.
        var blocked = await userClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Equal("/change-password", LocationPath(blocked));

        // Escolhe a password privada (via provider — Supabase é dono).
        var change = await PostAsync(
            userClient, "/change-password",
            ("newPassword", "SenhaPrivadaDoChefe1!"),
            ("confirmPassword", "SenhaPrivadaDoChefe1!"));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);

        // Acesso operacional normal: a nova sessão (re-emitida com a nova
        // credencial — o re-sign-in PREFERE o claim Email, D4) entra —
        // sem template, a aterragem é o início sem módulos.
        var normal = await userClient.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
    }

    [Fact]
    public async Task AdminUsers_CreatedOperator_LoginByNumberWithLeadingZeros_ChangesPassword_AndRelogsByBothIdentifiers()
    {
        SeedConfiguredAdmin("operator-admin@example.test", "operator-admin-provider", "AdminPassword123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "operator-admin@example.test", "AdminPassword123!");

        const string operatorId = "0042";
        var (email, temporaryPassword) = await CreateUserViaAppAsync(
            adminClient, name: "Operador 0042", operatorId: operatorId, email: "operator.0042@empresa.pt");

        using var firstClient = CreateClient();
        var firstLogin = await PostAsync(
            firstClient, "/login",
            ("identifier", operatorId),
            ("password", temporaryPassword));
        Assert.Equal(HttpStatusCode.Redirect, firstLogin.StatusCode);
        Assert.Equal("/change-password", LocationPath(firstLogin));

        var change = await PostAsync(
            firstClient, "/change-password",
            ("newPassword", "PasswordDefinitiva123!"),
            ("confirmPassword", "PasswordDefinitiva123!"));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.Equal("/Modules/Home", LocationPath(change));

        using var numberClient = CreateClient();
        var numberLogin = await PostAsync(
            numberClient, "/login",
            ("identifier", operatorId),
            ("password", "PasswordDefinitiva123!"));
        Assert.Equal(HttpStatusCode.Redirect, numberLogin.StatusCode);
        Assert.Equal("/Modules/Home", LocationPath(numberLogin));

        using var emailClient = CreateClient();
        var emailLogin = await PostAsync(
            emailClient, "/login",
            ("identifier", email),
            ("password", "PasswordDefinitiva123!"));
        Assert.Equal(HttpStatusCode.Redirect, emailLogin.StatusCode);
        Assert.Equal("/Modules/Home", LocationPath(emailLogin));

        using var oldClient = CreateClient();
        var oldLogin = await PostAsync(
            oldClient, "/login",
            ("identifier", operatorId),
            ("password", temporaryPassword));
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        Assert.Contains("Credenciais inv", await oldLogin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdminUsers_CreateUser_BootstrapPairShowsOnceOnly()
    {
        SeedConfiguredAdmin("once-admin@example.test", "once-admin-provider", "OnceAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "once-admin@example.test", "OnceAdmin123!");

        var (_, temporaryPassword) = await CreateUserViaAppAsync(
            adminClient, operatorId: "4343", email: "once.user@empresa.pt");

        // O painel vive de TempData: o refresh seguinte JÁ NÃO mostra o
        // par — a entrega é UMA vez, gestão do ADMIN (USERS.md §3).
        var secondRead = await adminClient.GetAsync("/Admin/Users");
        secondRead.EnsureSuccessStatusCode();
        var html = await secondRead.Content.ReadAsStringAsync();
        // O PIN tem 4 algarismos e pode aparecer por acaso noutro sítio da
        // página (datas, números): verifica-se o painel, não os algarismos.
        Assert.DoesNotContain("PIN temporário (mostrado uma vez)", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"<strong>{temporaryPassword}</strong>", html, StringComparison.Ordinal);
        // O email FICA associado e VISIVEL na tabela (o Owner 2026-10-13) —
        // o que NUNCA volta e a PASSWORD (uma vez, TempData).
        Assert.Contains("once.user@empresa.pt", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Worked example 2 (USERS.md L57-71): reset → a password velha falha
    // → nova temporária funciona → mudança obrigatória → normal.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsers_ResetMissingIdentity_RecoversSameUserAndOpensOperationalPages()
    {
        SeedConfiguredAdmin("recovery-admin@example.test", "recovery-admin-provider", "RecoveryAdmin1!");
        using var admin = await LoginAdminAsync(_factory, "recovery-admin@example.test", "RecoveryAdmin1!");
        var (email, _) = await CreateUserViaAppAsync(admin, operatorId:"4461", email:"recover.user@empresa.pt");
        Guid id;
        string oldProvider;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = db.Users.Single(u => u.OperatorId == "4461");
            id = user.Id; oldProvider = user.ProviderUserId;
            var template = new Template { Id=Guid.NewGuid(), TemplateKey="tpl-recovery-operations", Name="Operacional",
                Capabilities=[DmoCapabilities.JobOnCreate,DmoCapabilities.ControloCreate,DmoCapabilities.Boquilhas,DmoCapabilities.Ferramentas] };
            db.Templates.Add(template);
            db.TemplateAssignments.Add(new TemplateAssignment { Id=Guid.NewGuid(), UserId=id, TemplateId=template.Id });
            await db.SaveChangesAsync();
        }
        Assert.True(_factory.AuthProvider.TryDeleteAccount(oldProvider));
        using var reset = await PostAsync(admin, "/Admin/Users?handler=Reset", ("operator_id","4461"));
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        var page = await admin.GetStringAsync("/Admin/Users");
        var password = WebUtility.HtmlDecode(Regex.Match(page,
            @"PIN temporário \(mostrado uma vez\)</span>\s*<strong>([^<]+)</strong>", RegexOptions.IgnoreCase).Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(password));
        using var userClient = CreateClient();
        using var login = await PostAsync(userClient,"/login",("identifier",email),("password",password));
        Assert.Equal("/change-password",LocationPath(login));
        using var change = await PostAsync(userClient,"/change-password",
            ("newPassword","RecoveredPrivate1!"),("confirmPassword","RecoveredPrivate1!"));
        Assert.Equal(HttpStatusCode.Redirect,change.StatusCode);
        foreach (var url in new[] { "/JobOn", "/Controlo/Resumo", "/Boquilhas", "/Ferramentas" })
            Assert.Equal(HttpStatusCode.OK,(await userClient.GetAsync(url)).StatusCode);
        using var check = _factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        var saved = database.Users.Single(u => u.OperatorId == "4461");
        Assert.Equal(id,saved.Id);
        Assert.NotEqual(oldProvider,saved.ProviderUserId);
        Assert.False(saved.RequiresPasswordChange);
        Assert.Single(database.TemplateAssignments.Where(a=>a.UserId==id));
    }

    [Fact]
    public async Task AdminUsers_ResetAccess_OldCredentialFails_NewTemporaryForcesChangeThenNormal()
    {
        SeedConfiguredAdmin("reset-admin@example.test", "reset-admin-provider", "ResetAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "reset-admin@example.test", "ResetAdmin123!");

        var (email, firstTemporary) = await CreateUserViaAppAsync(
            adminClient, operatorId: "4444", email: "reset.user@empresa.pt");

        // O utilizador muda para a SUA password privada.
        using var userClient = CreateClient();
        await PostAsync(
            userClient, "/login",
            ("identifier", email),
            ("password", firstTemporary),
            ("returnUrl", "/"));
        await PostAsync(
            userClient, "/change-password",
            ("newPassword", "SenhaPrivada1!"),
            ("confirmPassword", "SenhaPrivada1!"));
        var normalBefore = await userClient.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.OK, normalBefore.StatusCode);

        // O ADMIN faz o reset de acesso: nova password temporária.
        var reset = await PostAsync(
            adminClient, "/Admin/Users?handler=Reset",
            ("operator_id", "4444"));
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);

        var newPassword = Regex.Match(
            await (await adminClient.GetAsync("/Admin/Users")).Content.ReadAsStringAsync(),
            @"PIN temporário \(mostrado uma vez\)</span>\s*<strong>([^<]+)</strong>",
            RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(newPassword));
        Assert.NotEqual(firstTemporary, newPassword);

        // A password VELHA (privada) deixa de valer.
        using var oldCredentialClient = CreateClient();
        var oldLogin = await PostAsync(
            oldCredentialClient, "/login",
            ("identifier", email),
            ("password", "SenhaPrivada1!"),
            ("returnUrl", "/"));
        var oldLoginHtml = await oldLogin.Content.ReadAsStringAsync();
        // P2-6: a mensagem de credenciais inválidas em PORTUGUÊS.
        Assert.Contains("Credenciais inv", oldLoginHtml, StringComparison.Ordinal);

        // A NOVA temporária funciona e FORÇA a mudança de novo.
        using var newCredentialClient = CreateClient();
        var newLogin = await PostAsync(
            newCredentialClient, "/login",
            ("identifier", email),
            ("password", newPassword),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
        Assert.Equal("/change-password", LocationPath(newLogin));

        await PostAsync(
            newCredentialClient, "/change-password",
            ("newPassword", "SenhaPrivada2!"),
            ("confirmPassword", "SenhaPrivada2!"));
        var normalAfter = await newCredentialClient.GetAsync("/Modules/Home");
        Assert.Equal(HttpStatusCode.OK, normalAfter.StatusCode);
    }

    // ------------------------------------------------------------------
    // Recusas tipadas renderizadas (PRG) — o vocabulário fechado dos enums.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsers_CreateRefusals_RenderTypedMessagesViaPRG()
    {
        SeedConfiguredAdmin("refusal-admin@example.test", "refusal-admin-provider", "RefusalAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "refusal-admin@example.test", "RefusalAdmin123!");

        // Nº de operador com caracteres não-dígitos: recusa tipada
        // InvalidOperator, voltando por query (PRG) com os factos
        // preservados e a mensagem renderizada na folha.
        var invalidOperator = await PostAsync(
            adminClient, "/Admin/Users?handler=Create",
            ("name", "Operador Inválido"),
            ("operator_id", "12a4"),
            ("email", "invalid@empresa.pt"),
            ("template_key", ""));
        Assert.Equal(HttpStatusCode.Redirect, invalidOperator.StatusCode);
        Assert.Equal("InvalidOperator", LocationQueryValue(invalidOperator, "create_refusal"));

        var refusalPage = await adminClient.GetAsync("/Admin/Users?create_refusal=InvalidOperator");
        refusalPage.EnsureSuccessStatusCode();
        var html = await refusalPage.Content.ReadAsStringAsync();
        Assert.Contains("não são dígitos", html, StringComparison.Ordinal);

        // Duplicado: semeia e repete o nº — a recusa é DuplicateOperator.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "7777",
                Name = "Operador Existente",
                ProviderUserId = "provider-existing-7777"
            });
            db.SaveChanges();
        }

        var duplicate = await PostAsync(
            adminClient, "/Admin/Users?handler=Create",
            ("name", "Duplicado"),
            ("operator_id", "7777"),
            ("email", "dup@empresa.pt"),
            ("template_key", ""));
        Assert.Equal(HttpStatusCode.Redirect, duplicate.StatusCode);
        Assert.Equal("DuplicateOperator", LocationQueryValue(duplicate, "create_refusal"));
        // A recusa leva à ficha de quem já tem o nº (Diogo, 19/10/2026).
        var duplicateHtml = await (await adminClient.GetAsync(duplicate.Headers.Location!.OriginalString)).Content.ReadAsStringAsync();
        Assert.Contains("<a class=\"dmo-aviso-link\" href=\"/Admin/Users/Ficha?operator_id=7777\">Abrir a ficha do nº 7777</a></div>", duplicateHtml, StringComparison.Ordinal);

        // Nenhum utilizador foi criado pelas recusas.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Single(db.Users.Where(u => u.OperatorId == "7777").ToList());
        }
    }

    // ------------------------------------------------------------------
    // A lista (D5): factos de aplicação, SEM email, SEM ids; a pílula
    // «Password temporária»; o estado Ativo/Stand-by.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsers_ListShowsApplicationFacts_StandByAndTemplateEditing_ThroughTheSingleRelation()
    {
        SeedConfiguredAdmin("list-admin@example.test", "list-admin-provider", "ListAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "list-admin@example.test", "ListAdmin123!");

        // Template + user com template atribuído na criação.
        var createdTemplate = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Responsável Operacional"),
            ("capabilities", DmoCapabilities.JobOnCreate),
            ("capabilities", DmoCapabilities.ControloApprove));
        Assert.Equal(HttpStatusCode.Redirect, createdTemplate.StatusCode);
        var templateKey = LocationQueryValue(createdTemplate, "template");
        Assert.False(string.IsNullOrWhiteSpace(templateKey));

        await CreateUserViaAppAsync(
            adminClient, name: "Chefe Com Template", operatorId: "4545", email: "com.template@empresa.pt");

        // Atribui a template pelo select da LINHA (a mesma relação).
        var assign = await PostAsync(
            adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4545"),
            ("template_key", templateKey!));
        Assert.Equal(HttpStatusCode.Redirect, assign.StatusCode);

        // A lista de templates mostra quantos utilizadores têm cada uma.
        var templatesHtml = await (await adminClient.GetAsync("/Admin/Templates")).Content.ReadAsStringAsync();
        Assert.Contains(">1 utilizador</a>", templatesHtml, StringComparison.Ordinal);

        // O pill leva à template já na lista dos utilizadores dela (Diogo,
        // 17/10/2026): a lista mostra quem a tem e o duplo clique abre a ficha.
        Assert.Contains($"?template={templateKey}#dmt-users", templatesHtml, StringComparison.Ordinal);
        var selectedHtml = await (await adminClient.GetAsync($"/Admin/Templates?template={templateKey}")).Content.ReadAsStringAsync();
        Assert.Contains("id=\"dmt-users\"", selectedHtml, StringComparison.Ordinal);
        Assert.Contains("Chefe Com Template", selectedHtml, StringComparison.Ordinal);
        Assert.Contains("data-dmo-open=\"/Admin/Users/Ficha?operator_id=4545\"", selectedHtml, StringComparison.Ordinal);

        var list = await adminClient.GetAsync("/Admin/Users");
        list.EnsureSuccessStatusCode();
        var html = await list.Content.ReadAsStringAsync();

        // Factos de aplicação visíveis: nome, nº operador, template
        // (rótulo corrente), estado, pílula de password temporária.
        Assert.Contains("Chefe Com Template", html, StringComparison.Ordinal);
        Assert.Contains("Responsável Operacional", html, StringComparison.Ordinal);
        Assert.Contains("PIN temporário", html, StringComparison.Ordinal);
        Assert.Contains("Ativo", html, StringComparison.Ordinal);

        // O EMAIL NA FOLHA (o Owner 2026-10-13: «o email tem de ficar
        // associado ao user» — o rótulo visível do mockup 13_ADMIN: a coluna
        // Email) e SEM ids visíveis (não há Guids do User).
        Assert.Contains("com.template@empresa.pt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("@(user.Email", html, StringComparison.Ordinal);

        // Stand-by pela linha: o User e a atribuição ficam preservados.
        var standBy = await PostAsync(
            adminClient, "/Admin/Users?handler=StandBy",
            ("operator_id", "4545"),
            ("stand_by", "1"));
        Assert.Equal(HttpStatusCode.Redirect, standBy.StatusCode);

        var afterStandBy = await adminClient.GetAsync("/Admin/Users");
        var standByHtml = await afterStandBy.Content.ReadAsStringAsync();
        Assert.Contains("Arquivado", standByHtml, StringComparison.Ordinal);

        // «Sem template» (select vazio) remove a atribuição corrente.
        var remove = await PostAsync(
            adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4545"),
            ("template_key", ""));
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = db.Users.Include(u => u.CurrentTemplateAssignment)
                .Single(u => u.OperatorId == "4545");
            Assert.Null(user.CurrentTemplateAssignment);
            Assert.True(user.IsStandBy);
        }

        var emptyTemplatesHtml = await (await adminClient.GetAsync("/Admin/Templates")).Content.ReadAsStringAsync();
        Assert.Contains(">0 utilizadores</a>", emptyTemplatesHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminTemplates_Delete_OnlyWhenNoUserHasIt_FromTheOptionsDialog()
    {
        // Diogo, 17/10/2026: «não existe forma de apagar os templates». O
        // «Eliminar template» fica no botão «Opções» e confirma numa janela
        // pequena; uma template com utilizadores não se apaga.
        SeedConfiguredAdmin("del-tpl-admin@example.test", "del-tpl-admin-provider", "DelTpl123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "del-tpl-admin@example.test", "DelTpl123!");

        var created = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Para Apagar"),
            ("capabilities", DmoCapabilities.JobOnCreate));
        var templateKey = LocationQueryValue(created, "template")!;
        await CreateUserViaAppAsync(
            adminClient, name: "Com Template Apagar", operatorId: "4646", email: "apagar@empresa.pt");
        await PostAsync(
            adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4646"),
            ("template_key", templateKey));

        // Em uso: a janela diz quantos utilizadores a têm e não oferece o POST.
        var inUseHtml = await (await adminClient.GetAsync($"/Admin/Templates?template={templateKey}")).Content.ReadAsStringAsync();
        Assert.Contains("data-dmo-dialog=\"dmt-delete-dialog\"", inUseHtml, StringComparison.Ordinal);
        Assert.Contains("atribuída a 1 utilizador.", inUseHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Delete", inUseHtml, StringComparison.Ordinal);

        // Mesmo forçando o POST, a recusa tipada protege a template.
        var refused = await PostAsync(
            adminClient, "/Admin/Templates?handler=Delete",
            ("template_key", templateKey));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Equal("InUse", LocationQueryValue(refused, "delete_refusal"));
        var refusedHtml = await (await adminClient.GetAsync(refused.Headers.Location!.OriginalString)).Content.ReadAsStringAsync();
        Assert.Contains("<a class=\"dmo-aviso-link\" href=\"/Admin/Users\">Abrir os Utilizadores</a></div>", refusedHtml, StringComparison.Ordinal);

        // Sem utilizadores: a janela confirma e o POST apaga.
        await PostAsync(
            adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4646"),
            ("template_key", ""));
        var freeHtml = await (await adminClient.GetAsync($"/Admin/Templates?template={templateKey}")).Content.ReadAsStringAsync();
        Assert.Contains("handler=Delete", freeHtml, StringComparison.Ordinal);

        var deleted = await PostAsync(
            adminClient, "/Admin/Templates?handler=Delete",
            ("template_key", templateKey));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal("1", LocationQueryValue(deleted, "deleted"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.False(db.Templates.Any(t => t.TemplateKey == templateKey));
        Assert.Single(db.Users.Where(u => u.OperatorId == "4646").ToList());
    }

    [Fact]
    public async Task AdminUsers_ListCount_ShowsTheTotal_NotJustTheCurrentPage()
    {
        // 30 utilizadores geridos: a lista mostra 25 por página, mas a
        // contagem do cartão é o total (antes mostrava «25 utilizador(es)»).
        SeedConfiguredAdmin("count-admin@example.test", "count-admin-provider", "CountAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "count-admin@example.test", "CountAdmin123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            for (var i = 1; i <= 30; i++)
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    OperatorId = (5000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Name = $"Operador Contagem {i}",
                    ProviderUserId = $"provider-count-{i}"
                });
            }

            db.SaveChanges();
        }

        var firstPage = await (await adminClient.GetAsync("/Admin/Users")).Content.ReadAsStringAsync();
        Assert.Contains("30 utilizadores", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("25 utilizadores", firstPage, StringComparison.Ordinal);
        Assert.Equal(25, Regex.Matches(firstPage, "name=\"user_id\"").Count);

        var secondPage = await (await adminClient.GetAsync("/Admin/Users?pagina=2")).Content.ReadAsStringAsync();
        Assert.Contains("30 utilizadores", secondPage, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(secondPage, "name=\"user_id\"").Count);
    }

    [Fact]
    public async Task AdminUsers_AdminAssociatedRow_HiddenFromList_BoundaryStillRendersInFicha()
    {
        SeedConfiguredAdmin("marker-admin@example.test", "marker-admin-provider", "MarkerAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "marker-admin@example.test", "MarkerAdmin123!");

        // Uma linha de User cujo email É o do ADMIN das Definições (dados
        // inconsistentes sob o boundary — o ADMIN é o email) + um
        // utilizador gerido normal: o Owner 2026-10-16 («admin nao deve
        // aparecer na lista») quer a linha ADMIN FORA da lista — ela não
        // é um utilizador gerido desta folha.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "9000",
                Name = "Linha ADMIN",
                Email = "marker-admin@example.test",
                ProviderUserId = "marker-admin-provider"
            });
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "1234",
                Name = "Operador Gerido",
                Email = "gerido@example.test",
                ProviderUserId = "provider-operador-gerido"
            });
            db.SaveChanges();
        }

        var page = await adminClient.GetAsync("/Admin/Users");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();

        // A linha da identidade ADMIN não é listada; os geridos ficam.
        Assert.DoesNotContain("Linha ADMIN", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>9000</td>", html, StringComparison.Ordinal);
        Assert.Contains("Operador Gerido", html, StringComparison.Ordinal);
        Assert.Contains("<td>1234</td>", html, StringComparison.Ordinal);

        // A GRAMÁTICA GLOBAL (o Owner 2026-10-12) mantém-se para os
        // utilizadores geridos: seleção por radio, ficha por duplo clique
        // e ZERO botões/forms no tbody inteiro.
        var tbodyStart = html.IndexOf("<tbody", StringComparison.Ordinal);
        var tbodyEnd = html.IndexOf("</tbody>", StringComparison.Ordinal);
        Assert.True(tbodyStart >= 0 && tbodyEnd > tbodyStart, "A tabela dos utilizadores não renderizou.");
        var tbody = html[tbodyStart..tbodyEnd];
        Assert.DoesNotContain("<button", tbody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", tbody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input type=\"radio\"", tbody, StringComparison.Ordinal);

        // A ficha da identidade ADMIN: o boundary defensivo renderiza
        // quando o nº é aberto diretamente — sem ações operacionais.
        var ficha = await adminClient.GetAsync("/Admin/Users/Ficha?operator_id=9000");
        ficha.EnsureSuccessStatusCode();
        var fichaHtml = await ficha.Content.ReadAsStringAsync();
        Assert.Contains("não é gerido aqui", fichaHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"template_key\"", fichaHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Repor palavra-passe", fichaHtml, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // A GRAMÁTICA GLOBAL DAS TABELAS (o Owner 2026-10-12, verbatim:
    // «botoes vivem sempre fora, inverter cor ao selecionar um row, e
    // duplo click na row abre a funçao se existir, no caso dos users
    // abre a folha do user»): ZERO botões na tabela, o radio escondido
    // por linha, a linha escolhida VESTIDA com o BOTÃO INVERTIDO (o
    // padrão exato do .resumo-row-select) e o DUPLO CLIQUE abrindo A
    // FICHA DO UTILIZADOR.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsers_TableHasZeroButtons_RowSelectsInvertedAndDoubleClickOpensTheFicha()
    {
        SeedConfiguredAdmin("grammar-admin@example.test", "grammar-admin-provider", "GrammarAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "grammar-admin@example.test", "GrammarAdmin123!");

        await CreateUserViaAppAsync(
            adminClient, name: "Chefe Gramatica", operatorId: "4646", email: "gramatica@empresa.pt");

        var page = await adminClient.GetAsync("/Admin/Users");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();

        // ZERO botões/selects/forms no TBODY: o trabalho vive na ficha.
        var tbodyStart = html.IndexOf("<tbody", StringComparison.Ordinal);
        var tbodyEnd = html.IndexOf("</tbody>", StringComparison.Ordinal);
        Assert.True(tbodyStart >= 0 && tbodyEnd > tbodyStart, "A tabela dos utilizadores não renderizou.");
        var tbody = html[tbodyStart..tbodyEnd];
        Assert.DoesNotContain("<button", tbody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", tbody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", tbody, StringComparison.OrdinalIgnoreCase);

        // A coluna «Ações» saiu da lista.
        Assert.DoesNotContain("<th>Ações</th>", html, StringComparison.Ordinal);

        // O radio ESCONDIDO por linha (name=user_id, o OperatorId — o
        // fato humano) e a linha canónica: o duplo clique ou o Enter
        // abrem A FICHA do utilizador pelo ouvinte único (dmo-live.js).
        Assert.Contains("data-dmo-list=\"Utilizadores internos\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "data-dmo-open=\"/Admin/Users/Ficha?operator_id=4646\"",
            html, StringComparison.Ordinal);
        Assert.Contains(
            "<input type=\"radio\" name=\"user_id\" value=\"4646\"",
            html, StringComparison.Ordinal);
        Assert.DoesNotContain("ondblclick", tbody, StringComparison.Ordinal);

        // A linha escolhida INVERTE como os botões (Diogo, 16/10/2026): a
        // regra única das tabelas vive no dmo-normalize.css, não na página.
        Assert.DoesNotContain(".dmu-row-select", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminUsers_FichaShowsHumanFacts_AndTheActionsMovedFromTheTablePostToTheSameIndexHandlers()
    {
        SeedConfiguredAdmin("ficha-admin@example.test", "ficha-admin-provider", "FichaAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "ficha-admin@example.test", "FichaAdmin123!");

        // A template + o user (nasce NA APP), com a template atribuída.
        var createdTemplate = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Chefe de Turno"),
            ("capabilities", DmoCapabilities.JobOnCreate));
        Assert.Equal(HttpStatusCode.Redirect, createdTemplate.StatusCode);
        var templateKey = LocationQueryValue(createdTemplate, "template");

        await CreateUserViaAppAsync(
            adminClient, name: "Chefe Da Ficha", operatorId: "4747", email: "ficha@empresa.pt");
        var assign = await PostAsync(
            adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4747"),
            ("template_key", templateKey!));
        Assert.Equal(HttpStatusCode.Redirect, assign.StatusCode);

        var ficha = await adminClient.GetAsync("/Admin/Users/Ficha?operator_id=4747");
        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
        var html = await ficha.Content.ReadAsStringAsync();

        // OS FATOS HUMANOS: o nome, o nº, a template atual PELO NOME, o
        // estado, a senha por mudar e o CreatedAt. SEM EMAIL: a conta entra
        // pelo nº e não precisa de email, só o ADMIN (Diogo, 26/10/2026,
        // cmsg_01K75FTs9bQnbhhH7qttpgraEMxzGkVktKoK7yyAw6RnoU: «podemos
        // remover o block do email»). A SENHA nunca existe aqui.
        Assert.Contains("Chefe Da Ficha — nº 4747", html, StringComparison.Ordinal);
        Assert.Contains("Chefe de Turno", html, StringComparison.Ordinal);
        Assert.Contains("Ativo", html, StringComparison.Ordinal);
        Assert.Contains("Obrigatória — PIN temporário", html, StringComparison.Ordinal);
        Assert.Contains("Criado em", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ficha@empresa.pt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Email</span>", html, StringComparison.Ordinal);
        // Design novo: os factos numa só faixa, e o cartão da template
        // abre-a para editar (Diogo, 17/10/2026).
        Assert.Contains("class=\"dmo-facts dfu-factos\"", html, StringComparison.Ordinal);
        Assert.Contains($"class=\"dmo-fact dmo-fact-link\" href=\"/Admin/Templates?template={templateKey}\"", html, StringComparison.Ordinal);
        Assert.Contains("Editar template", html, StringComparison.Ordinal);

        // AS AÇÕES (os forms movidos da tabela): os MESMOS handlers do
        // Index — as assinaturas intocadas, o PRG e as recusas atuais
        // preservados.
        Assert.Contains("action=\"/Admin/Users?handler=Template\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/Admin/Users?handler=StandBy\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/Admin/Users?handler=Reset\"", html, StringComparison.Ordinal);
        // A template aplica-se ao escolhê-la no dropdown, sem botão (Diogo, 17/10/2026).
        Assert.Contains("name=\"template_key\" data-dmo-autosubmit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Guardar template", html, StringComparison.Ordinal);
        // O BOTÃO ARQUIVAR (o Owner 2026-10-13: «nao deveria ser stand-by
        // isso nao faz sentido. Arquivar»): a ficha do user ATIVO oferece
        // o [Arquivar] — o estado dele mostra «Ativo».
        Assert.Contains(">Arquivar</button>", html, StringComparison.Ordinal);
        // MENOS CLUTTER (Diogo, 17/10/2026): Arquivar, Repor e Eliminar vivem
        // no dropdown «Opções»; a template fica à vista.
        var options = html.IndexOf("<details class=\"dmo-options\"", StringComparison.Ordinal);
        Assert.True(options > html.IndexOf("name=\"template_key\"", StringComparison.Ordinal));
        var optionsEnd = html.IndexOf("</details>", options, StringComparison.Ordinal);
        foreach (var action in new[] { ">Arquivar</button>", "Repor palavra-passe</button>", "Eliminar utilizador</button>" })
        {
            var at = html.IndexOf(action, StringComparison.Ordinal);
            Assert.InRange(at, options, optionsEnd);
        }
        Assert.Contains("Repor palavra-passe", html, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"operator_id\" value=\"4747\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminUsers_FichaActions_SaveAndStayOnTheFicha_ChangingOnlyTheirZones()
    {
        // Deep sweep, 18/10/2026 (n.º 117 / B-admin-13): a template, Arquivar
        // e Repor gravavam, recarregavam e deixavam o ADMIN na lista. Vindas
        // da ficha (origem=ficha), voltam à ficha, com a recusa ou o PIN
        // temporário na pilha do canto; fora das zonas a ficha é igual, e o
        // DmoTroca troca só o que muda. Eliminar continua a voltar à lista.
        SeedConfiguredAdmin("ficha-troca-admin@example.test", "ficha-troca-admin-provider", "FichaTroca123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "ficha-troca-admin@example.test", "FichaTroca123!");
        var createdTemplate = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Chefe da Troca"),
            ("capabilities", DmoCapabilities.JobOnCreate));
        var templateKey = LocationQueryValue(createdTemplate, "template");
        await CreateUserViaAppAsync(
            adminClient, name: "Utilizador Da Troca", operatorId: "4848", email: "troca@empresa.pt");

        const string ficha = "/Admin/Users/Ficha?operator_id=4848";
        var antes = await adminClient.GetStringAsync(ficha);
        Assert.Contains("<main class=\"dfu-page\" aria-labelledby=\"dfu-title\" data-dmo-troca>", antes);
        foreach (var zona in new[] { "dfuAvisos", "dfuFactos", "dfuTemplate", "dfuArquivar" })
            Assert.Contains($"<div id=\"{zona}\" class=\"dmo-zona\" data-dmo-regiao>", antes);
        // As ações ficam como o ADMIN as deixou (as «Opções» abertas), e
        // cada formulário leva a origem.
        Assert.Contains("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>", antes);
        Assert.Equal(3, Regex.Matches(antes, "<input type=\"hidden\" name=\"origem\" value=\"ficha\" />").Count);

        // A template, Arquivar e Repor voltam à ficha.
        var template = await PostAsync(adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4848"), ("template_key", templateKey!), ("origem", "ficha"));
        Assert.Equal(HttpStatusCode.Redirect, template.StatusCode);
        Assert.Equal(ficha, LocationText(template));
        var comTemplate = await adminClient.GetStringAsync(ficha);
        Assert.Contains("Chefe da Troca", Controlo.PesoZonas.Zona(comTemplate, "dfuFactos"));

        var arquivar = await PostAsync(adminClient, "/Admin/Users?handler=StandBy",
            ("operator_id", "4848"), ("stand_by", "1"), ("origem", "ficha"));
        Assert.Equal(ficha, LocationText(arquivar));
        var arquivado = await adminClient.GetStringAsync(ficha);
        Assert.Contains("<strong>Arquivado</strong>", Controlo.PesoZonas.Zona(arquivado, "dfuFactos"));
        Assert.Contains(">Ativar</button>", Controlo.PesoZonas.Zona(arquivado, "dfuArquivar"));

        var repor = await PostAsync(adminClient, "/Admin/Users?handler=Reset",
            ("operator_id", "4848"), ("origem", "ficha"));
        Assert.Equal(ficha, LocationText(repor));
        var reposto = await adminClient.GetStringAsync(ficha);
        var avisos = Controlo.PesoZonas.Zona(reposto, "dfuAvisos");
        Assert.Contains("data-dmo-toast=\"aviso\" data-dmo-toast-fica", avisos);
        Assert.Contains("Novo PIN temporário (mostrado uma vez)", avisos);
        // Mostrado uma vez: a ficha seguinte já não o tem.
        Assert.DoesNotContain("Novo PIN temporário", await adminClient.GetStringAsync(ficha));

        // A recusa volta à ficha, no canto.
        var recusa = await PostAsync(adminClient, "/Admin/Users?handler=Template",
            ("operator_id", "4848"), ("template_key", "nao-existe"), ("origem", "ficha"));
        Assert.Equal("/Admin/Users/Ficha", LocationText(recusa).Split('?')[0]);
        Assert.Equal("4848", LocationQueryValue(recusa, "operator_id"));
        Assert.Equal("UnknownTemplate", LocationQueryValue(recusa, "template_refusal"));
        var recusada = await adminClient.GetStringAsync(LocationText(recusa));
        Assert.Contains("<div data-dmo-toast=\"erro\" class=\"dfu-aviso\" role=\"alert\">A template indicada não existe.</div>", Controlo.PesoZonas.Zona(recusada, "dfuAvisos"));

        // Fora das zonas, a ficha é igual em todos estes estados.
        // Eliminar confirma na janela da casa (regra 118 do deep sweep de
        // 18/10/2026), como as Templates: o «Eliminar utilizador» só a abre,
        // sem pedido; quem chega com ?confirm_delete=1 vê-a já aberta.
        Assert.Contains("<button type=\"button\" class=\"dmo-options-item\" id=\"dfu-eliminar\" data-dmo-dialog=\"dfu-delete-dialog\">Eliminar utilizador</button>", antes);
        var janela = Regex.Match(antes, "<dialog id=\"dfu-delete-dialog\" class=\"dmo-dialog\"[^>]*>[\\s\\S]*?</dialog>").Value;
        Assert.Contains("Eliminar o utilizador Utilizador Da Troca (nº 4848)?", System.Net.WebUtility.HtmlDecode(janela));
        Assert.Contains("<input type=\"hidden\" name=\"confirm_delete\" value=\"1\" />", janela);
        Assert.Contains("data-dmo-dialog-close>Cancelar</button>", janela);
        Assert.DoesNotContain("data-dmo-dialog-aberta=\"1\"", janela);
        // Pela fila da página (B-admin-3): cliques repetidos fazem um só pedido.
        Assert.DoesNotContain("data-dmo-nativo", janela);
        var confirmar = await adminClient.GetStringAsync(ficha + "&confirm_delete=1");
        Assert.Contains("<dialog id=\"dfu-delete-dialog\" class=\"dmo-dialog\" aria-labelledby=\"dfu-delete-title\" data-dmo-dialog-aberta=\"1\">", confirmar);
        Assert.DoesNotContain("dfuEliminar", confirmar);
        var fora = Controlo.PesoZonas.ForaDasZonas(antes);
        foreach (var html in new[] { comTemplate, arquivado, reposto, recusada, confirmar })
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));

        // Sem origem (a lista), tudo volta à lista como antes.
        var daLista = await PostAsync(adminClient, "/Admin/Users?handler=StandBy",
            ("operator_id", "4848"), ("stand_by", "0"));
        Assert.Equal("/Admin/Users", LocationText(daLista));
    }

    [Fact]
    public async Task AdminUsers_FichaOfUnknownOperator_ShowsTheCleanWarning_NoSheetInvented()
    {
        SeedConfiguredAdmin("ficha404-admin@example.test", "ficha404-admin-provider", "Ficha404Admin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "ficha404-admin@example.test", "Ficha404Admin123!");

        var ficha = await adminClient.GetAsync("/Admin/Users/Ficha?operator_id=9999");
        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
        var html = await ficha.Content.ReadAsStringAsync();

        Assert.Contains("O utilizador indicado não existe", html, StringComparison.Ordinal);
        Assert.Contains("Nenhuma ficha foi aberta", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"template_key\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Repor palavra-passe", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Templates (D6): criar/editar com as 3 capabilities Beta, key mintada
    // exibida no detalhe como âncora estável, zero capabilities permitido.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminTemplates_CreateAndEdit_KeyMintedDetailAnchorLabelMutable()
    {
        SeedConfiguredAdmin("tpl-admin@example.test", "tpl-admin-provider", "TplAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "tpl-admin@example.test", "TplAdmin123!");

        // Criar: Nome + as capabilities Beta como checkboxes.
        var create = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Operador Beta"),
            ("capabilities", DmoCapabilities.JobOnCreate));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        var location = LocationText(create);
        Assert.StartsWith("/Admin/Templates", location, StringComparison.Ordinal);
        var templateKey = LocationQueryValue(create, "template");
        Assert.NotNull(templateKey);
        Assert.Matches(@"^tpl-[0-9a-f]{8}$", templateKey);

        // O detalhe abre com a key MINTADA como âncora estável + o estado
        // das checkboxes refletido.
        var detail = await adminClient.GetAsync($"/Admin/Templates?template={templateKey}");
        detail.EnsureSuccessStatusCode();
        var detailHtml = await detail.Content.ReadAsStringAsync();
        Assert.Contains(templateKey, detailHtml, StringComparison.Ordinal);
        Assert.Contains(
            "checked",
            CheckboxTag(detailHtml, $"dmt-edit-{DmoCapabilities.JobOnCreate}"),
            StringComparison.Ordinal);

        // Editar: renomear o rótulo (livre — nunca a âncora) e mudar o
        // conjunto de capabilities.
        var edit = await PostAsync(
            adminClient, "/Admin/Templates?handler=Edit",
            ("template_key", templateKey),
            ("name", "Responsável Operacional (renomeado)"),
            ("capabilities", DmoCapabilities.ControloCreate));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Equal("1", LocationQueryValue(edit, "saved"));

        var editedDetail = await adminClient.GetAsync($"/Admin/Templates?template={templateKey}");
        var editedHtml = await editedDetail.Content.ReadAsStringAsync();
        Assert.Contains("Responsável Operacional (renomeado)", editedHtml, StringComparison.Ordinal);
        Assert.Contains(templateKey, editedHtml, StringComparison.Ordinal);
        Assert.Contains(
            "checked",
            CheckboxTag(editedHtml, $"dmt-edit-{DmoCapabilities.ControloCreate}"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "checked",
            CheckboxTag(editedHtml, $"dmt-edit-{DmoCapabilities.ControloApprove}"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "checked",
            CheckboxTag(editedHtml, $"dmt-edit-{DmoCapabilities.JobOnCreate}"),
            StringComparison.Ordinal);

        var both = await PostAsync(
            adminClient, "/Admin/Templates?handler=Edit",
            ("template_key", templateKey),
            ("name", "Cria e aprova"),
            ("capabilities", DmoCapabilities.ControloCreate),
            ("capabilities", DmoCapabilities.ControloApprove));
        Assert.Null(LocationQueryValue(both, "edit_refusal"));
        var warned = await (await adminClient.GetAsync($"/Admin/Templates?template={templateKey}")).Content.ReadAsStringAsync();
        Assert.Contains("Cria e aprova", warned);
        Assert.Contains("a mesma pessoa cria e aprova o Controlo", warned);
        // É um aviso, não um resultado bom: amarelo (Diogo 17/10/2026, cores com significado).
        Assert.Contains("<div class=\"dmo-aviso warn\" role=\"status\">Aviso: com esta template", warned);
    }

    [Fact]
    public async Task AdminTemplates_ZeroCapabilitiesAllowed_NonBetaVocabularyRefused()
    {
        SeedConfiguredAdmin("caps-admin@example.test", "caps-admin-provider", "CapsAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "caps-admin@example.test", "CapsAdmin123!");

        // Zero capabilities é uma template válida: «Sem capabilities».
        var empty = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Consulta"),
            ("template_key", ""));
        Assert.Equal(HttpStatusCode.Redirect, empty.StatusCode);

        var list = await adminClient.GetAsync("/Admin/Templates");
        list.EnsureSuccessStatusCode();
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.Contains("Sem capabilities", listHtml, StringComparison.Ordinal);

        // O matrix de permissões por módulo do mockup NÃO é vocabulário
        // Beta: um POST forjado com jobon.edit é recusado tipadamente
        // (UnknownCapability) e renderizado por PRG. (O jobon.view passou a
        // ser a chave «Job On ver», Diogo 22/10/2026.)
        var nonBeta = await PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", "Forjada"),
            ("capabilities", "jobon.edit"));
        Assert.Equal(HttpStatusCode.Redirect, nonBeta.StatusCode);
        Assert.Equal("UnknownCapability", LocationQueryValue(nonBeta, "create_refusal"));

        var refusalPage = await adminClient.GetAsync("/Admin/Templates?create_refusal=UnknownCapability");
        var refusalHtml = await refusalPage.Content.ReadAsStringAsync();
        Assert.Contains("Um dos módulos indicados não existe. Nenhuma template foi criada.", refusalHtml, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // D8: os placeholders «Utilizadores»/«Templates de acesso» ficam
    // LIGADOS em Definições; o shell das folhas novas é o da área Admin.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Admin_DefinicoesPlaceholdersAreLinked_AndNewPagesAreDressedWithTheAdminShell()
    {
        SeedConfiguredAdmin("nav-admin@example.test", "nav-admin-provider", "NavAdmin123!");
        using var adminClient = await LoginAdminAsync(
            _factory, "nav-admin@example.test", "NavAdmin123!");

        // BETA: só as Definições comuns. A página Sistema/Setup foi removida.
        foreach (var path in new[] { "/Admin/Users" })
        {
            var page = await adminClient.GetAsync(path);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync();

            var primary = System.Text.RegularExpressions.Regex.Match(
                html, "<nav class=\"dmo-primary-nav\"[\\s\\S]*?</nav>").Value;
            Assert.Contains("Utilizadores", primary, StringComparison.Ordinal);
            Assert.Contains("Templates de acesso", primary, StringComparison.Ordinal);
            Assert.DoesNotContain("Canal Jobon", primary, StringComparison.Ordinal);
            Assert.DoesNotContain(">Setup</a>", primary, StringComparison.Ordinal);
            Assert.DoesNotContain(">Ferramentas</a>", primary, StringComparison.Ordinal);
            Assert.DoesNotContain("dmo-secondary-nav", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Admin/Users\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Admin/Templates\"", html, StringComparison.Ordinal);
        }

        var removedSetup = await adminClient.GetAsync("/Admin/Definicoes/Sistema");
        Assert.Equal(HttpStatusCode.NotFound, removedSetup.StatusCode);

        // As folhas novas vestem o MESMO shell (a função ativa marcada).
        var users = await adminClient.GetAsync("/Admin/Users");
        var usersHtml = await users.Content.ReadAsStringAsync();
        Assert.Contains("Utilizadores", usersHtml, StringComparison.Ordinal);
        Assert.Contains("Templates de acesso", usersHtml, StringComparison.Ordinal);

        var templates = await adminClient.GetAsync("/Admin/Templates");
        var templatesHtml = await templates.Content.ReadAsStringAsync();
        Assert.Contains("Templates de acesso", templatesHtml, StringComparison.Ordinal);
        foreach (var (html, selectedHref) in new[] { (usersHtml, "/Admin/Users"), (templatesHtml, "/Admin/Templates") })
        {
            var primary = System.Text.RegularExpressions.Regex.Match(
                html, "<nav class=\"dmo-primary-nav\"[\\s\\S]*?</nav>").Value;
            Assert.Contains("Utilizadores", primary, StringComparison.Ordinal);
            Assert.Contains("Templates de acesso", primary, StringComparison.Ordinal);
            Assert.DoesNotContain("Canal Jobon", primary, StringComparison.Ordinal);
            Assert.DoesNotContain(">Setup</a>", primary, StringComparison.Ordinal);
            Assert.DoesNotContain(">Ferramentas</a>", primary, StringComparison.Ordinal);
            Assert.DoesNotContain("dmo-secondary-nav", html, StringComparison.Ordinal);
            Assert.Matches($"href=\"{selectedHref}\"\\s+class=\"active\"\\s+aria-current=\"page\"", primary);
        }
    }

    // ------------------------------------------------------------------
    // O gate da pasta /Admin cobre as superfícies novas (D2): anónimo e
    // operador sem claim ADMIN são negados; o ADMIN entra.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminUsersAndTemplatesPages_AreGatedByTheRequireAdminFolderPolicy()
    {
        SeedConfiguredAdmin("gate-admin@example.test", "gate-admin-provider", "GateAdmin123!");

        // Operador sem o claim ADMIN (instalação configurada).
        _factory.AuthProvider.AddAccount("5555@dmo.local", "gate-operator-provider", "OperatorPass123!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "5555",
                Name = "Operador Sem Claim",
                ProviderUserId = "gate-operator-provider"
            });
            db.SaveChanges();
        }

        using var anonymousClient = CreateClient();
        using var operatorClient = CreateClient();

        var operatorLogin = await PostAsync(
            operatorClient, "/login",
            ("identifier", "5555"),
            ("password", "OperatorPass123!"),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, operatorLogin.StatusCode);

        foreach (var path in new[] { "/Admin/Users", "/Admin/Templates" })
        {
            // Anónimo: challenge para a surface de login.
            var anonymous = await anonymousClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            Assert.Equal("/login", LocationPath(anonymous));

            // Operador autenticado SEM o claim ADMIN: negado (forbid →
            // /login) — a policy de pasta cobre as folhas novas desde o
            // nascimento.
            var refused = await operatorClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal("/login", LocationPath(refused));
        }

        // O ADMIN entra nas duas.
        using var adminClient = await LoginAdminAsync(
            _factory, "gate-admin@example.test", "GateAdmin123!");
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Admin/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Admin/Templates")).StatusCode);
    }

    // ------------------------------------------------------------------
    // D4: o caminho nº-operador ({op}@dmo.local) fica intocado para seeds;
    // um email autenticado mas não-atribuído mantém a recusa tipada.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Login_SeedOperatorPathStillWorks_UnattributedEmailKeepsTheTypedRefusal()
    {
        SeedConfiguredAdmin("seed-admin@example.test", "seed-admin-provider", "SeedAdmin123!");

        // O caminho dos seeds: user por nº de operador, conta InMemory
        // {op}@dmo.local — login pelo Nº (intocado pelo corte I).
        _factory.AuthProvider.AddAccount("6060@dmo.local", "seed-operator-provider", "SeedPass123!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                OperatorId = "6060",
                Name = "Operador Seed",
                ProviderUserId = "seed-operator-provider"
            });
            db.SaveChanges();
        }

        using var operatorClient = CreateClient();
        var operatorLogin = await PostAsync(
            operatorClient, "/login",
            ("identifier", "6060"),
            ("password", "SeedPass123!"),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, operatorLogin.StatusCode);
        Assert.Equal("/Modules/Home", LocationPath(operatorLogin));

        // Um email que autentica mas não resolve nem o ADMIN nem nenhum
        // User: a recusa tipada mantém a mensagem.
        _factory.AuthProvider.AddAccount("orphan@empresa.pt", "orphan-provider", "OrphanPass123!");
        using var orphanClient = CreateClient();
        var orphanLogin = await PostAsync(
            orphanClient, "/login",
            ("identifier", "orphan@empresa.pt"),
            ("password", "OrphanPass123!"),
            ("returnUrl", "/"));
        var orphanHtml = await orphanLogin.Content.ReadAsStringAsync();
        Assert.Contains("tem acesso", orphanHtml, StringComparison.Ordinal);
    }
    [Fact]
    public async Task DeleteUser_TwoStepConfirmation_RemovesTheRow_AdminNever()
    {
        // O DELETE REAL (o Owner 2026-10-13: [sim mas eu preciso de apagar
        // os que estao la] — o principio das Tools: a linha sai, os factos
        // congelados ficam): o 1.º clique VESTE o aviso (o redirect ao
        // ?confirm_delete=1), o 2.º apaga; a identidade ADMIN recusa.
        SeedConfiguredAdmin("del-admin@example.test", "pages-admin-del", "DelAdmin123!");
        using var client = await LoginAdminAsync(_factory, "del-admin@example.test", "DelAdmin123!");
        await CreateUserViaAppAsync(client, name: "Para Apagar", operatorId: "4848", email: "del-1@empresa.pt");
        var token = await GetTokenAsync(client, "/Admin/Users");

        // O 1.º clique: a confirmacao (nunca apaga direto).
        var first = await PostAsync(client, "/Admin/Users?handler=Delete",
            ("operator_id", "4848"));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Contains("confirm_delete=1", first.Headers.Location?.ToString(), StringComparison.Ordinal);

        // O 2.º clique: apaga.
        var second = await PostAsync(client, "/Admin/Users?handler=Delete",
            ("operator_id", "4848"), ("confirm_delete", "1"));
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains("deleted=1", second.Headers.Location?.ToString(), StringComparison.Ordinal);

        var list = await client.GetAsync(second.Headers.Location!.ToString());
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Para Apagar", listHtml, StringComparison.Ordinal);
        Assert.Contains("Utilizador eliminado.", listHtml, StringComparison.Ordinal);
        // A mensagem do resultado flutua por cima da página: não empurra a lista.
        Assert.Contains("<div data-dmo-toast=\"ok\" class=\"dmu-aviso\" role=\"status\">Utilizador eliminado", listHtml, StringComparison.Ordinal);

        // A linha ADMIN: recusa tipada (o boundary canonico).
        var adminRow = await PostAsync(client, "/Admin/Users?handler=Delete",
            ("operator_id", "0000"), ("confirm_delete", "1"));
        Assert.Contains("delete_refusal", adminRow.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

}
