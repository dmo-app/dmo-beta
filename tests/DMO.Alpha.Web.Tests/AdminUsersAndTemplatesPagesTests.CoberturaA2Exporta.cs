using System.Net;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class AdminUsersAndTemplatesPagesTests
{
    // DMO-493: a lista dos Utilizadores do Admin, com mais de 25, usa a estrutura
    // fixa das páginas (só o resto desliza). A página vai para o Chromium
    // (tests/visual/cobertura_a2_browser.py), que mede o encaixe em 1366×768.
    [Fact]
    public async Task ListaDosUtilizadores_ComMaisDe25_ParaOBrowser()
    {
        SeedConfiguredAdmin("users-493@example.test", "users-493-provider", "Users493Pass1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            for (var i = 1; i <= 27; i++)
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    OperatorId = $"49{i:00}",
                    Name = $"Utilizador Cobertura {i:00}",
                    ProviderUserId = $"users-493-{i:00}"
                });
            }

            await db.SaveChangesAsync();
        }

        using var admin = await LoginAdminAsync(_factory, "users-493@example.test", "Users493Pass1!");
        var response = await admin.GetAsync("/Admin/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Utilizador Cobertura 01", html);
        ExportarParaOBrowser.Gravar("cobertura-a2-admin-users.html", html);
    }
}
