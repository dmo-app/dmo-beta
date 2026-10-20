using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // Sweep de 19/10/2026, 3.16: no Resumo, o cartão dos Pegamentos com
    // medições não avaliáveis (sem nominal) fica cinzento, como na página
    // dos Pegamentos e nos PDFs. Antes ficava âmbar, a cor de «por verificar».
    // A página já não grava uma medição sem nominal; a linha sem nominal é a
    // de um registo antigo, por isso o teste tira-o na base.
    [Fact]
    public async Task Resumo_MedicoesNaoAvaliaveis_OCartaoFicaCinzento()
    {
        SeedUser("4432", "Pegamentos Cinzento", "provider-peg-cinzento", "PegCinza1!");
        using var client = await LoginClientAsync("4432", "PegCinza1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using var posted = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00");
        Assert.Equal(url, posted.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var measurement = await db.PegamentosMeasurements.SingleAsync();
        measurement.NominalGargaloMm = null;
        await db.SaveChangesAsync();
        var jobOnId = await db.Controlos.AsNoTracking()
            .Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");

        Assert.Contains("resumo-linked-card resumo-linked-peg na", resumo);
        Assert.Contains("Existem medições não avaliáveis", resumo);
        Assert.DoesNotContain("resumo-linked-peg warn", resumo);
        // O cinzento é o do mapa dos Pegamentos.
        var css = await client.GetStringAsync("/css/dmo-resumo-prototype.css");
        Assert.Matches(@"\.resumo-linked-peg\.na\s*\{[^}]*border-color:\s*"
            + DMO.Alpha.Infrastructure.Controlo.PegamentosLimitMap.NotEvaluableColor, css);
    }
}
