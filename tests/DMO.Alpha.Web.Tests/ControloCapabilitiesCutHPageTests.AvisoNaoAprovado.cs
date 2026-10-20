using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026 (F5): «O não aprovado deve deixar um notice no resumo
    // de controlo, requer que seja revisto ou então posto de parte. Mas nunca
    // é razão de bloqueio». O aviso leva o link para a Revisão só a quem a
    // tem. Antes, o Resumo só mostrava «Não aprovado» no cartão.
    [Fact]
    public async Task NaoAprovado_DeixaAvisoNoResumo_ComLinkParaARevisaoSoAQuemATem()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        const string aviso = "O controlo desta produção não foi aprovado: tem de ser revisto ou posto de parte.";
        var revisao = $"<a class=\"dmo-aviso-link\" href=\"/Controlo/Approve?jobon_id={jobOnId}\">Rever na Revisão</a>";

        // Quem regista e aprova vê o Resumo e tem a Revisão.
        SeedUserWithTemplate("4785", "Chefe Completo", "provider-aviso-nao-aprovado-ambos", "AvisoNaoAprovado1!",
            "tpl-teste-aviso-ambos", "Registo e aprovação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var ambos = await LoginClientAsync("4785", "AvisoNaoAprovado1!");
        var antes = WebUtility.HtmlDecode(await GetPageHtmlAsync(ambos, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.DoesNotContain(aviso, antes);

        var token = await GetVerificationTokenAsync(ambos, $"/Controlo/Approve?jobon_id={jobOnId}");
        var naoAprovar = await ambos.PostAsync("/Controlo/Approve?handler=NaoAprovar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("parte", "peso"), ("nota", "Repetir a pesagem."),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, naoAprovar.StatusCode);

        var resumoAmbos = WebUtility.HtmlDecode(await GetPageHtmlAsync(ambos, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains($"<div class=\"dmo-aviso warn\" role=\"status\" data-resumo-nao-aprovado>{aviso} {revisao}</div>", resumoAmbos);

        SeedOperacional("4786", "provider-aviso-nao-aprovado-operacional", "AvisoNaoAprovado2!");
        using var operacional = await LoginClientAsync("4786", "AvisoNaoAprovado2!");
        var resumoOperacional = WebUtility.HtmlDecode(await GetPageHtmlAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains($"data-resumo-nao-aprovado>{aviso}</div>", resumoOperacional);
        Assert.DoesNotContain(revisao, resumoOperacional);
    }
}
