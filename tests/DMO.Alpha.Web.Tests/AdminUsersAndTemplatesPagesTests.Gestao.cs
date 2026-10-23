using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

public sealed partial class AdminUsersAndTemplatesPagesTests
{
    // «Gestão» (Diogo, 24/10/2026): Ferramentas e Armazém numa só secção; as chaves não mudam.
    [Fact]
    public async Task AdminTemplates_FerramentasEArmazem_NaSeccaoGestao()
    {
        SeedConfiguredAdmin("tpl-gestao@example.test", "tpl-gestao-provider", "TplGestao123!");
        using var admin = await LoginAdminAsync(_factory, "tpl-gestao@example.test", "TplGestao123!");
        var html = System.Net.WebUtility.HtmlDecode(await (await admin.GetAsync("/Admin/Templates")).Content.ReadAsStringAsync());
        var cabecas = Regex.Matches(html, "<p class=\"dmt-module-head\"[^>]*>([^<]+)</p>").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("Gestão", cabecas);
        Assert.DoesNotContain("Ferramentas", cabecas);
        Assert.DoesNotContain("Armazém", cabecas);
        var gestao = html[html.IndexOf(">Gestão</p>", StringComparison.Ordinal)..];
        gestao = gestao[..gestao.IndexOf("dmt-module-head", StringComparison.Ordinal)];
        Assert.Contains($"value=\"{DmoCapabilities.Ferramentas}\"", gestao);
        Assert.Contains("Ferramentas · gestão", gestao);
        Assert.Contains($"value=\"{DmoCapabilities.ArmazemGestao}\"", gestao);
        Assert.Contains("Armazém · gestão", gestao);
    }
}
