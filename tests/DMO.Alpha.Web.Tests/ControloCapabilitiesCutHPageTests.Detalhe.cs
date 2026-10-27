using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Deep sweep de 18/10/2026, regra 66: o Detalhe da Revisão mostrava
    // «129.1462888016308987015073762» e «1» onde a Revisão mostra «129.15» e
    // «Leitura 1». As leituras do Peso aparecem iguais nas duas páginas, e as
    // dos Pegamentos com as casas da folha dos Pegamentos.
    [Fact]
    public async Task RevisaoDetalhe_MostraAsLeiturasDoPesoComoARevisao()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = new Peso
            {
                CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.50m,
                CmToolReferenceFrozen = "5447", CmToolLotFrozen = "3", ControloId = controloId
            };
            peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "12", WaterWeightGrams = 128.9m });
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
        }

        SeedChefe("4791", "provider-chefe-revisao-detalhe", "ChefeRevisao1!");
        using var client = await LoginClientAsync("4791", "ChefeRevisao1!");

        static string[] Linha(string html)
        {
            var tabela = Regex.Match(html, "data-revisao-peso-comparacao>[\\s\\S]*?</table>").Value;
            var linha = Regex.Match(tabela, "<tbody>\\s*<tr>([\\s\\S]*?)</tr>").Groups[1].Value;
            return Regex.Matches(linha, "<td>([^<]*)</td>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToArray();
        }

        // As leituras passaram da Revisão para a Consulta do Peso (19/10/2026).
        var detalhe = Linha(await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso"));
        // Numa só tabela com a comparação (20/10/2026): a CM, a massa de
        // água, o volume e o peso.
        Assert.True(detalhe.Length >= 4);
        Assert.Equal("128.90", detalhe[1]);
        Assert.Matches("^\\d+\\.\\d{2}$", detalhe[2]);
        Assert.Matches("^\\d+\\.\\d{2}$", detalhe[3]);
    }

    // Os Pegamentos pela página desenhada (antes lia-se o .cshtml): duas
    // casas em cada coluna, como na folha dos Pegamentos.
    [Fact]
    public async Task RevisaoDetalhe_OsPegamentosUsamOFormatoDaFolha()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            // Uma casa decimal, como o operador as escreve; a segunda leitura
            // fica fora do corredor (70.00 ± 0.20). A primeira tem ovalização
            // 0.10, abaixo da tolerância (a ovalização igual a ela é alerta).
            var folha = new Pegamentos
            {
                ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m
            };
            folha.Measurements.Add(new PegamentosMeasurement
            {
                RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 70.1m, ContraCosturaMm = 70.0m, NominalGargaloMm = 70.00m
            });
            folha.Measurements.Add(new PegamentosMeasurement
            {
                RowPosition = 2, Component = PegamentosComponent.Cm, CosturaMm = 70.3m, ContraCosturaMm = 70.2m, NominalGargaloMm = 70.00m
            });
            db.Pegamentos.Add(folha);
            await db.SaveChangesAsync();
        }

        SeedChefe("4788", "provider-chefe-revisao-pegamentos", "ChefeRevisao2!");
        using var client = await LoginClientAsync("4788", "ChefeRevisao2!");

        static string Texto(string html) =>
            Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ").Trim();

        // As leituras pelos títulos das colunas que o chefe lê.
        static (string, string, string, string, string)[] Leituras(string html)
        {
            var tabela = Assert.Single(Regex.Matches(html, "<table\\b[\\s\\S]*?</table>"),
                t => Texto(t.Value).Contains("Contra costura (90°) mm", StringComparison.Ordinal)).Value;
            var titulos = Regex.Matches(tabela, "<th\\b[^>]*>([\\s\\S]*?)</th>").Select(m => Texto(m.Groups[1].Value)).ToList();
            var corpo = Regex.Match(tabela, "<tbody\\b[^>]*>([\\s\\S]*?)</tbody>").Groups[1].Value;
            return Regex.Matches(corpo, "<tr\\b[^>]*>([\\s\\S]*?)</tr>").Select(linha =>
            {
                var celulas = Regex.Matches(linha.Groups[1].Value, "<td\\b[^>]*>([\\s\\S]*?)</td>").Select(c => Texto(c.Groups[1].Value)).ToList();
                string Coluna(string titulo) => celulas[titulos.IndexOf(titulo)];
                return (Coluna("N.º"), Coluna("Costura (0°) mm"), Coluna("Contra costura (90°) mm"), Coluna("Média mm"), Coluna("Estado"));
            }).ToArray();
        }

        // O div com este id, até ao seu fecho (a ordem dos atributos não conta).
        static string Zona(string html, string id)
        {
            var abertura = Regex.Match(html, $"<div\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
            Assert.True(abertura.Success, $"a página não tem #{id}");
            var nivel = 1;
            for (var m = Regex.Match(html[(abertura.Index + abertura.Length)..], "<div\\b|</div>"); m.Success; m = m.NextMatch())
            {
                nivel += m.Value == "</div>" ? -1 : 1;
                if (nivel == 0)
                    return html.Substring(abertura.Index, abertura.Length + m.Index + m.Length);
            }
            throw new InvalidOperationException($"#{id} não fecha");
        }

        var detalhe = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");
        Assert.Equal(
            new[] { ("1", "70.10", "70.00", "70.05", "dentro"), ("2", "70.30", "70.20", "70.25", "fora") },
            Leituras(detalhe));

        // A Revisão só resume (19/10/2026): as mesmas medições e os mesmos alertas.
        var revisao = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        var naRevisao = Regex.Match(Texto(Zona(revisao, "revisaoPartePegamentos")), "(\\d+) medi(?:ção|ções) · (\\d+) alertas?");
        Assert.True(naRevisao.Success, "a Revisão não resume os Pegamentos");
        Assert.Equal(("2", "1"), (naRevisao.Groups[1].Value, naRevisao.Groups[2].Value));
        // O Detalhe sem contadores no título (20/10/2026); a tabela tem as
        // mesmas medições e o mesmo alerta.
        Assert.DoesNotMatch("\\d+ medi(ção|ções) ·", detalhe);
        Assert.Single(Regex.Matches(detalhe, "<span class=\"peg-state-bad\">fora</span>"));
    }
}
