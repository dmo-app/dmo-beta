using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A utilização pelo SAP nas Definições das Ferramentas (Diogo,
/// 20–21/10/2026): a lista lida da pasta do SAP vai para o servidor, que
/// grava a utilização de cada ferramenta pelo nome técnico e o lote; a ficha
/// mostra-a. Os códigos a pedir ao SAP são os das ferramentas à espera da
/// utilização (o lembrete), aqui pedida no Opções da ficha.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    private string SeedToolComNomeTecnico(string nomeTecnico, string lote)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var toolId = "T" + Guid.NewGuid().ToString("N")[..10];
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = "5447T173", Lot = lote, TechnicalName = nomeTecnico });
        db.SaveChanges();
        return toolId;
    }

    private static string ListaDoSap(string codigo, string percentagem) =>
        "<html><body><table class=\"dd\"><tr><th></th><th>Equipamento</th><th>Expected life</th><th>Consumption [million units]</th><th>% Use</th></tr>"
        + $"<tr><td></td><td>{codigo}</td><td>43.200.000</td><td>38.962.451</td><td>{percentagem}</td></tr></table></body></html>";

    [Fact]
    public async Task AListaDoSap_GravaOUsoQueAFichaDaFerramentaMostra()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var toolId = SeedToolComNomeTecnico(nome, "11");
        var client = await LoginOperatorAsync("7101", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync(FerramentasPath)).Content.ReadAsStringAsync())!;

        var resposta = await client.PostAsync($"{FerramentasPath}?handler=ImportarUsoSap", HtmlFormHelpers.BuildFormPost(
            ("conteudo", ListaDoSap(nome + "11", "90,2")),
            ("ficheiro", "percentagem.HTML"),
            ("modificado", "1792562400000"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("gravado").GetBoolean());
        Assert.Equal("Utilização lida do SAP: 1 ferramenta atualizada.", json.RootElement.GetProperty("mensagem").GetString());
        var ficha = await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync();
        var utilizacao = ZonaUsoSap(ficha, "ferrUtilizacao");
        Assert.Contains("Utilização", utilizacao, StringComparison.Ordinal);
        Assert.Contains("90.2 %", utilizacao, StringComparison.Ordinal);
        var definicoes = await (await client.GetAsync(FerramentasPath)).Content.ReadAsStringAsync();
        Assert.Contains("(ficheiro percentagem.HTML)", definicoes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UmFicheiroQueNaoEALista_DizQueNadaFoiGravado()
    {
        var client = await LoginOperatorAsync("7102", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync(FerramentasPath)).Content.ReadAsStringAsync())!;

        var resposta = await client.PostAsync($"{FerramentasPath}?handler=ImportarUsoSap", HtmlFormHelpers.BuildFormPost(
            ("conteudo", "notas soltas"),
            ("ficheiro", "notas.txt"),
            ("__RequestVerificationToken", token)));

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("gravado").GetBoolean());
        Assert.Contains("«notas.txt» não tem a lista do SAP", json.RootElement.GetProperty("mensagem").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OsCodigosAPedirAoSap_SaoOsPedidosNaFicha()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var outro = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var toolId = SeedToolComNomeTecnico(nome, "3");
        SeedToolComNomeTecnico(outro, "4");
        var client = await LoginOperatorAsync("7103", DmoCapabilities.Ferramentas);
        var fichaPath = $"/Ferramentas/Ficha?tool_id={toolId}";

        // O «Pedir a utilização ao SAP» do Opções da ficha, pelo formulário que a página mostra.
        var ficha = await (await client.GetAsync(fichaPath)).Content.ReadAsStringAsync();
        Assert.Contains("Pedir a utilização ao SAP", ZonaUsoSap(ficha, "ferrOpcoes"), StringComparison.Ordinal);
        var (acao, campos) = EnvioDoFormulario.Enviar(ficha, [], idDoBotao: "ferr-pedir-utilizacao-botao");
        var pedir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, pedir.StatusCode);
        var depois = await (await client.GetAsync(pedir.Headers.Location!.ToString())).Content.ReadAsStringAsync();
        Assert.Contains("Utilização pedida ao SAP: vem na próxima leitura.", ZonaUsoSap(depois, "ferrUtilizacaoPedida"), StringComparison.Ordinal);
        Assert.Contains("Utilização por atualizar desde", ZonaUsoSap(depois, "ferrUtilizacaoLembrete"), StringComparison.Ordinal);
        Assert.DoesNotContain("Pedir a utilização ao SAP", ZonaUsoSap(depois, "ferrOpcoes"), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(await (await client.GetAsync($"{FerramentasPath}?handler=UsoSap")).Content.ReadAsStringAsync());
        var codigos = json.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains(nome + "03", codigos);
        Assert.DoesNotContain(outro + "04", codigos);
    }

    // O elemento com este id, até ao seu fecho (a ordem dos atributos não conta).
    private static string ZonaUsoSap(string html, string id)
    {
        var abertura = Regex.Match(html, $"<(\\w+)\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
        Assert.True(abertura.Success, $"a página não tem #{id}");
        var tag = abertura.Groups[1].Value;
        var nivel = 1;
        for (var m = Regex.Match(html[(abertura.Index + abertura.Length)..], $"<{tag}\\b|</{tag}>"); m.Success; m = m.NextMatch())
        {
            nivel += m.Value == $"</{tag}>" ? -1 : 1;
            if (nivel == 0)
                return html.Substring(abertura.Index, abertura.Length + m.Index + m.Length);
        }

        throw new InvalidOperationException($"#{id} não fecha");
    }

    [Fact]
    public async Task SemOModuloFerramentas_ALeituraNaoGravaNada()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var toolId = SeedToolComNomeTecnico(nome, "11");
        var client = await LoginOperatorAsync("7104", DmoCapabilities.JobOnCreate);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/login")).Content.ReadAsStringAsync());

        var resposta = await client.PostAsync($"{FerramentasPath}?handler=ImportarUsoSap", HtmlFormHelpers.BuildFormPost(
            ("conteudo", ListaDoSap(nome + "11", "90,2")),
            ("__RequestVerificationToken", token ?? "")));

        Assert.NotEqual(HttpStatusCode.OK, resposta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<DmoDbContext>().FerramentaUtilizacoes.Any(u => u.ToolId == toolId));
    }
}
