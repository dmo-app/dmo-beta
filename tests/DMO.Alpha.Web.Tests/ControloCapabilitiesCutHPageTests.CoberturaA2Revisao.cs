using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // DMO-483 (beta.193): a Revisão tem o título «Revisão» e, no Resumo do
    // Controlo, as consultas junto ao título; na consulta dos Pegamentos o
    // «Voltar» vem por cima do título, sai o quadro «Leituras» que repetia a
    // tabela e o mapa fica depois das tabelas.
    [Fact]
    public async Task Revisao_TemOTituloRevisao_EAsConsultasJuntoAoTituloDoResumo()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(NovoPeso(controloId, null, null));
            await db.SaveChangesAsync();
        }
        SeedChefe("4831", "provider-revisao-483a", "Revisao483a!");
        using var client = await LoginClientAsync("4831", "Revisao483a!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");

        Assert.Contains(">Revisão</h1>", html);
        var topo = html[Posicao(html, "<h3 class=\"revisao-section\">Resumo do Controlo</h3>")..];
        topo = topo[..Posicao(topo, "</nav>")];
        Assert.Contains("id=\"revisaoConsultas\"", topo);
        Assert.Contains($"tipo=pegamentos\">Pegamentos</a>", topo);
        Assert.Contains($"controlo_id={controloId}", topo);
    }

    [Fact]
    public async Task ConsultaDosPegamentos_VoltarPorCimaDoTitulo_SemOQuadroLeituras_EOMapaDepoisDasTabelas()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m };
            folha.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m, NominalGargaloMm = 30m });
            db.Pegamentos.Add(folha);
            await db.SaveChangesAsync();
        }
        SeedChefe("4832", "provider-revisao-483b", "Revisao483b!");
        using var client = await LoginClientAsync("4832", "Revisao483b!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");

        Assert.Contains("Consulta dos Pegamentos", html);
        Assert.True(Posicao(html, "id=\"approveVoltar\"") < Posicao(html, "id=\"approve-detail-title\""));
        Assert.Contains($"href=\"/Controlo/Approve?jobon_id={jobOnId}\"", html);
        // O contexto do componente sem o quadro «Leituras» (a contagem são as linhas da tabela).
        Assert.Contains("<span>Gargalo</span>", html);
        Assert.DoesNotContain("<span>Leituras</span>", html);
        // O mapa vem depois das tabelas de leituras.
        Assert.True(Posicao(html, "Mapa de limites") > Posicao(html, "<table"));
    }
}
