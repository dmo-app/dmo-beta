using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A CADÊNCIA DO SAP NO JOB ON (Diogo, 27/10/2026: «cadência para meter no
/// Job On»; 28/10/2026: «fica Cadência usada»): o cabeçalho da folha e a folha
/// impressa mostram a «Cadência usada», a moda das ordens do SAP com a mesma
/// referência, máquina e datas (a das ordens com mais dias), com a média no
/// título. Sem relatório dessa produção, a da produção anterior, marcada
/// «ant.»; sem nenhuma, «—» no ecrã e a linha para escrever à mão.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_MostraACadenciaDoSap_DaOrdemComMaisDias()
    {
        SeedUser("4432", "Chefe Cadência", "provider-folha-cadencia", "Folha1234!");
        var marca = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var referencia = "92" + marca + "Y1";
        int comSap, semSap, outraReferencia;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var maquina = new Machine { Code = "Q" + marca };
            var com = new JobOn { Reference = referencia, ProductionNumber = "202682", Machine = maquina, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 10) };
            var sem = new JobOn { Reference = referencia, ProductionNumber = "202683", Machine = maquina, InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 5) };
            var outra = new JobOn { Reference = "93" + marca + "Y1", ProductionNumber = "202684", Machine = maquina, InicioEm = new DateOnly(2026, 9, 1) };
            db.JobOns.AddRange(com, sem, outra);
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRZ", Linha = maquina.Code, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 7), ProducaoBruta = 656_341, Cadencia = 489, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRZ", Linha = maquina.Code, InicioEm = new DateOnly(2026, 7, 7), FimEm = new DateOnly(2026, 7, 10), ProducaoBruta = 120_000, Cadencia = 500, LidoEmUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            (comSap, semSap, outraReferencia) = (com.Id, sem.Id, outra.Id);
        }
        using var client = await LoginClientAsync("4432", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={comSap}"));

        // 489 × 7 dias e 500 × 3: moda 489, média 492.30.
        Assert.Contains("<span class=\"fl\">Cadência usada</span><span class=\"fv\"><strong title=\"Do SAP: moda 489 · média 492.30\">489</strong></span>", html, StringComparison.Ordinal);
        Assert.Contains("<div><span>Cadência usada</span><b class=\"jfn-escrever\">489</b></div>", html, StringComparison.Ordinal);

        // Sem relatório desta produção: a da anterior da mesma referência.
        var semRelatorio = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={semSap}"));
        Assert.Contains("<span class=\"fl\">Cadência usada</span><span class=\"fv\"><strong title=\"Do SAP, produção anterior 202682: moda 489 · média 492.30\">489</strong><span class=\"unit\">ant.</span></span>", semRelatorio, StringComparison.Ordinal);
        Assert.Contains("<div><span>Cadência usada</span><b class=\"jfn-escrever\">489 ant.</b></div>", semRelatorio, StringComparison.Ordinal);

        // Sem nenhuma: «—» e a linha para escrever à mão.
        var semNenhuma = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={outraReferencia}"));
        Assert.Contains("<span class=\"fl\">Cadência usada</span><span class=\"fv\"><strong title=\"Do SAP (relatório de produções)\">—</strong></span>", semNenhuma, StringComparison.Ordinal);
        Assert.Contains("<div><span>Cadência usada</span><b class=\"jfn-escrever\"></b></div>", semNenhuma, StringComparison.Ordinal);
    }
}
