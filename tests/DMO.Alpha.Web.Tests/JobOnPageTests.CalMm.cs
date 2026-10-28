using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// NOS CALIBRES O «MM» VAI PARA O TÍTULO (Diogo, 28/10/2026: «nos calibres
/// metemos os MM nos títulos da tabela assim poupamos espaço»): «75.20 MM» e
/// «32.60MM» mostram-se «75.20» e «32.60», com «(mm)» no nome da medida. O
/// valor gravado não muda e o Editar mostra-o como está. «56.40 X 58.60» fica
/// em três linhas, número / x / número («ficar número x, muda linha, número é
/// confuso»).
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_CalibresComMm_OMmVaiParaOTitulo()
    {
        SeedUser("4608", "Chefe calibres", "provider-folha-cal-mm", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOnId = id, Ordem = 51, Designacao = "Inversão MF", Valor = "75.20 MM", QtdMaq = "2" });
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOnId = id, Ordem = 52, Designacao = "Inversão MP", Valor = "32.60MM" });
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOnId = id, Ordem = 53, Designacao = "Nível", Valor = "8" });
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOnId = id, Ordem = 54, Designacao = "CS P interna", Valor = "56.40 X 58.60" });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4608", "Folha1234!");

        var cal = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        cal = cal[cal.IndexOf("data-jof-cal", StringComparison.Ordinal)..];
        Assert.Matches(@"Inversão MF \(mm\)\s*</th>\s*<td>75.20</td>", cal);
        Assert.Matches(@"Inversão MP \(mm\)\s*</th>\s*<td>32.60</td>", cal);
        Assert.Matches(@">\s*Nível\s*</th>\s*<td>8</td>", cal);
        Assert.DoesNotContain("MM</td>", cal, StringComparison.OrdinalIgnoreCase);
        // «56.40 X 58.60»: número, x e número, cada um na sua linha.
        Assert.Contains("<td><span class=\"cal-vezes\"><span>56.40</span><span>x</span><span>58.60</span></span></td>", cal, StringComparison.Ordinal);

        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1"));
        Assert.Contains("value=\"Inversão MF\"", editar, StringComparison.Ordinal);
        Assert.Matches("value=\"75.20 (MM|mm)\"", editar);
    }
}
