using DMO.Alpha.Web.Services;

namespace DMO.Alpha.Web.Tests;

// Sweep de 19/10/2026, 5.7: uma só regra de caminho local para o login, o
// registo nas Ferramentas e a ficha da ferramenta.
public sealed class LocalUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/JobOn?novo=1&cm_tool_id={tool_id}#lightSheet")]
    [InlineData("/Boquilhas?ficheiros=1&ferramenta={tool_id}&saved=created#bq-tool-sheet")]
    [InlineData("/Controlo/Peso?cm=%20T1")]
    public void CaminhosDaApp_Passam(string url) => Assert.True(LocalUrl.IsSafe(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("JobOn")]
    [InlineData("~/JobOn")]
    [InlineData("https://outro.site")]
    [InlineData("//outro.site")]
    [InlineData("/\\outro.site")]
    [InlineData("/JobOn\\x")]
    [InlineData("/\t/outro.site")]
    [InlineData("/\r\n/outro.site")]
    [InlineData("/ /outro.site")]
    [InlineData("/JobOn\u0000")]
    public void OutrosSitiosOuCaracteresQueOBrowserTira_SaoRecusados(string? url) => Assert.False(LocalUrl.IsSafe(url));

    // Tudo o que a regra aceita, o LocalRedirect também aceita: nunca um erro
    // 500 depois de gravar.
    [Theory]
    [InlineData("/")]
    [InlineData("/JobOn?novo=1&cm_tool_id=T0123456789#lightSheet")]
    [InlineData("/Controlo/Peso?cm=%20T1")]
    public void OQueARegraAceita_OLocalRedirectTambemAceita(string url)
    {
        var helper = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(new Microsoft.AspNetCore.Mvc.ActionContext(
            new Microsoft.AspNetCore.Http.DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));

        Assert.True(LocalUrl.IsSafe(url));
        Assert.True(helper.IsLocalUrl(url));
    }
}
