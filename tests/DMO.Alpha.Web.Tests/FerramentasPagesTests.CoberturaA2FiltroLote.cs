using System.Text.RegularExpressions;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// DMO-404 (beta.90): o filtro de Lote das Ferramentas vem logo a seguir à
/// Referência e filtra de facto a lista por esse lote.
/// </summary>
public partial class FerramentasPagesTests
{
    [Fact]
    public async Task Pesquisar_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote()
    {
        SeedUser("4404", "Ferramentas Lote", "provider-ferr-lote", "FerrLote04!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.AddRange(
                new Tool { ToolId = "Tlote000001", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
                new Tool { ToolId = "Tlote000002", Type = ToolType.Cm, Reference = "5447", Lot = "8" },
                new Tool { ToolId = "Tlote000003", Type = ToolType.Cm, Reference = "5447", Lot = "12" });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4404", "FerrLote04!");

        var todas = await GetPageHtmlAsync(client, $"{IndexPath}?reference=5447");
        Assert.Equal(3, Regex.Matches(todas, "name=\"ferr_tool\"").Count);
        // A ordem dos filtros: Referência, Lote e só depois o Tipo.
        Assert.True(Posicao(todas, "for=\"ferr-reference\"") < Posicao(todas, "for=\"ferr-lot\""));
        Assert.True(Posicao(todas, "for=\"ferr-lot\"") < Posicao(todas, "for=\"ferr-type\""));

        // Com o lote, só essa ferramenta, e o campo guarda o que se escreveu.
        var soUm = await GetPageHtmlAsync(client, $"{IndexPath}?reference=5447&lot=8");
        Assert.Single(Regex.Matches(soUm, "name=\"ferr_tool\""));
        Assert.Contains("CM · 5447 · lote 8", soUm);
        Assert.DoesNotContain("CM · 5447 · lote 3", soUm);
        Assert.DoesNotContain("CM · 5447 · lote 12", soUm);
        var campoLote = Regex.Match(soUm, "<input[^>]*id=\"ferr-lot\"[^>]*>");
        Assert.True(campoLote.Success, "falta o campo Lote");
        Assert.Contains("value=\"8\"", campoLote.Value);
    }
}
