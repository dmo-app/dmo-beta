using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // DMO-510 (beta.205): no cartão do Peso do Resumo os números longos
    // (1130.00 g) não são cortados. A página vai para o Chromium
    // (tests/visual/cobertura_a2_browser.py), que mede se algum texto sai cortado.
    [Fact]
    public async Task Resumo_ComPesoDaProducaoAnteriorLongo_ParaOBrowser()
    {
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-18", 1130m, 12);
        SeedSapFabrico("2026-04-18", "2026-04-24", 1131m, 6);
        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            jobOnId = db.Controlos.Single(c => c.Id == controloId).JobOnId;
            var anterior = db.Controlos.SingleOrDefault(c => c.JobOnId == anteriorJobOnId);
            if (anterior is null)
            {
                anterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anteriorJobOnId };
                db.Controlos.Add(anterior);
                db.SaveChanges();
            }
            db.Pesos.Single(p => p.Id == pesoId).ProducaoAnteriorControloId = anterior.Id;
            db.SaveChanges();
        }
        SeedUser("4510", "Peso Operador", "provider-folha-510", "Folha510!");
        using var client = await LoginClientAsync("4510", "Folha510!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));

        Assert.Contains("<td>1130", html);
        ExportarParaOBrowser.Gravar("cobertura-a2-resumo-peso.html", html);
    }
}
