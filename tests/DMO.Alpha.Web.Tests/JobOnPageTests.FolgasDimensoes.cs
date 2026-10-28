using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AS DIMENSÕES E AS FOLGAS da folha (Diogo, 28/10/2026): a tolerância com um
/// só sinal («± +0.03» era repetido); as folgas com um número por espaço, no MF
/// a folga, a tolerância e a da flecha, no CM só as duas primeiras; o Editar
/// recusa dois números no mesmo espaço («o Job On não deve aceitar mais que 1
/// valor por cada zona»).
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_ToleranciaComUmSoSinal_EFolgasUmNumeroPorEspaco()
    {
        SeedUser("4606", "Chefe folgas", "provider-folha-folgas", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await FolhaComAsync(id, f =>
        {
            f.CmPata = "28.50"; f.CmPataTol = "+0.03"; f.CmGargalo = "24.80"; f.CmGargaloTol = "±0.05";
            f.CmFolga1 = "0.16"; f.MfFolga1 = "0.12 / 0.16";
        });
        using var client = await LoginClientAsync("4606", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.Contains("<span class=\"jof-tol\"> ± 0.03</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"jof-tol\"> ± 0.05</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("± +", html, StringComparison.Ordinal);
        Assert.DoesNotContain("± ±", html, StringComparison.Ordinal);

        // MF: 0.12 na folga, a tolerância vazia, 0.16 na flecha.
        var mf = html[html.IndexOf("data-jof-dims=\"MF\"", StringComparison.Ordinal)..];
        Assert.Contains("<span class=\"folga\" title=\"Folga\"><small>Folga</small>0.12</span>", mf, StringComparison.Ordinal);
        Assert.Contains("<span class=\"folga\" title=\"Folga da flecha (hollow mill)\"><small>Flecha</small>0.16</span>", mf, StringComparison.Ordinal);
        // CM: só a folga e a tolerância.
        var cm = html[html.IndexOf("data-jof-dims=\"CM\"", StringComparison.Ordinal)..html.IndexOf("data-jof-dims=\"MF\"", StringComparison.Ordinal)];
        Assert.Contains("<small>Tol.</small>", cm, StringComparison.Ordinal);
        Assert.DoesNotContain("<small>Flecha</small>", cm, StringComparison.Ordinal);

        // No Editar as caixas já vêm com um número cada.
        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1"));
        Assert.Contains("id=\"jobon-mf-folga1\" name=", editar, StringComparison.Ordinal);
        Assert.Matches("id=\"jobon-mf-folga1\"[^>]*value=\"0.12\"", editar);
        Assert.Matches("id=\"jobon-mf-folga3\"[^>]*value=\"0.16\"", editar);
        Assert.DoesNotContain("id=\"jobon-cm-folga3\"", editar, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_DoisNumerosNumaFolga_ERecusado()
    {
        SeedUser("4607", "Chefe folga dupla", "provider-folha-folga-dupla", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4607", "Folha1234!");

        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-mf-folga2", "0.02 / 0.03");
        var resposta = WebUtility.HtmlDecode(await (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).Content.ReadAsStringAsync());

        Assert.Contains("«MF · Folgas 2 (mm)» só leva um número", resposta, StringComparison.Ordinal);
        using var scope = _factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnFolhas.AsNoTracking().SingleOrDefault(f => f.JobOnId == id)?.MfFolga2);
    }

    private async Task FolhaComAsync(int jobOnId, Action<JobOnFolha> mudar)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var folha = await db.JobOnFolhas.SingleOrDefaultAsync(f => f.JobOnId == jobOnId);
        if (folha is null)
        {
            folha = new JobOnFolha { JobOnId = jobOnId };
            db.JobOnFolhas.Add(folha);
        }
        mudar(folha);
        await db.SaveChangesAsync();
    }
}
