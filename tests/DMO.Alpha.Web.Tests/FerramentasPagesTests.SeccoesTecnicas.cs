using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DMO.Alpha.Web.Pages.Ferramentas;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // Diogo, 20/10/2026: no registo, «Tampão» passa a título com Diâmetro,
    // Raio e Calote por baixo; cada secção da informação técnica tem a dica
    // de onde a app usa os dados; a Quantidade do lote vem antes da Ref. MF
    // (raramente usada) e não diz «opcional».
    [Fact]
    public async Task Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf()
    {
        SeedUser("4993", "Ferramentas Secções", "provider-ferr-seccoes", "FerrSeccoes1!");
        using var client = await LoginClientAsync("4993", "FerrSeccoes1!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=CM"));

        Assert.True(Posicao(html, "id=\"ferr-new-qty\"") < Posicao(html, "id=\"ferr-new-mf\""));
        var quantidade = System.Text.RegularExpressions.Regex.Match(html, "<input id=\"ferr-new-qty\"[^>]*>").Value;
        Assert.StartsWith("<input id=\"ferr-new-qty\" name=\"bq_quantity\"", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder", quantidade, StringComparison.Ordinal);
        Assert.DoesNotContain("Tampão D (mm)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Gargalo (mm)", html, StringComparison.Ordinal);
        var tampao = html[Posicao(html, ">Tampão<")..];
        Assert.True(Posicao(tampao, ">Diâmetro (mm)</label>") < Posicao(tampao, ">Raio (mm)</label>"));
        Assert.True(Posicao(tampao, ">Raio (mm)</label>") < Posicao(tampao, ">Calote (mm)</label>"));
        foreach (var dica in new[] { AjudaTecnica.Peso, AjudaTecnica.Gargalo, AjudaTecnica.Tampao })
            Assert.Contains($"<span class=\"dmo-help-tip\" role=\"tooltip\">{dica}</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"dmo-field ferr-w-processo\"", html, StringComparison.Ordinal);
    }

    // «nao pode ser optional pq da erro se nao tiver» (Diogo, 20/10/2026):
    // a BQ grava sem quantidade (só se bloqueia o impossível), mas a ficha
    // avisa que a Fabricação das Boquilhas fica por calcular, com o link
    // para o Editar.
    [Fact]
    public async Task Ficha_BqSemQuantidade_AvisaQueAFabricacaoNaoECalculada()
    {
        SeedUser("4994", "Ferramentas Sem Qtd", "provider-ferr-sem-qtd", "FerrSemQtd1!");
        using var client = await LoginClientAsync("4994", "FerrSemQtd1!");
        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1&type=BQ");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "BQ"), ("reference", "Z991"), ("lot", "4"), ("bq_quantity", ""),
            ("quantity_manual", "1"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, response.Headers.Location!.ToString()));
        Assert.Contains("Sem quantidade do lote: nas Boquilhas, a Fabricação desta BQ não é calculada.", ficha, StringComparison.Ordinal);
        Assert.Contains("href=\"#ferrEditar\" id=\"ferr-sem-quantidade-editar\">Acrescentar a quantidade</a>", ficha, StringComparison.Ordinal);
    }

    // «tem de existir uma pagina de crir ferramenras» (Diogo, 20/10/2026): o
    // Criar e o Editar da ficha são o mesmo formulário: os mesmos campos, pela
    // mesma ordem e com os mesmos nomes; o Editar também grava o Nome técnico.
    [Fact]
    public async Task CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico()
    {
        SeedUser("4995", "Ferramentas Um Form", "provider-ferr-um-form", "FerrUmForm1!");
        using var client = await LoginClientAsync("4995", "FerrUmForm1!");
        var toolId = await SeedRealToolAsync();

        var criar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, IndexPath + "?criar=1&type=CM"));
        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}"));
        string[] Rotulos(string html, string prefixo) => System.Text.RegularExpressions.Regex
            .Matches(html, $"<label for=\"{prefixo}-[a-z-]+\">([^<]+)<")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(
            ["Tipo", "Referência", "Lote", "Quantidade do lote", "Ref. MF associada", "Nome técnico (SAP)"],
            Rotulos(criar, "ferr-new").Take(6));
        Assert.Equal(Rotulos(criar, "ferr-new").Take(6), Rotulos(ficha, "ferr-edit"));
        // A ficha da CM só tem os campos da CM (a Marisa é da BQ).
        Assert.Equal(Rotulos(criar, "ferr-new").Skip(6).Where(r => r != "Marisa (cm³)"), Rotulos(ficha, "ferr-tech"));

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "3"),
            ("technical_name", "m107544mfnnv"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("facts_refusal", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("M107544MFNNV", (await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).TechnicalName);
    }
}
