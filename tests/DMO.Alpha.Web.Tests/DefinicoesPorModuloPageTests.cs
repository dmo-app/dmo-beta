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
            // A codificação por linha (Diogo, 28/10/2026) no lugar do «Total do lote por máquina».
            Assert.Contains("Codificação por linha", html);
            Assert.Contains("<span class=\"ferr-totais-maquina mch-B1\">B1</span>", html);
            Assert.Contains("<strong>BQ</strong><small>Boquilha</small>", html);
            Assert.Contains($"name=\"quantities[{b1}]\"", html);
            // O quadro (#ferrDefTotais) tem o «Atualizar» (com quantas ferramentas mudam) e o «Guardar».
            var inicio = html.IndexOf("id=\"ferrDefTotais\"", StringComparison.Ordinal);
            Assert.True(inicio >= 0, "O quadro dos totais não aparece.");
            var quadro = html[inicio..html.IndexOf("</section>", inicio, StringComparison.Ordinal)];
            Assert.Contains($"name=\"quantities[{c3}]\"", quadro);
            var botoes = System.Text.RegularExpressions.Regex.Matches(quadro, "<button\\b[^>]*>([\\s\\S]*?)</button>")
                .Select(m => m.Groups[1].Value.Trim()).ToList();
            Assert.Equal(new[] { "Atualizar (0)", "Guardar" }, botoes);

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
            Assert.Contains("Tabela guardada.", after);
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

    /// <summary>
    /// Garante: a codificação por linha (Diogo, 28/10/2026) mostra os nomes
    /// técnicos editáveis, a B2 Dupla e a B2 Tripla com o seletor (Dupla por
    /// omissão) e escolher a Tripla fica escolhido.
    /// </summary>
    [Fact]
    public async Task FerramentasDefinicoes_Codificacao_SeletorDaB2()
    {
        var added = await SeedMachinesAsync("B2");
        var chaves = new[] { Codificacao.ModoB2Chave }
            .Concat(Codificacao.Tipos.Select(t => ToolLotQuantityKeys.Prefix + t + ":B2")).ToArray();
        try
        {
            using var client = await LoginOperatorAsync("5103", DmoCapabilities.Ferramentas);
            var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Definicoes"));
            Assert.Contains($"name=\"quantities[{Codificacao.ChavePadrao("BQ", Codificacao.B1)}]\" value=\"ABxxxxNX34\"", html);
            // Uma só fila da B2 com o modo num dropdown; aparece só o modo ativo (Diogo, 29/10/2026).
            Assert.Matches("<select name=\"modo\" id=\"ferrDefModoB2\" form=\"ferrDefModoB2Form\" data-dmo-autosubmit[^>]*>\\s*<option value=\"Dupla\" selected=\"selected\">Dupla</option>\\s*<option value=\"Tripla\">Tripla</option>", html);
            Assert.Contains("id=\"ferr-nome-CM-B2-Dupla\"", html);
            Assert.DoesNotContain("id=\"ferr-nome-CM-B2-Tripla\"", html);
            Assert.DoesNotContain("type=\"radio\" name=\"modo\"", html);
            Assert.Contains("id=\"ferrDefAtualizar\"", html);

            var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
            var escolher = await client.PostAsync($"{FerramentasPath}?handler=ModoB2",
                HtmlFormHelpers.BuildFormPost(("modo", "Tripla"), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, escolher.StatusCode);
            Assert.Equal("Tripla", Stored(Codificacao.ModoB2Chave));
            Assert.Equal("72", Stored(ToolLotQuantityKeys.For(ToolType.Cm, "B2")));
            var depois = WebUtility.HtmlDecode(await client.GetStringAsync("/Definicoes"));
            Assert.Contains("<option value=\"Tripla\" selected=\"selected\">Tripla</option>", depois);
            Assert.Contains("id=\"ferr-nome-CM-B2-Tripla\"", depois);
            Assert.DoesNotContain("id=\"ferr-nome-CM-B2-Dupla\"", depois);
        }
        finally
        {
            await RemoveAsync(added, chaves);
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
    public async Task Admin_TemDefinicoesProprias_SoAApp()
    {
        // Diogo, 22/10/2026: «canal jobon e app ficam dentro das definições do admin»; a 25/10 as horas de viragem foram cada uma para o seu módulo e o Admin ficou só com a App.
        using var admin = await LoginAdminAsync();
        foreach (var (antigo, novo) in new[] { ("/Admin/Definicoes?definition=boquilhas", "/Definicoes?secao=admin&sub=app"), ("/Definicoes?secao=app", "/Definicoes?secao=admin&sub=app") })
        {
            var r = await admin.GetAsync(antigo);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Equal(novo, r.Headers.Location?.OriginalString);
        }

        var html = await admin.GetStringAsync("/Definicoes?secao=admin&sub=app");
        var menu = System.Text.RegularExpressions.Regex.Match(html, "<nav class=\"def-menu\"[\\s\\S]*?</nav>").Value;
        // Separadores (Diogo, 26/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraXJAtaKT9Zq5Rd7rwLJZDkP): o ADMIN só tem A minha conta e Admin.
        Assert.Contains("data-dmo-def-separador=\"admin\" data-dmo-def-abre=\"admin\" class=\"active\"", menu);
        Assert.Equal(new[] { "pessoal", "admin" }, System.Text.RegularExpressions.Regex.Matches(menu, "data-dmo-def-separador=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray());
        Assert.DoesNotContain("data-dmo-ctl-tab=\"canal\"", menu);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Admin/CanalJobon")).StatusCode);
        var app = html[html.IndexOf("id=\"sec-admin\"", StringComparison.Ordinal)..];
        Assert.Contains("Email do ADMIN", app);
        Assert.Contains("PIN das Definições", app);
        Assert.DoesNotContain("ViragemRi", app);
        Assert.DoesNotContain("hora de viragem", app, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("data-dmo-def-separador=\"admin\"", opHtml);
        Assert.DoesNotContain("id=\"sec-admin\"", opHtml);
        Assert.DoesNotContain("admEmail", opHtml);
    }

    [Fact]
    public async Task HoraDeViragem_CadaUmaNoSeuModulo_ComOsSeusHandlers()
    {
        using var bq = await LoginOperatorAsync("5105", DmoCapabilities.Boquilhas);
        var html = await bq.GetStringAsync("/Definicoes?secao=boquilhas");
        var cartao = html[html.IndexOf("id=\"bqDefViragem\"", StringComparison.Ordinal)..];
        Assert.Contains("Hora de viragem do dia", cartao);
        Assert.Contains("<option value=\"8\" selected=\"selected\">8:00</option>", cartao);
        Assert.Contains("data-dmo-autosubmit", cartao);

        using var admin = await LoginAdminAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await admin.GetStringAsync("/Definicoes"))!;
        var save = await admin.PostAsync("/Boquilhas/Definicoes?handler=SaveDayChangeHour", HtmlFormHelpers.BuildFormPost(
            ("dayChangeHour", "6"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("6", Stored(ModuleSettingKeys.BoquilhasDayChangeHour));
        Assert.Contains("<option value=\"6\" selected=\"selected\">6:00</option>", await bq.GetStringAsync("/Definicoes?secao=boquilhas"));

        var saveRi = await admin.PostAsync("/ReparacaoInterna/Definicoes?handler=SaveDayChangeHour", HtmlFormHelpers.BuildFormPost(
            ("dayChangeHour", "7"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saveRi.StatusCode);
        Assert.Equal("7", Stored(ModuleSettingKeys.ReparacaoInternaDayChangeHour));
        Assert.Equal("6", Stored(ModuleSettingKeys.BoquilhasDayChangeHour));

        // A Reparação fica com «Boquilhas · criar»; quem só tem «ver» não a vê.
        Assert.Contains("id=\"riDefViragem\"", await bq.GetStringAsync("/Definicoes?secao=reparacao"));
        using var soVer = await LoginOperatorAsync("5106", DmoCapabilities.BoquilhasView);
        Assert.DoesNotContain("id=\"riDefViragem\"", await soVer.GetStringAsync("/Definicoes"));
        Assert.DoesNotContain("id=\"bqDefViragem\"", await soVer.GetStringAsync("/Definicoes"));
    }
}
