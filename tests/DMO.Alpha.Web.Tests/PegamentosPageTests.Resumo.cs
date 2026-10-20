using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O cartão dos Pegamentos no Resumo (18/10/2026): o tom é o da pior
/// medição e os totais contam todas as folhas da produção, também as de
/// antes de trocar a CM, a BQ ou o MF. Uma medição em alerta nunca fica
/// escondida por uma não avaliável nem por estar numa folha antiga.
/// </summary>
public partial class PegamentosPageTests
{
    /// <summary>
    /// O cartão dos Pegamentos no Resumo, pelo título «Pegamentos» (o
    /// cartão não tem id nem data-*): o tom (as classes do cartão, o único
    /// sinal da cor), a frase do estado, os totais e as linhas das folhas.
    /// Falha se o cartão ou a frase não existirem.
    /// </summary>
    private static (string[] Tom, string Estado, IReadOnlyDictionary<string, string> Totais, string[] Folhas) CartaoDosPegamentos(string resumo)
    {
        var cartao = Assert.Single(Regex.Matches(resumo, "<article\\b([^>]*)>([\\s\\S]*?)</article>"),
            m => Regex.IsMatch(m.Groups[2].Value, "<span>\\s*Pegamentos\\s*</span>"));
        var tom = Regex.Match(cartao.Groups[1].Value, "\\bclass=\"([^\"]*)\"").Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var conteudo = cartao.Groups[2].Value;
        var estado = Regex.Match(conteudo, "<strong\\b[^>]*>([\\s\\S]*?)</strong>");
        Assert.True(estado.Success, "o cartão dos Pegamentos não tem a frase do estado");
        var totais = Regex.Matches(conteudo, "<span>([^<]*)</span>\\s*<b>([^<]*)</b>")
            .ToDictionary(m => Texto(m.Groups[1].Value), m => Texto(m.Groups[2].Value));
        var folhas = Regex.Matches(conteudo, "<li\\b[^>]*>([\\s\\S]*?)</li>").Select(m => Texto(m.Groups[1].Value)).ToArray();
        return (tom, Texto(estado.Groups[1].Value), totais, folhas);

        // O texto que o operador lê: sem as marcas e com um espaço entre palavras.
        static string Texto(string html) =>
            Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ").Trim();
    }

    /// <summary>
    /// Uma medição em alerta e uma não avaliável (uma leitura antiga, sem
    /// nominal; hoje a folha já não a aceita): o cartão diz que há medições
    /// fora das tolerâncias e fica NOK, não «não avaliável».
    /// </summary>
    [Fact]
    public async Task ResumoCartao_AlertaENaoAvaliavel_FicaNok()
    {
        SeedUser("4807", "Pegamentos Operador", "provider-peg-resumo-1", "PegResumo1!");
        using var client = await LoginClientAsync("4807", "PegResumo1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (var fora = await PostMeasurementAsync(client, url, controloId, "CM", "71.00", "71.00", "70.00"))
        {
            Assert.Equal(url, fora.Headers.Location!.ToString());
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = await db.Pegamentos.SingleAsync(p => p.ControloId == controloId);
            db.PegamentosMeasurements.Add(new PegamentosMeasurement
            {
                PegamentosId = folha.Id, RowPosition = 2, Component = PegamentosComponent.Bq, CosturaMm = 59.90m
            });
            await db.SaveChangesAsync();
        }

        var (tom, estado, totais, _) = CartaoDosPegamentos(
            await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={await GetJobOnIdAsync()}"));

        Assert.Equal("2", totais["Medições"]);
        Assert.Equal("1", totais["Em alerta"]);
        Assert.Equal("1", totais["Não avaliáveis"]);
        Assert.Equal("Existem medições fora das tolerâncias", estado);
        Assert.Contains("nok", tom);
        Assert.DoesNotContain("warn", tom);
    }

    /// <summary>
    /// Depois de trocar a CM, a folha antiga tem uma medição em alerta e a
    /// atual só medições dentro: o cartão conta as duas folhas e fica NOK.
    /// </summary>
    [Fact]
    public async Task ResumoCartao_AlertaNaFolhaAntiga_FicaNok_ComAFolhaAtualDentro()
    {
        SeedUser("4808", "Pegamentos Operador", "provider-peg-resumo-2", "PegResumo2!");
        using var client = await LoginClientAsync("4808", "PegResumo2!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (var fora = await PostMeasurementAsync(client, url, controloId, "CM", "71.00", "71.00", "70.00"))
        {
            Assert.Equal(url, fora.Headers.Location!.ToString());
        }

        // A T4 (nominal 70.50): a leitura dentro começa a folha nova.
        var (jobOnId, _, _) = await SwapCmAsync(controloId);
        using (var dentro = await PostMeasurementAsync(client, url, controloId, "CM", "70.50", "70.50"))
        {
            Assert.Equal($"{url}&folha=nova", dentro.Headers.Location!.ToString());
        }

        var (tom, estado, totais, folhas) = CartaoDosPegamentos(
            await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));

        // O alerta está só na folha antiga.
        Assert.Equal(2, folhas.Length);
        Assert.StartsWith("Folha 1 ", folhas[0]);
        Assert.Contains("1 em alerta", folhas[0]);
        Assert.StartsWith("Folha 2 ", folhas[1]);
        Assert.DoesNotContain("em alerta", folhas[1]);

        Assert.Equal("Existem medições fora das tolerâncias", estado);
        Assert.Equal("2", totais["Medições"]);
        Assert.Equal("1", totais["Em alerta"]);
        Assert.Contains("nok", tom);
        Assert.DoesNotContain("ok", tom);
    }
}
