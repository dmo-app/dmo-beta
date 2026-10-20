using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // Sweep de 19/10/2026, 5.7 (m9): o regresso com um TAB passava a regra
    // das Ferramentas, a ferramenta ficava gravada e o redirecionamento dava
    // erro 500. Agora a regra é a do login: o regresso é ignorado e abre a ficha.
    [Theory]
    [InlineData("/\t/outro.site/?x={tool_id}")]
    [InlineData("/\n/outro.site/?x={tool_id}")]
    [InlineData("/ /outro.site/?x={tool_id}")]
    public async Task PostCreate_RegressoComTabOuEspaco_GravaEAbreAFicha_SemErro(string regresso)
    {
        SeedUser("4475", "Ferramentas Owner", "provider-ferr-tab", "FerrTab1!");
        using var client = await LoginClientAsync("4475", "FerrTab1!");

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "4"),
            ("return", regresso),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        Assert.DoesNotContain("continuar", location, StringComparison.Ordinal);
        Assert.DoesNotContain("outro.site", location, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.Tools.AsNoTracking().CountAsync(t => t.Reference == "5447" && t.Lot == "4"));
    }
}
