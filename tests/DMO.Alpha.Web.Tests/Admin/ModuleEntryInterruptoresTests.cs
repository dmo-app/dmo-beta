using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Admin;

/// <summary>
/// Os interruptores dos módulos no Admin → Setup (plano de uma versão, §2):
/// os módulos em desenvolvimento (Armazém, Reparação Interna) nascem
/// desligados, o ADMIN liga-os para todos ou só para os utilizadores de
/// teste, e as suas páginas mostram a faixa «Em desenvolvimento».
/// </summary>
public sealed class ModuleEntryInterruptoresTests : IDisposable
{
    private const string Faixa = "Em desenvolvimento: pode mudar e os dados podem ser de teste";
    private readonly CustomWebApplicationFactory _factory = new() { ModulosEmDesenvolvimento = "desligado" };

    public ModuleEntryInterruptoresTests()
    {
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.RemoveRange(db.ModuleSettings);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _factory.ResetState();
        _factory.Dispose();
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

    private async Task<(HttpClient Client, Guid UserId)> LoginOperatorAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Modulos{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                TemplateKey = $"tpl-modulos-{operatorId}-{Guid.NewGuid():N}",
                Name = $"Modulos {operatorId}",
                Capabilities = [DmoCapabilities.Boquilhas, DmoCapabilities.ArmazemGestao, DmoCapabilities.ReparacaoInterna]
            };
            db.Templates.Add(template);
            db.Users.Add(new User { Id = userId, OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" });
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = userId, TemplateId = template.Id });
            db.SaveChanges();
        }

        return (await LoginAsync(email, password), userId);
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        const string email = "interruptores-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "interruptores-admin-provider", "Interruptores1!", email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        return await LoginAsync(email, "Interruptores1!");
    }

    private static async Task GuardarAsync(HttpClient admin, string armazem, string ri, params Guid[] testadores)
    {
        var html = await admin.GetStringAsync("/Admin/Definicoes");
        var campos = new List<(string, string)>
        {
            ("modulo_" + DmoCapabilities.ArmazemGestao, armazem),
            ("modulo_" + DmoCapabilities.ReparacaoInterna, ri),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)
        };
        campos.AddRange(testadores.Select(t => ("testadores", t.ToString())));
        var response = await admin.PostAsync("/Admin/Definicoes?handler=SaveModulos", HtmlFormHelpers.BuildFormPost(campos.ToArray()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("save=ok-modulos", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PorOmissao_OsModulosEmDesenvolvimentoFicamDesligados()
    {
        var (operador, _) = await LoginOperatorAsync("7101");

        foreach (var pagina in new[] { "/Armazem/Posicoes", "/ReparacaoInterna" })
        {
            var response = await operador.GetAsync(pagina);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Boquilhas", response.Headers.Location!.ToString());
        }

        var boquilhas = await operador.GetStringAsync("/Boquilhas");
        Assert.DoesNotContain("href=\"/Armazem/Posicoes\"", boquilhas);
        Assert.DoesNotContain("href=\"/ReparacaoInterna\"", boquilhas);
        Assert.DoesNotContain(Faixa, boquilhas);
    }

    [Fact]
    public async Task Setup_MostraOsInterruptoresComOGuardarNoTopo()
    {
        using var admin = await LoginAdminAsync();
        var html = await admin.GetStringAsync("/Admin/Definicoes");

        var cartao = html.IndexOf("<section id=\"admModulos\"", StringComparison.Ordinal);
        Assert.True(cartao > 0);
        var topo = html.IndexOf("<div class=\"dmo-card-topo-acoes\">", cartao, StringComparison.Ordinal);
        var guardar = html.IndexOf("id=\"admModulosGuardar\" form=\"admModulosForm\">Guardar</button>", cartao, StringComparison.Ordinal);
        var formulario = html.IndexOf("<form id=\"admModulosForm\"", cartao, StringComparison.Ordinal);
        Assert.True(topo < guardar && guardar < formulario);
        Assert.Contains("<option value=\"desligado\" selected=\"selected\">Desligado</option>", html);
        Assert.Contains("<option value=\"todos\" selected=\"selected\">Ligado para todos</option>", html);
        Assert.Contains("<span class=\"adm-modulo-dev\">Em desenvolvimento</span>", html);
    }

    [Fact]
    public async Task LigadoSoParaTeste_EntraQuemEDeTesteEVeAFaixa()
    {
        var (testador, testadorId) = await LoginOperatorAsync("7102");
        var (outro, _) = await LoginOperatorAsync("7103");
        using var admin = await LoginAdminAsync();

        await GuardarAsync(admin, "teste", "desligado", testadorId);

        var posicoes = await testador.GetAsync("/Armazem/Posicoes");
        Assert.Equal(HttpStatusCode.OK, posicoes.StatusCode);
        var html = await posicoes.Content.ReadAsStringAsync();
        Assert.Contains($"<div class=\"dmo-faixa-dev\" role=\"note\" data-dmo-em-desenvolvimento>{Faixa}</div>", html);
        Assert.Contains("href=\"/Armazem/Posicoes\"", html);
        Assert.Equal(HttpStatusCode.Redirect, (await testador.GetAsync("/ReparacaoInterna")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await outro.GetAsync("/Armazem/Posicoes")).StatusCode);

        var setup = await admin.GetStringAsync("/Admin/Definicoes");
        Assert.Contains($"name=\"testadores\" value=\"{testadorId}\" checked=\"checked\"", setup);
    }

    [Fact]
    public async Task LigadoParaTodos_EntramTodosOsQueTemOModulo()
    {
        var (operador, _) = await LoginOperatorAsync("7104");
        using var admin = await LoginAdminAsync();

        await GuardarAsync(admin, "todos", "todos");

        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/Armazem/Posicoes")).StatusCode);
        var ri = await operador.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.OK, ri.StatusCode);
        Assert.Contains(Faixa, await ri.Content.ReadAsStringAsync());

        // Um módulo acabado não tem a faixa.
        Assert.DoesNotContain(Faixa, await operador.GetStringAsync("/Boquilhas"));
    }

    [Fact]
    public void Estado_FiltraSoOsModulosDesligados()
    {
        var user = Guid.NewGuid();
        var state = new ModuleSwitchState(
            new Dictionary<string, ModuleSwitchMode>
            {
                [DmoCapabilities.ArmazemGestao] = ModuleSwitchMode.Teste,
                [DmoCapabilities.ReparacaoInterna] = ModuleSwitchMode.Desligado
            },
            [user]);
        string[] caps = [DmoCapabilities.Boquilhas, DmoCapabilities.ArmazemGestao, DmoCapabilities.ReparacaoInterna];

        Assert.Equal([DmoCapabilities.Boquilhas, DmoCapabilities.ArmazemGestao], state.Filter(caps, user));
        Assert.Equal([DmoCapabilities.Boquilhas], state.Filter(caps, Guid.NewGuid()));
    }
}
