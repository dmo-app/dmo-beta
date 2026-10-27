using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// O peso do SAP na produção anterior (Diogo, 21/10/2026): pelas datas da
/// produção escolhida e a referência, as ordens do relatório de fabrico dão
/// a média pelos dias e a moda (o peso com mais dias), numa caixa onde se
/// escolhe a Média, a Moda ou as duas (cada uma com a diferença do peso de
/// hoje); o campo vem com a média, e cada produção leva a sua no select.
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

    // A caixa do peso do SAP, até à zona da diferença.
    private static string ZonaSap(string html)
    {
        var inicio = html.IndexOf("id=\"pesoAnteriorSap\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a página não tem #pesoAnteriorSap");
        return WebUtility.HtmlDecode(html[inicio..html.IndexOf("id=\"pesoAnteriorDelta\"", inicio, StringComparison.Ordinal)]);
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
        Assert.Contains("Peso da produção (18 dias)", texto, StringComparison.Ordinal);
        // Nenhum texto à vista diz «SAP» (o aviso sem dados está escondido; tiram-se as etiquetas e os atributos).
        var visivel = System.Text.RegularExpressions.Regex.Replace(sap[..sap.IndexOf("<span data-sap-sem", StringComparison.Ordinal)], "<[^>]+>", " ");
        Assert.DoesNotContain("SAP", visivel, StringComparison.Ordinal);
        Assert.Contains("Média 181.67 g", texto, StringComparison.Ordinal);
        Assert.Contains("Moda 181 g", texto, StringComparison.Ordinal);
        // A caixa mostra só a média, com a diferença do peso de hoje; a moda e a sua diferença abrem numa janela pequena.
        Assert.DoesNotContain("data-sap-ver", sap, StringComparison.Ordinal);
        Assert.Contains("data-dmo-dialog=\"pesoSapModa\"", sap, StringComparison.Ordinal);
        Assert.Matches("data-sap-delta=\"media\">Hoje [+−]\\d+\\.\\d{2} g \\([+−]\\d+\\.\\d{2} %\\)<", sap);
        Assert.Matches("data-sap-delta=\"moda\">Hoje [+−]\\d+\\.\\d{2} g \\([+−]\\d+\\.\\d{2} %\\)<", sap);
        Assert.DoesNotContain("data-sap-usar", sap, StringComparison.Ordinal);
        Assert.Contains("id=\"producaoAnteriorPeso\" name=\"anterior_peso\" type=\"text\" inputmode=\"decimal\" autocomplete=\"off\" placeholder=\"Peso usado (g)\" aria-label=\"Peso usado na produção (g)\" value=\"181.67\"", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        // Cada produção do select leva o seu peso; a mais antiga não tem ordens do SAP.
        Assert.Contains($"data-sap-opcao=\"{anteriorJobOnId}\" data-sap-media=\"181.67\" data-sap-moda=\"181\" data-sap-dias=\"18\"", sap, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-sap-opcao=\"{maisAntigaJobOnId}\"", sap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4411", "Peso Operador", "provider-folha-sap2", "FolhaSap11!");
        using var client = await LoginClientAsync("4411", "FolhaSap11!");

        var sap = ZonaSap(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));

        Assert.Contains("<span data-sap-sem>Ainda não há o peso desta produção.", sap, StringComparison.Ordinal);
        // «Ler a pasta» é um botão e diz «produção», nunca «SAP» (Diogo, 23/10/2026).
        // A pasta da produção está em Definições › Este computador (Diogo, 26/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraXJAtaKT9Zq5Rd7rwLJZDkP).
        Assert.Contains("<a class=\"dmo-button\" href=\"/Definicoes?secao=computador\">Ler a pasta da produção</a>", sap, StringComparison.Ordinal);
        Assert.DoesNotContain("Ler a pasta do SAP", sap, StringComparison.Ordinal);
        Assert.Contains("<div data-sap-com hidden=\"hidden\">", sap, StringComparison.Ordinal);
    }
}
