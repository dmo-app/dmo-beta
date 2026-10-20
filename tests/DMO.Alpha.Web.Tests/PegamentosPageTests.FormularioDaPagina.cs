using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Fase base dos testes (19/10/2026): a costura e a contra costura estão
/// fora do &lt;form&gt; do cartão, ligadas pelo form="peg-registar-…". Os
/// outros testes enviam os nomes que escolhem, por isso um name trocado na
/// contra costura deixava o CI verde e a leitura gravada só com a costura.
/// Aqui vai o envio que a página desenha.
/// </summary>
public partial class PegamentosPageTests
{
    [Fact]
    public async Task LeituraPeloFormularioDaPagina_GravaACosturaEAContraCostura()
    {
        SeedUser("4471", "Pegamentos Formulário", "provider-peg-formulario", "PegForm1!");
        using var client = await LoginClientAsync("4471", "PegForm1!");
        var controloId = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            // A CM com o gargalo: o cartão desenha a linha de leitura nova.
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var t3 = await db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "T3");
            t3.DiametroGargaloMm = 70.00m;
            await db.SaveChangesAsync();
        }

        var sheetUrl = $"{PagePath}?controlo_id={controloId}";
        var html = await GetPageHtmlAsync(client, sheetUrl);
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("costura-Cm", "70.10"), ("contra-Cm", "69.95"));
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var leitura = await db.PegamentosMeasurements.AsNoTracking().SingleAsync();
            Assert.Equal(PegamentosComponent.Cm, leitura.Component);
            Assert.Equal(70.10m, leitura.CosturaMm);
            Assert.Equal(69.95m, leitura.ContraCosturaMm);
        }
    }
}
