using System.Net;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // Ronda de erros (26/10/2026, J11): com «12,345» o aviso dizia «12,3», porque a
    // caixa do % uso cortava o que se escrevia aos 4 caracteres. O aviso tem de
    // mostrar o que se escreveu.
    [Fact]
    public async Task Criar_CaixaDoUso_DeixaEscreverOValorTodo()
    {
        SeedUser("4995", "Ferramentas Uso", "provider-ferr-uso-escrito", "FerrUsoE1!");
        using var client = await LoginClientAsync("4995", "FerrUsoE1!");

        var form = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{IndexPath}?criar=1"));

        Assert.Matches("<input id=\"ferr-new-uso\" name=\"uso_percentagem\"[^>]*maxlength=\"8\"", form);
    }
}
