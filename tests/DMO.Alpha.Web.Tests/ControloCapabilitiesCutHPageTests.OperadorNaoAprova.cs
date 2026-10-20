using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026: «operador nao pode aprovar». Quem tem o Controlo mas
    // não o Controlo Aprovar não aprova nem «não aprova» o Peso, mesmo com um
    // pedido feito à mão: nada muda no controlo.
    [Fact]
    public async Task Revisao_OperadorSemControloAprovar_NaoAprovaNemNaoAprova()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        SeedUserWithTemplate("4799", "Operador", "provider-operador-nao-aprova", "OperadorNao1!",
            "tpl-operador-nao-aprova", "Operador", [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate]);
        using var client = await LoginClientAsync("4799", "OperadorNao1!");

        // O Resumo abre (tem o Controlo) e dá o token do formulário; a Revisão não abre.
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(
            await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"))!;
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync($"/Controlo/Approve?jobon_id={jobOnId}")).StatusCode);

        foreach (var (handler, nota) in new[] { ("Approve", ""), ("NaoAprovar", "Repetir o Peso.") })
        {
            var resposta = await client.PostAsync($"/Controlo/Approve?handler={handler}", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("parte", "peso"), ("nota", nota), ("__RequestVerificationToken", token)));
            Assert.NotEqual(HttpStatusCode.OK, resposta.StatusCode);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Null(controlo.ApprovedByOperatorId);
            Assert.Null(controlo.ApprovedAtUtc);
            Assert.Null(controlo.NotApprovedByOperatorId);
            Assert.Empty(await db.ControloDecisoes.AsNoTracking().Where(d => d.ControloId == controloId).ToListAsync());
        }
    }
}
