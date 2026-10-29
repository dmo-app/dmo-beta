using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    // No Duplicar a utilização não vem da folha de origem: é a % de uso atual da ferramenta (Diogo, 29/10/2026:
    // «dupliquei o jobon e % uso do CM passou 6.7% para 2%»).
    [Fact]
    public async Task Duplicar_UtilizacaoEAPercentagemAtualDaFerramenta_NaoADaOrigem()
    {
        SeedUser("4463", "Chefe duplicar uso", "provider-folha-dup-uso", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = db.JobOnFolhas.SingleOrDefault(f => f.JobOnId == id);
            if (folha is null)
            {
                folha = new DMO.Alpha.Core.Production.JobOnFolha { JobOnId = id };
                db.JobOnFolhas.Add(folha);
            }
            folha.CmUtilizacao = "2";
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "F-CM", Percentagem = 6.7m, RegistadaEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4463", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?duplicar={id}");

        Assert.Matches("id=\"jobon-cm-utilizacao\"[^>]*value=\"6[.,]70?\"", html);
    
        // No ver, a utilização também é a da ferramenta e não os «2» que ficaram escritos na folha.
        var ver = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        var cartaoCm = ver[ver.IndexOf("data-jof-familia=\"CM\"", StringComparison.Ordinal)..ver.IndexOf("data-jof-familia=\"MF\"", StringComparison.Ordinal)];
        Assert.Matches(">6[.,]70?%</b>", cartaoCm);
        Assert.DoesNotContain(">2%</b>", cartaoCm);
    }
}
