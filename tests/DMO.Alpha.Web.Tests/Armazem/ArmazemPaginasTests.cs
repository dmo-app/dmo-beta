using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// As primeiras páginas do Armazém (0.2.0-beta.8): só quem tem a chave
/// armazem.gestao entra; as Posições criam, procuram e arquivam no próprio
/// valor da linha; as Definições criam e mudam os reparadores de CM e MF;
/// o Admin › Templates mostra a chave nova.
/// </summary>
public sealed class ArmazemPaginasTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ArmazemPaginasTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Reparadores.RemoveRange(db.Reparadores);
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
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armazem-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        return await LoginAsync(email, password);
    }

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string pagina, string handler, params (string, string)[] campos)
    {
        var html = await client.GetStringAsync(pagina);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        return await client.PostAsync($"{pagina}?handler={handler}",
            HtmlFormHelpers.BuildFormPost([.. campos, ("__RequestVerificationToken", token)]));
    }

    [Theory]
    [InlineData("/Armazem/Posicoes")]
    [InlineData("/Armazem/Definicoes")]
    public async Task SemAChave_NaoEntra(string pagina)
    {
        using var client = await LoginOperatorAsync("6101", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync(pagina);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ComAChave_AsDuasFuncoesEOMenu()
    {
        using var client = await LoginOperatorAsync("6102", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Posicoes");

        Assert.Contains("<h1 id=\"arm-pos-title\">Posições do Armazém</h1>", html);
        Assert.Matches("<a href=\"/Armazem/Posicoes\"[^>]*aria-current=\"page\">Posições</a>", html);
        Assert.Contains("<a href=\"/Armazem/Definicoes\"", html);
        Assert.Contains("Ainda não há posições. Crie a primeira acima.", html);
        // Depois do login, quem só tem o Armazém abre-o.
        using var inicio = await client.GetAsync("/");
        Assert.Equal("/Armazem/Posicoes", inicio.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Posicoes_CriarEArquivarNaLinha()
    {
        using var client = await LoginOperatorAsync("6103", DmoCapabilities.ArmazemGestao);

        using var criada = await PostAsync(client, "/Armazem/Posicoes", "Criar", ("novo_codigo", "7001a"));
        Assert.Equal(HttpStatusCode.Redirect, criada.StatusCode);
        var depois = await client.GetStringAsync(criada.Headers.Location!.ToString());
        Assert.Contains("Posição 7001A criada.", depois);
        Assert.Contains("<td><strong>7001A</strong></td>", depois);
        // Sem botões na linha: só o estado, que grava ao escolher.
        var linha = depois[depois.IndexOf("<td><strong>7001A</strong></td>", StringComparison.Ordinal)..depois.IndexOf("</tr>", depois.IndexOf("<td><strong>7001A</strong></td>", StringComparison.Ordinal), StringComparison.Ordinal)];
        Assert.DoesNotContain("<button", linha);
        Assert.Contains("data-dmo-autosubmit", linha);

        using var repetida = await PostAsync(client, "/Armazem/Posicoes", "Criar", ("novo_codigo", "7001A"));
        Assert.Contains("A posição 7001A já existe.", await client.GetStringAsync(repetida.Headers.Location!.ToString()));
        using var invalida = await PostAsync(client, "/Armazem/Posicoes", "Criar", ("novo_codigo", "70 01"));
        Assert.Contains("só letras e algarismos", await client.GetStringAsync(invalida.Headers.Location!.ToString()));

        var id = NaBase(db => db.ArmazemPosicoes.AsNoTracking().Single().Id);
        using var arquivada = await PostAsync(client, "/Armazem/Posicoes", "Estado",
            ("posicao_id", id.ToString()), ("posicao_codigo", "7001A"), ("ativa", "nao"));
        Assert.Contains("Posição 7001A arquivada.", await client.GetStringAsync(arquivada.Headers.Location!.ToString()));
        Assert.False(NaBase(db => db.ArmazemPosicoes.AsNoTracking().Single().Ativa));
        Assert.Contains("<td><strong>7001A</strong></td>", await client.GetStringAsync("/Armazem/Posicoes?estado=arquivadas"));
        Assert.Contains("Nenhuma posição com esse filtro.", await client.GetStringAsync("/Armazem/Posicoes?estado=ativas"));
    }

    [Fact]
    public async Task Definicoes_CriarEMudarReparadores()
    {
        using var client = await LoginOperatorAsync("6104", DmoCapabilities.ArmazemGestao);

        using var criado = await PostAsync(client, "/Armazem/Definicoes", "Criar", ("nome", "Moldes Lda"), ("familias", "cm"));
        var depois = await client.GetStringAsync(criado.Headers.Location!.ToString());
        Assert.Contains("Definição gravada.", depois);
        Assert.Contains("value=\"Moldes Lda\"", depois);
        Assert.Contains("<option value=\"cm\" selected=\"selected\">Só CM</option>", depois);

        using var repetido = await PostAsync(client, "/Armazem/Definicoes", "Criar", ("nome", "moldes lda"), ("familias", "ambos"));
        Assert.Contains("Já existe um reparador de CM e MF com esse nome.", await client.GetStringAsync(repetido.Headers.Location!.ToString()));

        var id = NaBase(db => db.Reparadores.AsNoTracking().Single().Id).ToString();
        // O nome no próprio valor: pedido JSON.
        var html = await client.GetStringAsync("/Armazem/Definicoes");
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/Armazem/Definicoes?handler=Mudar")
        {
            Content = HtmlFormHelpers.BuildFormPost(("reparador_id", id), ("nome", " Moldes do Norte "),
                ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!))
        };
        pedido.Headers.Accept.ParseAdd("application/json");
        using var json = await client.SendAsync(pedido);
        using var resposta = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        Assert.True(resposta.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Moldes do Norte", resposta.RootElement.GetProperty("valor").GetString());

        using var familias = await PostAsync(client, "/Armazem/Definicoes", "Mudar", ("reparador_id", id), ("familias", "mf"));
        using var arquivado = await PostAsync(client, "/Armazem/Definicoes", "Mudar", ("reparador_id", id), ("ativo", "nao"));
        var r = NaBase(db => db.Reparadores.AsNoTracking().Single());
        Assert.Equal(("Moldes do Norte", false, true, false), (r.Nome, r.AplicaCm, r.AplicaMf, r.Ativo));
    }

    [Fact]
    public async Task AdminTemplates_MostraAChaveDoArmazem()
    {
        const string email = "armazem-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "armazem-admin-provider", "ArmazemAdmin1!", email);
        NaBase(db =>
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            return db.SaveChanges();
        });
        using var admin = await LoginAsync(email, "ArmazemAdmin1!");

        var html = await admin.GetStringAsync("/Admin/Templates");

        Assert.Contains("Armazém — Gestão", html);
        Assert.Contains("value=\"armazem.gestao\"", html);
    }
}
