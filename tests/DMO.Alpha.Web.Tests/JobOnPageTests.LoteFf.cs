using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O LOTE FF (Diogo, 22/10/2026): o bloco MF da folha tem o «Lote FF», que
/// abre com o lote do MF enquanto está vazio e grava o lote escrito quando o
/// FF trabalha com outro.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task FolhaNova_LoteFf_AbreComOLoteDoMf_EGravaOutro()
    {
        SeedUser("4408", "Chefe lote FF", "provider-folha-loteff", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4408", "Folha1234!");

        var lida = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        Assert.Contains("Lote FF", lida, StringComparison.Ordinal);

        var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Contains("id=\"jobon-mf-lote-ff\"", editar, StringComparison.Ordinal);
        Assert.Matches("id=\"jobon-mf-lote-ff\"[^>]*value=\"1\"", editar);

        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-mf-lote-ff", "7");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("7", db.JobOnFolhas.AsNoTracking().Single(f => f.JobOnId == id).MfLoteFf);
        var depois = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
        Assert.Matches("id=\"jobon-mf-lote-ff\"[^>]*value=\"7\"", depois);
    }
}
