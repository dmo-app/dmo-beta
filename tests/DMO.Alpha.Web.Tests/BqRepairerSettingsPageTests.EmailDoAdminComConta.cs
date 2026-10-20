using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Sweep de 19/10/2026, AD-1: o Setup só aceita como email do ADMIN um
/// email com que já se entra, o atual ou o de um utilizador criado em
/// Admin → Utilizadores. Um email sem conta deixava toda a gente fora do
/// Admin: a sessão do ADMIN atual caía e o email novo não conseguia entrar.
/// </summary>
public sealed partial class BqRepairerSettingsPageTests
{
    /// <summary>Um utilizador criado em Admin → Utilizadores, com a conta no fornecedor.</summary>
    private void SeedUserWithEmail(string email, string password = "Colega123!")
    {
        var providerUserId = $"provider-{email}";
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = "4800",
            Name = "Colega",
            Email = email,
            ProviderUserId = providerUserId
        });
        db.SaveChanges();
    }

    private string GuardadoNoSetup()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return db.ModuleSettings.AsNoTracking()
            .Single(s => s.SettingKey == ModuleSettingKeys.AdminEmail).SettingValue;
    }

    [Fact]
    public async Task AdminEmailSetting_EmailSemConta_NaoGravaEAvisaComLinkParaCriarOUtilizador()
    {
        using var admin = await LoginAdminAsync();
        var token = await GetVerificationTokenAsync(admin, SetupPath);

        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", "colega@example.test"),
                ("admin_email_confirm", "colega@example.test"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var location = save.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("save=admin-email-sem-conta", location, StringComparison.Ordinal);

        // Nada mudou, e o ADMIN continua dentro da app.
        Assert.Equal("repairers-admin@example.test", GuardadoNoSetup());
        var after = await admin.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var html = WebUtility.HtmlDecode(await after.Content.ReadAsStringAsync());
        Assert.Contains(
            "Este email ainda não tem conta. Crie primeiro o utilizador em Admin → Utilizadores. <a class=\"dmo-aviso-link\" href=\"/Admin/Users?email=colega%40example.test#dmuCriar\">Criar o utilizador</a>",
            html,
            StringComparison.Ordinal);

        // O link abre o «Criar utilizador» já com o email escrito.
        var users = await admin.GetAsync("/Admin/Users?email=colega%40example.test");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        Assert.Contains("value=\"colega@example.test\"", await users.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminEmailSetting_EmailDeUmUtilizador_GravaEEsseEmailEntraComoAdmin()
    {
        using var admin = await LoginAdminAsync();
        SeedUserWithEmail("colega@example.test");
        var token = await GetVerificationTokenAsync(admin, SetupPath);

        // Sem distinguir maiúsculas nem espaços, como no login.
        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", " Colega@Example.test "),
                ("admin_email_confirm", "colega@example.test"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains("save=ok-admin", save.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Colega@Example.test", GuardadoNoSetup());

        // O email novo entra e chega ao Admin.
        using var colega = CreateClient();
        var loginToken = await GetVerificationTokenAsync(colega, "/login");
        var login = await colega.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "colega@example.test"),
            ("password", "Colega123!"),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", loginToken)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var setup = await colega.GetAsync(SetupPath);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Contains("value=\"Colega@Example.test\"", await setup.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminEmailSetting_OProprioEmailComOutrasMaiusculas_Grava()
    {
        // O ADMIN atual não precisa de utilizador: já entra com o email.
        using var admin = await LoginAdminAsync();
        var token = await GetVerificationTokenAsync(admin, SetupPath);

        var save = await admin.PostAsync(
            $"{SetupPath}?handler=SaveAdminEmail",
            HtmlFormHelpers.BuildFormPost(
                ("admin_email", "Repairers-Admin@example.test"),
                ("admin_email_confirm", "Repairers-Admin@example.test"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains("save=ok-admin", save.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Repairers-Admin@example.test", GuardadoNoSetup());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(SetupPath)).StatusCode);
    }
}
