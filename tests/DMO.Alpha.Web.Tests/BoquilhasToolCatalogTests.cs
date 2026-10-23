using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task ToolCatalog_CriarBoquilha_AbreORegistoDasFerramentasComBqEAMaquina_EVoltaComAFicha()
    {
        // Diogo, 18/10/2026 (pergunta 6 do deep sweep): «não vamos inventar
        // mais páginas para ferramentas, 1 página e funciona com todas». O
        // formulário curto «+ Criar boquilha / lote» saiu; «Criar boquilha»
        // abre o registo das Ferramentas com o tipo BQ e a máquina do filtro
        // já escolhidos, e volta aqui com a ficha da boquilha registada.
        SeedUser("4920", "Catálogo BQ", "catalog-create", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4920", "CatalogPass1!");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        db.Machines.AddRange(b1, c3);
        await db.SaveChangesAsync();

        const string regresso = "%2FBoquilhas%3Fficheiros%3D1%26ferramenta%3D%7Btool_id%7D%26saved%3Dcreated%23bq-tool-sheet";
        var criar = $"/Ferramentas?criar=1&type=BQ&machine_ids={b1.Id}&return={regresso}";
        // O botão é uma zona: mudar a máquina do filtro troca-o com a lista.
        // Só com a máquina não há lista (deep sweep 2, 18/10/2026, F-16), mas
        // o botão já leva a máquina.
        var filtrado = WebUtility.HtmlDecode(await client.GetStringAsync($"/Boquilhas?ficheiros=1&machine_id={b1.Id}"));
        Assert.Contains("id=\"bq-tool-results\" hidden", filtrado);
        Assert.Contains($"<div id=\"bqCriarBoquilha\" class=\"dmo-card-topo-acoes\" data-dmo-regiao><a class=\"btn primary\" href=\"{criar}\">Criar boquilha</a></div>", filtrado);
        var todas = WebUtility.HtmlDecode(await client.GetStringAsync("/Boquilhas?ficheiros=1"));
        Assert.Contains($"href=\"/Ferramentas?criar=1&type=BQ&return={regresso}\">Criar boquilha</a>", todas);
        Assert.DoesNotContain("bq-tool-create", todas);
        Assert.DoesNotContain("handler=CreateTool", todas);
        Assert.DoesNotContain("Draft.", todas);

        // O registo abre com o tipo BQ fixo e só a B1 escolhida.
        var registo = WebUtility.HtmlDecode(await client.GetStringAsync(criar));
        Assert.Contains("<option value=\"BQ\" selected=\"selected\">BQ — Boquilha</option>", registo);
        Assert.Contains("data-ferr-tipo-fixo=\"BQ\"", registo);
        Assert.Matches($"name=\"machine_ids\" value=\"{b1.Id}\"\\s+checked", registo);
        Assert.DoesNotMatch($"name=\"machine_ids\" value=\"{c3.Id}\"\\s+checked", registo);

        // Registada, volta ao catálogo com a ficha aberta e a mensagem.
        var token = await GetVerificationTokenAsync(client, criar);
        var response = await client.PostAsync("/Ferramentas?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "BQ"), ("reference", "T173"), ("lot", "24/33"), ("bq_quantity", "120"), ("quantity_manual", "1"),
            ("machine_ids", b1.Id.ToString()),
            ("return", "/Boquilhas?ficheiros=1&ferramenta={tool_id}&saved=created#bq-tool-sheet"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        db.ChangeTracker.Clear();
        var tool = await db.Tools.SingleAsync(t => t.Reference == "T173");
        Assert.Equal(ToolType.Bq, tool.Type);
        Assert.Equal(120, tool.BqAccountedQuantity);
        Assert.Equal(b1.Id, (await db.ToolMachineLinks.SingleAsync(l => l.ToolId == tool.ToolId)).MachineId);
        Assert.Equal($"/Boquilhas?ficheiros=1&ferramenta={tool.ToolId}&saved=created#bq-tool-sheet", response.Headers.Location!.OriginalString);
        var html = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Boquilha guardada. A ficha está disponível abaixo.", html);
        Assert.Contains("Ficha da boquilha — T173 · Lote 24/33", html);
        Assert.Contains("Máquinas compatíveis: B1", html);
        Assert.Contains("Editar ficha / duplicar lote", html);
        Assert.Contains("handler=RegistarSemJobOn", html);
        ExportCatalogEvidence("sheet", html);
    }

    [Fact]
    public async Task ToolCatalog_SearchAndPaginationPreserveSelectionContext_AndExcludeOtherToolTypes()
    {
        SeedUser("4922", "Pesquisa BQ", "catalog-search", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4922", "CatalogPass1!");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        for (var i = 1; i <= 25; i++)
        {
            var tool = new Tool { ToolId = $"catalog-{i:00}", Type = ToolType.Bq, Reference = "T173", Lot = $"24/{i:00}", BqAccountedQuantity = 120 };
            db.Tools.Add(tool);
            db.ToolMachineLinks.Add(new ToolMachineLink { Tool = tool, Machine = machine });
        }
        db.Tools.Add(new Tool { ToolId = "catalog-cm", Type = ToolType.Cm, Reference = "T173", Lot = "CM" });
        await db.SaveChangesAsync();
        var url = $"/Boquilhas?ficheiros=1&q=t17&machine_id={machine.Id}&pagina=2";
        var html = await client.GetStringAsync(url);
        Assert.Equal(10, Regex.Matches(html, "name=\"bq_tool\"").Count);
        Assert.Contains("25 encontrada(s)", html);
        Assert.Contains("data-dmo-troca", html);
        Assert.Contains("id=\"bq-tool-results\"", html);
        Assert.Contains("data-dmo-regiao", html);
        Assert.Contains("<nav data-dmo-parcial class=\"bq-tool-pagination\"", html);
        Assert.Contains(">2 de 3</span>", html);
        Assert.DoesNotContain("catalog-cm", html);
        // «Criar boquilha» leva a máquina do filtro; sem ela, vai sem máquina.
        Assert.Contains($"machine_ids={machine.Id}&amp;return=", html);
        var next = WebUtility.HtmlDecode(Regex.Match(html, "href=\"([^\"]*)\" aria-label=\"Página seguinte\"").Groups[1].Value);
        Assert.Contains("q=t17", next);
        Assert.Contains($"machine_id={machine.Id}", next);
        Assert.Contains("pagina=3", next);
        var nextHtml = await client.GetStringAsync(next);
        Assert.Equal(5, Regex.Matches(nextHtml, "name=\"bq_tool\"").Count);
        ExportCatalogEvidence("catalog-next", nextHtml);
        var sheet = WebUtility.HtmlDecode(Regex.Match(html, "data-dmo-open=\"([^\"]*ferramenta=catalog-[^\"]*)\"").Groups[1].Value);
        var sheetHtml = await client.GetStringAsync(sheet);
        Assert.Contains("Ficha da boquilha — T173", sheetHtml);
        Assert.Contains(">2 de 3</span>", sheetHtml);
        var byMachine = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=b1&por_pagina=20");
        Assert.Equal(20, Regex.Matches(byMachine, "name=\"bq_tool\"").Count);
        var byLot = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=24%2F25");
        Assert.Single(Regex.Matches(byLot, "name=\"bq_tool\""));
        Assert.Contains("catalog-25", byLot);
        ExportCatalogEvidence("catalog", html);
        var allMachines = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=t17&machine_id=&por_pagina=10");
        Assert.Contains("href=\"/Ferramentas?criar=1&amp;type=BQ&amp;return=", allMachines);
        Assert.DoesNotContain("machine_ids=", allMachines);
        ExportCatalogEvidence("catalog-all", allMachines);
    }

    // O «Por página» é da página, junto da paginação, nunca nos filtros (Diogo,
    // 23/10/2026): muda mantendo a pesquisa e a máquina e volta à página 1;
    // os filtros guardam o tamanho escolhido; sem pesquisa não aparece.
    [Fact]
    public async Task ToolCatalog_PorPagina_NaPaginacao_NuncaNosFiltros()
    {
        SeedUser("4923", "Por página BQ", "catalog-por-pagina", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4923", "CatalogPass1!");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "C2" };
        db.Machines.Add(machine);
        for (var i = 1; i <= 12; i++)
        {
            var tool = new Tool { ToolId = $"porpag-{i:00}", Type = ToolType.Bq, Reference = "T909", Lot = $"9/{i:00}", BqAccountedQuantity = 10 };
            db.Tools.Add(tool);
            db.ToolMachineLinks.Add(new ToolMachineLink { Tool = tool, Machine = machine });
        }
        await db.SaveChangesAsync();

        var vazia = await client.GetStringAsync("/Boquilhas?ficheiros=1");
        Assert.DoesNotContain("id=\"bq-tool-size\"", vazia);

        var html = await client.GetStringAsync($"/Boquilhas?ficheiros=1&q=t909&machine_id={machine.Id}&pagina=2&por_pagina=10");
        var filtros = Regex.Match(html, "<form[^>]*class=\"bq-tool-search\"[\\s\\S]*?</form>").Value;
        Assert.DoesNotContain("<select id=\"bq-tool-size\"", filtros);
        Assert.Contains("<input type=\"hidden\" name=\"por_pagina\" value=\"10\" />", filtros);
        var paginacao = Regex.Match(html, "<nav[^>]*class=\"bq-tool-pagination\"[\\s\\S]*?</nav>").Value;
        Assert.Contains("<select id=\"bq-tool-size\" name=\"por_pagina\"", paginacao);
        Assert.Contains("name=\"q\" value=\"t909\"", paginacao);
        Assert.Contains($"name=\"machine_id\" value=\"{machine.Id}\"", paginacao);
        Assert.DoesNotContain("name=\"pagina\"", paginacao);

        var vinte = await client.GetStringAsync($"/Boquilhas?ficheiros=1&q=t909&machine_id={machine.Id}&por_pagina=20");
        Assert.Equal(12, Regex.Matches(vinte, "name=\"bq_tool\"").Count);
        Assert.Contains(">1 de 1</span>", vinte);
    }

    [Fact]
    public async Task ToolCatalog_SoAMaquinaNaoMostraALista_OTextoMostra_EAMaquinaSoReduz()
    {
        // Deep sweep 2, 18/10/2026, F-16 (resposta R17 do especialista): a
        // lista só aparece depois de escrever a referência ou o lote, como nas
        // Ferramentas (Diogo, 17/10/2026: «não deve existir lista até
        // procurar, evitar peso nos carregamentos»; 18/10/2026: «escolher BQ
        // carregava as BQ todas»). Escolher só a máquina carregava todas as
        // BQ dela; agora a máquina só reduz a lista.
        var filtros = new List<ToolCandidateFilter?>();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IToolCandidateQuery>(sp =>
                new CountingToolCandidateQuery(new ToolCandidateQuery(sp.GetRequiredService<DmoDbContext>()), filtros))));
        SeedUser("4924", "Máquina BQ", "catalog-maquina", "CatalogPass1!", withFerramentas: true);
        using var client = await LoginClientAsync("4924", "CatalogPass1!", factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        db.Machines.AddRange(b1, c3);
        var naB1 = new Tool { ToolId = "BQ-MAQ-B1", Type = ToolType.Bq, Reference = "T173", Lot = "24/01", BqAccountedQuantity = 120 };
        var naC3 = new Tool { ToolId = "BQ-MAQ-C3", Type = ToolType.Bq, Reference = "T173", Lot = "24/02", BqAccountedQuantity = 120 };
        db.Tools.AddRange(naB1, naC3);
        db.ToolMachineLinks.AddRange(
            new ToolMachineLink { Tool = naB1, Machine = b1 },
            new ToolMachineLink { Tool = naC3, Machine = c3 });
        await db.SaveChangesAsync();
        filtros.Clear();

        // Só a máquina: sem lista e sem consultar as boquilhas. A máquina
        // fica escolhida e «Criar boquilha» leva-a.
        var soMaquinaHtml = await client.GetStringAsync($"/Boquilhas?ficheiros=1&machine_id={b1.Id}");
        var soMaquina = WebUtility.HtmlDecode(soMaquinaHtml);
        Assert.Empty(filtros);
        Assert.Contains("id=\"bq-tool-results\" hidden", soMaquina);
        Assert.DoesNotContain("name=\"bq_tool\"", soMaquina);
        Assert.DoesNotContain("encontrada(s)", soMaquina);
        Assert.Contains($"<option value=\"{b1.Id}\" selected=\"selected\">B1</option>", soMaquina);
        Assert.Contains($"/Ferramentas?criar=1&type=BQ&machine_ids={b1.Id}&return=", soMaquina);
        // A ajuda diz quando aparece a lista.
        Assert.Contains("A lista aparece quando escreve a referência ou o lote e muda enquanto escreve. A máquina só reduz a lista.", soMaquina);
        // O browser apaga a pesquisa no catálogo e vê a lista sair no lugar.
        ExportCatalogEvidence("catalog-maquina", soMaquinaHtml);
        // Só espaços na pesquisa também não é procurar.
        var espacos = await client.GetStringAsync($"/Boquilhas?ficheiros=1&q=%20%20&machine_id={b1.Id}&por_pagina=20");
        Assert.Empty(filtros);
        Assert.Contains("id=\"bq-tool-results\" hidden", espacos);

        // Só o texto: a lista, com as boquilhas de todas as máquinas.
        var soTexto = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=t17");
        var filtro = Assert.Single(filtros);
        Assert.Equal(new ToolCandidateFilter(Type: ToolType.Bq, Search: "t17"), filtro);
        Assert.Equal(2, Regex.Matches(soTexto, "name=\"bq_tool\"").Count);
        Assert.Contains("value=\"BQ-MAQ-B1\"", soTexto);
        Assert.Contains("value=\"BQ-MAQ-C3\"", soTexto);
        Assert.Contains("2 encontrada(s)", soTexto);

        // O texto e a máquina: a lista reduzida às boquilhas dessa máquina.
        var comMaquina = await client.GetStringAsync($"/Boquilhas?ficheiros=1&q=t17&machine_id={b1.Id}");
        Assert.Equal(2, filtros.Count);
        Assert.Equal(new ToolCandidateFilter(Type: ToolType.Bq, MachineId: b1.Id, Search: "t17"), filtros[1]);
        Assert.Single(Regex.Matches(comMaquina, "name=\"bq_tool\""));
        Assert.Contains("value=\"BQ-MAQ-B1\"", comMaquina);
        // (A janela «Registar sem Job On» lista todas as BQ; a lista é que reduz.)
        Assert.DoesNotContain("value=\"BQ-MAQ-C3\" aria-label", comMaquina);
        Assert.Contains("1 encontrada(s)", comMaquina);

        // O lote também mostra a lista, e a máquina continua só a reduzir.
        var porLote = await client.GetStringAsync("/Boquilhas?ficheiros=1&q=24%2F02");
        Assert.Contains("value=\"BQ-MAQ-C3\"", porLote);
        Assert.DoesNotContain("value=\"BQ-MAQ-B1\" aria-label", porLote);
        var loteNoutraMaquina = await client.GetStringAsync($"/Boquilhas?ficheiros=1&q=24%2F02&machine_id={b1.Id}");
        Assert.DoesNotContain("name=\"bq_tool\"", loteNoutraMaquina);
        Assert.Contains("0 encontrada(s)", loteNoutraMaquina);
        Assert.Contains("Nenhuma boquilha encontrada com os filtros atuais.", loteNoutraMaquina);
        Assert.Equal(4, filtros.Count);
    }

    /// <summary>Guarda os filtros de cada consulta das boquilhas, por cima da query real.</summary>
    private sealed class CountingToolCandidateQuery(IToolCandidateQuery inner, List<ToolCandidateFilter?> filtros) : IToolCandidateQuery
    {
        public Task<IReadOnlyList<ToolCandidate>> FindCandidatesAsync(ToolCandidateFilter? filter, CancellationToken cancellationToken = default)
        {
            lock (filtros) filtros.Add(filter);
            return inner.FindCandidatesAsync(filter, cancellationToken);
        }

        public Task<ToolCandidatePage> FindPageAsync(ToolCandidateFilter? filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            lock (filtros) filtros.Add(filter);
            return inner.FindPageAsync(filter, pageNumber, pageSize, cancellationToken);
        }
    }

    private static void ExportCatalogEvidence(string name, string html)
    {
        var directory = Environment.GetEnvironmentVariable("DMO_UI_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".html"), html);
    }
}
