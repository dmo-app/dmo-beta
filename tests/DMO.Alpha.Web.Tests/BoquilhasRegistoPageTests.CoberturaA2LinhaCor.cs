using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// DMO-381 (beta.67): em todos os painéis laterais cada cartão tem a cor da
/// sua linha. O servidor marca o cartão com a linha (data-dmo-linha-cor) e é
/// essa marca que o CSS lê para a cor do cartão e da barra da esquerda.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task PainelDasMaquinas_CadaCartaoLevaALinhaDele_ESoAsLinhasComCor()
    {
        await SeedToolAsync("BQ-COR-1", "RGT381", "81");
        await SeedProductionAsync("BQ-COR-1", "ZQREF-381", "PN-381001", "B1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            // Uma linha sem produção (continua a ter a sua cor) e uma sem cor definida.
            db.Machines.Add(new Machine { Code = "B2" });
            db.Machines.Add(new Machine { Code = "B10" });
            await db.SaveChangesAsync();
        }

        SeedUser("4381", "Cor Operador", "provider-cor-381", "CorPass381!");
        using var client = await LoginClientAsync("4381", "CorPass381!");

        var html = await GetPageHtmlAsync(client, PagePath);

        var comProducao = LinhaDoRail(html, "B1");
        Assert.Contains("data-dmo-linha-cor=\"B1\"", comProducao);
        var semProducao = LinhaDoRail(html, "B2");
        Assert.Contains("Sem produção", semProducao);
        Assert.Contains("data-dmo-linha-cor=\"B2\"", semProducao);
        // Cada cartão leva a sua linha, não a do vizinho.
        Assert.DoesNotContain("data-dmo-linha-cor=\"B2\"", comProducao);
        // Uma linha fora das seis com cor fica sem marca (cor neutra).
        var semCor = LinhaDoRail(html, "B10");
        Assert.DoesNotMatch("data-dmo-linha-cor=\"[^\"]+\"", semCor);
    }
}
