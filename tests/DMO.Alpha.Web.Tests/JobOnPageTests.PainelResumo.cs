using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O JOB ON NO PAINEL DESLIZANTE (Diogo, 29/10/2026: no painel aparecia a folha inteira apertada, com o
/// cabeçalho por cima de si mesmo e a deslizar para o lado): o endereço do Planeamento com ?painel=1 leva
/// ao resumo da folha (Folha?painel=1), não à folha inteira.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Planeamento_NoPainel_AbreOResumoDaFolha()
    {
        SeedUser("4614", "Painel", "provider-jobon-painel", "Folha1234!");
        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = new JobOn
            {
                Reference = "9836T188",
                ProductionNumber = "202606",
                Machine = db.Machines.FirstOrDefault(m => m.Code == "B1") ?? new Machine { Code = "B1" },
                InicioEm = new DateOnly(2026, 10, 26),
            };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            jobOnId = jobOn.Id;
        }

        using var client = await LoginClientAsync("4614", "Folha1234!");
        using var resposta = await client.GetAsync($"/JobOn?jobon_id={jobOnId}&painel=1");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        var destino = resposta.Headers.Location!.OriginalString;
        Assert.StartsWith("/JobOn/Folha?", destino, StringComparison.Ordinal);
        Assert.Contains("painel=1", destino, StringComparison.Ordinal);

        var html = await GetPageHtmlAsync(client, destino);
        Assert.Contains("class=\"jof-painel\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"jof2", html, StringComparison.Ordinal);
    }
}
