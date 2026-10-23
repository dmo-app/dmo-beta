namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // O regresso à folha do Job On (duplo clique numa ferramenta da
    // /JobOn/Folha): com «voltar» local do Job On, a ficha mostra «Voltar ao
    // Job On» para essa folha; com outro destino, nada muda.
    [Fact]
    public async Task Ficha_ComVoltarDoJobOn_MostraOVoltarParaAFolhaExata()
    {
        SeedUser("4481", "Ferramentas Owner", "provider-ferr-voltar-1", "FerrVolta1!");
        var toolId = await SeedRealToolAsync();
        using var client = await LoginClientAsync("4481", "FerrVolta1!");

        var html = await GetPageHtmlAsync(client, $"/Ferramentas/Ficha?tool_id={toolId}&voltar={Uri.EscapeDataString("/JobOn/Folha?jobon_id=7&dia=23")}");

        Assert.Contains("<a class=\"dmo-button\" href=\"/JobOn/Folha?jobon_id=7&amp;dia=23\" data-ferr-voltar-jobon>Voltar ao Job On</a>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://outro.site/JobOn/Folha")]
    [InlineData("//outro.site/JobOn/Folha")]
    [InlineData("/Ferramentas")]
    [InlineData("/JobOn/\\outro")]
    public async Task Ficha_SemVoltarValido_NaoMostraOVoltarAoJobOn(string voltar)
    {
        SeedUser("4482", "Ferramentas Owner", "provider-ferr-voltar-2", "FerrVolta2!");
        var toolId = await SeedRealToolAsync();
        using var client = await LoginClientAsync("4482", "FerrVolta2!");

        var html = await GetPageHtmlAsync(client, $"/Ferramentas/Ficha?tool_id={toolId}&voltar={Uri.EscapeDataString(voltar)}");

        Assert.DoesNotContain("data-ferr-voltar-jobon", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Voltar ao Job On", html, StringComparison.Ordinal);
        Assert.Contains("Voltar à lista", html, StringComparison.Ordinal);
    }
}
