using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// PRODUÇÃO NO SAP (Diogo, 27/10/2026: «está a faltar muita informação que
/// não foi aplicada»): na folha do Job On, as Opções abrem uma janela com
/// todas as colunas do relatório da ZFMD das ordens com a mesma referência,
/// máquina e datas. Sem relatório dessa produção, não há a opção.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_ProducaoNoSap_MostraTodasAsColunasDaZfmd_SoComRelatorio()
    {
        SeedUser("4431", "Chefe SAP", "provider-folha-sap", "Folha1234!");
        var marca = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var referencia = "91" + marca + "Y1";
        int comSap, semSap;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var maquina = new Machine { Code = "Q" + marca };
            var com = new JobOn { Reference = referencia, ProductionNumber = "202680", Machine = maquina, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 6) };
            var sem = new JobOn { Reference = referencia, ProductionNumber = "202681", Machine = maquina, InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 5) };
            db.JobOns.AddRange(com, sem);
            db.SapFabricos.Add(new SapFabrico
            {
                Material = referencia + "BRZ", Linha = maquina.Code, InicioEm = new DateOnly(2026, 6, 30), FimEm = new DateOnly(2026, 7, 6),
                DiasProducao = 6, ProducaoBruta = 656_341, ProducaoLiquida = 597_103, PesoMedioGramas = 157m, Centro = "MG",
                Cadencia = 489, Gob = 3, Sectores = 12, Kcr = 170.195m, DeltaPesoGramas = -3m, PackToPull = 90.97m,
                NetQuant = 3_802_656, Paletes = 1_418m, RejeicaoTotal = 1.55m, RejeicaoMil = 4.33m, HotEnd = 95.56m,
                LidoEmUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            (comSap, semSap) = (com.Id, sem.Id);
        }
        using var client = await LoginClientAsync("4431", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={comSap}"));

        Assert.Contains("<a href=\"#jobon-sap-dialog\" role=\"button\" data-dmo-dialog=\"jobon-sap-dialog\">Produção no SAP</a>", html, StringComparison.Ordinal);
        var janela = html[html.IndexOf("<dialog id=\"jobon-sap-dialog\"", StringComparison.Ordinal)..];
        janela = janela[..(janela.IndexOf("</dialog>", StringComparison.Ordinal) + 9)];
        Assert.Contains($"{referencia}BRZ · Q{marca} · MG · 30/06/2026 a 06/07/2026", janela, StringComparison.Ordinal);
        var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-PT");
        foreach (var (rotulo, valor) in new[]
        {
            ("Dias", "6"), ("Garrafas (bruto)", 656_341.ToString("#,0", pt)), ("Garrafas (líquido)", 597_103.ToString("#,0", pt)),
            ("Peso médio", "157 g"), ("Δ peso", "−3 g"), ("Cadência", "489"), ("Gota", "3"), ("Sectores", "12"), ("KCR", "170.195"),
            ("Pack to pull", "90.97 %"), ("Net Quant", 3_802_656.ToString("#,0", pt)), ("Paletes", "1418"),
            ("Rejeição total", "1.55 %"), ("Rej. MIL", "4.33"), ("HotEnd", "95.56 %")
        })
        {
            Assert.Contains($"<div class=\"dmo-fact\"><span>{rotulo}</span><strong>{valor}</strong></div>", janela, StringComparison.Ordinal);
        }

        var semRelatorio = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={semSap}");
        Assert.DoesNotContain("jobon-sap-dialog", semRelatorio, StringComparison.Ordinal);
    }
}
