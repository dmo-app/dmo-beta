using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O painel das linhas com várias produções no dia operacional (Diogo,
/// 22/10/2026): a que ficava escondida não se podia escolher para registar
/// a BQ. Agora aparecem todas; só a sobreposição real (JobOnOverlap.Crosses)
/// mostra o aviso, e nada bloqueia.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private async Task<(int Entra, int Sai)> SeedLinhaAsync(string linha, string bq, DateOnly saiInicio, DateOnly saiFim, DateOnly entraInicio)
    {
        await SeedToolAsync(bq, "R" + linha, "1");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = linha };
        var sai = new BqContext
        {
            ToolId = bq,
            JobOn = new JobOn { Reference = "SAI" + linha, ProductionNumber = "PN-SAI-" + linha, Machine = machine, InicioEm = saiInicio, FimEm = saiFim }
        };
        var entra = new BqContext
        {
            ToolId = bq,
            JobOn = new JobOn { Reference = "ENTRA" + linha, ProductionNumber = "PN-ENTRA-" + linha, Machine = machine, InicioEm = entraInicio }
        };
        db.AddRange(sai, entra);
        await db.SaveChangesAsync();
        return (entra.Id, sai.Id);
    }

    private static string PainelDasLinhas(string html)
    {
        var inicio = html.IndexOf("id=\"bqCurrentLines\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "O painel das linhas não aparece.");
        var fim = html.IndexOf("</aside>", inicio, StringComparison.Ordinal);
        return html[inicio..fim];
    }

    [Fact]
    public async Task DiaDaTroca_OPainelMostraAsDuasProducoes_SemAviso()
    {
        var dia = new DateOnly(2031, 5, 20);
        var (entra, sai) = await SeedLinhaAsync("TX1", "BQ-TROCA-TX1", dia.AddDays(-10), dia, dia);
        SeedUser("4198", "Painel Troca", "provider-painel-troca", "PainelTroca1!");

        using var app = FactoryWithBoquilhasClock(new DateTime(2031, 5, 20, 10, 0, 0, DateTimeKind.Utc));
        using var client = await LoginClientAsync("4198", "PainelTroca1!", app);
        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));

        var posEntra = painel.IndexOf($"data-dmo-open=\"/Boquilhas?bq_id={entra}\"", StringComparison.Ordinal);
        var posSai = painel.IndexOf($"data-dmo-open=\"/Boquilhas?bq_id={sai}\"", StringComparison.Ordinal);
        Assert.True(posEntra >= 0, "A produção que entra não aparece.");
        Assert.True(posSai > posEntra, "A produção que sai não aparece depois da que entra.");
        Assert.DoesNotContain("Sobreposição na linha TX1", painel);
    }

    [Fact]
    public async Task DiaDaTroca_AQueEntraSemBq_MostraAProducaoEAbreOJobOn_NaoSemProducao()
    {
        // Diogo, 22/10/2026: «na C3 aparecem 2 opções mas uma delas mostra
        // sem produção; deveria ser a produção que vai sair e a que vai entrar».
        var dia = new DateOnly(2031, 5, 20);
        await SeedToolAsync("BQ-SEMBQ-NX1", "RNX1", "1");
        int entraId, saiCtx;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "NX1" };
            var sai = new BqContext
            {
                ToolId = "BQ-SEMBQ-NX1",
                JobOn = new JobOn { Reference = "SAINX1", ProductionNumber = "PN-SAI-NX1", Machine = machine, InicioEm = dia.AddDays(-10), FimEm = dia }
            };
            var entra = new JobOn { Reference = "ENTRANX1", ProductionNumber = "PN-ENTRA-NX1", Machine = machine, InicioEm = dia };
            db.AddRange(sai, entra);
            await db.SaveChangesAsync();
            entraId = entra.Id;
            saiCtx = sai.Id;
        }

        SeedUser("4203", "Painel Sem BQ", "provider-painel-sembq", "PainelSemBq1!");
        using var app = FactoryWithBoquilhasClock(new DateTime(2031, 5, 20, 10, 0, 0, DateTimeKind.Utc));
        using var client = await LoginClientAsync("4203", "PainelSemBq1!", app);
        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));

        Assert.Contains($"/JobOn?jobon_id={entraId}#lightFicha", painel);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={saiCtx}\"", painel);

        // A zona da NX1: do primeiro <strong>NX1</strong> até à linha seguinte
        // de outra linha (ou ao fim do painel).
        var inicio = painel.IndexOf("<strong>NX1</strong>", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A NX1 não aparece no painel.");
        var ultima = painel.LastIndexOf("<strong>NX1</strong>", StringComparison.Ordinal);
        var seguinte = painel.IndexOf("<strong>", ultima + "<strong>NX1</strong>".Length, StringComparison.Ordinal);
        var zona = painel[inicio..(seguinte < 0 ? painel.Length : seguinte)];
        Assert.Contains("ENTRANX1 · PN-ENTRA-NX1 · sem BQ", zona);
        Assert.Contains("SAINX1 · PN-SAI-NX1", zona);
        Assert.DoesNotContain("Sem produção", zona);
        Assert.DoesNotContain("sem produção", zona);
    }

    [Fact]
    public async Task SobreposicaoReal_OPainelMostraAsDuasProducoes_ComAviso()
    {
        var dia = new DateOnly(2031, 5, 20);
        var (entra, sai) = await SeedLinhaAsync("SX1", "BQ-SOBRE-SX1", dia.AddDays(-10), dia.AddDays(3), dia.AddDays(-1));
        SeedUser("4199", "Painel Sobre", "provider-painel-sobre", "PainelSobre1!");

        using var app = FactoryWithBoquilhasClock(new DateTime(2031, 5, 20, 10, 0, 0, DateTimeKind.Utc));
        using var client = await LoginClientAsync("4199", "PainelSobre1!", app);
        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));

        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={entra}\"", painel);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={sai}\"", painel);
        Assert.Contains("Sobreposição na linha SX1: corrija as datas no Job On", painel);
        Assert.Contains("href=\"/JobOn?jobon_id=", painel);
    }

    [Fact]
    public async Task UmaSoProducao_OPainelFicaComoAntes()
    {
        var dia = new DateOnly(2031, 5, 20);
        await SeedToolAsync("BQ-UMA-UX1", "RUX1", "1");
        int contexto;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var ctx = new BqContext
            {
                ToolId = "BQ-UMA-UX1",
                JobOn = new JobOn { Reference = "UNICAUX1", ProductionNumber = "PN-UNICA", Machine = new Machine { Code = "UX1" }, InicioEm = dia.AddDays(-2), FimEm = dia.AddDays(2) }
            };
            db.Add(ctx);
            await db.SaveChangesAsync();
            contexto = ctx.Id;
        }
        SeedUser("4200", "Painel Uma", "provider-painel-uma", "PainelUma1!");

        using var app = FactoryWithBoquilhasClock(new DateTime(2031, 5, 20, 10, 0, 0, DateTimeKind.Utc));
        using var client = await LoginClientAsync("4200", "PainelUma1!", app);
        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(painel, "<strong>UX1</strong>"));
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={contexto}\"", painel);
        Assert.Contains("<span>UNICAUX1 · PN-UNICA</span>", painel);
        Assert.DoesNotContain("dmo-rail-warning", painel);
    }
    [Fact]
    public async Task DiaDaTroca_RegistaPeloPainelUmMovimentoEmCadaProducao_NoJobOnCerto()
    {
        var dia = new DateOnly(2031, 5, 20);
        await SeedToolAsync("BQ-TROCA-SAI-MX1", "RMXS", "1");
        await SeedToolAsync("BQ-TROCA-ENTRA-MX1", "RMXE", "1");
        int sai, entra; int? jobSai, jobEntra;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "MX1" };
            var cSai = new BqContext
            {
                ToolId = "BQ-TROCA-SAI-MX1",
                JobOn = new JobOn { Reference = "SAIMX1", ProductionNumber = "PN-SAI-MX1", Machine = machine, InicioEm = dia.AddDays(-10), FimEm = dia }
            };
            var cEntra = new BqContext
            {
                ToolId = "BQ-TROCA-ENTRA-MX1",
                JobOn = new JobOn { Reference = "ENTRAMX1", ProductionNumber = "PN-ENTRA-MX1", Machine = machine, InicioEm = dia }
            };
            db.AddRange(cSai, cEntra);
            await db.SaveChangesAsync();
            (sai, entra, jobSai, jobEntra) = (cSai.Id, cEntra.Id, cSai.JobOnId, cEntra.JobOnId);
        }
        SeedUser("4201", "Painel Registo", "provider-painel-registo", "PainelRegisto1!");

        using var app = FactoryWithBoquilhasClock(new DateTime(2031, 5, 20, 10, 0, 0, DateTimeKind.Utc));
        using var client = await LoginClientAsync("4201", "PainelRegisto1!", app);
        var painel = PainelDasLinhas(await GetPageHtmlAsync(client, "/Boquilhas"));
        var links = System.Text.RegularExpressions.Regex.Matches(painel, "data-dmo-open=\"(/Boquilhas\\?bq_id=(\\d+))\"")
            .Select(m => (Url: System.Net.WebUtility.HtmlDecode(m.Groups[1].Value), Id: int.Parse(m.Groups[2].Value)))
            .Where(l => l.Id == sai || l.Id == entra)
            .ToList();
        Assert.Equal(2, links.Count);
        Assert.Equal([entra, sai], links.Select(l => l.Id).ToArray());

        var quantidades = new Dictionary<int, int> { [sai] = 3, [entra] = 5 };
        foreach (var link in links)
        {
            var html = await GetPageHtmlAsync(client, link.Url);
            var (acao, campos) = EnvioDoFormulario.Enviar(html,
                [("quantity", quantidades[link.Id].ToString()), ("occurredAt", "2031-05-20T09:00")],
                radios: [("movement_type", "saida")]);
            Assert.Contains("RegisterMovement", acao);
            using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var verificar = _factory.Services.CreateScope();
        var dbv = verificar.ServiceProvider.GetRequiredService<DmoDbContext>();
        var movimentos = await dbv.BqMovements.AsNoTracking()
            .Where(m => m.Trace.BqContextId == sai || m.Trace.BqContextId == entra)
            .Select(m => new { m.Quantity, m.Trace.BqContextId, m.Trace.BqContext!.JobOnId, ToolId = m.Trace.BqContext.ToolId })
            .ToListAsync();
        Assert.Equal(2, movimentos.Count);
        var doSai = Assert.Single(movimentos, m => m.Quantity == 3);
        Assert.Equal(sai, doSai.BqContextId);
        Assert.Equal(jobSai, doSai.JobOnId);
        Assert.Equal("BQ-TROCA-SAI-MX1", doSai.ToolId);
        var doEntra = Assert.Single(movimentos, m => m.Quantity == 5);
        Assert.Equal(entra, doEntra.BqContextId);
        Assert.Equal(jobEntra, doEntra.JobOnId);
        Assert.Equal("BQ-TROCA-ENTRA-MX1", doEntra.ToolId);
        Assert.NotEqual(jobSai, jobEntra);
    }
}
