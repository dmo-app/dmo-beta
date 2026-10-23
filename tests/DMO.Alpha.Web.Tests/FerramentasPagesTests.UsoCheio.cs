using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // O USO CHEIO (Diogo, 23/10/2026): com 100% ou mais a ficha e o Pesquisar
    // abrem como sempre, sem aviso nenhum; só a referência e o lote
    // levam a classe dmo-uso-cheio. Abaixo de 100, nada muda.
    [Theory]
    [InlineData(null, false)]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(130, true)]
    public async Task UsoCheio_SoMudaACor_NaFichaENoPesquisar_NuncaAvisaNemBloqueia(int? uso, bool cheio)
    {
        SeedUser("4995", "Ferramentas Uso Cheio", "provider-ferr-uso-cheio", "FerrCheio1!");
        using var client = await LoginClientAsync("4995", "FerrCheio1!");
        var toolId = await SeedRealToolAsync();
        if (uso is int valor)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new DMO.Alpha.Core.Tools.FerramentaUtilizacao { ToolId = toolId, Percentagem = valor, RegistadaEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var ficha = await client.GetAsync($"{FichaPath}?tool_id={toolId}");
        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
        var fichaHtml = WebUtility.HtmlDecode(await ficha.Content.ReadAsStringAsync());
        var pesquisar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{IndexPath}?reference=5447"));
        Assert.Contains($"data-id=\"{toolId}\"", pesquisar, StringComparison.Ordinal);

        if (cheio)
        {
            Assert.Contains("<strong class=\"dmo-uso-cheio\">5447</strong>", fichaHtml, StringComparison.Ordinal);
            Assert.Contains("<strong class=\"dmo-uso-cheio\">3</strong>", fichaHtml, StringComparison.Ordinal);
            Assert.Contains("<strong class=\"dmo-uso-cheio\">CM · 5447 · lote 3</strong>", pesquisar, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("dmo-uso-cheio", fichaHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("dmo-uso-cheio", pesquisar, StringComparison.Ordinal);
            Assert.Contains("<strong>5447</strong>", fichaHtml, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("id=\"ferrUsoAviso\"", fichaHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("acima de 100", fichaHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("acima de 100", pesquisar, StringComparison.Ordinal);
    }
}
