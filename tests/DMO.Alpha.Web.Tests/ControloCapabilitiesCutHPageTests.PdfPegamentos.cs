using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // «PDF Pegamentos» só com medições (Diogo, 27/10/2026,
    // cmsg_01K75FTs9bQnbhhH7qttpgraB4cWtFy2LfgzGWkBMpzRuk): com a folha das
    // ferramentas da produção, o botão aparece no Exportar do Resumo.
    [Fact]
    public async Task Resumo_PdfPegamentos_ApareceComMedicoes()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m };
            folha.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m, NominalGargaloMm = 30m });
            db.Pegamentos.Add(folha);
            await db.SaveChangesAsync();
        }
        SeedOperacional("4796", "provider-pdf-pegamentos", "PdfPegamentos1!");
        using var client = await LoginClientAsync("4796", "PdfPegamentos1!");

        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains(">PDF Pegamentos</button>", resumo);
    }
}
