using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// O peso do SAP na produção anterior (Diogo, 21/10/2026): pelas datas da
/// produção escolhida e a referência, as ordens do relatório de fabrico dão
/// a média pelos dias e a moda (o peso com mais dias), cada uma com a
/// diferença do peso de hoje; cada produção leva a sua no select.
/// </summary>
public partial class PesoFolhaProducaoAnteriorPageTests
{
    private void SeedSapFabrico(string inicio, string fim, decimal peso, int dias)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.SapFabricos.Add(new SapFabrico
        {
            Material = "5447T173BRA", Linha = "B1", InicioEm = DateOnly.Parse(inicio), FimEm = DateOnly.Parse(fim),
            PesoMedioGramas = peso, DiasProducao = dias, LidoEmUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    // A tabela do peso do SAP, até ao fim do cartão.
    private static string ZonaSap(string html)
    {
        var inicio = html.IndexOf("id=\"pesoAnteriorSap\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a página não tem #pesoAnteriorSap");
        return WebUtility.HtmlDecode(html[inicio..html.IndexOf("</section>", inicio, StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia()
    {
        var (controloId, _, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        // A anterior (06/04 a 24/04/2026): 181 g durante 12 dias, 183 g durante 6.
        SeedSapFabrico("2026-04-06", "2026-04-18", 181m, 12);
        SeedSapFabrico("2026-04-18", "2026-04-24", 183m, 6);
        SeedUser("4410", "Peso Operador", "provider-folha-sap", "FolhaSap10!");
        using var client = await LoginClientAsync("4410", "FolhaSap10!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        var sap = ZonaSap(html);
        var texto = System.Text.RegularExpressions.Regex.Replace(sap, "<[^>]+>|\\s+", m => m.Value.StartsWith('<') ? "" : " ");
        // Uma tabela: os títulos numa linha e os valores por baixo (Diogo,
        // 28/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraPN7CxcF7Rzwfmmd7mHxLcm).
        Assert.Contains("<tr><th>Peso da produção</th><th>Média</th><th>Moda</th></tr>", sap, StringComparison.Ordinal);
        Assert.Contains("Fim 24/04/2026 · 18 dias", texto, StringComparison.Ordinal);
        // Nenhum texto à vista diz «SAP» (o aviso sem dados está escondido; tiram-se as etiquetas e os atributos).
        var visivel = System.Text.RegularExpressions.Regex.Replace(sap[..sap.IndexOf("data-sap-sem", StringComparison.Ordinal)], "<[^>]+>", " ");
        Assert.DoesNotContain("SAP", visivel, StringComparison.Ordinal);
        // A média e a moda, a moda com os dias em que esse peso se usou (Diogo,
        // 28/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraVoNvoQBUPf4cwx9jYiHvPJ e
        // cmsg_01K75FTs9bQnbhhH7qttpgraTaRYMDqbdFc2okYBHmExZW).
        Assert.Contains("<td><strong><span data-sap-valor=\"media\">181.67</span> g</strong></td>", sap, StringComparison.Ordinal);
        Assert.Contains("<td><strong><span data-sap-valor=\"moda\">181</span> g</strong> · <span data-sap-valor=\"moda-dias\">12</span> de <span data-sap-valor=\"dias\">18</span> dias</td>", sap, StringComparison.Ordinal);
        // A média e a moda ficam as duas à vista, cada uma com a diferença do
        // peso deste controlo, sem janela e sem «Hoje» (Diogo, 27/10/2026,
        // cmsg_01K75FTs9bQnbhhH7qttpgra64wN7wJBwgW1MPgAeTBaS9).
        Assert.DoesNotContain("data-sap-ver", sap, StringComparison.Ordinal);
        Assert.DoesNotContain("pesoSapModa", sap, StringComparison.Ordinal);
        Assert.DoesNotContain("Hoje", sap, StringComparison.Ordinal);
        Assert.Matches("data-sap-delta=\"media\">Peso médio atual − Média: [+−]\\d+\\.\\d{2} g \\([+−]\\d+\\.\\d{2} %\\)<", sap);
        Assert.Matches("data-sap-delta=\"moda\">Peso médio atual − Moda: [+−]\\d+\\.\\d{2} g \\([+−]\\d+\\.\\d{2} %\\)<", sap);
        Assert.DoesNotContain("data-sap-usar", sap, StringComparison.Ordinal);
        // Sem o peso escrito à mão: contam a média e a moda (Diogo, 28/10/2026).
        Assert.DoesNotContain("name=\"anterior_peso\"", html, StringComparison.Ordinal);
        // Cada produção do select leva o seu peso; a mais antiga não tem ordens do SAP.
        Assert.Contains($"data-sap-opcao=\"{anteriorJobOnId}\" data-sap-media=\"181.67\" data-sap-moda=\"181\" data-sap-moda-dias=\"12\" data-sap-dias=\"18\"", sap, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-sap-opcao=\"{maisAntigaJobOnId}\"", sap, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProducaoAnterior_DatasDentroDaTabela_ComESemPeso(bool comPeso)
    {
        var (controloId, _, _, _) = await SeedAsync();
        if (comPeso) SeedSapFabrico("2026-04-06", "2026-04-24", 170m, 18);
        SeedUser("4413", "Peso Dates", "provider-peso-dates", "Dates689!");
        using var client = await LoginClientAsync("4413", "Dates689!");
        var sap = ZonaSap(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        var tabela = System.Text.RegularExpressions.Regex.Match(sap, @"<table\b[^>]*>([\s\S]*?)</table>");
        Assert.True(tabela.Success, "The previous production table must exist.");
        var datas = System.Text.RegularExpressions.Regex.Matches(tabela.Value, @"<tr\b[^>]*>[\s\S]*?</tr>")
            .Cast<System.Text.RegularExpressions.Match>()
            .SingleOrDefault(row => row.Value.Contains("data-producao-anterior-periodo", StringComparison.Ordinal));
        Assert.NotNull(datas);
        var texto = System.Text.RegularExpressions.Regex.Replace(datas.Value, "<[^>]+>", "");
        Assert.Contains("Datas", texto);
        Assert.Contains("Início 06/04/2026 · Fim 24/04/2026", texto);
        if (comPeso) Assert.Contains("18 dias", texto);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sap, "data-producao-anterior-periodo"));
    }

    [Theory]
    [InlineData(170, "+7.31 g (+4.30 %)")]
    [InlineData(190, "−12.69 g (−6.68 %)")]
    public async Task ProducaoAnterior_DiferencaIdentificaAtualMenosReferencia_ComSinalEPercentagem(int referencia, string esperado)
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-24", referencia, 18);
        SeedUser("4414", "Peso Difference", "provider-peso-difference", "Difference689!");
        using var client = await LoginClientAsync("4414", "Difference689!");
        var sap = ZonaSap(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        foreach (var (chave, nome) in new[] { ("media", "Média"), ("moda", "Moda") })
        {
            var celula = System.Text.RegularExpressions.Regex.Match(sap, $"<td\\b[^>]*data-sap-delta=\"{chave}\"[^>]*>([\\s\\S]*?)</td>");
            Assert.True(celula.Success, $"The {chave} difference cell must exist.");
            Assert.Equal($"Peso médio atual − {nome}: {esperado}", WebUtility.HtmlDecode(celula.Groups[1].Value).Trim());
        }
        var texto = System.Text.RegularExpressions.Regex.Replace(sap, "<[^>]+>", "");
        Assert.Contains("Diferença para o peso atual", texto);
    }

    // Diogo, 29/10/2026: «ao carregar Enter para registar leva-me para o
    // início da página» (DMO-673): a diferença da produção anterior depende do
    // peso médio e, fora de uma zona, mudava a cada leitura; a troca abria a
    // página inteira (voltava ao topo). As células ficam como estão
    // (data-dmo-fica) e o peso-live refaz-lhes o texto.
    [Fact]
    public async Task ProducaoAnterior_DiferencasFicamForaDaTroca_RegistarNaoVoltaAoTopo()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-18", 181m, 12);
        SeedUser("4411", "Peso Topo", "provider-folha-topo", "FolhaTopo10!");
        using var client = await LoginClientAsync("4411", "FolhaTopo10!");
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("<td data-dmo-fica data-sap-delta=\"media\">", html, StringComparison.Ordinal);
        Assert.Contains("<td data-dmo-fica data-sap-delta=\"moda\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4411", "Peso Operador", "provider-folha-sap2", "FolhaSap11!");
        using var client = await LoginClientAsync("4411", "FolhaSap11!");

        var sap = ZonaSap(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));

        Assert.Contains("<td colspan=\"3\">Ainda não há o peso desta produção.", sap, StringComparison.Ordinal);
        // «Ler a pasta» é um botão e diz «produção», nunca «SAP» (Diogo, 23/10/2026).
        // A pasta da produção está em Definições › Este computador (Diogo, 26/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraXJAtaKT9Zq5Rd7rwLJZDkP).
        Assert.Contains("<a class=\"dmo-button\" href=\"/Definicoes?secao=computador\">Ler a pasta da produção</a>", sap, StringComparison.Ordinal);
        Assert.DoesNotContain("Ler a pasta do SAP", sap, StringComparison.Ordinal);
        Assert.Contains("<tr data-sap-com hidden=\"hidden\">", sap, StringComparison.Ordinal);
    }
    /// <summary>
    /// O Resumo usa as duas (Diogo, 28/10/2026,
    /// cmsg_01K75FTs9bQnbhhH7qttpgraVoNvoQBUPf4cwx9jYiHvPJ: «começarmos a usar
    /// os dois»): a produção anterior guardada dá uma linha da média e outra
    /// da moda, cada uma com a diferença; o peso escrito à mão já não conta.
    /// </summary>
    [Fact]
    public async Task Resumo_ProducaoAnteriorComAMediaEAModa()
    {
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        SeedSapFabrico("2026-04-06", "2026-04-18", 181m, 12);
        SeedSapFabrico("2026-04-18", "2026-04-24", 183m, 6);
        int jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            jobOnId = db.Controlos.Single(c => c.Id == controloId).JobOnId;
            var anterior = db.Controlos.SingleOrDefault(c => c.JobOnId == anteriorJobOnId);
            if (anterior is null)
            {
                anterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anteriorJobOnId };
                db.Controlos.Add(anterior);
                db.SaveChanges();
            }
            var peso = db.Pesos.Single(p => p.Id == pesoId);
            peso.ProducaoAnteriorControloId = anterior.Id;
            peso.ProducaoAnteriorPesoGramas = 190m;
            db.SaveChanges();
        }
        SeedUser("4412", "Peso Operador", "provider-folha-sap3", "FolhaSap12!");
        using var client = await LoginClientAsync("4412", "FolhaSap12!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        var tabela = html[html.IndexOf("data-resumo-peso-tabela", StringComparison.Ordinal)..];
        tabela = tabela[..tabela.IndexOf("</table>", StringComparison.Ordinal)];
        Assert.Contains("<th scope=\"row\">Produção anterior · média<small> 202604</small></th>\n                      <td>181.67 g</td>", tabela, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"row\">Produção anterior · moda<small> 202604</small></th>\n                      <td>181.00 g</td>", tabela, StringComparison.Ordinal);
        Assert.DoesNotContain("190.00 g", tabela, StringComparison.Ordinal);
    }
}
