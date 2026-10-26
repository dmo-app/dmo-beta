using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A FERRAMENTA A 100% NA FOLHA DO JOB ON (Diogo, 23/10/2026): a referência e
/// o lote da ferramenta com 100% ou mais de uso ficam só com a cor ligeira
/// (.dmo-uso-cheio), como no resto da app; as outras ficam como estão.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_FerramentaA100PorCento_ReferenciaELoteComCorLigeira()
    {
        SeedUser("4421", "Chefe uso", "provider-folha-uso", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "F-CM", Percentagem = 100m, RegistadaEmUtc = DateTime.UtcNow });
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "F-MF", Percentagem = 40m, RegistadaEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4421", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");

        Assert.Contains("<b data-jof-tool-ref class=\"dmo-uso-cheio\">5809</b>", html, StringComparison.Ordinal);
        Assert.Contains("<b data-jof-tool-lote class=\"dmo-uso-cheio\">", html, StringComparison.Ordinal);
        Assert.Contains("<b data-jof-tool-ref>5810</b>", html, StringComparison.Ordinal);
        Assert.Contains("<b data-jof-tool-ref>T188</b>", html, StringComparison.Ordinal);
    }
}
