using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Admin;

/// <summary>
/// O TAB «DEFINIÇÕES» na barra de navegação da casa (o achado do Owner
/// 14/10, verbatim: «o botao definiçoes devia ser um tab» + «um tab
/// encostado a direita»): o último tab, encostado à direita, rendered for
/// ADMIN principals only (o claim IsAdmin checked per request — os
/// operadores e o chefe NÃO o vêem; a página Admin/Definicoes
/// re-verifica server-side e fica INTACTA). A lição de 13/10 respeitada
/// (sem duplicados: o tab na navegação global é O acesso principal).
/// </summary>
public sealed class DefinicoesNavTabTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DefinicoesNavTabTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>Semeia o ADMIN: o email das Definições (o claim IsAdmin).</summary>
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

    /// <summary>Semeia um utilizador NORMAL (o chefe ou o operador — o claim
    /// IsAdmin = false, a rota do corte H) com um template que dá o
    /// Planeamento: o /JobOn abre e a navegação desenha-se.</summary>
    private void SeedNormalUser(string operatorId, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount($"{operatorId}@dmo.local", providerUserId, password, $"{operatorId}@dmo.local");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template { TemplateKey = $"tpl-navtab-{operatorId}", Name = "Planeamento", Capabilities = [DmoCapabilities.JobOnCreate] };
        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = "Utilizador Normal",
            ProviderUserId = providerUserId
        };
        db.Templates.Add(template);
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    /// <summary>A navegação da casa (o div com data-active-shell-destination), até ao seu fecho.</summary>
    private static string Navegacao(string html)
    {
        var marca = html.IndexOf("data-active-shell-destination=", StringComparison.Ordinal);
        Assert.True(marca >= 0, "A página não desenhou a navegação.");
        var inicio = html.LastIndexOf("<div", marca, StringComparison.Ordinal);
        var nivel = 0;
        foreach (Match tag in Regex.Matches(html[inicio..], "<div\\b|</div>"))
        {
            nivel += tag.Value == "</div>" ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, tag.Index + tag.Length);
            }
        }

        throw new InvalidOperationException("A navegação ficou por fechar.");
    }

    private async Task<HttpClient> LoginAsync(string identifier, string password)
    {
        var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    [Fact]
    public async Task Admin_SeestTheDefinicoesTab_EncostadoADireita()
    {
        // O TAB «Definições» para o ADMIN: o ÚLTIMO tab, encostado à
        // direita (o margin-inline-start:auto — o alinhamento do Owner),
        // na navegação da casa (a mesma gramática dos outros tabs).
        SeedConfiguredAdmin("navtab-admin@example.test", "navtab-admin-provider", "NavTabAdmin1!");
        using var client = await LoginAsync("navtab-admin@example.test", "NavTabAdmin1!");

        // O LOGIN do admin aterra em /Admin/Users (o fluxo de signIn);
        // a barra de navegação lê-se numa página que renderiza (o JobOn —
        // o CreateClient não segue redirects).
        var landing = await client.GetAsync("/");
        var workHref = landing.Headers.Location?.OriginalString ?? "/JobOn";
        var html = await (await client.GetAsync(workHref)).Content.ReadAsStringAsync();

        // O TAB na navegação: o link para as Definições com o empurrão à
        // direita (o último da fila). A PRECISÃO DO OWNER («no caso do
        // admin o segundo tab não existe»): o tab é a ÚNICA ocorrência —
        // o item «Definições» saiu da barra da admin (a barra fica
        // Utilizadores · Templates).
        Assert.Contains("href=\"/Admin/Definicoes\"", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("href=\"/Admin/Definicoes\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("style=\"margin-inline-start:auto\"", html, StringComparison.Ordinal);
        // Desde 17/10/2026 o tab chama-se «Setup» (Diogo: «o associar email
        // entra numa página de setup»).
        Assert.Contains("aria-label=\"Setup (administração)\"", html, StringComparison.Ordinal);
        Assert.Contains(">Setup</a>", html, StringComparison.Ordinal);

        // A PÁGINA DA ADMIN: a barra interna fica Utilizadores · Templates
        // (o item «Definições» removido — um único acesso pelo tab).
        var adminPage = await (await client.GetAsync("/Admin/Users")).Content.ReadAsStringAsync();
        Assert.Contains("href=\"/Admin/Users\"", adminPage, StringComparison.Ordinal);
        Assert.Contains("href=\"/Admin/Templates\"", adminPage, StringComparison.Ordinal);
        Assert.Equal(1, adminPage.Split("href=\"/Admin/Definicoes\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("href=\"/Admin/Definicoes?definition=", adminPage, StringComparison.Ordinal);

        // A PÁGINA fica INTACTA e acessível ao admin (o tab muda o acesso,
        // não a página).
        var definicoes = await client.GetAsync("/Admin/Definicoes");
        Assert.Equal(HttpStatusCode.OK, definicoes.StatusCode);
    }

    // SAP nas Definições do Admin (Diogo, 21/10/2026 14:11–14:12): um cartão
    // «SAP» com a informação e um LEIA-ME detalhado de como funciona.
    [Fact]
    public async Task Definicoes_Admin_TemOCartaoSap_ComOLeiaMe()
    {
        SeedConfiguredAdmin("navtab-sap@example.test", "navtab-sap-provider", "NavTabSap1!");
        using var client = await LoginAsync("navtab-sap@example.test", "NavTabSap1!");
        var html = await (await client.GetAsync("/Admin/Definicoes")).Content.ReadAsStringAsync();

        Assert.Contains("id=\"admSap\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"admSapLeiaMe\"", html, StringComparison.Ordinal);
        Assert.Contains("LEIA-ME: como funciona", html, StringComparison.Ordinal);
        Assert.Contains("DMO_USO_SAP.vbs", html, StringComparison.Ordinal);
        Assert.Contains("dmo-codigos.txt", html, StringComparison.Ordinal);
        Assert.Contains("Se alguma coisa mudar no SAP", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalUser_DoesNotSeeTheDefinicoesTab()
    {
        // O OPERADOR (o claim IsAdmin = false): NÃO vê o tab — a página
        // re-verifica server-side e o acesso fica gated.
        SeedNormalUser("4200", "navtab-operator-provider", "NavTabOp1!");
        using var client = await LoginAsync("4200", "NavTabOp1!");

        var response = await client.GetAsync("/JobOn");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var nav = Navegacao(await response.Content.ReadAsStringAsync());

        // A navegação do operador desenha o módulo do template, sem o Setup.
        Assert.Contains(">Planeamento</a>", nav, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Admin/Definicoes\"", nav, StringComparison.Ordinal);
        Assert.DoesNotContain("Setup", nav, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_DoesNotSeeTheDefinicoesTab()
    {
        // SEM sessão: nenhum tab (a navegação mostra o que a sessão vê).
        using var client = CreateClient();

        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("href=\"/Admin/Definicoes\"", html, StringComparison.Ordinal);
    }
}
