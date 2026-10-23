using static DMO.Alpha.Web.Tests.PosicaoNoHtml;
using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    // O % uso também no «Registar ferramenta real» (Diogo, 23/10/2026: «não
    // tem a percentagem de uso»): ao lado da Quantidade do lote, opcional;
    // grava como a primeira leitura (manual) e a ficha mostra-o; acima de 100
    // grava sem aviso nenhum; negativo ou ilegível não grava e avisa — nunca recusa.
    [Theory]
    [InlineData("0", 0, null)]
    [InlineData("100", 100, null)]
    [InlineData("101", 101, null)]
    [InlineData("-1", null, "O % uso «-1» não é um número inteiro positivo e não foi gravado")]
    [InlineData("", null, null)]
    public async Task Criar_PercentagemDeUso_GravaAPrimeiraLeitura_SoAvisos(string uso, int? gravado, string? aviso)
    {
        SeedUser("4993", "Ferramentas Uso", "provider-ferr-uso-criar", "FerrUsoC1!");
        using var client = await LoginClientAsync("4993", "FerrUsoC1!");

        var form = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{IndexPath}?criar=1"));
        Assert.Matches("<input id=\"ferr-new-uso\" name=\"uso_percentagem\"", form);
        Assert.True(Posicao(form, "id=\"ferr-new-qty\"") < Posicao(form, "id=\"ferr-new-uso\""));

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var lote = uso.Length == 0 ? "77" : uso.Replace("-", "9", StringComparison.Ordinal);
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"), ("reference", "6123"), ("lot", lote),
            ("uso_percentagem", uso), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=", location, StringComparison.Ordinal);
        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));

        string toolId;
        decimal[] leituras;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            toolId = db.Tools.Single(t => t.Reference == "6123" && t.Lot == lote).ToolId;
            leituras = db.FerramentaUtilizacoes.Where(u => u.ToolId == toolId).Select(u => u.Percentagem).ToArray();
        }
        if (gravado is int valor)
        {
            Assert.Equal([(decimal)valor], leituras);
            Assert.Matches($"<input id=\"ferr-edit-uso\" name=\"uso_percentagem\"[^>]*value=\"{valor}\"", ficha);
        }
        else
        {
            Assert.Empty(leituras);
        }
        if (aviso is null)
            Assert.DoesNotContain("id=\"ferrUsoAviso\"", ficha, StringComparison.Ordinal);
        else
            Assert.Contains(aviso, ficha, StringComparison.Ordinal);
    }

    // O % uso ilegível no Criar (o Guardião, 23/10/2026): «abc» e «12,5» (com
    // vírgula — o % uso é um número inteiro, não se lê como 12,5 nem como 125)
    // não gravam leitura nenhuma; a ferramenta grava na mesma e a ficha avisa
    // com o que se escreveu.
    [Theory]
    [InlineData("abc", "61")]
    [InlineData("12,5", "62")]
    public async Task Criar_PercentagemDeUsoIlegivel_NaoGrava_EAvisa(string uso, string lote)
    {
        SeedUser("4994", "Ferramentas Uso", "provider-ferr-uso-ilegivel", "FerrUsoI1!");
        using var client = await LoginClientAsync("4994", "FerrUsoI1!");

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"), ("reference", "6124"), ("lot", lote),
            ("uso_percentagem", uso), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=", location, StringComparison.Ordinal);
        var ficha = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var toolId = db.Tools.Single(t => t.Reference == "6124" && t.Lot == lote).ToolId;
            Assert.Empty(db.FerramentaUtilizacoes.Where(u => u.ToolId == toolId));
        }
        Assert.Contains("id=\"ferrUsoAviso\"", ficha, StringComparison.Ordinal);
        Assert.Contains($"O % uso «{uso}» não é um número inteiro positivo e não foi gravado", ficha, StringComparison.Ordinal);
    }
}
