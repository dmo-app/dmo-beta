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
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync())!;

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
    }

    [Fact]
    public async Task UmFicheiroQueNaoEALista_DizQueNadaFoiGravado()
    {
        var client = await LoginOperatorAsync("7102", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync())!;

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

        // O «Atualizar informação do SAP» do Opções da ficha (Diogo, 28/10/2026), pelo formulário que a página mostra.
        var ficha = await (await client.GetAsync(fichaPath)).Content.ReadAsStringAsync();
        Assert.Contains("Atualizar informação do SAP", ZonaUsoSap(ficha, "ferrOpcoes"), StringComparison.Ordinal);
        var (acao, campos) = EnvioDoFormulario.Enviar(ficha, [], idDoBotao: "ferr-pedir-utilizacao-botao");
        var pedir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, pedir.StatusCode);
        var depois = await (await client.GetAsync(pedir.Headers.Location!.ToString())).Content.ReadAsStringAsync();
        Assert.Contains("Utilização pedida à produção: vem na próxima leitura.", ZonaUsoSap(depois, "ferrUtilizacaoPedida"), StringComparison.Ordinal);
        var lembrete = ZonaUsoSap(depois, "ferrUtilizacaoLembrete");
        Assert.Contains("Utilização por atualizar desde", lembrete, StringComparison.Ordinal);
        Assert.Contains("vem na próxima leitura da produção.", lembrete, StringComparison.Ordinal);
        Assert.Contains(">Ler a pasta da produção</a>", lembrete, StringComparison.Ordinal);
        Assert.DoesNotContain("SAP<", lembrete, StringComparison.Ordinal);
        Assert.DoesNotContain("Atualizar informação do SAP", ZonaUsoSap(depois, "ferrOpcoes"), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(await (await client.GetAsync($"{FerramentasPath}?handler=UsoSap")).Content.ReadAsStringAsync());
        var codigos = json.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains(nome + "03", codigos);
        Assert.DoesNotContain(outro + "04", codigos);
    }

    [Fact]
    public async Task OScriptDoSap_EnviaDireto_ComAChave_ESemElaNao()
    {
        var nome = "MEN" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPNNX";
        var toolId = SeedToolComNomeTecnico(nome, "4");
        string chave;
        using (var scope = _factory.Services.CreateScope())
        {
            chave = await scope.ServiceProvider.GetRequiredService<ISapDefinicoes>().ChaveDeEnvioAsync();
        }

        var anonimo = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var lista = "search,equipment,percent_use,last_production,batch_description,status,error\r\n"
            + $"{nome}04,{nome}04,77.5,,,OK,\r\n";
        var semChave = await anonimo.PostAsync("/sap/enviar?ficheiro=dmo-uso-sap.csv", new ByteArrayContent(System.Text.Encoding.Latin1.GetBytes(lista)));
        Assert.Equal(HttpStatusCode.Forbidden, semChave.StatusCode);

        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/sap/enviar?ficheiro=dmo-uso-sap.csv") { Content = new ByteArrayContent(System.Text.Encoding.Latin1.GetBytes(lista)) };
        pedido.Headers.Add("X-DMO-SAP", chave);
        var comChave = await anonimo.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, comChave.StatusCode);
        using var json = JsonDocument.Parse(await comChave.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("gravado").GetBoolean(), json.RootElement.GetProperty("mensagem").GetString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(77.5m, db.FerramentaUtilizacoes.Single(u => u.ToolId == toolId).Percentagem);
        }

        using var codigos = new HttpRequestMessage(HttpMethod.Get, "/sap/codigos");
        codigos.Headers.Add("X-DMO-SAP", chave);
        Assert.Contains(nome + "04\r\n", await (await anonimo.SendAsync(codigos)).Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AtualizarPorReferencia_DaOsCodigosDessaReferenciaEDoTipo()
    {
        var nome = "MRF" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPNNX";
        var referencia = "R" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "T" + Guid.NewGuid().ToString("N")[..10], Type = ToolType.Cm, Reference = referencia, Lot = "2", TechnicalName = nome });
            db.Tools.Add(new Tool { ToolId = "T" + Guid.NewGuid().ToString("N")[..10], Type = ToolType.Bq, Reference = referencia, Lot = "7", TechnicalName = nome });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7190", DmoCapabilities.Ferramentas);
        async Task<List<string?>> Codigos(string tipo)
        {
            using var json = JsonDocument.Parse(await (await client.GetAsync($"{FerramentasPath}?handler=CodigosSap&referencia={referencia.ToLowerInvariant()}&tipo={tipo}")).Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()).ToList();
        }

        Assert.Equal([nome + "*07", nome + "02"], await Codigos(""));
        Assert.Equal([nome + "*07"], await Codigos("bq"));
        Assert.Empty(await Codigos("mf"));
    }

    [Fact]
    public async Task PedirTodas_DaOsCodigosDeTodasAsFerramentasComNomeTecnico()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        SeedToolComNomeTecnico(nome, "5");
        var client = await LoginOperatorAsync("7189", DmoCapabilities.Ferramentas);

        using var so = JsonDocument.Parse(await (await client.GetAsync($"{FerramentasPath}?handler=UsoSap")).Content.ReadAsStringAsync());
        using var todas = JsonDocument.Parse(await (await client.GetAsync($"{FerramentasPath}?handler=UsoSap&todas=true")).Content.ReadAsStringAsync());
        Assert.DoesNotContain(nome + "05", so.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()));
        Assert.Contains(nome + "05", todas.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()));
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
