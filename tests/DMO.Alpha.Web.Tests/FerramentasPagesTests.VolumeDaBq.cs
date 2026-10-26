using System.Net;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// «Peso mudar para volume» (Diogo, 26/10/2026): na BQ o bloco da marisa
/// chama-se Volume; na CM continua Peso (processo e peso nominal).
/// </summary>
public partial class FerramentasPagesTests
{
    [Fact]
    public async Task Criar_TituloDoBloco_VolumeNaBqEPesoNaCm()
    {
        SeedUser("4485", "Ferramentas Volume", "provider-ferr-volume", "FerrVolume1!");
        using var client = await LoginClientAsync("4485", "FerrVolume1!");

        var bq = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=BQ"));
        Assert.Contains("<span data-ferr-tech-tipo=\"BQ\">Volume</span>", bq, StringComparison.Ordinal);
        Assert.Contains("<span data-ferr-tech-tipo=\"CM\" hidden=\"hidden\">Peso</span>", bq, StringComparison.Ordinal);

        var cm = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=CM"));
        Assert.Contains("<span data-ferr-tech-tipo=\"CM\">Peso</span>", cm, StringComparison.Ordinal);
        Assert.Contains("<span data-ferr-tech-tipo=\"BQ\" hidden=\"hidden\">Volume</span>", cm, StringComparison.Ordinal);
    }
}
