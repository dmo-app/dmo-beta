using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// Deep sweep 2 (18/10/2026): os dropdowns de produções da folha e da
/// Comparação de Peso. F-12: a «Produção a comparar» só com as anteriores à
/// atual (a resposta R18: o PDF chama-lhe «Comparação com o controlo
/// anterior»), as últimas 5, e a pedida fica, mais antiga ou posterior. F-3:
/// o «Produção / Job On» da Comparação pelo início da referência ou da parte
/// BQ, nunca a meio, as últimas 5, cada produção pelo jobon_id.
/// </summary>
public partial class PesoComparacaoPageTests
{
    /// <summary>O select indicado, sem o resto da página.</summary>
    private static string Select(string html, string id)
    {
        var inicio = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem #{id}");
        return html[inicio..html.IndexOf("</select>", inicio, StringComparison.Ordinal)];
    }

    /// <summary>
    /// Outra produção da 1075C142 na máquina da folha, com o início dado e um
    /// Peso de uma leitura com a CM da produção principal; devolve o Peso.
    /// </summary>
    private async Task<int> SeedProducaoComPesoAsync(string numero, DateOnly? inicioEm)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var job = new JobOn { Reference = "1075C142", ProductionNumber = numero, MachineId = (await db.Machines.SingleAsync()).Id, InicioEm = inicioEm };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = job.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        var peso = new Peso
        {
            ControloId = controlo.Id, CmToolId = "T3", BqToolId = "BQ-TOOL-9",
            Process = "NNPB", WaterTemperatureC = 20, VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m
        };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 198.64m });
        await db.SaveChangesAsync();
        return peso.Id;
    }

    /// <summary>O início da produção principal (a da folha).</summary>
    private async Task DefinirInicioAsync(int jobOnId, DateOnly inicioEm)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        (await db.JobOns.SingleAsync(j => j.Id == jobOnId)).InicioEm = inicioEm;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica()
    {
        // F-12: o dropdown tinha todas as produções da referência com Peso,
        // depois as 5 mais recentes, também as posteriores à atual. Ficam as
        // últimas 5 que começaram antes dela (aqui registadas depois, conta
        // o início); as posteriores não aparecem.
        var (controloId, jobOnId, pesoId) = await SeedProducaoAsync();
        await DefinirInicioAsync(jobOnId, new DateOnly(2026, 8, 1));
        var pesosPorProducao = new Dictionary<string, int>();
        foreach (var n in Enumerable.Range(2, 6))
        {
            pesosPorProducao[$"20260{n}"] = await SeedProducaoComPesoAsync($"20260{n}", new DateOnly(2026, n, 1));
        }
        pesosPorProducao["202609"] = await SeedProducaoComPesoAsync("202609", new DateOnly(2026, 9, 1));
        pesosPorProducao["202610"] = await SeedProducaoComPesoAsync("202610", null);

        SeedUser("4330", "Peso Operador", "provider-cmp-30", "PesoCmp30!");
        using var client = await LoginClientAsync("4330", "PesoCmp30!");

        foreach (var semEscolhaUrl in new[] { $"{PagePath}?controlo_id={controloId}", $"{PagePath}?peso_id={pesoId}" })
        {
            var semEscolha = Select(await GetPageHtmlAsync(client, semEscolhaUrl), "compararDropdown");
            foreach (var numero in new[] { "202603", "202604", "202605", "202606", "202607" })
            {
                Assert.Contains($"<option value=\"{pesosPorProducao[numero]}\">", semEscolha);
            }
            foreach (var fora in new[] { "202602", "202609", "202610" })
            {
                Assert.DoesNotContain($"<option value=\"{pesosPorProducao[fora]}\"", semEscolha);
            }
            Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(semEscolha, "<option ").Count);
        }

        // A pedida mais antiga ou posterior (um link, o «Guardar», o
        // «Supomos», uma comparação já guardada) fica no dropdown, no fim, e
        // escolhida, na folha da produção e no Peso aberto pelo Histórico
        // (que só compara com uma do dropdown).
        foreach (var numero in new[] { "202602", "202609" })
        {
            var pedida = pesosPorProducao[numero];
            foreach (var url in new[]
                     {
                         $"{PagePath}?controlo_id={controloId}&comparar={pedida}",
                         $"{PagePath}?peso_id={pesoId}&comparar={pedida}"
                     })
            {
                var html = await GetPageHtmlAsync(client, url);
                var dropdown = Select(html, "compararDropdown");
                Assert.Matches($"<option value=\"{pedida}\" selected=\"selected\">\\s*{numero} ", dropdown);
                Assert.True(
                    Posicao(dropdown, $"<option value=\"{pesosPorProducao["202603"]}\"")
                        < Posicao(dropdown, $"<option value=\"{pedida}\""),
                    $"a pedida {numero} vem depois das 5 anteriores");
                Assert.Contains($"<th colspan=\"3\" scope=\"colgroup\">Controlo anterior · {numero}</th>", html);
            }
        }
    }

    [Fact]
    public async Task Comparacao_AsProducoesPeloInicioDaReferencia_CadaUmaPeloJobOnId()
    {
        // F-3: «35» trazia as produções de quatro referências com 35 a meio.
        // Agora o início da referência ou da parte BQ (ReferenceMatch), as
        // últimas 5, nunca um pedaço do meio. Como o início junta várias
        // referências, cada opção é a produção pelo jobon_id: o mesmo nº
        // noutra referência é outra opção, e a escolhida é a do jobon_id.
        var (_, primeira, pesoInicial) = await SeedProducaoAsync(); // 1075C142 · 202601
        int mesmoNumero;
        int bqAMeio;
        var maisRecentes = new List<int>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machineId = (await db.Machines.SingleAsync()).Id;
            async Task<int> NovaAsync(string reference, string number)
            {
                var job = new JobOn { Reference = reference, ProductionNumber = number, MachineId = machineId };
                db.JobOns.Add(job);
                await db.SaveChangesAsync();
                return job.Id;
            }

            mesmoNumero = await NovaAsync("1075X001", "202601");
            bqAMeio = await NovaAsync("51075C142", "202601");
            foreach (var number in new[] { "202602", "202603", "202604", "202605" })
            {
                maisRecentes.Add(await NovaAsync("1075C142", number));
            }
        }

        SeedUser("4331", "Peso Operador", "provider-cmp-31", "PesoCmp31!");
        using var client = await LoginClientAsync("4331", "PesoCmp31!");

        static string[] Opcoes(string select) =>
            System.Text.RegularExpressions.Regex.Matches(select, "<option value=\"(\\d+)\"[^>]*>([^<]+)</option>")
                .Select(m => $"{m.Groups[1].Value}={m.Groups[2].Value}").ToArray();

        // «1075»: as das referências que começam por 1075, as últimas 5; a
        // 51075C142 (1075 a meio) nunca. O 202601 da 1075X001 é uma opção à
        // parte do 202601 da 1075C142.
        var inicio = Select(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075"), "cmpProducao");
        Assert.Equal(
            new[]
            {
                $"{maisRecentes[3]}=1075C142 · 202605 · B1", $"{maisRecentes[2]}=1075C142 · 202604 · B1",
                $"{maisRecentes[1]}=1075C142 · 202603 · B1", $"{maisRecentes[0]}=1075C142 · 202602 · B1",
                $"{mesmoNumero}=1075X001 · 202601 · B1"
            },
            Opcoes(inicio));
        Assert.DoesNotContain("selected=\"selected\"", inicio);

        // «c14»: a parte BQ, também a da 51075C142; a 1075X001 não.
        var parteBq = Opcoes(Select(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=c14"), "cmpProducao"));
        Assert.Equal(5, parteBq.Length);
        Assert.Contains($"{maisRecentes[3]}=1075C142 · 202605 · B1", parteBq);
        Assert.Contains($"{bqAMeio}=51075C142 · 202601 · B1", parteBq);
        Assert.DoesNotContain(parteBq, o => o.StartsWith($"{mesmoNumero}=", StringComparison.Ordinal));

        // «075» só existe a meio: nenhuma produção.
        var meio = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=075"));
        Assert.Contains("Sem produções para «075».", meio);
        Assert.Empty(Opcoes(Select(meio, "cmpProducao")));

        // A pedida, mais antiga que as 5, fica e é a escolhida pelo jobon_id,
        // não pelo nº: a 1075X001 · 202601 continua lá, sem estar escolhida.
        var pedida = Select(
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075&jobon_id={primeira}"), "cmpProducao");
        Assert.Contains($"<option value=\"{primeira}\" selected=\"selected\">1075C142 · 202601 · B1</option>", pedida);
        Assert.Contains($"<option value=\"{mesmoNumero}\">1075X001 · 202601 · B1</option>", pedida);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(pedida, "selected=\"selected\""));
        var outra = Select(
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075&jobon_id={mesmoNumero}"), "cmpProducao");
        Assert.Contains($"<option value=\"{mesmoNumero}\" selected=\"selected\">1075X001 · 202601 · B1</option>", outra);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(outra, "selected=\"selected\""));

        // A produção escolhida segue pelo jobon_id para o Peso inicial e as
        // leituras (nunca pelo nº, que se repete entre referências).
        var leituras = await GetPageHtmlAsync(
            client, $"{PagePath}?comparacao=1&referencia=1075&jobon_id={primeira}&peso_inicial={pesoInicial}");
        Assert.Contains("<select id=\"cmpProducao\" name=\"jobon_id\"", leituras);
        Assert.Contains($"<input type=\"hidden\" name=\"jobon_id\" value=\"{primeira}\" />", leituras);
        Assert.Contains($"<option value=\"{pesoInicial}\" selected=\"selected\">", Select(leituras, "cmpInicial"));
    }
}
