using System.Net;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    // Diogo, 27/10/2026: quem só tem o Job On ver «nao duplica nao ve contorlo nao ve
    // reparaçao internao nao ve boquilhas so ve os detalhes do plnaneamento e os
    // jobons». Sem as Ferramentas, também não há caminho para a ficha da ferramenta.
    [Fact]
    public async Task JobOnVer_SoVeOPlaneamentoEOsJobOn_SemDuplicarNemOutrosModulos()
    {
        SeedUserSoVer("4493", "provider-so-ver-modulos", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4493", "Folha1234!");

        var folha = await GetPageHtmlAsync(client, $"/JobOn/Folha?jobon_id={id}");
        Assert.DoesNotContain("data-jof-duplicar", folha, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-opcoes", folha, StringComparison.Ordinal);
        Assert.DoesNotContain("/Controlo/", folha, StringComparison.Ordinal);
        Assert.DoesNotContain("/ReparacaoInterna", folha, StringComparison.Ordinal);
        Assert.DoesNotContain("/Boquilhas", folha, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-ficha=\"/Ferramentas", folha, StringComparison.Ordinal);

        var planeamento = await client.GetAsync("/JobOn?ano=2026&mes=10&dia=22");
        Assert.Equal(HttpStatusCode.OK, planeamento.StatusCode);
        var html = await planeamento.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Controlo/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/ReparacaoInterna", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Ferramentas/Ficha", html, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicar=", html, StringComparison.Ordinal);
    }
}
