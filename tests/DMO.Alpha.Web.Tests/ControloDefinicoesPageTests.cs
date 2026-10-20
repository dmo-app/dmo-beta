using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As Definições do Controlo (Diogo, 17/10/2026: «remover do main, pertence
/// ao controlo» e «associaçao do dir fica no controlo.create»): a pasta
/// partilhada dos PDFs e a tolerância dos Pegamentos vivem num separador do
/// Controlo Criar, e já não no Admin → Definições. A tolerância sem valor
/// guardado mostra a norma 0,20; gravar cria ou atualiza a linha única.
/// A tolerância nunca é recusada (Diogo, 19/10/2026: «nunca bloqueia»): fora
/// do habitual (0,01 a 2,00) ou com mais de duas casas grava com aviso; só o
/// que não é um número sem sinal fica por gravar.
/// </summary>
public sealed class ControloDefinicoesPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Controlo/Definicoes";

    private readonly CustomWebApplicationFactory _factory;

    public ControloDefinicoesPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PegamentosTolerances.RemoveRange(db.PegamentosTolerances);
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

    /// <summary>Um operador com um template que dá só os módulos indicados.</summary>
    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Definicoes{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                TemplateKey = $"tpl-definicoes-{operatorId}",
                Name = $"Definições {operatorId}",
                Capabilities = capabilities
            };
            db.Templates.Add(template);
            var user = new User
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Name = $"Operador {operatorId}",
                ProviderUserId = $"provider-{operatorId}"
            };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        return await LoginAsync(email, password);
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        const string email = "definicoes-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "definicoes-admin-provider", "DefinicoesAdmin1!", email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        return await LoginAsync(email, "DefinicoesAdmin1!");
    }

    private static async Task<string> GetPageAsync(HttpClient client, string? save = null)
    {
        var response = await client.GetAsync($"{PagePath}{(save is null ? null : $"?save={save}")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostToleranceAsync(HttpClient client, string value)
    {
        var html = await GetPageAsync(client);
        return await client.PostAsync(
            $"{PagePath}?handler=SavePegamentosTolerance",
            HtmlFormHelpers.BuildFormPost(
                ("tolerance_mm", value),
                ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
    }

    private List<decimal> StoredTolerances()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return db.PegamentosTolerances.AsNoTracking().Select(t => t.ToleranceMm).ToList();
    }

    [Fact]
    public async Task ControloCriar_SeesThePdfFolderAndTheNorm_InItsOwnTab()
    {
        using var client = await LoginOperatorAsync("4410", DmoCapabilities.ControloCreate);

        var html = await GetPageAsync(client);

        Assert.Contains("Pasta partilhada dos PDFs", html);
        Assert.Contains("data-dmo-pasta-pdf-escolher", html);
        Assert.Contains(">Associar</button>", html);
        Assert.Contains(">Remover</button>", html);
        Assert.Contains("js/dmo-pasta-pdf.js", html);
        Assert.Contains("Pegamentos — tolerância", html);
        Assert.Contains("name=\"tolerance_mm\"", html);
        Assert.Contains("value=\"0.20\"", html);
        Assert.Contains("Sem valor guardado: vale a norma de 0,20 mm.", html);

        var nav = System.Text.RegularExpressions.Regex.Match(html,
            "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
        Assert.Matches("<a href=\"/Controlo/Definicoes\"[^>]*aria-current=\"page\">Definições</a>", nav);
    }

    [Fact]
    public async Task Save_CreatesThenUpdatesTheSingleRow()
    {
        using var client = await LoginOperatorAsync("4411", DmoCapabilities.ControloCreate);

        var first = await PostToleranceAsync(client, "0,30");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Contains("/Controlo/Definicoes", first.Headers.Location!.ToString());
        Assert.Contains("save=ok-tolerancia", first.Headers.Location!.ToString());
        Assert.Equal(new[] { 0.30m }, StoredTolerances());

        var second = await PostToleranceAsync(client, "0.25");
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(new[] { 0.25m }, StoredTolerances());

        var html = await GetPageAsync(client, "ok-tolerancia");
        Assert.Contains("value=\"0.25\"", html);
        Assert.Contains("Tolerância guardada.", html);
        Assert.DoesNotContain("Sem valor guardado", html);

        using var scope = _factory.Services.CreateScope();
        var configured = await scope.ServiceProvider
            .GetRequiredService<IPegamentosToleranceConfiguration>()
            .FindConfiguredAsync();
        Assert.Equal(0.25m, configured);
    }

    [Theory]
    [InlineData("4420", "abc")]
    [InlineData("4422", "-0.1")]
    [InlineData("4425", "")]
    public async Task NotANumberWithoutSign_IsNotSavedAndNothingChanges(string operatorId, string value)
    {
        using var client = await LoginOperatorAsync(operatorId, DmoCapabilities.ControloCreate);

        var response = await PostToleranceAsync(client, value);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("save=tolerancia-invalida", response.Headers.Location!.ToString());
        Assert.Empty(StoredTolerances());

        var html = await GetPageAsync(client, "tolerancia-invalida");
        Assert.Contains("<p data-dmo-toast=\"erro\" role=\"alert\">A tolerância é um número em mm, sem sinal (por exemplo 0,20). Não foi guardada.</p>", html);
    }

    // Diogo, 19/10/2026: «tolerância é escolhida nas definições, está 0.2,
    // nunca bloqueia» e «os Pegamentos é mesmo para ver se existem cotas fora
    // de tolerância, seria estúpido bloquear». Antes, estes valores eram
    // recusados (BP-20261019-005/006); agora gravam e o aviso diz porquê. O
    // aviso fica sem link: resolve-se aqui, no mesmo campo.
    [Theory]
    [InlineData("4421", "0", "0.00", "aviso-tolerancia-fora", "± 0.00 mm, fora do habitual (0.01 a 2.00 mm).")]
    [InlineData("4423", "2.5", "2.50", "aviso-tolerancia-fora", "± 2.50 mm, fora do habitual (0.01 a 2.00 mm).")]
    [InlineData("4424", "0.125", "0.13", "aviso-tolerancia-arredondada", "± 0.13 mm, arredondada a duas casas decimais.")]
    [InlineData("4426", "12,345", "12.35", "aviso-tolerancia-arredondada-fora", "± 12.35 mm, arredondada a duas casas decimais e fora do habitual (0.01 a 2.00 mm).")]
    public async Task OutsideTheUsual_IsSavedWithAWarning(string operatorId, string value, string saved, string save, string detalhe)
    {
        using var client = await LoginOperatorAsync(operatorId, DmoCapabilities.ControloCreate);

        var response = await PostToleranceAsync(client, value);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"save={save}", response.Headers.Location!.ToString());
        Assert.Equal(new[] { decimal.Parse(saved, System.Globalization.CultureInfo.InvariantCulture) }, StoredTolerances());

        var html = await GetPageAsync(client, save);
        Assert.Contains($"<p data-dmo-toast=\"aviso\" role=\"status\">Tolerância guardada: {detalhe} Vale para os próximos registos de Pegamentos.</p>", html);
        Assert.Contains($"value=\"{saved}\"", html);
        Assert.DoesNotContain("dmo-aviso-link", html);

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(decimal.Parse(saved, System.Globalization.CultureInfo.InvariantCulture), await scope.ServiceProvider
            .GetRequiredService<IPegamentosToleranceConfiguration>()
            .FindConfiguredAsync());
    }

    [Fact]
    public async Task WithoutControloCriar_TheTabDoesNotOpenAndNothingIsSaved()
    {
        using var client = await LoginOperatorAsync("4412", DmoCapabilities.Boquilhas);

        var get = await client.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.DoesNotContain("/Controlo/Definicoes", get.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AdminDefinicoes_NoLongerCarryTheControloSettings()
    {
        using var client = await LoginAdminAsync();

        foreach (var url in new[] { "/Admin/Definicoes/Index", "/Admin/Definicoes/Index?definition=controlo", "/Admin/Definicoes/Index?definition=pegamentos" })
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("name=\"tolerance_mm\"", html);
            Assert.DoesNotContain("data-dmo-pasta-pdf-escolher", html);
            Assert.DoesNotContain("<option value=\"controlo\"", html);
            Assert.Contains("Email do ADMIN", html);
        }
    }
}
