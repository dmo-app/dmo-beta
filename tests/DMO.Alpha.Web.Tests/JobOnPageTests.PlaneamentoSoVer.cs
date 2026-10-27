using System.Net;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    // O Planeamento também para o Job On ver (Diogo, 27/10/2026: «jobon view esta
    // sem planeamento»): o calendário e as linhas do dia em leitura; o Criar, o
    // Histórico, a folha antiga e o Guardar continuam a pedir o Job On criar.
    [Fact]
    public async Task Planeamento_JobOnVer_VeOCalendarioEAsLinhas_SemCriarNemHistorico()
    {
        SeedUserSoVer("4491", "provider-planeamento-ver", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4491", "Folha1234!");

        var resposta = await client.GetAsync($"{PagePath}?ano=2026&mes=10&dia=22");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("data-dmo-calendar", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"jobonLinha-{id}\"", html, StringComparison.Ordinal);
        Assert.Contains($"/JobOn/Folha?jobon_id={id}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"jobonCriar\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/JobOn/Folha?novo=1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/JobOn?historico=1\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/JobOn\"", html, StringComparison.Ordinal);

        // Nem pelo endereço: o Histórico, o Criar e o Duplicar antigos não abrem.
        var historico = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.DoesNotContain("Histórico de Job On", historico, StringComparison.Ordinal);
        var novo = await GetPageHtmlAsync(client, $"{PagePath}?novo=1");
        Assert.DoesNotContain("id=\"lightSheet\"", novo, StringComparison.Ordinal);
        var resultados = await client.GetAsync($"{PagePath}?handler=HistoricoResultados");
        Assert.Equal(HttpStatusCode.Redirect, resultados.StatusCode);
        Assert.Equal(PagePath, resultados.Headers.Location!.ToString());

        // Uma produção abre a folha em leitura.
        var abrir = await client.GetAsync($"{PagePath}?jobon_id={id}");
        Assert.Equal(HttpStatusCode.Redirect, abrir.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={id}", abrir.Headers.Location!.ToString());

        // Quem tem o Job On criar continua a ter o Criar Job On.
        SeedUser("4492", "Chefe planeamento", "provider-planeamento-criar", "Folha1234!");
        using var chefe = await LoginClientAsync("4492", "Folha1234!");
        Assert.Contains("id=\"jobonCriar\"", await GetPageHtmlAsync(chefe, $"{PagePath}?ano=2026&mes=10&dia=22"), StringComparison.Ordinal);
    }
}
