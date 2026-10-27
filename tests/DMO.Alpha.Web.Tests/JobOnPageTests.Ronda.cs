using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Ronda de erros de 26/10/2026 (grupo Job On).
/// </summary>
public partial class JobOnPageTests
{
    // J3: no Criar, a referência que já existe com uma produção nova escrita
    // não perde a produção nem a linha: abre o Duplicar da última com elas.
    // A produção que já existe abre esse Job On.
    [Fact]
    public async Task Criar_ReferenciaExistente_ComProducaoNova_AbreODuplicarComAProducaoEALinha()
    {
        SeedUser("4426", "Chefe ronda", "provider-folha-ronda", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        var b2 = await MachineIdAsync("B2");
        using var client = await LoginClientAsync("4426", "Folha1234!");

        var nova = await client.GetAsync($"{FolhaPath}?novo=1&etapa=criar&machine_id={b2}&inicio_em=2026-11-02&referencia=5810t188&producao=880001");
        Assert.Equal(HttpStatusCode.Redirect, nova.StatusCode);
        // A última abre-se para ver; só o «Duplicar» passa a duplicar, com a produção
        // e a linha (Diogo, 27/10/2026: «analisa-se e depois duplica-se»).
        Assert.Equal($"/JobOn/Folha?jobon_id={id}&producao=880001&machine_id={b2}&inicio_em=2026-11-02", nova.Headers.Location!.ToString());
        var ver = await GetPageHtmlAsync(client, nova.Headers.Location!.ToString());
        Assert.Contains($"/JobOn/Folha?duplicar={id}&amp;producao=880001&amp;machine_id={b2}&amp;inicio_em=2026-11-02", ver, StringComparison.Ordinal);

        var html = await GetPageHtmlAsync(client, $"/JobOn/Folha?duplicar={id}&producao=880001&machine_id={b2}&inicio_em=2026-11-02");
        Assert.Contains("id=\"lightProduction\" name=\"production_number\" type=\"text\" value=\"880001\"", html, StringComparison.Ordinal);

        var existente = await client.GetAsync($"{FolhaPath}?novo=1&etapa=criar&machine_id={b2}&referencia=5810T188&producao=202602");
        Assert.Equal(HttpStatusCode.Redirect, existente.StatusCode);
        Assert.StartsWith($"/JobOn/Folha?jobon_id={id}", existente.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    // J5: a utilização mostra-se com ponto e duas casas, e acima de 100% com a cor ligeira.
    [Fact]
    public async Task Folha_UtilizacaoComPontoEAcimaDe100ComCor()
    {
        SeedUser("4427", "Chefe uso ronda", "provider-folha-uso-ronda", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>();
            var folha = db.JobOnFolhas.SingleOrDefault(f => f.JobOnId == id);
            if (folha is null)
            {
                folha = new DMO.Alpha.Core.Production.JobOnFolha { JobOnId = id };
                db.JobOnFolhas.Add(folha);
            }
            folha.MfUtilizacao = "40,5";
            folha.BqUtilizacao = "120";
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4427", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}");

        Assert.Contains("<b>40.50%</b>", html, StringComparison.Ordinal);
        Assert.Contains("<b class=\"dmo-uso-cheio\" title=\"Acima de 100%\">120%</b>", html, StringComparison.Ordinal);
    }
}
