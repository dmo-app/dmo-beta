using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

public partial class PesoFolhaProducaoAnteriorPageTests
{
    // DMO-496: a tabela do Peso no Resumo tem as colunas «Valor», «g» e «%»
    // e cada linha da produção anterior leva o valor e as duas diferenças.
    [Fact]
    public async Task Resumo_TabelaDoPeso_TemAsColunasValorGEPercentagem_EACadaLinhaAsDuasDiferencas()
    {
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-18", 181m, 12);
        SeedSapFabrico("2026-04-18", "2026-04-24", 183m, 6);
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
            var peso = db.Pesos.Single(p => p.Id == pesoId);
            peso.ProducaoAnteriorControloId = anterior.Id;
            db.SaveChanges();
        }
        SeedUser("4876", "Peso Operador", "provider-folha-sap4", "FolhaSap13!");
        using var client = await LoginClientAsync("4876", "FolhaSap13!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        var tabela = html[html.IndexOf("data-resumo-peso-tabela", StringComparison.Ordinal)..];
        tabela = tabela[..tabela.IndexOf("</table>", StringComparison.Ordinal)];

        Assert.Contains("<th scope=\"col\">Valor</th><th scope=\"col\" title=\"Diferença em gramas\">g</th><th scope=\"col\" title=\"Diferença em %\">%</th>", tabela, StringComparison.Ordinal);
        Assert.Matches("Produção anterior · média<small> 202604</small></th>\\s*<td>181\\.67 g</td>\\s*<td>[+−]\\d+\\.\\d{2}</td>\\s*<td>[+−]\\d+\\.\\d{2}</td>", tabela);
        Assert.Matches("Produção anterior · moda<small> 202604</small></th>\\s*<td>181\\.00 g</td>\\s*<td>[+−]\\d+\\.\\d{2}</td>\\s*<td>[+−]\\d+\\.\\d{2}</td>", tabela);
    }
}
