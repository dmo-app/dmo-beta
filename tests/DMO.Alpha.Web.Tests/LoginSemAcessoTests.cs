using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Teste do ensaio (27/10/2026): um operador que abre uma página do Admin
/// continua a ir para /login, mas o login mostra «Sem acesso» em vez do
/// formulário de entrada; sem sessão, o login fica como sempre.
/// </summary>
public sealed class LoginSemAcessoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LoginSemAcessoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void Seed()
    {
        const string admin = "semacesso-admin@example.test";
        _factory.AuthProvider.AddAccount(admin, "semacesso-admin-provider", "SemAcessoAdmin1!", admin);
        _factory.AuthProvider.AddAccount("4321@dmo.local", "semacesso-operador-provider", "SemAcessoOp1!", "4321@dmo.local");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = admin });
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = "4321",
            Name = "Operador Sem Acesso",
            ProviderUserId = "semacesso-operador-provider"
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Operador_NoAdmin_VeSemAcesso_SemFormularioDeEntrada()
    {
        Seed();
        using var client = CreateClient();
        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "4321"), ("password", "SemAcessoOp1!"), ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var recusa = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, recusa.StatusCode);
        Assert.Equal("/login", recusa.Headers.Location?.AbsolutePath);

        var pagina = WebUtility.HtmlDecode(await (await client.GetAsync(recusa.Headers.Location!.OriginalString)).Content.ReadAsStringAsync());
        Assert.Contains("id=\"login-sem-acesso\"", pagina);
        Assert.Contains("Sem acesso", pagina);
        Assert.DoesNotContain("name=\"password\"", pagina);
    }

    [Fact]
    public async Task SemSessao_OLoginMostraOFormulario()
    {
        Seed();
        using var client = CreateClient();
        var recusa = await client.GetAsync("/Admin/Users");
        Assert.Equal("/login", recusa.Headers.Location?.AbsolutePath);
        var pagina = await (await client.GetAsync(recusa.Headers.Location!.OriginalString)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("login-sem-acesso", pagina);
        Assert.Contains("name=\"password\"", pagina);
    }
}
