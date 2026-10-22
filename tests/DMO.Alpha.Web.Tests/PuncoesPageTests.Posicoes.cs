using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AS PUNÇÕES POR POSIÇÃO DA LINHA (Diogo, 22/10/2026): pela ordem de
/// registo, A1, B1, C1, D1, A2 … D12; as que passam da linha são «Reserva»;
/// o mesmo número com outro lote aparece a negrito com o lote, sem
/// bloquear. A folha das punções abre-se do Resumo e do registo, e o PDF do
/// Controlo traz o das punções a acompanhar.
/// </summary>
public partial class PuncoesPageTests
{
    /// <summary>51 punções como na tabela aprovada: o 44 na B2 (lote 12) e na C7 (lote 13).</summary>
    private async Task<IReadOnlyList<(string Numero, string Lote)>> SeedTabelaAprovadaAsync(int controloId)
    {
        var lista = new List<(string, string)>();
        var proximo = 1;
        for (var i = 0; i < 51; i++)
        {
            if (i is 5 or 26)
            {
                lista.Add(("44", i == 5 ? "12" : "13"));
                continue;
            }

            if (proximo == 44) proximo++;
            lista.Add((proximo++.ToString(), "12"));
        }

        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PuncoesHandler>();
        var alvo = PuncoesTarget.ForControlo(controloId);
        foreach (var (numero, lote) in lista)
        {
            Assert.Equal(PuncoesResult.Success, await handler.AddAsync(alvo, numero, "1075", "12", "4330", default, lote));
        }

        return lista;
    }

    private static string Celula(string html, string posicao) =>
        Regex.Match(html, $"<td data-pu-posicao=\"{posicao}\"[^>]*>(.*?)</td>", RegexOptions.Singleline) is { Success: true } m
            ? Regex.Replace(m.Groups[1].Value, "<[^>]+>", " ").Trim()
            : throw new Xunit.Sdk.XunitException($"Sem a posição {posicao}.");

    private static string Bloco(string html, string marca, string tag) =>
        Regex.Match(html, $"<{tag}[^>]*{marca}[^>]*>.*?</{tag}>", RegexOptions.Singleline) is { Success: true } m
            ? m.Value
            : throw new Xunit.Sdk.XunitException($"Sem {marca}.");

    /// <summary>A zona da folha (#puFolha) da página: o que troca depois de gravar.</summary>
    private static string Zona(string html)
    {
        var inicio = html.IndexOf("<div id=\"puFolha\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Sem a zona puFolha.");
        var fim = html.IndexOf("</main>", inicio, StringComparison.Ordinal);
        return html[inicio..fim];
    }

    private static string Texto(string html) => Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), "\\s+", " ").Trim();

    [Fact]
    public async Task Posicoes_A1aD12_Reserva_RepetidosComLote_ETotais_NaZonaDaFolha()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4330", "Operador Posições", "provider-puncoes-pos", "PuncoesPos1!");
        using var client = await LoginClientAsync("4330", "PuncoesPos1!");
        var lista = await SeedTabelaAprovadaAsync(controloId);

        var zona = Zona(WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}")));

        Assert.Equal("1", Celula(zona, "A1"));
        Assert.Equal("2", Celula(zona, "B1"));
        Assert.Equal("3", Celula(zona, "C1"));
        Assert.Equal("4", Celula(zona, "D1"));
        Assert.Equal("5", Celula(zona, "A2"));
        Assert.Equal("44 Lote 12", Regex.Replace(Celula(zona, "B2"), "\\s+", " "));
        Assert.Equal("44 Lote 13", Regex.Replace(Celula(zona, "C7"), "\\s+", " "));
        Assert.Equal(lista[47].Numero, Celula(zona, "D12"));
        Assert.DoesNotContain("data-pu-posicao=\"A13\"", zona);
        Assert.Contains("pu-repetido", Bloco(zona, "data-pu-posicao=\"B2\"", "td"));
        Assert.DoesNotContain("pu-repetido", Bloco(zona, "data-pu-posicao=\"A1\"", "td"));

        Assert.Equal("Reserva " + string.Join(" ", lista.Skip(48).Select(p => p.Numero)), Texto(Bloco(zona, "data-pu-reserva", "table")));
        Assert.Equal("Punções verificados 51 Linha C2 48 Reserva 3", Texto(Bloco(zona, "data-pu-totais", "dl")));
        Assert.Equal("Punções repetidos Posição Punção Lote B2 44 12 C7 44 13", Texto(Bloco(zona, "data-pu-repetidos", "table")));
        // O «PDF» do cartão envia o formulário do PDF (em memória).
        Assert.Contains("form=\"pu-pdf-form\" id=\"pu-pdf\">PDF</button>", zona);
    }

    [Fact]
    public async Task Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4330", "Operador Posições", "provider-puncoes-pos", "PuncoesPos1!");
        using var client = await DefinicoesPin.DesbloquearAsync(_factory.Services, await LoginClientAsync("4330", "PuncoesPos1!"));
        var lista = await SeedTabelaAprovadaAsync(controloId);

        try
        {
            var token = await GetVerificationTokenAsync(client, "/Definicoes");
            var invalido = await client.PostAsync("/Controlo/Definicoes?handler=SavePuncoesSeccoes", HtmlFormHelpers.BuildFormPost(
                ("maquina", "C2"), ("seccoes", "0"), ("__RequestVerificationToken", token)));
            Assert.Contains("seccoes=Invalido", invalido.Headers.Location!.ToString());
            var gravado = await client.PostAsync("/Controlo/Definicoes?handler=SavePuncoesSeccoes", HtmlFormHelpers.BuildFormPost(
                ("maquina", "C2"), ("seccoes", "2"), ("__RequestVerificationToken", token)));
            Assert.Contains("seccoes=Gravado", gravado.Headers.Location!.ToString());

            var definicoes = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, "/Definicoes"));
            Assert.Equal("C2 8 Guardar", Texto(Bloco(definicoes, "data-dmo-linha=\"C2\"", "tr")));

            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
            Assert.Equal(lista[7].Numero, Celula(html, "D2"));
            Assert.DoesNotContain("data-pu-posicao=\"A3\"", html);
            Assert.Equal("Punções verificados 51 Linha C2 8 Reserva 43", Texto(Bloco(html, "data-pu-totais", "dl")));
            Assert.Equal("Punções repetidos Posição Punção Lote B2 44 12 Reserva 44 13", Texto(Bloco(html, "data-pu-repetidos", "table")));
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey.StartsWith(PuncoesLinhaDefinicoes.Prefixo)));
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task FolhaDasPuncoes_LigadaDoResumoEDoRegisto_SoComRegisto_EOPdfDoControloTrazODasPuncoes()
    {
        var (jobOnId, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4330", "Operador Posições", "provider-puncoes-pos", "PuncoesPos1!");
        using var client = await LoginClientAsync("4330", "PuncoesPos1!");

        var semRegisto = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.DoesNotContain("data-resumo-pu-ligacoes", semRegisto);
        Assert.DoesNotContain("data-dmo-pdf-acompanha=\"puncoes\"", semRegisto);

        await SeedTabelaAprovadaAsync(controloId);
        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        var ligacoes = Bloco(resumo, "data-resumo-pu-ligacoes", "nav");
        Assert.Contains($"href=\"/Controlo/Puncoes?controlo_id={controloId}\">Registo das punções", ligacoes);
        Assert.Contains($"href=\"/Controlo/Puncoes?controlo_id={controloId}&vista=folha\">Folha das punções", ligacoes);
        Assert.Contains("data-dmo-pdf-acompanha=\"puncoes\"", resumo);

        var registo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains($"href=\"/Controlo/Puncoes?controlo_id={controloId}&vista=folha\" data-pu-ligacoes>Folha das punções</a>", registo);

        var folha = Zona(WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&vista=folha")));
        Assert.Contains($"href=\"/Controlo/Puncoes?controlo_id={controloId}\">← Registo das punções</a>", Bloco(folha, "data-pu-ligacoes", "nav"));
        Assert.Contains("data-pu-grelha", folha);
        Assert.DoesNotContain("pu-add-form", folha);

        // O PDF do Controlo pede o das punções a seguir (o mesmo nome que o
        // PDF do Controlo liga); o PDF só do Resumo não.
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(resumo)!;
        var completo = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal("application/pdf", completo.Content.Headers.ContentType?.MediaType);
        var acompanha = completo.Headers.GetValues(ControloDocumentPaths.PdfCompanionHeader).Single();
        Assert.Equal($"/Controlo/Puncoes?handler=GeneratePdf&controlo_id={controloId}", acompanha);
        var soResumo = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("apenas_resumo", "true"), ("__RequestVerificationToken", token)));
        Assert.False(soResumo.Headers.Contains(ControloDocumentPaths.PdfCompanionHeader));

        var puncoes = await client.PostAsync("/Controlo/Puncoes?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal("application/pdf", puncoes.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ControloDocumentPaths.GetPuncoesFileName("1075C142", "202604"),
            puncoes.Content.Headers.ContentDisposition?.FileNameStar ?? puncoes.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }
}
