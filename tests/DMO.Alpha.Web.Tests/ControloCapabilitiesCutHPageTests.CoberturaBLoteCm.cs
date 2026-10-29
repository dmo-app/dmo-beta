using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // DMO-400: o Peso sem produção, com uma CM de vários lotes, pede o lote
    // («CM — lote»); sem o escolher a CM fica por resolver, e escolhido o lote
    // fica essa CM e o campo mostra-o selecionado.
    [Fact]
    public async Task PesoSemJobOn_CmComVariosLotes_PedeOLote_ESoResolveQuandoEscolhido()
    {
        await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "CM-LOTE-A", Type = ToolType.Cm, Reference = "ST777", Lot = "11" });
            db.Tools.Add(new Tool { ToolId = "CM-LOTE-B", Type = ToolType.Cm, Reference = "ST777", Lot = "12" });
            await db.SaveChangesAsync();
        }
        SeedOperacional("4875", "provider-peso-cm-lote", "PesoCmLote1!");
        using var client = await LoginClientAsync("4875", "PesoCmLote1!");

        var semLote = await GetPageHtmlAsync(client, "/Controlo/Peso?cm_tool_id=ST777");
        Assert.Contains("<label for=\"cmToolIdLote\">CM — lote</label>", semLote);
        Assert.Contains("<option value=\"\">Escolha o lote</option>", semLote);
        Assert.Contains("<option value=\"CM-LOTE-A\">11</option>", semLote);
        Assert.Contains("<option value=\"CM-LOTE-B\">12</option>", semLote);
        Assert.DoesNotContain("selected=\"selected\">11</option>", semLote);

        var comLote = await GetPageHtmlAsync(client, "/Controlo/Peso?cm_tool_id=ST777&cm_lote=CM-LOTE-B");
        Assert.Contains("<option value=\"CM-LOTE-B\" selected=\"selected\">12</option>", comLote);
        Assert.DoesNotContain("<option value=\"CM-LOTE-A\" selected", comLote);
    }
}
