using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A SECÇÃO DOS REPARADORES nas Definições do módulo Boquilhas (o bloco
/// dos reparadores — o Owner 2026-10-12: «reparadores sao as entidades
/// para onde enviamos as boquilhas»): a LISTA das entidades (criar/editar
/// o nome; zero entidades = o aviso), o MAPA das 6 máquinas (B1..C3) com
/// o dropdown do reparador de cada uma (o POST grava as chaves
/// ModuleSettings Boquilhas:Repairer&lt;código&gt;) e as recusas tipadas
/// informativas (nome vazio, nome repetido, código fora das 6 máquinas) —
/// a gravação nunca bloqueia a folha, o operador corrige e volta a
/// gravar. Desde 17/10/2026 (Diogo: «separamos as definições») vivem nas
/// Definições do módulo Boquilhas (/Boquilhas/Definicoes), para quem tem o
/// módulo; o email do ADMIN ficou no Setup da Administração.
/// </summary>
public sealed partial class BqRepairerSettingsPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Boquilhas/Definicoes";
    private const string SetupPath = "/Admin/Definicoes/Index";
    private const string AppPath = "/Definicoes?secao=app";

    private readonly CustomWebApplicationFactory _factory;

    public BqRepairerSettingsPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.BqMovements.RemoveRange(db.BqMovements);
        db.BqRepairTraces.RemoveRange(db.BqRepairTraces);
        db.BqRepairers.RemoveRange(db.BqRepairers);
        db.ModuleSettings.RemoveRange(db.ModuleSettings);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Machines.RemoveRange(db.Machines);
        db.Tools.RemoveRange(db.Tools);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>Semeia o ADMIN: o email das Definições (o claim autoriza os POSTs).</summary>
    private void SeedConfiguredAdmin(string email, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting
        {
            SettingKey = ModuleSettingKeys.AdminEmail,
            SettingValue = email
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        SeedConfiguredAdmin(
            "repairers-admin@example.test", "repairers-admin-provider", "RepairersAdmin1!");
        var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "repairers-admin@example.test"),
            ("password", "RepairersAdmin1!"),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>Um operador com o módulo Boquilhas (as Definições do módulo).</summary>
    private Task<HttpClient> LoginBoquilhasAsync(string operatorId = "4711", params string[] capabilities)
        => LoginBoquilhasAsync(_factory, operatorId, capabilities);

    private async Task<HttpClient> LoginBoquilhasAsync(WebApplicationFactory<Program> app, string operatorId, params string[] capabilities)
    {
        var email = $"repairers-{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, $"repairers-{operatorId}-provider", "RepairersOp1!", email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                TemplateKey = $"tpl-repairers-{operatorId}",
                Name = $"Reparadores {operatorId}",
                Capabilities = capabilities.Length == 0 ? [DmoCapabilities.Boquilhas] : capabilities
            };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"repairers-{operatorId}-provider" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", "RepairersOp1!"),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await DefinicoesPin.DesbloquearAsync(app.Services, client);
    }

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string? extraQuery = null)
    {
        var response = await client.GetAsync(
            $"/Definicoes{(string.IsNullOrEmpty(extraQuery) ? null : $"?{extraQuery}")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<int> SeedRepairerAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var repairer = new BqRepairer { Name = name };
        db.BqRepairers.Add(repairer);
        await db.SaveChangesAsync();
        return repairer.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string handler,
        params (string Name, string Value)[] fields)
    {
        var html = await GetPageHtmlAsync(client);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException("No antiforgery token found.");
        return await client.PostAsync(
            $"{PagePath}?handler={handler}",
            HtmlFormHelpers.BuildFormPost(
                fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    [Fact]
    public async Task BoquilhasSection_RendersTheRepairerList_WithZeroEntitiesWarning()
    {
        using var client = await LoginBoquilhasAsync();

        var html = await GetPageHtmlAsync(client);

        // A secção das entidades com o AVISO quando zero (criar a
        // primeira antes do mapa) + a secção da rota por máquina.
        Assert.Contains(">Reparadores<", html);
        Assert.Contains("Sem reparadores criados", html);
        // «Rota por máquina» passou a «Linhas», ao lado dos Reparadores
        // (Diogo, 21/10/2026: «rota por máquina fica apenas Linhas»).
        Assert.Contains(">Linhas<", html);
        Assert.Contains("Crie o primeiro ao lado", html);
    }

    [Fact]
    public async Task BoquilhasSection_WithEntities_RendersTheMachineMapOfTheSixCodes()
    {
        // COM entidades criadas, o MAPA abre: os 6 códigos canónicos com
        // o dropdown do reparador de cada um.
        using var client = await LoginBoquilhasAsync();
        await SeedRepairerAsync("Vidroart");

        var html = await GetPageHtmlAsync(client);

        foreach (var machineCode in BqRepairerRouting.MachineCodes)
        {
            Assert.Contains($"Linha {machineCode}", html);
            // Um formulário por máquina, com o select name=machine_code (o
            // valor «B1|id»; deep sweep, 18/10/2026, A1-M1).
            Assert.Contains($"id=\"machine-route-{machineCode}\" name=\"machine_code\"", html);
        }

        // O dropdown de cada máquina oferece o «— sem reparador —» (o
        // estado truthful: a máquina sem rota) e as entidades.
        Assert.Contains("— sem reparador —", html);
        Assert.Contains("Vidroart", html);

        // Sem botões: escolher o reparador de uma máquina grava logo as
        // rotas (Diogo, 17/10/2026: o dropdown aplica ao escolher).
        Assert.Contains("class=\"adm-route-select\" data-dmo-autosubmit", html);
        var rotas = Cartao(html, "bqDefRotas");
        foreach (var code in BqRepairerRouting.MachineCodes)
        {
            Assert.Contains($"Linha {code}", rotas, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("<button", rotas, StringComparison.Ordinal);
        Assert.DoesNotContain("Rota por máquina", rotas, StringComparison.Ordinal);
        Assert.DoesNotContain("Máquina ", rotas, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"submit\"", rotas, StringComparison.Ordinal);
    }

    /// <summary>O cartão (section) com este id, até ao seu fecho.</summary>
    private static string Cartao(string html, string id)
    {
        var inicio = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"O cartão #{id} não aparece.");
        return html[inicio..html.IndexOf("</section>", inicio, StringComparison.Ordinal)];
    }

    /// <summary>A tabela dos reparadores, dentro do cartão #bqDefReparadores.</summary>
    private static string TabelaDosReparadores(string html)
    {
        var cartao = Cartao(html, "bqDefReparadores");
        var inicio = cartao.IndexOf("<table", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A tabela dos reparadores não aparece.");
        return cartao[inicio..cartao.IndexOf("</table>", inicio, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task CreateRepairer_Post_PersistsTheEntity_AndListsIt()
    {
        using var client = await LoginBoquilhasAsync();

        var response = await PostAsync(
            client, "CreateRepairer", ("name", "Vidroart"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.StartsWith("/Definicoes", location);
        Assert.Contains("save=ok-reparador", location);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var repairer = await db.BqRepairers.AsNoTracking().SingleAsync();
            Assert.Equal("Vidroart", repairer.Name);
        }

        // A lista mostra a entidade criada; o aviso sai; o mapa abre os
        // dropdowns com o nome.
        var html = await GetPageHtmlAsync(client);
        Assert.DoesNotContain("Sem reparadores criados", html);
        Assert.Contains("Vidroart", html);
    }

    [Fact]
    public async Task CreateRepairer_DuplicateName_TypedRefusal_KeepsTheOriginal()
    {
        using var client = await LoginBoquilhasAsync();
        await SeedRepairerAsync("Vidroart");

        var response = await PostAsync(
            client, "CreateRepairer", ("name", "Vidroart"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "save=reparador-existente",
            response.Headers.Location?.OriginalString ?? string.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.BqRepairers.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task EditRepairer_Post_RenamesTheEntity()
    {
        using var client = await LoginBoquilhasAsync();
        var repairerId = await SeedRepairerAsync("Vidroart");

        var response = await PostAsync(
            client,
            "EditRepairer",
            ("repairer_id", repairerId.ToString()),
            ("name", "Boquilharia Central"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "save=ok-reparador",
            response.Headers.Location?.OriginalString ?? string.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var renamed = await db.BqRepairers.AsNoTracking()
            .SingleAsync(r => r.Id == repairerId);
        Assert.Equal("Boquilharia Central", renamed.Name);

        var html = await GetPageHtmlAsync(client);
        Assert.Contains("Boquilharia Central", html);
    }

    [Fact]
    public async Task MachineRoute_Post_SavesTheModuleSettingKey_AndSelectsIt()
    {
        using var client = await LoginBoquilhasAsync();
        var repairerId = await SeedRepairerAsync("Vidroart");

        // A rota de uma máquina: o par «B1|3» no machine_code (sem
        // botões; um formulário por máquina, deep sweep 18/10/2026, A1-M1).
        var response = await PostAsync(
            client,
            "SaveMachineRoutes",
            ("machine_code", "B1|" + repairerId));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "save=ok-rota",
            response.Headers.Location?.OriginalString ?? string.Empty);

        // O POST grava a CHAVE ModuleSettings da máquina B1 com o Id.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var stored = await db.ModuleSettings.AsNoTracking()
            .SingleAsync(s => s.SettingKey == "Boquilhas:RepairerB1");
        Assert.Equal(repairerId.ToString(), stored.SettingValue);

        // O dropdown da B1 VESTE a escolha (selected no Id do reparador).
        var html = await GetPageHtmlAsync(client);
        Assert.Contains($"value=\"B1|{repairerId}\" selected", html);
    }

    [Fact]
    public async Task MachineRoute_EmptyValue_ClearsTheRoute()
    {
        // «— sem reparador —» LIMPA a rota da máquina: estado válido (a
        // saída de uma máquina sem rota grava na mesma, sem reparador).
        using var client = await LoginBoquilhasAsync();
        var repairerId = await SeedRepairerAsync("Vidroart");

        await PostAsync(
            client,
            "SaveMachineRoutes",
            ("machine_code", "C2|" + repairerId));

        using (var scope0 = _factory.Services.CreateScope())
        {
            var db0 = scope0.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Single(await db0.ModuleSettings.AsNoTracking()
                .Where(s => s.SettingKey == "Boquilhas:RepairerC2").ToListAsync());
        }

        var cleared = await PostAsync(
            client,
            "SaveMachineRoutes",
            ("machine_code", "C2|"));

        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        Assert.Contains(
            "save=ok-rota",
            cleared.Headers.Location?.OriginalString ?? string.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Empty(await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == "Boquilhas:RepairerC2").ToListAsync());
    }

    [Fact]
    public async Task MachineRoute_CodeOutsideTheSixMachines_TypedRefusal()
    {
        // O vocabulário fechado (B1..C3): um código fora dele é recusa
        // tipada — nunca texto livre nas chaves.
        using var client = await LoginBoquilhasAsync();
        var repairerId = await SeedRepairerAsync("Vidroart");

        var response = await PostAsync(
            client,
            "SaveMachineRoutes",
            ("machine_code", "B9|" + repairerId));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "save=rota-maquina",
            response.Headers.Location?.OriginalString ?? string.Empty);

        // A recusa tipada via PRG: a folha mostra a mensagem ao voltar
        // (com o save transportado na query).
        var html = await GetPageHtmlAsync(client, "save=rota-maquina");
        Assert.Contains("não é um dos 6 do encaminhamento", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Empty(await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith("Boquilhas:Repairer", StringComparison.Ordinal)).ToListAsync());
    }

    [Fact]
    public async Task EditRepairer_InPlace_AnswersJson_AndTheTableHasNoSaveButton()
    {
        // O nome edita-se no próprio valor da linha (Diogo, 17/10/2026:
        // «para editar, clica-se no valor da linha»): o pedido JSON grava e
        // devolve o valor; o nome vazio é recusado com o aviso.
        using var client = await LoginBoquilhasAsync();
        var repairerId = await SeedRepairerAsync("Vidroart");

        var html = await GetPageHtmlAsync(client);
        Assert.Contains("class=\"dmo-valor\" data-dmo-valor name=\"name\" value=\"Vidroart\"", html);
        // Nas tabelas nunca há botões repetidos (Diogo, 21/10/2026): a linha
        // só tem o nome e abre a ficha com um clique; o nome edita-se e o
        // «Remover reparador» está na ficha.
        var tabela = TabelaDosReparadores(html);
        Assert.Contains($"<tr data-dmo-ficha=\"bqDefFicha-{repairerId}\" tabindex=\"0\"", tabela, StringComparison.Ordinal);
        Assert.Contains(">Vidroart</td>", tabela, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", tabela, StringComparison.Ordinal);
        Assert.DoesNotContain("Opções", tabela, StringComparison.Ordinal);
        var ficha = Cartao(html, "bqDefReparadores");
        ficha = ficha[ficha.IndexOf($"<dialog id=\"bqDefFicha-{repairerId}\"", StringComparison.Ordinal)..];
        ficha = ficha[..ficha.IndexOf("</dialog>", StringComparison.Ordinal)];
        Assert.Contains("class=\"dmo-valor\" data-dmo-valor name=\"name\" value=\"Vidroart\"", ficha, StringComparison.Ordinal);
        Assert.Contains($"data-dmo-dialog=\"bqDefRemover-{repairerId}\"", ficha, StringComparison.Ordinal);
        Assert.Contains(">Remover reparador</button>", ficha, StringComparison.Ordinal);
        // O campo do nome novo também filtra a tabela.
        Assert.Contains("data-dmo-filtra=\"bqDefTabelaReparadores\"", html);
        Assert.DoesNotContain("type=\"submit\"", tabela, StringComparison.Ordinal);
        Assert.DoesNotContain("Hora de viragem", html);

        async Task<string> EditJson(string name)
        {
            var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(client))!;
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{PagePath}?handler=EditRepairer")
            {
                Content = HtmlFormHelpers.BuildFormPost(
                    ("__RequestVerificationToken", token), ("repairer_id", repairerId.ToString()), ("name", name))
            };
            request.Headers.Accept.ParseAdd("application/json");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        var ok = await EditJson(" Boquilharia Central ");
        Assert.Contains("\"ok\":true", ok);
        Assert.Contains("\"valor\":\"Boquilharia Central\"", ok);

        var empty = await EditJson("");
        Assert.Contains("\"ok\":false", empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("Boquilharia Central", (await db.BqRepairers.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task BoquilhasDefinicoes_IsATabOfTheModule_AndOnlyForWhoHasIt()
    {
        using var client = await LoginBoquilhasAsync();
        var html = await GetPageHtmlAsync(client);
        // As Definições estão na barra de cima, com a secção das Boquilhas (Diogo, 22/10/2026).
        Assert.Matches("<a href=\"/Definicoes\"[^>]*aria-current=\"page\">Definições</a>", html);
        Assert.Contains("id=\"bqDefReparadores\"", html);
        Assert.DoesNotContain("href=\"/Boquilhas/Definicoes\"", html);

        // Sem o módulo Boquilhas, a secção não aparece e a página antiga não abre.
        using var other = await LoginBoquilhasAsync("4712", DmoCapabilities.Ferramentas);
        var semModulo = await (await other.GetAsync("/Definicoes")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"bqDefReparadores\"", semModulo);
        var denied = await other.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.DoesNotContain("/Definicoes", denied.Headers.Location!.ToString());
    }

    [Fact]
    public async Task MachineRoute_Posting_WithoutAdminSession_IsRedirectedToLogin()
    {
        // A sessão anónima não grava definições: nada é persistido.
        SeedConfiguredAdmin(
            "repairers-gate-admin@example.test", "repairers-gate-provider", "GateAdmin1!");
        using var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");

        var response = await client.PostAsync(
            $"{PagePath}?handler=CreateRepairer",
            HtmlFormHelpers.BuildFormPost(
                ("name", "Vidroart"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? string.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Empty(await db.BqRepairers.AsNoTracking().ToListAsync());
    }

    // ------------------------------------------------------------------
    // O EMAIL DO ADMIN nas Definições (o Owner 2026-10-16: o ADMIN é o
    // email — «usa uma solução apenas para o mail»)
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdminEmailSetting_SaveUpdatesTheFieldAndPreviousSessionDies()
    {
        using var admin = await LoginAdminAsync();
        var token = await GetVerificationTokenAsync(admin, AppPath);

        // O campo existe com o email corrente do ADMIN.
        var html = await (await admin.GetAsync(AppPath)).Content.ReadAsStringAsync();
        Assert.Contains("Email do ADMIN", html, StringComparison.Ordinal);
        Assert.Contains("value=\"repairers-admin@example.test\"", html, StringComparison.Ordinal);

        // O email novo já tem conta: um utilizador criado em Admin →
        // Utilizadores (um email sem conta não se grava, AD-1).
        SeedUserWithEmail("novo.admin@example.test");

        // Guardar um novo email: PRG com a confirmação.
        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", "novo.admin@example.test"),
                ("admin_email_confirm", "novo.admin@example.test"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains("app=ok-admin", save.Headers.Location?.ToString(), StringComparison.Ordinal);

        // O campo ficou com o novo email.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var setting = db.ModuleSettings.AsNoTracking()
                .Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
            Assert.Equal("novo.admin@example.test", setting.SettingValue);
        }

        // A sessão do ADMIN anterior morre no pedido seguinte (a validação
        // compara o email da sessão com o campo das Definições).
        var after = await admin.GetAsync(AppPath);
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/login", after.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminEmailSetting_InvalidEmail_IsTypedRefusalAndKeepsTheField()
    {
        using var admin = await LoginAdminAsync();
        var token = await GetVerificationTokenAsync(admin, AppPath);

        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", "sem-arroba"),
                ("admin_email_confirm", "sem-arroba"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains("app=admin-email-invalida", save.Headers.Location?.ToString(), StringComparison.Ordinal);

        // O campo anterior fica intocado.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var setting = db.ModuleSettings.AsNoTracking()
                .Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
            Assert.Equal("repairers-admin@example.test", setting.SettingValue);
        }
    }
    [Fact]
    public async Task AdminEmailSetting_ConfirmationMismatch_KeepsTheFieldAndTheSession()
    {
        using var admin = await LoginAdminAsync();
        var token = await GetVerificationTokenAsync(admin, AppPath);

        // Um erro de escrita na confirmação não grava nada: o ADMIN
        // continua dentro da app.
        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", "novo.admin@example.test"),
                ("admin_email_confirm", "novo.admin@example.tset"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains("app=admin-email-diferente", save.Headers.Location?.ToString(), StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var setting = db.ModuleSettings.AsNoTracking()
                .Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
            Assert.Equal("repairers-admin@example.test", setting.SettingValue);
        }

        var after = await admin.GetAsync(AppPath + "&app=admin-email-diferente");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Contains("Os dois emails não coincidem", await after.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
