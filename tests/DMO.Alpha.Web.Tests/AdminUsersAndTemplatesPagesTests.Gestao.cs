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

    // «Deixar em branco ou mais leve o que não está selecionado e mais escuro
    // com font branco o que está e remover as boxes» (Diogo, 26/10/2026): a
    // caixa continua no formulário, escondida; o cartão é a etiqueta.
    [Fact]
    public async Task AdminTemplates_SemCaixas_EscolhidoEscuroComLetraBranca()
    {
        SeedConfiguredAdmin("tpl-caixas@example.test", "tpl-caixas-provider", "TplCaixas123!");
        using var admin = await LoginAdminAsync(_factory, "tpl-caixas@example.test", "TplCaixas123!");
        var html = await (await admin.GetAsync("/Admin/Templates")).Content.ReadAsStringAsync();
        Assert.Matches($"<input type=\"checkbox\" id=\"dmt-new-{Regex.Escape(DmoCapabilities.JobOnCreate)}\" name=\"capabilities\" value=\"{Regex.Escape(DmoCapabilities.JobOnCreate)}\"", html);
        Assert.Contains($"<label for=\"dmt-new-{DmoCapabilities.JobOnCreate}\">", html);
        Assert.Contains(".dmt-check input{position:absolute;width:1px;height:1px;margin:0;opacity:0;pointer-events:none}", html);
        Assert.Contains(".dmt-check:has(input:checked){background:var(--dmo-palette-slate);border-color:var(--dmo-palette-slate)}", html);
        Assert.Contains(".dmt-check:has(input:checked) label,.dmt-check:has(input:checked) label span{color:#fff}", html);
        Assert.Contains("[data-theme=\"dark\"] .dmt-check:has(input:checked){background:var(--dmo-palette-ocean)", html);
    }
}
