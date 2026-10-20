using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Sweep de 19/10/2026, C-3: o «Não aprovar» e a nota chegam ao Resumo e ao
    // Histórico, com o nome de quem decidiu, como na Revisão. Antes, o Resumo
    // dizia «Sem registo · Por aprovar» e o Histórico «Por aprovar».
    [Fact]
    public async Task NaoAprovado_ChegaAoResumoEAoHistorico_ComQuemEANota()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        SeedChefe("4783", "provider-tres-estados-chefe", "TresEstados1!");
        using (var chefe = await LoginClientAsync("4783", "TresEstados1!"))
        {
            var token = await GetVerificationTokenAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
            var naoAprovar = await chefe.PostAsync("/Controlo/Approve?handler=NaoAprovar", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("parte", "peso"), ("nota", "Repetir a pesagem."),
                ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, naoAprovar.StatusCode);
        }

        SeedOperacional("4784", "provider-tres-estados-operacional", "TresEstados2!");
        using var operacional = await LoginClientAsync("4784", "TresEstados2!");
        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains("<div class=\"resumo-mini-card resumo-approval not-approved\">", resumo);
        Assert.Matches("<strong>Não aprovado</strong>\\s*<small>Chefe Aprovação · [^<]+ · Repetir a pesagem\\.</small>", resumo);
        Assert.Matches(
            "<td class=\"resumo-nao-aprovado\" colspan=\"2\">Não aprovado por Chefe Aprovação · [^<]+ · Repetir a pesagem\\.</td>",
            resumo);
        Assert.DoesNotContain("Por aprovar — nada bloqueia", resumo);

        var historico = WebUtility.HtmlDecode(await GetPageHtmlAsync(operacional, "/Controlo/Peso?historico=1&tipo=Resumo"));
        Assert.Contains("Não aprovado por Chefe Aprovação · Repetir a pesagem.", historico);
    }
}
