using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>Envia o formulário que a página desenha, com o campo mudado (como o browser), e devolve para onde levou.</summary>
    private static async Task<string> EnviarAsync(HttpClient client, string html, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(html, preenchidos);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }

    /// <summary>O elemento que abre com este atributo (id="…" ou data-…="…"), até ao seu fecho. Falha se não existir.</summary>
    private static string Elemento(string html, string atributo)
    {
        var inicio = html.IndexOf(atributo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem {atributo}");
        inicio = html.LastIndexOf('<', inicio);
        var tag = Regex.Match(html[(inicio + 1)..], "^[a-z][a-z0-9]*").Value;
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"{atributo} ficou por fechar");
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

        Assert.Contains("Posições do Armazém", Elemento(html, "id=\"arm-pos-title\""));
        Assert.Matches("<a href=\"/Armazem/Posicoes\"[^>]*aria-current=\"page\">Posições</a>", html);
        Assert.Contains("href=\"/Armazem/Definicoes\"", html);
        Assert.Contains("Ainda não há posições. Crie a primeira acima.", Elemento(html, "id=\"armPosLista\""));
        // Depois do login, quem só tem o Armazém abre-o.
        using var inicio = await client.GetAsync("/");
        Assert.Equal("/Armazem/Posicoes", inicio.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Posicoes_CriarEArquivarNaLinha()
    {
        using var client = await LoginOperatorAsync("6103", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync("/Armazem/Posicoes");

        var depois = await client.GetStringAsync(await EnviarAsync(client, pagina, ("armNovoCodigo", "7001")));
        Assert.Contains("Posição 7001 criada.", Elemento(depois, "id=\"armPosAvisos\""));
        var linha = Elemento(depois, "data-dmo-posicao=\"7001\"");
        Assert.Contains("7001", linha);
        // Sem botões na linha: o estado muda-se no próprio valor.
        Assert.DoesNotContain("<button", linha);
        Assert.Equal("7001", NaBase(db => db.ArmazemPosicoes.AsNoTracking().Single().Codigo));

        var repetida = await client.GetStringAsync(await EnviarAsync(client, depois, ("armNovoCodigo", "7001")));
        Assert.Contains("A posição 7001 já existe.", Elemento(repetida, "id=\"armPosAvisos\""));
        var invalida = await client.GetStringAsync(await EnviarAsync(client, repetida, ("armNovoCodigo", "701")));
        Assert.Contains("O código da posição tem 4 algarismos (ex.: 0042).", Elemento(invalida, "id=\"armPosAvisos\""));
        Assert.Single(NaBase(db => db.ArmazemPosicoes.AsNoTracking().ToList()));

        var id = NaBase(db => db.ArmazemPosicoes.AsNoTracking().Single().Id);
        var campo = $"armPosAtiva-{id}";
        var arquivada = await client.GetStringAsync(await EnviarAsync(client, invalida, (campo, EnvioDoFormulario.Opcao(invalida, campo, "Arquivada"))));
        Assert.Contains("Posição 7001 arquivada.", Elemento(arquivada, "id=\"armPosAvisos\""));
        Assert.False(NaBase(db => db.ArmazemPosicoes.AsNoTracking().Single().Ativa));

        var soArquivadas = await client.GetStringAsync("/Armazem/Posicoes?estado=arquivadas");
        Assert.Contains("7001", Elemento(soArquivadas, "data-dmo-posicao=\"7001\""));
        var soAtivas = Elemento(await client.GetStringAsync("/Armazem/Posicoes?estado=ativas"), "id=\"armPosLista\"");
        Assert.Contains("Nenhuma posição com esse filtro.", soAtivas);
        Assert.DoesNotContain("7001", soAtivas);
    }

    [Fact]
    public async Task Definicoes_CriarEMudarReparadores()
    {
        using var client = await LoginOperatorAsync("6104", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync("/Armazem/Definicoes");

        var depois = await client.GetStringAsync(await EnviarAsync(client, pagina,
            ("armRepNome", "Moldes Lda"), ("armRepFamilias", EnvioDoFormulario.Opcao(pagina, "armRepFamilias", "Só CM"))));
        Assert.Contains("Definição gravada.", Elemento(depois, "id=\"armDefAvisos\""));
        var r = NaBase(db => db.Reparadores.AsNoTracking().Single());
        Assert.Equal(("Moldes Lda", true, false), (r.Nome, r.AplicaCm, r.AplicaMf));
        Assert.Contains("Moldes Lda", Elemento(depois, $"data-dmo-reparador=\"{r.Id}\""));

        var repetido = await client.GetStringAsync(await EnviarAsync(client, depois, ("armRepNome", "moldes lda")));
        Assert.Contains("Já existe um reparador de CM e MF com esse nome.", Elemento(repetido, "id=\"armDefAvisos\""));
        Assert.Single(NaBase(db => db.Reparadores.AsNoTracking().ToList()));

        // O nome no próprio valor: o pedido JSON com o formulário da linha.
        var (acao, campos) = EnvioDoFormulario.Mudar(repetido, $"armRepNomeValor-{r.Id}", " Moldes do Norte ");
        var pedido = new HttpRequestMessage(HttpMethod.Post, acao) { Content = EnvioDoFormulario.Corpo(campos) };
        pedido.Headers.Accept.ParseAdd("application/json");
        using var json = await client.SendAsync(pedido);
        using var resposta = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        Assert.True(resposta.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Moldes do Norte", resposta.RootElement.GetProperty("valor").GetString());

        var recebe = $"armRepRecebe-{r.Id}";
        var estado = $"armRepEstado-{r.Id}";
        var html = await client.GetStringAsync("/Armazem/Definicoes");
        html = await client.GetStringAsync(await EnviarAsync(client, html, (recebe, EnvioDoFormulario.Opcao(html, recebe, "Só MF"))));
        html = await client.GetStringAsync(await EnviarAsync(client, html, (estado, EnvioDoFormulario.Opcao(html, estado, "Arquivado"))));
        Assert.Contains("Definição gravada.", Elemento(html, "id=\"armDefAvisos\""));
        r = NaBase(db => db.Reparadores.AsNoTracking().Single());
        Assert.Equal(("Moldes do Norte", false, true, false), (r.Nome, r.AplicaCm, r.AplicaMf, r.Ativo));
        // Cada envio da linha leva só esse reparador e esse campo.
        Assert.DoesNotContain(EnvioDoFormulario.Mudar(html, recebe, "cm").Campos, c => c.Nome is "nome" or "ativo");
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
        Assert.Contains(DmoCapabilities.ArmazemGestao, html);
    }
}
