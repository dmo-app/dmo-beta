using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// Deep sweep 2 (18/10/2026): a referência procura-se pelo início dela ou da
// parte BQ, nunca a meio (F-2); a BQ encontrada por uma produção diz qual e
// abre o registo dela (F-14); o «Associar Job On» lista as produções pelo
// início da referência ou da parte BQ, nunca a meio (F-1); a pesquisa do Histórico sugere as
// referências das produções e a dica fala num clique (F-15).
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task Procura_PorUmaProducaoAntiga_DizQualEAbreORegistoDela()
    {
        await SeedToolAsync("BQ-INICIO-1", "QY171", "3");
        await SeedToolAsync("BQ-INICIO-2", "T350", "1");
        var antiga = await SeedProductionAsync("BQ-INICIO-1", "2847QY171", "PN-880001", "B3");
        var atual = await SeedProductionAsync("BQ-INICIO-1", "4526QY171", "PN-880002", "B4");
        await SeedProductionAsync("BQ-INICIO-2", "4996T350", "PN-880003", "B5");
        var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = await db.JobOns.SingleAsync(j => j.ProductionNumber == "PN-880002");
            jobOn.InicioEm = hoje.AddDays(-1);
            await db.SaveChangesAsync();
        }

        SeedUser("4196", "Registo Procura Início", "provider-registo-procura-inicio", "RegistoInicio1!");
        using var client = await LoginClientAsync("4196", "RegistoInicio1!");

        // Pela referência de uma produção antiga: a linha diz em que produção
        // foi encontrada e o clique abre o registo dessa produção.
        var porAntiga = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=2847");
        Assert.Contains("<td><strong>QY171</strong></td>", porAntiga);
        Assert.Contains("Em produção", porAntiga);
        Assert.Contains("B4 · 4526QY171 · PN-880002", porAntiga);
        Assert.Contains("Encontrada na produção B3 · 2847QY171 · PN-880001", porAntiga);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={antiga.Id}\"", porAntiga);

        // Pela produção que está em máquina: nada a acrescentar.
        var porAtual = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=4526");
        Assert.DoesNotContain("Encontrada na produção", porAtual);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={atual.Id}\"", porAtual);

        // Pela própria BQ (a parte BQ da referência): abre a produção em máquina.
        var porBq = await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=qy17");
        Assert.DoesNotContain("Encontrada na produção", porBq);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?bq_id={atual.Id}\"", porBq);

        // «35» a meio da BQ T350 e da referência 4996T350 já não encontra nada;
        // «T35», a parte BQ, encontra.
        Assert.Contains("Nenhuma boquilha encontrada.", await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=35"));
        Assert.Contains("<td><strong>T350</strong></td>", await GetPageHtmlAsync(client, "/Boquilhas?handler=ProcurarBq&q=t35"));
    }

    [Fact]
    public async Task AssociarJobOn_PassoDois_PeloInicioOuPelaParteBq_NuncaAMeio()
    {
        await SeedToolAsync("BQ-ASSOC-INICIO", "QZ172", "3");
        await SeedProductionAsync("BQ-ASSOC-INICIO", "3847QZ172", "2026999", "B3");
        await SeedProductionAsync("BQ-ASSOC-INICIO", "4526QZ172", "2026999", "B4");
        await SeedProductionAsync("BQ-ASSOC-INICIO", "13847QZ17", "2026998", "B5");
        var trace = await SeedTraceAsync("BQ-ASSOC-INICIO");
        SeedUser("4197", "Registo Associar Início", "provider-registo-associar-inicio", "RegistoInicio2!");
        using var client = await LoginClientAsync("4197", "RegistoInicio2!");

        // O início da referência («3847», em minúsculas também): só as
        // produções das referências que começam por ele, nunca a 13847QZ17
        // (3847 a meio) nem a 4526QZ172 com o mesmo nº.
        foreach (var inicio in new[] { "3847", "3847qz172" })
        {
            var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + $"&associar_ref={inicio}");
            Assert.Contains("id=\"jobNumber\"", html);
            Assert.Contains(">3847QZ172 · 2026999 · B3</option>", html);
            Assert.DoesNotContain(">4526QZ172 · 2026999 · B4</option>", html);
            Assert.DoesNotContain(">13847QZ17 · 2026998 · B5</option>", html);
        }

        // A parte BQ: as referências dela, cada uma com a sua linha.
        var parteBq = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=qz172");
        Assert.Contains(">3847QZ172 · 2026999 · B3</option>", parteBq);
        Assert.Contains(">4526QZ172 · 2026999 · B4</option>", parteBq);
        Assert.DoesNotContain(">13847QZ17 · 2026998 · B5</option>", parteBq);

        // Um pedaço do meio não encontra nada.
        var meio = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=847");
        Assert.Contains("Sem produções para «847».", meio);
        Assert.DoesNotContain("id=\"jobNumber\"", meio);
    }

    [Fact]
    public async Task Historico_PesquisaSugereReferenciasEBq_EADicaFalaNumClique()
    {
        await SeedToolAsync("BQ-HIST-SUG", "Y079", "1");
        await SeedProductionAsync("BQ-HIST-SUG", "3540Y079", "PN-880010", "B2");
        SeedUser("4198", "Histórico Sugestões", "provider-bq-hist-sugestoes", "RegistoInicio3!");
        using var client = await LoginClientAsync("4198", "RegistoInicio3!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1");
        Assert.Contains("id=\"pesquisa\"", html);
        Assert.Contains("data-dmo-suggest=\"referencias_bq\"", html);
        Assert.DoesNotContain("Duplo clique no dia escolhido", html);
        Assert.Contains("Clique outra vez no dia escolhido para tirar a seleção.", html);

        // «3540» dava «Sem sugestões» (só BQ); agora vem a referência da produção.
        var referencia = await client.GetStringAsync("/Sugestoes?fonte=referencias_bq&q=3540");
        Assert.Contains("\"value\":\"3540Y079\"", referencia, StringComparison.Ordinal);
        var bq = await client.GetStringAsync("/Sugestoes?fonte=referencias_bq&q=y07");
        Assert.Contains("\"value\":\"3540Y079\"", bq, StringComparison.Ordinal);
        Assert.Contains("\"label\":\"BQ Y079\"", bq, StringComparison.Ordinal);
        Assert.Equal("[]", await client.GetStringAsync("/Sugestoes?fonte=referencias_bq&q=079"));
    }
}
