using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    // Sweep de 19/10/2026, 3.13 (B-pegamentos-5): antes da primeira medição,
    // a folha dizia «tolerância predefinida (±0.20 mm)» mesmo com outra
    // tolerância nas Definições do Controlo. Diz de onde vem a tolerância,
    // e a primeira medição congela-a no registo.
    [Fact]
    public async Task FolhaSemMedicoes_MostraAToleranciaDasDefinicoes_EAPrimeiraMedicaoCongelaA()
    {
        SeedUser("4427", "Pegamentos Tolerância", "provider-peg-tolerancia", "PegTolerancia1!");
        using var client = await LoginClientAsync("4427", "PegTolerancia1!");
        var controloId = await SeedRealProductionAsync();
        await GuardarToleranciaAsync(0.30m);
        var url = $"{PagePath}?controlo_id={controloId}";

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, url));
        Assert.Contains("nominal ± 0.30 mm", html);
        Assert.Contains("tolerância das Definições do Controlo — fica congelada no registo na primeira medição", html);
        Assert.DoesNotContain("predefinida", html);
        Assert.DoesNotContain("±0.20", html);

        var posted = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", contraCostura: "69.90", gargalo: "70.00");
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        // Mudar as Definições depois não muda a folha: os 0.30 ficaram no registo.
        await GuardarToleranciaAsync(0.25m);
        html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, url));
        Assert.Contains("nominal ± 0.30 mm", html);
        Assert.Contains("congelada no registo desta produção", html);
        Assert.DoesNotContain("tolerância das Definições do Controlo", html);
    }

    private async Task GuardarToleranciaAsync(decimal toleranceMm)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var existing = await db.PegamentosTolerances.SingleOrDefaultAsync();
        if (existing is null)
        {
            db.PegamentosTolerances.Add(new PegamentosTolerance { ToleranceMm = toleranceMm });
        }
        else
        {
            existing.ToleranceMm = toleranceMm;
        }

        await db.SaveChangesAsync();
    }
}
