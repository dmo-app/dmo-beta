using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Guarda o HTML do Peso sem Job On (DMO-473) para
    // tests/visual/cobertura_b_browser.py medir a arrumação a 1366x768.
    [Fact]
    public async Task PesoSemJobOn_ComCmEBq_ParaOBrowser()
    {
        await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "BQ-B182", Type = ToolType.Bq, Reference = "C142", Lot = "182" });
            db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-B182", VolumeMarisaCm3 = 10m });
            await db.SaveChangesAsync();
        }
        SeedOperacional("4883", "provider-peso-b-export", "PesoBExp1!");
        using var client = await LoginClientAsync("4883", "PesoBExp1!");

        var html = await GetPageHtmlAsync(client, "/Controlo/Peso?cm_tool_id=5447&bq_tool_id=C142");

        Assert.Contains("data-peso-ferramentas", html);
        Assert.Contains("C142", html);
        ExportarParaOBrowser.Gravar("cobertura-b-peso-sem-jobon.html", html);
    }
}
