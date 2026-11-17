using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;
public partial class FerramentasPagesTests
{
    // Diogo, 10/11/2026: «se uma ferramenta não tem nome que funciona na B2 não deveria ter o
    // dropdown». Sem a B2 escolhida nas Linhas compatíveis, o seletor B2 vem escondido, e o botão
    // B2 é o que o mostra (data-ferr-b2-linha).
    [Fact]
    public async Task Registar_SemAB2Escolhida_EscondeOSeletorB2()
    {
        SeedUser("4496", "Seletor B2", "provider-ferr-b2-escondido", "FerrB2Esc1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Machines.AddRange(new Machine { Code = "B1" }, new Machine { Code = "B2" });
            db.SaveChanges();
        }

        using var client = await LoginClientAsync("4496", "FerrB2Esc1!");
        var html = await GetPageHtmlAsync(client, IndexPath + "?criar=1");
        var criar = html[html.IndexOf("id=\"ferr-create-form\"", StringComparison.Ordinal)..];

        Assert.Matches("<div class=\"dmo-field ferr-w-b2\" hidden=\"hidden\">", criar);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(criar, "data-ferr-b2-linha=\"true\""));
    }
}
