using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// As listas em tabela são paginadas (o Owner 2026-10-16: «tudo que sejam
/// listas em tabelas limitar numero e criar paginaçao»): 25 linhas por
/// página e a barra de paginação só quando há mais de uma página.
/// </summary>
public sealed class ListPaginationPagesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ListPaginationPagesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    [Fact]
    public async Task AdminUsers_ListShows25PerPage_WithPagerKeepingOtherParameters()
    {
        const string adminEmail = "paging-admin@example.test";
        const string password = "PagingAdmin123!";
        _factory.AuthProvider.AddAccount(adminEmail, "paging-admin-provider", password, adminEmail);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = adminEmail });
            for (var i = 1; i <= 30; i++)
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    OperatorId = (7000 + i).ToString(),
                    Name = $"Paginado {i:00}",
                    ProviderUserId = $"paging-user-{i}"
                });
            }
            db.SaveChanges();
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var loginPage = await client.GetStringAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(loginPage)!;
        var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", adminEmail), ("password", password), ("returnUrl", "/Admin/Users"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page1 = await client.GetStringAsync("/Admin/Users?standby_refusal=x");
        Assert.Contains("Paginado 25", page1);
        Assert.Contains("<main data-dmo-troca", page1);
        Assert.Contains("id=\"dmu-resultados\" data-dmo-regiao", page1);
        Assert.Contains("<nav data-dmo-parcial class=\"dmo-list-pager\"", page1);
        Assert.DoesNotContain("Paginado 26", page1);
        Assert.Contains(">1 de 2</span>", page1);
        Assert.Contains("standby_refusal=x&amp;pagina=2", page1);

        var page2 = await client.GetStringAsync("/Admin/Users?pagina=2");
        Assert.Contains("Paginado 26", page2);
        Assert.Contains("Paginado 30", page2);
        Assert.DoesNotContain("Paginado 25", page2);
        Assert.Contains(">2 de 2</span>", page2);
    }

    [Fact]
    public async Task AdminUsers_ShortList_StillShowsThePagerWithOnePage()
    {
        const string adminEmail = "paging-short@example.test";
        const string password = "PagingShort123!";
        _factory.AuthProvider.AddAccount(adminEmail, "paging-short-provider", password, adminEmail);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = adminEmail });
            db.Users.Add(new User { Id = Guid.NewGuid(), OperatorId = "7101", Name = "Único", ProviderUserId = "paging-short-user" });
            db.SaveChanges();
        }

        using var client = await LoginAsync(adminEmail, password);
        var html = await client.GetStringAsync("/Admin/Users");

        Assert.Contains(">1 de 1</span>", html);
        Assert.DoesNotContain("· 1 registo", html);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var loginPage = await client.GetStringAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(loginPage)!;
        var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email), ("password", password), ("returnUrl", "/Admin/Users"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }
}
