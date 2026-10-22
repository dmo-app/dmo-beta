using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// Deep sweep 2 (18/10/2026): o «Associar Job On» lista as produções pelo
// início da referência ou da parte BQ, nunca a meio (F-1); a pesquisa do Histórico sugere as
// referências das produções e a dica fala num clique (F-15). A procura das
// BQ no Registo saiu (Diogo, 22/10/2026): o ProcurarBq já não responde.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task ProcurarBq_JaNaoResponde_ComALista()
    {
        // Sem «Procurar / trocar» (Diogo, 22/10/2026): o handler saiu; o pedido
        // cai na página, sem a lista da procura.
        await SeedToolAsync("BQ-SEM-PROCURA", "QY179", "3");
        SeedUser("4293", "Registo Sem Procura", "provider-registo-sem-procura", "RegistoSemProc1!");
        using var client = await LoginClientAsync("4293", "RegistoSemProc1!");
        using var response = await client.GetAsync("/Boquilhas?handler=ProcurarBq&q=qy17");
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is System.Net.HttpStatusCode.NotFound || html.Contains("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("QY179", html);
        Assert.DoesNotContain("Nenhuma boquilha encontrada.", html);
        Assert.DoesNotContain("class=\"bq-procura", html);
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
