using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As definições separadas por módulo (Diogo, 17/10/2026: «ferramentas nao
/// deve ficar no admin e o botao nao esta bem» e «separamos as definições
/// para não ter tudo misturado numa página»): os totais de lote nas
/// Definições das Ferramentas, com um único «Guardar»; a hora
/// de viragem no Canal Jobon da Administração; o email do ADMIN e o PIN nas Definições → App.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string FerramentasPath = "/Ferramentas/Definicoes";
    private const string CanalJobonPath = "/Admin/CanalJobon";
    private const string AppPath = "/Definicoes?secao=admin&sub=app";

    private readonly CustomWebApplicationFactory _factory;

    public DefinicoesPorModuloPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.RemoveRange(db.ModuleSettings);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Modulo{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-modulo-{operatorId}", Name = $"Módulo {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        return await DefinicoesPin.DesbloquearAsync(_factory.Services, await LoginAsync(email, password));
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        const string email = "modulos-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "modulos-admin-provider", "ModulosAdmin1!", email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        return await LoginAsync(email, "ModulosAdmin1!");
    }

    private async Task<List<Machine>> SeedMachinesAsync(params string[] codes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var added = new List<Machine>();
        foreach (var code in codes)
            if (!db.Machines.Any(m => m.Code == code)) { var machine = new Machine { Code = code }; db.Machines.Add(machine); added.Add(machine); }
        await db.SaveChangesAsync();
        return added;
    }

    private async Task RemoveAsync(IEnumerable<Machine> machines, params string[] keys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => keys.Contains(s.SettingKey)));
        db.Machines.RemoveRange(db.Machines.Where(m => machines.Select(x => x.Id).Contains(m.Id)));
        await db.SaveChangesAsync();
    }

    private string? Stored(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return db.ModuleSettings.AsNoTracking().SingleOrDefault(s => s.SettingKey == key)?.SettingValue;
    }

    [Fact]
    public async Task FerramentasDefinicoes_TotalsGrid_SavesWithGuardar()
    {
        var added = await SeedMachinesAsync("B1", "C3");
        var b1 = ToolLotQuantityKeys.For(ToolType.Bq, "B1");
        var c3 = ToolLotQuantityKeys.For(ToolType.Bq, "C3");
        try
        {
            using var client = await LoginOperatorAsync("5101", DmoCapabilities.Ferramentas);
            var html = await client.GetStringAsync("/Definicoes");

            // Um separador do módulo Ferramentas; máquinas e componentes
            // legíveis; um único «Guardar» (Diogo, 17/10/2026).
            Assert.Matches("<a href=\"/Definicoes\"[^>]*aria-current=\"page\">Definições</a>", html);
            Assert.Contains("Total do lote por máquina", html);
            Assert.Contains("<span class=\"ferr-totais-maquina mch-B1\">B1</span>", html);
            Assert.Contains("<strong>BQ</strong><small>Boquilha</small>", html);
            Assert.Contains($"name=\"quantities[{b1}]\"", html);
            // O quadro dos totais (#ferrDefTotais) tem um só botão, o «Guardar».
            var inicio = html.IndexOf("id=\"ferrDefTotais\"", StringComparison.Ordinal);
            Assert.True(inicio >= 0, "O quadro dos totais não aparece.");
            var quadro = html[inicio..html.IndexOf("</section>", inicio, StringComparison.Ordinal)];
            Assert.Contains($"name=\"quantities[{c3}]\"", quadro);
            var botao = Assert.Single(System.Text.RegularExpressions.Regex.Matches(quadro, "<button\\b[^>]*>([\\s\\S]*?)</button>"));
            Assert.Equal("Guardar", botao.Groups[1].Value.Trim());

            async Task<HttpResponseMessage> Save(params (string Name, string Value)[] fields)
            {
                var token = HtmlFormHelpers.ExtractRequestVerificationToken(await client.GetStringAsync("/Definicoes"))!;
                return await client.PostAsync($"{FerramentasPath}?handler=SaveToolLotQuantities",
                    HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
            }

            var saved = await Save(($"quantities[{b1}]", "120"), ($"quantities[{c3}]", "90"));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal("120", Stored(b1));
            Assert.Equal("90", Stored(c3));
            var after = await client.GetStringAsync(saved.Headers.Location!.ToString());
            Assert.Contains("Totais guardados.", after);
            Assert.Contains("value=\"120\"", after);

            // A quantidade má fica de fora com aviso; a válida grava
            // (decisão do proprietário, 16/10/2026).
            var partial = await Save(($"quantities[{b1}]", "130"), ($"quantities[{c3}]", "bad"));
            // A gravação volta às Definições (PRG); o aviso vem uma vez (TempData).
            Assert.Equal(HttpStatusCode.Redirect, partial.StatusCode);
            var warned = WebUtility.HtmlDecode(await client.GetStringAsync(partial.Headers.Location!.ToString()));
            Assert.Contains("exceto: BQ na C3 («bad»)", warned);
            Assert.Contains("value=\"130\"", warned);
            Assert.Contains("value=\"90\"", warned);
            Assert.DoesNotContain("value=\"bad\"", warned);
            Assert.Equal("130", Stored(b1));
            Assert.Equal("90", Stored(c3));
        }
        finally
        {
            await RemoveAsync(added, b1, c3);
        }
    }

    [Fact]
    public async Task FerramentasDefinicoes_OnlyForWhoHasTheModule_AndLeftTheAdmin()
    {
        using var other = await LoginOperatorAsync("5102", DmoCapabilities.Boquilhas);
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync(FerramentasPath)).StatusCode);

        using var admin = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Admin/Ferramentas")).StatusCode);
        var pagina = await admin.GetStringAsync("/Definicoes?secao=admin&sub=app");
        var setup = pagina[pagina.IndexOf("id=\"sec-admin\"", StringComparison.Ordinal)..];
        Assert.DoesNotContain("href=\"/Admin/Ferramentas\"", setup);
        Assert.DoesNotContain("total do lote", setup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_TemDefinicoesProprias_CanalJobonEApp()
    {
        // Diogo, 22/10/2026: «canal jobon e app ficam dentro das definições do admin, admin tem definições diferentes dos users».
        using var admin = await LoginAdminAsync();
        foreach (var (antigo, novo) in new[] { ("/Admin/Definicoes?definition=boquilhas", "/Definicoes?secao=admin&sub=app"), ("/Admin/CanalJobon", "/Definicoes?secao=admin&sub=canal"), ("/Definicoes?secao=app", "/Definicoes?secao=admin&sub=app") })
        {
            var r = await admin.GetAsync(antigo);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Equal(novo, r.Headers.Location?.OriginalString);
        }

        var html = await admin.GetStringAsync("/Definicoes?secao=admin&sub=app");
        var menu = System.Text.RegularExpressions.Regex.Match(html, "<nav class=\"def-menu\"[\\s\\S]*?</nav>").Value;
        Assert.Contains("data-dmo-def-grupo=\"admin\" data-aberto>", menu);
        // O grupo fechado não leva data-aberto (o Razor não tira um data-* nulo).
        Assert.DoesNotContain("data-dmo-def-grupo=\"controlo\" data-aberto", menu);
        Assert.Contains("data-dmo-ctl-tab=\"canal\" data-dmo-grupo=\"admin\"", menu);
        Assert.Contains("data-dmo-ctl-tab=\"app\" data-dmo-grupo=\"admin\" class=\"active\"", menu);
        var app = html[html.IndexOf("id=\"sec-admin\"", StringComparison.Ordinal)..];
        Assert.Contains("Email do ADMIN", app);
        Assert.Contains("PIN das Definições", app);
        Assert.Contains("id=\"canalJobonViragem\"", app);
        Assert.Contains("data-dmo-ctl-sub=\"canal\" data-dmo-grupo=\"admin\" hidden=\"hidden\"", app);
        Assert.DoesNotContain("id=\"admModulos\"", app);

        // O Admin fica só com Utilizadores e Templates.
        var users = await admin.GetStringAsync("/Admin/Users");
        Assert.DoesNotContain(">Canal Jobon</a>", users);
        Assert.DoesNotContain(">App</a>", users);
        Assert.DoesNotContain(">Setup</a>", users);
        var templates = await admin.GetStringAsync("/Admin/Templates");
        Assert.DoesNotContain("id=\"admModulos\"", templates);
        Assert.DoesNotContain("name=\"testadores\"", templates);

        // Quem não é ADMIN não vê o grupo.
        using var op = await LoginOperatorAsync("5104", DmoCapabilities.Boquilhas);
        var opHtml = await op.GetStringAsync("/Definicoes?secao=admin&sub=app");
        Assert.DoesNotContain("data-dmo-def-grupo=\"admin\"", opHtml);
        Assert.DoesNotContain("id=\"sec-admin\"", opHtml);
        Assert.DoesNotContain("admEmail", opHtml);
    }

    [Fact]
    public async Task CanalJobon_SavesTheDayChangeHour_ForTheAdminOnly()
    {
        using var admin = await LoginAdminAsync();
        var html = await admin.GetStringAsync("/Definicoes?secao=admin&sub=canal");
        Assert.Contains("Boquilhas — hora de viragem do dia", html);
        Assert.Contains("<option value=\"8\" selected=\"selected\">8:00</option>", html);
        Assert.Contains("data-dmo-autosubmit", html);

        var save = await admin.PostAsync($"{CanalJobonPath}?handler=SaveBoquilhasDayChangeHour", HtmlFormHelpers.BuildFormPost(
            ("dayChangeHour", "6"), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/Definicoes?secao=admin&sub=canal&viragem=ok", save.Headers.Location?.ToString());
        Assert.Equal("6", Stored(ModuleSettingKeys.BoquilhasDayChangeHour));
        Assert.Contains("<option value=\"6\" selected=\"selected\">6:00</option>", await admin.GetStringAsync("/Definicoes?secao=admin&sub=canal"));

        using var op = await LoginOperatorAsync("5103", DmoCapabilities.Boquilhas);
        var denied = await op.GetAsync(CanalJobonPath);
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
    }
}
