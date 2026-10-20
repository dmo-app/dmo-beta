using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// Deep sweep 2 (18/10/2026): o Histórico do Controlo com a data do registo
/// dos Pesos e dos Pegamentos (F-4), o «Sem Job On associado» que filtra pela
/// referência e pela data (F-5), o dropdown «Nº de produção» com a regra da
/// tabela e a referência de cada produção (F-6) e a produção mais antiga
/// pedida por link (F-18).
/// </summary>
public partial class PesoComparacaoPageTests
{
    /// <summary>Uma produção do 1075… com controlo (o Resumo dela entra no histórico).</summary>
    private async Task<int> SeedOutraProducaoAsync(string reference, string productionNumber)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var job = new JobOn { Reference = reference, ProductionNumber = productionNumber, MachineId = db.Machines.Single().Id };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();
        db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOnId = job.Id });
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<string> HistoricoAsync(HttpClient client, string filtros) =>
        WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&{filtros}"));

    private static int Linhas(string html, string referenciaProducao) =>
        Regex.Matches(html, $"<td>{Regex.Escape(referenciaProducao)} · B1</td>").Count;

    [Fact]
    public async Task Historico_ODropdownDasProducoes_TemARegraDaTabelaEAReferenciaDeCadaProducao()
    {
        // F-6: «1075» na referência mostrava os registos na tabela e o
        // dropdown ficava só com «Todas» (o dropdown queria a referência
        // inteira). O mesmo nº em duas referências: a opção leva a referência
        // e filtra só essa produção.
        await SeedProducaoAsync();
        await SeedOutraProducaoAsync("1075T999", "202601");
        SeedUser("4391", "Peso Operador", "provider-cmp-hist-1", "PesoHist1!");
        using var client = await LoginClientAsync("4391", "PesoHist1!");

        var inicio = await HistoricoAsync(client, "referencia=1075");
        var zona = PesoZonas.Zona(inicio, "pesoHistoricoProducaoZona");
        Assert.Contains("<option value=\"202601|1075T999\">202601 · 1075T999</option>", zona);
        Assert.Contains("<option value=\"202601|1075C142\">202601 · 1075C142</option>", zona);
        Assert.True(Linhas(inicio, "1075C142 · 202601") > 0);
        Assert.Equal(1, Linhas(inicio, "1075T999 · 202601"));

        var escolhida = await HistoricoAsync(client, "referencia=1075&producao=202601%7C1075T999");
        Assert.Contains("<option value=\"202601|1075T999\" selected=\"selected\">202601 · 1075T999</option>", escolhida);
        Assert.Equal(1, Linhas(escolhida, "1075T999 · 202601"));
        Assert.Equal(0, Linhas(escolhida, "1075C142 · 202601"));

        // Um link antigo só com o nº: a produção mais recente com esse nº, nunca as duas juntas.
        var antigo = await HistoricoAsync(client, "referencia=1075&producao=202601");
        Assert.Contains("selected=\"selected\">202601 · 1075T999</option>", antigo);
        Assert.Equal(0, Linhas(antigo, "1075C142 · 202601"));

        // Uma só referência: a opção diz só o nº (e leva a referência), também
        // com o início da referência; um link antigo só com o nº continua a servir.
        var umaReferencia = await HistoricoAsync(client, "referencia=1075c1");
        Assert.Contains("<option value=\"202601|1075C142\">202601</option>", umaReferencia);
        Assert.Contains("<option value=\"202601|1075C142\" selected=\"selected\">202601</option>",
            await HistoricoAsync(client, "referencia=1075C1&producao=202601"));

        // Um pedaço a meio da referência não encontra nada, nem no dropdown nem na tabela.
        var meio = await HistoricoAsync(client, "referencia=075");
        Assert.DoesNotContain("<option value=\"202601", meio);
        Assert.Contains("Sem registos para esta procura.", meio);

        // A produção só filtra com a referência (sem ela não há dropdown).
        var semReferencia = await HistoricoAsync(client, "tipo=Resumo&producao=202601%7C1075T999");
        Assert.Equal(1, Linhas(semReferencia, "1075T999 · 202601"));
        Assert.Equal(1, Linhas(semReferencia, "1075C142 · 202601"));

        // As escolhas novas continuam a trocar só as zonas.
        var fora = PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?historico=1"));
        Assert.Equal(fora, PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=1075&producao=202601%7C1075T999")));
    }

    [Fact]
    public async Task Historico_AProducaoEscolhida_FicaAMesmaQuandoAReferenciaSeAlarga()
    {
        // A produção escolhida com uma só referência no dropdown vai no
        // pedido seguinte (data-dmo-fica). Se a opção levasse só o nº, ao
        // alargar o texto da referência («1075C142» → «1075») a produção
        // passava, sem o operador a escolher, para a mais recente com esse nº
        // noutra referência (a 1075T999). O nº só é único com a referência.
        await SeedProducaoAsync();
        await SeedOutraProducaoAsync("1075T999", "202601");
        SeedUser("4394", "Peso Operador", "provider-cmp-hist-4", "PesoHist4!");
        using var client = await LoginClientAsync("4394", "PesoHist4!");

        var umaReferencia = PesoZonas.Zona(await HistoricoAsync(client, "referencia=1075C142"), "pesoHistoricoProducaoZona");
        var valor = Regex.Match(umaReferencia, "<option value=\"([^\"]+)\">202601</option>").Groups[1].Value;
        Assert.Equal("202601|1075C142", valor);

        var alargada = await HistoricoAsync(client, $"referencia=1075&producao={Uri.EscapeDataString(valor)}");
        Assert.Contains("<option value=\"202601|1075C142\" selected=\"selected\">202601 · 1075C142</option>", alargada);
        Assert.True(Linhas(alargada, "1075C142 · 202601") > 0);
        Assert.Equal(0, Linhas(alargada, "1075T999 · 202601"));
    }

    [Fact]
    public async Task Historico_UmaProducaoMaisAntigaPedidaPorLink_ContinuaAFiltrarEEntraNoDropdown()
    {
        // F-18: o dropdown tem as últimas 5; uma produção mais antiga da
        // mesma referência, pedida por link, era tirada sem aviso.
        await SeedProducaoAsync();
        foreach (var numero in new[] { "202602", "202603", "202604", "202605", "202606" })
        {
            await SeedOutraProducaoAsync("1075C142", numero);
        }
        SeedUser("4392", "Peso Operador", "provider-cmp-hist-2", "PesoHist2!");
        using var client = await LoginClientAsync("4392", "PesoHist2!");

        var ultimas = PesoZonas.Zona(await HistoricoAsync(client, "referencia=1075C142"), "pesoHistoricoProducaoZona");
        Assert.Equal(5, Regex.Matches(ultimas, "<option value=\"2026").Count);
        Assert.DoesNotContain("202601", ultimas);

        var antiga = await HistoricoAsync(client, "referencia=1075C142&producao=202601");
        Assert.Contains("<option value=\"202601|1075C142\" selected=\"selected\">202601</option>", antiga);
        Assert.Equal(6, Regex.Matches(PesoZonas.Zona(antiga, "pesoHistoricoProducaoZona"), "<option value=\"2026").Count);
        Assert.Equal(0, Linhas(antiga, "1075C142 · 202606"));
        Assert.True(Linhas(antiga, "1075C142 · 202601") > 0);

        // Um nº que a referência não tem não filtra.
        var outra = await HistoricoAsync(client, "referencia=1075C142&producao=999999");
        Assert.Contains("<option value=\"\" selected=\"selected\">Todas</option>", outra);
        Assert.True(Linhas(outra, "1075C142 · 202606") > 0);
    }

    [Fact]
    public async Task Historico_PesosEPegamentos_ComADataDoRegisto_ESemJobOnAReferenciaEADataFiltram()
    {
        // F-4: com a data de um dia com Pesos, «Sem registos para esta
        // procura.», e a coluna «Data» com «—». F-5: com «Sem Job On
        // associado», a referência e a data continuavam escritas e não
        // filtravam nada.
        var (_, _, pesoId) = await SeedProducaoAsync();
        int soltoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Single(p => p.Id == pesoId).RegistadoEmUtc = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
            var solto = new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-TOOL-9",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m,
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3",
                RegistadoEmUtc = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc)
            };
            db.Pesos.Add(solto);
            await db.SaveChangesAsync();
            soltoId = solto.Id;
        }
        SeedUser("4393", "Peso Operador", "provider-cmp-hist-3", "PesoHist3!");
        using var client = await LoginClientAsync("4393", "PesoHist3!");

        var doDia = await HistoricoAsync(client, "tipo=Peso&data=2026-10-17");
        Assert.Contains($"data-dmo-open=\"/Controlo/Peso?peso_id={pesoId}\"", doDia);
        Assert.Contains("<td>17/10/2026 11:00</td>", doDia);
        Assert.Contains("Sem registos para esta procura.", await HistoricoAsync(client, "tipo=Peso&data=2026-10-15"));

        // Sem Job On: a referência procura-se nas ferramentas do registo, a data é a do registo.
        var soltos = $"data-dmo-open=\"/Controlo/Peso?peso_id={soltoId}\"";
        var porReferencia = await HistoricoAsync(client, "sem_jobon=1&referencia=5447");
        Assert.Contains(soltos, porReferencia);
        Assert.Contains("<td>16/10/2026 10:00</td>", porReferencia);
        Assert.Contains(soltos, await HistoricoAsync(client, "sem_jobon=1&referencia=t19&data=2026-10-16"));
        const string vazioComFiltros = "Não há Pesos nem Pegamentos por associar a um Job On com estes filtros.";
        Assert.Contains(vazioComFiltros, await HistoricoAsync(client, "sem_jobon=1&referencia=1075C142"));
        Assert.Contains(vazioComFiltros, await HistoricoAsync(client, "sem_jobon=1&data=2026-10-17"));

        // Os campos não mudam com o «Sem Job On» (ficam a filtrar): só as zonas trocam.
        var fora = PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?historico=1"));
        Assert.Equal(fora, PesoZonas.ForaDasZonas(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&sem_jobon=1&referencia=5447&data=2026-10-16")));
    }
}
