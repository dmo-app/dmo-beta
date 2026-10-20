using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Fase base dos testes (19/10/2026): Aprovar e Não aprovar como o chefe os
/// carrega na Revisão. Os botões estão no canto do cartão de cada parte,
/// fora do &lt;form&gt; da Nota (form="revisaoDecisao"), e levam a parte no
/// name e value; o Não aprovar muda o destino pelo formaction. Os outros
/// testes enviam os nomes que escolhem, por isso um name trocado na página
/// deixava o CI verde.
/// </summary>
public sealed partial class ControloCapabilitiesCutHPageTests
{
    [Fact]
    public async Task AprovarENaoAprovarComNota_PeloFormularioDaRevisao_GravamADecisaoEANotaDeCadaParte()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }
        SeedChefe("4801", "provider-revisao-formulario", "RevisaoForm1!");
        using var client = await LoginClientAsync("4801", "RevisaoForm1!");
        var revisao = $"/Controlo/Approve?jobon_id={jobOnId}";

        // Aprovar o Peso com uma nota. A página leva o dia aberto na Revisão
        // (ano, mes, dia) e o servidor volta a ele.
        var html = await GetPageHtmlAsync(client, revisao);
        var (acao, campos) = EnvioDoFormulario.Enviar(html, [("revisao-nota", "Peso conferido.")], idDoBotao: "revisaoAprovar-peso");
        using (var aprovado = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, aprovado.StatusCode);
            Assert.StartsWith(revisao, aprovado.Headers.Location?.ToString());
        }

        // Não aprovar o Peso com o que tem de mudar.
        html = await GetPageHtmlAsync(client, revisao);
        Assert.DoesNotContain("revisaoNaoAprovar-pegamentos", html);
        (acao, campos) = EnvioDoFormulario.Enviar(html, [("revisao-nota", "Repetir o Peso.")], idDoBotao: "revisaoNaoAprovar-peso");
        using (var naoAprovado = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, naoAprovado.StatusCode);
            Assert.StartsWith(revisao, naoAprovado.Headers.Location?.ToString());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = Assert.Single(await db.ControloDecisoes.AsNoTracking()
                .Where(d => d.ControloId == controloId).ToListAsync());
            Assert.Equal(ControloParte.Peso, peso.Parte);
            Assert.False(peso.Aprovado);
            Assert.Equal("Repetir o Peso.", peso.Nota);
            Assert.Equal("4801", peso.OperatorId);

            var controlo = await db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
            Assert.Equal("4801", controlo.NotApprovedByOperatorId);
            Assert.Equal("Repetir o Peso.", controlo.ReviewNote);
        }
    }
}
