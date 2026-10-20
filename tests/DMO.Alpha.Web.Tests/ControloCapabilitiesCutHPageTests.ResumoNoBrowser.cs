using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Teste do agente de 19/10/2026 (T20123bb): no Resumo de uma produção
    // NNPB sem Peso, a 1400 px, «Associar Peso existente» e «Criar Peso»
    // desciam para baixo do título do cartão. A página vai para o Chromium
    // (tests/visual/nok_agente_browser.py), que mede os botões na linha do título.
    [Fact]
    public async Task Resumo_SemPesoComUmPesoParaAssociar_ParaOBrowser()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedOperacional("4714", "provider-oper-resumo-browser", "OperResumo1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
                GlassDensityGramsPerCm3 = 2.50m, CmToolReferenceFrozen = "5447", CmToolLotFrozen = "3" });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4714", "OperResumo1!");

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.Contains(">Associar Peso existente</a>", resumo);
        Assert.Contains(">Criar Peso</a>", resumo);
        ExportarParaOBrowser.Gravar("resumo-sem-peso.html", resumo);
    }
}
