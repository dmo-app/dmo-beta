using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Tampão e a Punção saíram da ficha da CM (Diogo, 26/10/2026). Guardar a
/// informação técnica sem esses campos não apaga o que estava gravado: o Peso
/// continua a usá-lo numa CM sem Job On.
/// </summary>
public partial class FerramentasPagesTests
{
    [Fact]
    public async Task Ficha_GuardarSemTampaoNemPuncao_MantemOsValoresGravados()
    {
        SeedUser("4484", "Ferramentas Owner", "provider-ferr-semtp", "FerrSemTp1!");
        var toolId = await SeedRealToolAsync();
        using var client = await LoginClientAsync("4484", "FerrSemTp1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.Single(t => t.ToolId == toolId).VolumePuncaoCm3 = 30.2m;
            await db.SaveChangesAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("process_token", "NNPB"), ("nominal_weight", "140"),
            ("diametro_gargalo", "36.85"), ("__RequestVerificationToken", token)));
        Assert.Contains("guardado=tecnica", response.Headers.Location!.OriginalString);

        using var verificar = _factory.Services.CreateScope();
        var tecnica = verificar.ServiceProvider.GetRequiredService<DmoDbContext>().ToolTechnicalContexts.AsNoTracking().Single(t => t.ToolId == toolId);
        Assert.Equal(140m, tecnica.NominalWeightGrams);
        Assert.Equal(28.45m, tecnica.TpDiametroMm);
        Assert.Equal(4.5m, tecnica.TpSagittaMm);
        Assert.Equal(30.2m, tecnica.VolumePuncaoCm3);
    }
}
