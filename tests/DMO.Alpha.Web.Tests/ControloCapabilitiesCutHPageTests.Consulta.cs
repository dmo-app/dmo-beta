using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026: «falta-me o histórico no controlo aprove. para
    // consultar o que já foi feito. ou então meter formas de consultar na
    // página atual com filtros». Os filtros da Revisão (numa linha) consultam
    // um período sem escolher um dia, e a lista diz quem aprovou e quando.
    [Fact]
    public async Task Revisao_Filtros_ConsultamOPeriodo_ComQuemAprovou()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        var registado = DateTime.UtcNow.AddDays(-2);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m, RegistadoEmUtc = registado });
            await db.SaveChangesAsync();
            await new ControloApprovalHandler(db).ApproveAndGetAsync(controloId, "4798", "Ok.");
        }

        SeedChefe("4798", "provider-revisao-consulta-periodo", "RevisaoPeriodo1!");
        using var client = await LoginClientAsync("4798", "RevisaoPeriodo1!");
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var dia = DateOnly.FromDateTime(DmoBusinessTime.ToLocal(registado));

        // Os filtros numa linha, sempre à vista: pesquisar, estado, máquina, de e até.
        var pagina = await GetPageHtmlAsync(client, "/Controlo/Approve");
        Assert.Contains("id=\"revisaoFiltros\" data-dmo-regiao", pagina);
        Assert.Contains("<option value=\"B1\">B1</option>", pagina);
        Assert.Contains("<input id=\"revisao-de\" type=\"date\" name=\"de\"", pagina);
        Assert.Contains("<input id=\"revisao-ate\" type=\"date\" name=\"ate\"", pagina);

        // Só o estado, sem dia: o último ano, com quem aprovou e quando.
        var aprovados = await GetPageHtmlAsync(client, "/Controlo/Approve?estado=aprovado");
        Assert.Contains($"Controlos de {hoje.AddYears(-1):dd/MM/yyyy} a {hoje:dd/MM/yyyy}", aprovados);
        Assert.Contains("class=\"revisao-item mch-B1", aprovados);
        Assert.Contains($"Registado {DmoBusinessTime.ToLocal(registado):dd/MM/yyyy HH:mm}", aprovados);
        Assert.Contains("Aprovado por Chefe Aprovação em ", aprovados);

        // Outra máquina, ou um período sem o registo: nada.
        Assert.Contains("Nenhum controlo neste período com estes filtros.",
            await GetPageHtmlAsync(client, "/Controlo/Approve?estado=aprovado&maquina=C2"));
        Assert.Contains("Nenhum controlo neste período com estes filtros.",
            await GetPageHtmlAsync(client, $"/Controlo/Approve?de={dia.AddDays(1):yyyy-MM-dd}"));

        // De e até: o período escolhido; o link do controlo mantém-no.
        var periodo = await GetPageHtmlAsync(client, $"/Controlo/Approve?de={dia:yyyy-MM-dd}&ate={dia:yyyy-MM-dd}");
        Assert.Contains($"Controlos de {dia:dd/MM/yyyy} a {dia:dd/MM/yyyy}", periodo);
        Assert.Contains($"href=\"/Controlo/Approve?jobon_id={jobOnId}&amp;de={dia:yyyy-MM-dd}&amp;ate={dia:yyyy-MM-dd}\"", periodo);

        // A lista sozinha (escrever num filtro): o pendente não aparece.
        var lista = await GetPageHtmlAsync(client, $"/Controlo/Approve?handler=Resultados&de={dia:yyyy-MM-dd}&estado=pendente");
        Assert.Contains("Nenhum controlo neste período com estes filtros.", lista);
        Assert.DoesNotContain("revisaoFiltros", lista);

        // No dia, a lista também diz quem aprovou.
        var doDia = await GetPageHtmlAsync(client, $"/Controlo/Approve?ano={dia.Year}&mes={dia.Month}&dia={dia.Day}");
        Assert.Contains("Aprovado por Chefe Aprovação em ", doDia);
    }
}
