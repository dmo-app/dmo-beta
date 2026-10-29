using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A folha CHEIA do Job On para o Chromium (tests/visual/cobertura_a1_browser.py):
/// os oito calibres escritos, notas compridas na CM e nas Notas gerais, e as quatro
/// folhas do Imprimir. O browser mede o que o operador vê (a página não desliza, os
/// calibres cabem, a nota abre por cima, o Imprimir cabe no A4); aqui só se confere
/// que a página que se exporta tem esses dados.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_Cheia_ComOitoCalibresENotasCompridas_ExportadaParaOBrowser()
    {
        SeedUser("9120", "Chefe folha cheia", "provider-a1-cheia", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9120", "Folha1234!");

        // Os dados do PDF de referência, sem acrescentos: é a folha que o Imprimir tem de fazer caber no A4.
        var normal = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        ExportarParaOBrowser.Gravar("cobertura-a1-imprimir.html", normal);
        Assert.Contains("data-jof-folha=\"moldes\"", normal, StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = await db.JobOnFolhas.SingleAsync(f => f.JobOnId == jobOnId);
            folha.DefinirValor("cm_notas", string.Join("\n", Enumerable.Range(1, 40).Select(n => $"Nota {n:00}: rodar justa.")));
            folha.DefinirValor("cm_reparador", "MOLDIN");
            folha.DefinirValor("mf_reparador", "MOLDIN");
            folha.DefinirValor("notas_gerais", string.Join("\n", Enumerable.Range(1, 30).Select(n => $"Nota geral {n:00}: linha B2.")));
            db.JobOnFolhaCalLinhas.AddRange(
                new JobOnFolhaCalLinha { JobOnId = jobOnId, Ordem = 5, Designacao = "Nível", Valor = "12.5 MM" },
                new JobOnFolhaCalLinha { JobOnId = jobOnId, Ordem = 6, Designacao = "Bucha Marcada", Valor = "P 59.2 / M 58.6" },
                new JobOnFolhaCalLinha { JobOnId = jobOnId, Ordem = 7, Designacao = "CS P Interna", Valor = "34.95" });
            await db.SaveChangesAsync();
        }

        var ver = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");
        ExportarParaOBrowser.Gravar("cobertura-a1-folha.html", ver);
        var calibres = Regex.Match(ver, "<table[^>]*data-jof-cal[\\s\\S]*?</table>").Value;
        Assert.Equal(8, Regex.Matches(calibres, "<th scope=\"row\">").Count);
        Assert.Contains("Nota 40: rodar justa.", WebUtility.HtmlDecode(ver), StringComparison.Ordinal);
        Assert.Contains("jof-onde-reparador", ver, StringComparison.Ordinal);
        Assert.Contains("data-jof-impressao", ver, StringComparison.Ordinal);

        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}&editar=1");
        ExportarParaOBrowser.Gravar("cobertura-a1-folha-editar.html", editar);
        Assert.Contains("data-jof-folha=\"editar\"", editar, StringComparison.Ordinal);
        Assert.Contains("Nota 40: rodar justa.", WebUtility.HtmlDecode(editar), StringComparison.Ordinal);
    }
}
