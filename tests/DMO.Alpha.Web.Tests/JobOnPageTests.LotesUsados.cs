using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// TODOS OS LOTES USADOS (Diogo, 26/10/2026: «ver se depois o jobon_id
/// apresenta todos os lotes que foram usados durante a fabricação»): depois do
/// «Alterar lote», a cabeça da BQ na Folha mostra o lote atual e os de antes.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_DepoisDeAlterarOLoteDaBq_MostraOAtualEOsDeAntes()
    {
        SeedUser("4422", "Chefe lotes", "provider-folha-lotes", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4422", "Folha1234!");

        var antes = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        Assert.DoesNotContain("data-jof-lotes-antes", antes, StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "F-BQ-167", Type = ToolType.Bq, Reference = "T188", Lot = "167" });
            await db.SaveChangesAsync();
            var bqId = await db.BqContexts.Where(c => c.JobOnId == id).Select(c => c.Id).SingleAsync();
            var alterar = scope.ServiceProvider.GetRequiredService<IAlterarLoteBq>();
            Assert.Equal(AlterarLoteResultado.Alterado, (await alterar.AlterarAsync(new AlterarLote(bqId, "F-BQ-167", "Chefe lotes"))).Resultado);
        }

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");

        Assert.Contains("<b data-jof-tool-lote>167</b>", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"jof-onde\" data-jof-lotes-antes>Antes nesta produção: lote 21</p>", html, StringComparison.Ordinal);
    }
}
