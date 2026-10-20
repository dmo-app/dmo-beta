using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// «Procurar / trocar de boquilha ou produção» (Diogo, 17/10/2026): sem botão
// «Abrir registo»; as boquilhas aparecem ao escrever com a BQ, o lote, as
// máquinas e se está em produção, e o duplo clique abre o registo.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task Procura_ShowsBqLotMachinesAndProductionState_AndOpensTheRegister()
    {
        await SeedToolAsync("BQ-PROCURA-1", "QX173", "8");
        await SeedToolAsync("BQ-PROCURA-2", "QX173", "9");
        await SeedToolAsync("BQ-PROCURA-3", "QX999", "1");
        var bq = await SeedProductionAsync("BQ-PROCURA-1", "5447QX173", "PN-770001", "B6");
        var oldTrace = await SeedTraceAsync("BQ-PROCURA-2");
        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = await db.JobOns.SingleAsync(j => j.ProductionNumber == "PN-770001");
            jobOn.InicioEm = hoje.AddDays(-1);
            var machine = await db.Machines.SingleAsync(m => m.Code == "B6");
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-PROCURA-1", MachineId = machine.Id });
            await db.SaveChangesAsync();
        }

        SeedUser("4195", "Registo Procura", "provider-registo-procura", "RegistoProcura1!");
        using var client = await LoginClientAsync("4195", "RegistoProcura1!");

        // O bloco já não tem o botão nem o título «Abrir registo existente».
        var page = await GetPageHtmlAsync(client, "/Boquilhas");
        Assert.DoesNotContain("Abrir registo existente", page);
        Assert.DoesNotContain(">Abrir registo</button>", page);
        Assert.Contains("data-dmo-procura=\"/Boquilhas?handler=ProcurarBq\"", page);

        var results = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=qx17");
        Assert.Contains("<td><strong>QX173</strong></td>", results);
        Assert.Contains("<td>8</td>", results);
        Assert.Contains("<td>9</td>", results);
        Assert.DoesNotContain("QX999", results);
        Assert.Contains("<td>B6</td>", results);
        Assert.Contains("Em produção", results);
        Assert.Contains("B6 · 5447QX173 · PN-770001", results);
        Assert.Contains("Sem produção", results);
        // Em produção abre o registo da produção; sem produção, o último registo.
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={bq.Id}\"", results);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?trace_id={oldTrace.Id}\"", results);

        // Pela produção também encontra a boquilha.
        var byProduction = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=PN-770001");
        Assert.Contains("<td><strong>QX173</strong></td>", byProduction);

        var none = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=ZZZNADA");
        Assert.Contains("Nenhuma boquilha encontrada.", none);
    }

    [Fact]
    public async Task Procura_DepoisDeTrocarABq_OLoteSubstituidoJaNaoEstaEmProducao()
    {
        // Bug (deep sweep 2, 18/10/2026): depois de trocar a BQ no Editar do
        // Job On, o lote substituído continuava «Em produção» ao lado do
        // lote novo, e o registo dele dizia «Produção em máquina».
        await SeedToolAsync("BQ-TROCA-8", "QW173", "8");
        await SeedToolAsync("BQ-TROCA-9", "QW173", "9");
        var antiga = await SeedProductionAsync("BQ-TROCA-8", "5447QW173", "PN-770101", "B6");
        var registoAntigo = await SeedTraceAsync(toolId: null, bqContextId: antiga.Id);
        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        int novaId;
        using (var scope = _factory.Services.CreateScope())
        {
            // A troca como o Editar do Job On a grava (beta.227): o bq_id
            // antigo fica na produção como substituído e nasce um novo.
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = await db.JobOns.SingleAsync(j => j.ProductionNumber == "PN-770101");
            jobOn.InicioEm = hoje.AddDays(-1);
            var contexto = await db.BqContexts.SingleAsync(c => c.Id == antiga.Id);
            contexto.JobOnId = null;
            contexto.ReplacedJobOnId = jobOn.Id;
            contexto.ReplacedAtUtc = DateTime.UtcNow;
            var nova = new BqContext { JobOnId = jobOn.Id, ToolId = "BQ-TROCA-9" };
            db.BqContexts.Add(nova);
            await db.SaveChangesAsync();
            novaId = nova.Id;
        }

        SeedUser("4196", "Registo Troca", "provider-registo-troca", "RegistoTroca1!");
        using var client = await LoginClientAsync("4196", "RegistoTroca1!");

        var results = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=qw17");
        var linhaAntiga = Linha(results, "BQ-TROCA-8");
        var linhaNova = Linha(results, "BQ-TROCA-9");
        Assert.Contains("Sem produção", linhaAntiga);
        Assert.DoesNotContain("Em produção", linhaAntiga);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?trace_id={registoAntigo.Id}\"", linhaAntiga);
        Assert.Contains("Em produção", linhaNova);
        Assert.Contains("B6 · 5447QW173 · PN-770101", linhaNova);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={novaId}\"", linhaNova);

        // Pela produção, o lote substituído continua a aparecer, como usado
        // nela, mas não «Em produção».
        var pelaProducao = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=PN-770101");
        var antigaPelaProducao = Linha(pelaProducao, "BQ-TROCA-8");
        Assert.Contains("Encontrada na produção B6 · 5447QW173 · PN-770101", antigaPelaProducao);
        Assert.DoesNotContain("Em produção", antigaPelaProducao);

        // O registo do lote substituído diz que a BQ foi trocada.
        var ficha = await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1&ferramenta=BQ-TROCA-8");
        Assert.Contains(">BQ trocada nesta produção</td>", ficha);
    }

    private static string Linha(string html, string toolId)
    {
        var inicio = html.IndexOf($"data-id=\"{toolId}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A linha de {toolId} não aparece.");
        var fim = html.IndexOf("</tr>", inicio, StringComparison.Ordinal);
        return html[inicio..fim];
    }

    [Fact]
    public async Task Procura_WithoutLogin_IsRefused()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Boquilhas?handler=ProcurarBq&q=T1");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
