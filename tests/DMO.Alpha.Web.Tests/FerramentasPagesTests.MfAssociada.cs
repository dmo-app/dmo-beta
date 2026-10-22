using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

// «Referência MF associada so deve aparecer se um CM tiver referencia
// diferente do MF» (Diogo, 21/10/2026): na ficha, dentro do Geral, só a CM
// com uma referência MF diferente da dela mostra o cartão.
public partial class FerramentasPagesTests
{
    [Theory]
    [InlineData("4481", "CM", "5447", "5810", true)]
    [InlineData("4482", "CM", "5810", "5810", false)]
    [InlineData("4483", "CM", "5810", " 5810 ", false)]
    [InlineData("4484", "MF", "5810", "6018", false)]
    public async Task Ficha_ReferenciaMfAssociada_SoNaCmComReferenciaDiferente(string numero, string tipo, string referencia, string mf, bool aparece)
    {
        var toolId = "Tmf" + Guid.NewGuid().ToString("N")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = toolId, Type = tipo == "CM" ? ToolType.Cm : ToolType.Mf, Reference = referencia, Lot = "1", AssociatedMfReference = mf });
            await db.SaveChangesAsync();
        }

        SeedUser(numero, "Ferramentas MF", "provider-ferr-mf-" + numero, "FerrMf1!");
        using var client = await LoginClientAsync(numero, "FerrMf1!");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));

        var geral = html.IndexOf("<section id=\"ferrGeral\"", StringComparison.Ordinal);
        var editar = html.IndexOf("<section id=\"ferrEditar\"", StringComparison.Ordinal);
        Assert.True(geral > 0 && editar > geral);
        var cartao = "<span>Referência MF associada</span>";
        Assert.Equal(aparece, html[geral..editar].Contains(cartao, StringComparison.Ordinal));
        // Fora do Geral o cartão nunca aparece.
        Assert.DoesNotContain(cartao, html[..geral] + html[editar..]);
    }
}
