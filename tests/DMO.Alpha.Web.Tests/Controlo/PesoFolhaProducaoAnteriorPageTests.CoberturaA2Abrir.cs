using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// DMO-402 (beta.88): na Comparação do Peso, a produção anterior tem o botão
/// «Abrir o Peso desta produção»: abre a folha de Peso dessa produção, gravada
/// nesse Job On.
/// </summary>
public partial class PesoFolhaProducaoAnteriorPageTests
{
    [Fact]
    public async Task ProducaoAnterior_AbrirLevaAoPesoDaProducaoEscolhidaNoSelect()
    {
        var (controloId, _, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        SeedUser("4421", "Peso Operador", "provider-folha-abrir", "Folha421!");
        using var client = await LoginClientAsync("4421", "Folha421!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        // O botão existe, com o nome acessível, e é o do formulário da produção anterior.
        var botao = Regex.Match(html, "<button\\b[^>]*data-peso-anterior-abrir[^>]*>");
        Assert.True(botao.Success, "a folha não tem o botão «Abrir» da produção anterior");
        Assert.Contains("aria-label=\"Abrir o Peso desta produção\"", botao.Value);
        var abrir = WebUtility.HtmlDecode(Regex.Match(botao.Value, "formaction=\"([^\"]+)\"").Groups[1].Value);
        Assert.Contains("handler=AbrirJobOn", abrir);

        // Com a produção proposta no select, abre o Peso dela.
        var (_, camposProposta) = EnvioDoFormulario.Mudar(html, "producaoAnterior", anteriorJobOnId.ToString());
        Assert.DoesNotContain(camposProposta, c => c.Nome == "jobon_id");
        using var respostaProposta = await client.PostAsync(abrir, EnvioDoFormulario.Corpo(camposProposta));
        Assert.Equal(HttpStatusCode.Redirect, respostaProposta.StatusCode);

        // Escolhendo outra no select, abre o Peso dessa, não o da proposta.
        var (_, camposEscolha) = EnvioDoFormulario.Mudar(html, "producaoAnterior", maisAntigaJobOnId.ToString());
        using var respostaEscolha = await client.PostAsync(abrir, EnvioDoFormulario.Corpo(camposEscolha));
        Assert.Equal(HttpStatusCode.Redirect, respostaEscolha.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var controloDaAnterior = db.Controlos.Single(c => c.JobOnId == anteriorJobOnId).Id;
        var controloDaMaisAntiga = db.Controlos.Single(c => c.JobOnId == maisAntigaJobOnId).Id;
        Assert.NotEqual(controloId, controloDaAnterior);
        Assert.NotEqual(controloDaAnterior, controloDaMaisAntiga);
        Assert.Equal($"/Controlo/Peso?controlo_id={controloDaAnterior}", respostaProposta.Headers.Location!.ToString());
        Assert.Equal($"/Controlo/Peso?controlo_id={controloDaMaisAntiga}", respostaEscolha.Headers.Location!.ToString());
    }
}
