using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Erros do Controlo (Diogo, 27/10/2026): a identificação da produção sem
/// «Linha» nem «Processo da CM»; a «Reserva» como quarto total (as punções que
/// passam das posições da linha, a mesma conta da folha); e, na folha, o
/// número igual e no mesmo sítio em todas as células, com ou sem lote.
/// </summary>
public partial class PuncoesPageTests
{
    [Fact]
    public async Task Identificacao_SemLinhaNemProcesso_ETotais_ComReserva()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4335", "Operador Reserva", "provider-puncoes-reserva", "PuncoesRes1!");
        using var client = await LoginClientAsync("4335", "PuncoesRes1!");

        var vazia = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains("<span>Produção</span>", vazia);
        Assert.Contains("<span>Referência total</span>", vazia);
        Assert.Contains("<span>CM</span>", vazia);
        Assert.Contains("data-pu-puncao><span>Punção</span>", vazia);
        Assert.DoesNotContain("<span>Linha</span>", vazia);
        Assert.DoesNotContain("Processo da CM", vazia);
        Assert.Contains("<span>Reserva</span><strong>0</strong>", vazia);

        await SeedTabelaAprovadaAsync(controloId);
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        // 51 punções na linha C2 (12 secções × 4 posições = 48): 3 na reserva, como na folha.
        Assert.Contains("<span>Total de punções</span><strong>51</strong>", html);
        Assert.Contains("data-pu-reserva-total><span>Reserva</span><strong>3</strong>", html);
        var folha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&vista=folha"));
        Assert.Contains("<div><dt>Reserva</dt><dd>3</dd></div>", folha);
    }

    [Fact]
    public async Task Folha_TodasAsCelulasComONumeroEALinhaDoLote()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4336", "Operador Grelha", "provider-puncoes-grelha", "PuncoesGre1!");
        using var client = await LoginClientAsync("4336", "PuncoesGre1!");
        await SeedTabelaAprovadaAsync(controloId);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&vista=folha"));
        var celulas = Regex.Matches(html, "<td data-pu-posicao=\"[A-D]\\d+\"[^>]*>(.*?)</td>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value).Where(c => c.Length > 0).ToArray();
        Assert.Equal(48, celulas.Length);
        Assert.All(celulas, c => Assert.Matches("^<span class=\"pu-grelha-numero\">[^<]+</span><small class=\"pu-grelha-lote\">(Lote [^<]+)?</small>$", c));
        Assert.Contains("<span class=\"pu-grelha-numero\">44</span><small class=\"pu-grelha-lote\">Lote 12</small>", html);

        // O mesmo tamanho e peso para todos os números: sem o negrito à parte dos repetidos.
        var css = await client.GetStringAsync("/css/dmo-puncoes.css");
        Assert.DoesNotContain(".pu-repetido .pu-grelha-numero", css);
        Assert.Matches(@"\.pu-grelha-lote\{[^}]*display:block;height:13px", css);
    }
}
