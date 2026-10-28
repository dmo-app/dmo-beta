using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// UMA REFERÊNCIA SEM JOB ON (Diogo, 28/10/2026: «estava no jobon da C3,
/// queria iniciar um jobon para essa linha, meti a referência que vai entrar
/// e disse que não existia»): no campo da referência de um Job On, a escrita
/// abre sempre. Com Job On, a última dela, de qualquer linha; sem nenhum, o
/// Job On novo dela na linha do Job On que se estava a ver. Quem só vê fica
/// com o aviso.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_ReferenciaSemJobOn_AbreOJobOnNovoNaLinhaDeOndeVeio()
    {
        SeedUser("4604", "Chefe referência nova", "provider-folha-ref-nova", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        int linha;
        using (var scope = _factory.Services.CreateScope())
        {
            linha = scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOns.Single(j => j.Id == id).MachineId;
        }
        using var client = await LoginClientAsync("4604", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");
        Assert.Contains("<input type=\"hidden\" name=\"linha_de\" value=\"B2\" />", html, StringComparison.Ordinal);

        var nova = await client.GetAsync($"{FolhaPath}?referencia=8326t194&linha_de=B2");
        Assert.Equal(HttpStatusCode.Redirect, nova.StatusCode);
        Assert.Equal($"/JobOn/Folha?novo=1&etapa=criar&referencia=8326T194&machine_id={linha}", nova.Headers.Location!.ToString());

        // A que existe abre a última dela, de qualquer linha.
        var existe = await client.GetAsync($"{FolhaPath}?referencia=5810T188&linha_de=C3");
        Assert.Equal($"/JobOn/Folha?jobon_id={id}", existe.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Folha_ReferenciaSemJobOn_QuemSoVe_FicaComOAviso()
    {
        SeedUserSoVer("4605", "provider-folha-ref-nova-ver", "Folha1234!");
        using var client = await LoginClientAsync("4605", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?referencia=8326T194&linha_de=B2"));
        Assert.Contains("Sem produções da referência 8326T194.", html, StringComparison.Ordinal);
    }
}
