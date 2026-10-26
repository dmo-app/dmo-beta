using System.Text.RegularExpressions;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Tests;

// «Quero que o histórico das boquilhas vista 6 máquinas diário; que ao
// carregar numa célula abra o registo associado completo, ou seja a vista da
// máquina + Job On» (Diogo, 21/10/2026). A aba Histórico abre no resumo
// diário das 6 máquinas; a célula abre o registo, e a produção do registo
// abre o Job On.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task Historico_AAbaAbreOResumoDiario_EACelulaAbreORegistoComOJobOn()
    {
        await SeedToolAsync("BQ-DIARIO", "RGT980", "7");
        var bq = await SeedProductionAsync("BQ-DIARIO", "ZQREF-DIA", "PN-550080", "B1");
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 2);
        SeedUser("4141", "Registo Diario", "provider-registo-diario", "RegistoDia1!");
        using var client = await LoginClientAsync("4141", "RegistoDia1!");

        var registo = await GetPageHtmlAsync(client, $"/Boquilhas?trace_id={trace.Id}");
        Assert.Contains("href=\"/Boquilhas?historico=1&amp;vista=matriz\"", registo);
        Assert.Matches("<a class=\"dmo-fact dmo-fact-link\" href=\"/JobOn\\?jobon_id=\\d+#lightFicha\" title=\"Abrir o Job On desta produção\"><span>Produção</span><strong>PN-550080</strong></a>", registo);

        var matriz = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=matriz");
        var tabela = Regex.Match(matriz, "<table class=\"dmo-table dmo-matrix\".*?</table>", RegexOptions.Singleline);
        Assert.True(tabela.Success, "A vista diária não tem a tabela das máquinas.");

        // As colunas são o dia e as 6 máquinas, por esta ordem.
        var cabecalho = Regex.Match(tabela.Value, "<thead>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value;
        var colunas = Regex.Matches(cabecalho, "<th>(.*?)</th>").Select(m => m.Groups[1].Value.Trim()).ToList();
        Assert.Equal(["Dia", "B1", "B2", "B3", "C1", "C2", "C3"], colunas);

        // A célula que abre o registo está na linha do dia do movimento e na
        // coluna B1, não apenas algures na página.
        var ocorreu = (await ReadMovementsAsync(trace.Id)).Single().OccurredAtUtc;
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ocorreu, DmoBusinessTime.Zone)).ToString("dd/MM");
        var corpo = Regex.Match(tabela.Value, "<tbody>(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value;
        var linhas = Regex.Matches(corpo, "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        var abrir = $"data-dmo-open=\"/Boquilhas?trace_id={trace.Id}\"";
        var linhasComACelula = linhas.Where(l => l.Contains(abrir)).ToList();
        var linha = Assert.Single(linhasComACelula);
        var celulas = Regex.Matches(linha, "<td>(.*?)</td>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(7, celulas.Count);
        Assert.Contains($"<strong>{dia}</strong>", celulas[0]);
        Assert.Equal(1, celulas.FindIndex(c => c.Contains(abrir)));
    }
}
