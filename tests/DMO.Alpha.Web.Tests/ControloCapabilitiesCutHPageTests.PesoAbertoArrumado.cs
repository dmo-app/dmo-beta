using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // O Peso aberto sem produção (Diogo, 27/10/2026): o cartão «Ferramentas»
    // sai depois de iniciar o Peso e os Job Ons para associar são linhas que
    // se escolhem com um clique, com um só «Associar» no canto, sem botões
    // nas linhas.
    [Fact]
    public async Task PesoAberto_SemFerramentas_EAssociarNumaTabelaComUmSoBotao()
    {
        await SeedRealProductionAsync();
        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var create = scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
            var other = await create.HandleAsync(new CreateJobOn("J20", "5810", db.Machines.Single().Id, "T3", "MF-TOOL-9", "BQ-1"));
            Assert.Equal(CreateJobOnResult.Success, other.Result);
            var peso = new Peso { CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.50m,
                CmToolReferenceFrozen = "5447", CmToolLotFrozen = "3" };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesoId = peso.Id;
        }
        SeedOperacional("4798", "provider-peso-arrumado", "PesoArrumado1!");
        using var client = await LoginClientAsync("4798", "PesoArrumado1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Peso?peso_id={pesoId}");

        Assert.DoesNotContain("id=\"peso-tools-title\"", html);
        var associar = Regex.Match(html, "<div class=\"peso-card peso-associar-producao\" data-peso-associar>[\\s\\S]*?</form>").Value;
        Assert.Contains("Existem 2 Job Ons com esta CM e ainda sem Peso. Quer associar este Peso?", associar);
        Assert.Single(Regex.Matches(associar, "<button"));
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" form=\"pesoAssociarProducaoForm\">Associar</button>", associar);
        var tabela = Regex.Match(associar, "<table[\\s\\S]*?</table>").Value;
        Assert.DoesNotContain("<button", tabela);
        Assert.Equal(2, Regex.Matches(tabela, "<tr data-dmo-row").Count);
        Assert.Contains("<input type=\"radio\" name=\"jobon_id\"", tabela);
        Assert.Contains($"<input type=\"hidden\" name=\"peso_id\" value=\"{pesoId}\" />", associar);
    }
}
