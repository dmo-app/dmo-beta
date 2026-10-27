using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 27/10/2026: «não quero botões em rows por todo o lado». Na
    // Revisão (Resumo do controlo), a referência do PU e do CS deixou de ter
    // «Guardar» na linha: grava no próprio valor (data-dmo-valor), ao sair do
    // campo. O pedido JSON recebe { ok, valor }; a CM continua a não se
    // escrever.
    [Fact]
    public async Task ResumoDoControlo_ReferenciaDoPu_GravaNoValor_SemBotaoNaLinha()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedUserWithTemplate("4808", "Chefe Valor", "provider-resumo-valor", "ResumoValor1!",
            "tpl-teste-resumo-valor", "Registo e aprovação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var client = await LoginClientAsync("4808", "ResumoValor1!");
        var pagina = $"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={jobOnId}";

        var antes = await GetPageHtmlAsync(client, pagina);
        foreach (var area in new[] { "PU", "CS" })
        {
            var form = Regex.Match(antes, $"data-revisao-area=\"{area}\">[\\s\\S]*?(<form[^>]*resumo-area-identificacao[\\s\\S]*?</form>)").Groups[1].Value;
            Assert.NotEmpty(form);
            Assert.DoesNotContain("<button", form);
            Assert.Contains("<input class=\"dmo-valor\" data-dmo-valor name=\"identificacao\"", form);
        }

        var token = await GetVerificationTokenAsync(client, pagina);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Resumo?handler=AreaState")
        {
            Content = HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("area", "PU"), ("voltar", "resumo-controlo"),
                ("identificacao", "  PU-901 Lote 7  "), ("__RequestVerificationToken", token))
        };
        pedido.Headers.Accept.ParseAdd("application/json");
        using var resposta = await client.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using (var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("PU-901 Lote 7", json.RootElement.GetProperty("valor").GetString());
        }
        Assert.Contains("name=\"identificacao\" value=\"PU-901 Lote 7\"", await GetPageHtmlAsync(client, pagina));

        // A CM tem ferramenta: o pedido JSON diz que não gravou.
        using var cm = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Resumo?handler=AreaState")
        {
            Content = HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("voltar", "resumo-controlo"),
                ("identificacao", "CM-nao-pode"), ("__RequestVerificationToken", token))
        };
        cm.Headers.Accept.ParseAdd("application/json");
        using var recusa = await client.SendAsync(cm);
        using (var json = JsonDocument.Parse(await recusa.Content.ReadAsStringAsync()))
        {
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        }
        Assert.DoesNotContain("CM-nao-pode", await GetPageHtmlAsync(client, pagina));
    }
}
