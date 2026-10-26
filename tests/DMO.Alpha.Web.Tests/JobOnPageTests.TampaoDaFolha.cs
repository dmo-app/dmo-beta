using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O TAMPÃO DA FOLHA (Diogo, 26/10/2026: um só campo): o «Diâmetro» do bloco
/// TP da folha é o diâmetro do tampão desta produção, o que o Peso usa.
/// Vírgula ou ponto; vazio limpa; um texto que não é número fica na folha
/// como foi escrito e não mexe no diâmetro gravado.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task FolhaNova_DiametroDoTp_EOTampaoDaProducao()
    {
        SeedUser("4407", "Chefe tampão", "provider-folha-tampao", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4407", "Folha1234!");

        async Task<decimal?> Gravar(string valor)
        {
            var editar = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1");
            var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-tp-diametro-ps", valor);
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            return db.JobOns.AsNoTracking().Single(j => j.Id == id).TampaoDiametroMm;
        }

        Assert.Equal(36.85m, await Gravar("36,85"));
        Assert.Equal(36.85m, await Gravar("ver desenho"));
        Assert.Contains("ver desenho", await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"), StringComparison.Ordinal);
        Assert.Equal(28.45m, await Gravar("28.45"));
        Assert.Null(await Gravar(""));
    }
}
