namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // O VOLTAR (Diogo, 26/10/2026: «o voltar quando entramos numa página a
    // partir de outra não volta à página anterior»): sem «voltar» do Job On,
    // o botão da ficha é o [data-dmo-voltar] (dmo-live.js troca-o pela página
    // de onde se veio, com os filtros); escrito pelo servidor, é a lista.
    [Fact]
    public async Task Ficha_OVoltar_VoltaAPaginaDeOndeSeVeio_SemOrigemALista()
    {
        SeedUser("4483", "Ferramentas Owner", "provider-ferr-voltar-3", "FerrVolta3!");
        var toolId = await SeedRealToolAsync();
        using var client = await LoginClientAsync("4483", "FerrVolta3!");

        var html = await GetPageHtmlAsync(client, $"/Ferramentas/Ficha?tool_id={toolId}");

        Assert.Contains($"<a class=\"dmo-button\" id=\"ferrVoltar\" href=\"/Ferramentas\" data-dmo-voltar=\"{toolId}\" data-dmo-voltar-ignorar=\"/Ferramentas/Ficha /Ferramentas/Historico\" data-dmo-voltar-texto=\"Voltar\">Voltar à lista</a>", html, StringComparison.Ordinal);
        Assert.Contains("dmo-live.js", html, StringComparison.Ordinal);
    }
}
