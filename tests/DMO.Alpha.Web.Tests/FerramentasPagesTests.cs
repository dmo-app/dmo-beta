using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova o slice vertical nativo do corte A (Ferramentas — a RAIZ da
/// cascata): a lista mostra FATOS HUMANOS («CM · 5447 · lote 3 · B1» — o
/// tool_id vive no URL/hidden, nunca como facto exibido) com filtros que
/// apenas reduzem candidatos; a criação contextual (?return=…{tool_id}…,
/// validação LOCAL-ONLY) devolve o tool_id mintado ao fluxo chamador; a
/// ficha por tool_id compõe factos + máquinas + valores técnicos
/// (incluindo a geometria TP registada tal como vem da folha genesis — a
/// app NUNCA recalcula derivados); editar factos é full-replace no MESMO
/// tool_id; duplicar exige lote novo com fonte intacta; eliminar é a
/// confirmação EXPLÍCITA do aviso com o impacto corrente (produções
/// afetadas + contagens) e a transação canónica reatribui à DUMMY os
/// registos de consumo, preservando os factos congelados; o anónimo é
/// desafiado ANTES de qualquer leitura/escrita; ZERO anti-padrões de
/// mockup (rotas demo, hrefs demo, JS demo, sessionStorage — precedente
/// Boquilhas/Index) e SEM campo Estado.
/// </summary>
public partial class FerramentasPagesTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string IndexPath = "/Ferramentas";
    private const string FichaPath = "/Ferramentas/Ficha";

    private readonly CustomWebApplicationFactory _factory;

    public FerramentasPagesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    /// <summary>
    /// A base InMemory é PARTILHADA por todas as classes de teste do host
    /// (nome fixo); cada teste limpa o estado de domínio que tocou ao
    /// sair, para que as classes seguintes encontrem o mesmo estado limpo
    /// (a ordem é dependente→principal das FKs Restrict).
    /// </summary>
    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PegamentosMeasurements.RemoveRange(db.PegamentosMeasurements);
        db.Pegamentos.RemoveRange(db.Pegamentos);
        db.PegamentosTolerances.RemoveRange(db.PegamentosTolerances);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.BqRepairTraces.RemoveRange(db.BqRepairTraces);
        db.JobOns.RemoveRange(db.JobOns);
        db.Machines.RemoveRange(db.Machines);
        // As leituras da % de uso (o Histórico semeia-as): sem elas a sobrar entre testes.
        db.FerramentaUtilizacoes.RemoveRange(db.FerramentaUtilizacoes);
        db.Tools.RemoveRange(db.Tools);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password, bool withOperationalAccess = true)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId
        };
        db.Users.Add(user);
        db.SaveChanges();

        if (!withOperationalAccess)
        {
            return;
        }

        // O acesso operacional comum do Beta (jobon.create): os POSTs desta
        // página exigem-no (TEMPLATES.md — sem template, sem operacional).
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = new[] { DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate, DMO.Alpha.Core.Admin.DmoCapabilities.Ferramentas }
            };
            db.Templates.Add(template);
            db.SaveChanges();
        }

        db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment
        {
            UserId = user.Id,
            TemplateId = template.Id
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password, WebApplicationFactory<Program>? factory = null)
    {
        var client = factory is null ? CreateClient() : factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Semeia a ferramenta real do J10 (CM 5447 lote 3, NNPB) com a
    /// geometria TP da folha genesis e uma ligação de máquina B1.
    /// </summary>
    private async Task<string> SeedRealToolAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var machine = new Machine { Code = "B1" };
        var tool = new Tool { ToolId = "Tabc1234567", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        db.Machines.Add(machine);
        db.Tools.Add(tool);
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = tool.ToolId,
            Process = CmProcess.Nnpb,
            TpDiametroMm = 28.45m,
            TpRaioMm = 24.11m,
            TpSagittaMm = 4.5m,
            TpRaioBaseMm = 14.225m,
            TpRaioEsferaMm = 28.84061800082953m,
            TpVolumeMm3 = 1438.3860504277206m
        });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = tool.ToolId, MachineId = machine.Id });
        await db.SaveChangesAsync();
        return tool.ToolId;
    }

    // ------------------------------------------------------------------
    // Acesso — o anónimo é desafiado antes de qualquer leitura/escrita.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnonymousRequests_ChallengedBeforeAnyReadOrWrite()
    {
        using var client = CreateClient();

        var index = await client.GetAsync(IndexPath);
        Assert.Equal(HttpStatusCode.Redirect, index.StatusCode);
        Assert.Contains("/login", index.Headers.Location?.ToString(), StringComparison.Ordinal);

        var ficha = await client.GetAsync($"{FichaPath}?tool_id=Tabc1234567");
        Assert.Equal(HttpStatusCode.Redirect, ficha.StatusCode);
        Assert.Contains("/login", ficha.Headers.Location?.ToString(), StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Tools.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.ToolMachineLinks.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task PostCreate_UserWithoutTemplate_IsRefusedAndNothingIsWritten()
    {
        // O template dá módulos: sem o módulo Ferramentas não se entra na
        // página nem corre nenhum handler; o pedido vai para o início.
        SeedUser("4490", "Sem Template", "provider-ferr-noaccess", "FerrPass90!", withOperationalAccess: false);
        using var client = await LoginClientAsync("4490", "FerrPass90!");

        var page = await client.GetAsync(IndexPath + "?criar=1");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/Modules/Home", page.Headers.Location?.ToString());

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "3"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Modules/Home", response.Headers.Location?.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Tools.AsNoTracking()
            .CountAsync(t => t.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId));
    }

    [Theory]
    [InlineData("CM")]
    [InlineData("MF")]
    [InlineData("BQ")]
    public async Task Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo(string tipo)
    {
        // O Diogo, 17/10/2026: «ao criarmos uma ferramenta a partir do card CM
        // deve ser o CM e não voltar a escolher». 18/10/2026: a página é a
        // mesma das Ferramentas, com o mesmo dropdown e o tipo escolhido.
        SeedUser("4492", "Ferramentas Cartão", "provider-ferr-cartao", "FerrCartao1!");
        using var client = await LoginClientAsync("4492", "FerrCartao1!");

        var regresso = Uri.EscapeDataString($"/JobOn?novo=1&{tipo.ToLowerInvariant()}_tool_id={{tool_id}}");
        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{IndexPath}?criar=1&type={tipo}&return={regresso}"));
        Assert.Contains($"<select id=\"ferr-new-type\" name=\"type_token\" required data-ferr-tipo-fixo=\"{tipo}\">", html, StringComparison.Ordinal);
        Assert.Matches($"<option value=\"{tipo}\" selected=\"selected\">", html);
        foreach (var outro in new[] { "CM", "BQ", "MF" }.Where(t => t != tipo))
            Assert.Contains($"<option value=\"{outro}\" disabled=\"disabled\">", html, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"ferr-create-title\">Registar ferramenta<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"hidden\" name=\"type_token\"", html, StringComparison.Ordinal);
        // A Referência sugere as referências completas do tipo (Diogo, 19/10/2026).
        Assert.Contains($"data-dmo-suggest=\"referencias_registo\" data-dmo-suggest-tipo=\"{tipo.ToLowerInvariant()}\"", html, StringComparison.Ordinal);

        // Sem vir do Job On, o mesmo formulário com todos os tipos disponíveis.
        var livre = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{IndexPath}?criar=1&type={tipo}"));
        Assert.Contains("<select id=\"ferr-new-type\" name=\"type_token\" required data-ferr-tipo-fixo=\"\">", livre, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"ferr-create-title\">Registar ferramenta<", livre, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled=\"disabled\">", livre[livre.IndexOf("id=\"ferr-new-type\"", StringComparison.Ordinal)..livre.IndexOf("</select>", livre.IndexOf("id=\"ferr-new-type\"", StringComparison.Ordinal), StringComparison.Ordinal)], StringComparison.Ordinal);
        // O botão fica no canto superior direito do card, ligado ao formulário.
        Assert.Contains("<button type=\"submit\" form=\"ferr-create-form\" class=\"dmo-button\">Registar</button>", livre, StringComparison.Ordinal);
        // O traço da informação técnica: Peso, Gargalo (o Tampão saiu da CM, Diogo 26/10/2026).
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(livre, "class=\"ferr-tech-sep\""));

        // No Ver, a Referência vem primeiro (Diogo, 23/10/2026: «começar com
        // campo de filtrar»; antes, 18/10, a Máquina vinha antes), a «Linha»
        // a seguir ao Tipo, e o «Por página» sai dos filtros para junto da
        // paginação, ligado ao formulário (uma opção da página).
        var ver = await GetPageHtmlAsync(client, IndexPath);
        Assert.True(Posicao(ver, "id=\"ferr-reference\"") < Posicao(ver, "id=\"ferr-type\""));
        Assert.True(Posicao(ver, "id=\"ferr-type\"") < Posicao(ver, "id=\"ferr-machine\""));
        Assert.Contains("<label for=\"ferr-machine\">Linha</label>", ver, StringComparison.Ordinal);
        Assert.DoesNotContain("Máquina / linha", ver, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ferr-page-size\"", ver[..ver.IndexOf("</form>", Posicao(ver, "id=\"ferrFiltros\""), StringComparison.Ordinal)], StringComparison.Ordinal);
        var comResultados = await GetPageHtmlAsync(client, $"{IndexPath}?reference=5");
        Assert.True(Posicao(comResultados, "id=\"ferr-page-size\"") > Posicao(comResultados, "aria-label=\"Paginação de ferramentas\""));
        Assert.Contains("<select id=\"ferr-page-size\" name=\"por_pagina\" form=\"ferrFiltros\"", comResultados, StringComparison.Ordinal);
        // Os filtros e os dois botões numa linha (Diogo, 19/10/2026): «Criar
        // ferramenta» sai do topo do card e fica a seguir a «Limpar filtros»,
        // no fim do formulário, e abre a página inteira.
        var filtros = ver[ver.IndexOf("class=\"ferr-filters\"", StringComparison.Ordinal)..ver.IndexOf("</form>", ver.IndexOf("class=\"ferr-filters\"", StringComparison.Ordinal), StringComparison.Ordinal)];
        var acoes = filtros[filtros.IndexOf("class=\"ferr-filter-actions\"", StringComparison.Ordinal)..];
        Assert.True(Posicao(acoes, ">Limpar filtros</a>") < Posicao(acoes, ">Criar ferramenta</a>"));
        Assert.Matches("<a class=\"dmo-button\" id=\"ferrCriarFerramenta\" data-dmo-completa href=\"/Ferramentas\\?criar=1\">Criar ferramenta</a>", acoes);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(ver, ">Criar ferramenta</a>"));
        foreach (var largura in new[] { "ferr-w-type", "ferr-w-machine", "ferr-w-ref", "ferr-w-lot" })
            Assert.Contains($"class=\"dmo-field {largura}\"", filtros, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_LowercaseReferences_AreSavedInUppercase()
    {
        // Pedido do proprietário (16/10/2026): as letras das referências ficam
        // em maiúsculas sozinhas, no ecrã e na gravação.
        SeedUser("4491", "Ferramentas Maiúsculas", "provider-ferr-maius", "FerrMaius1!");
        using var client = await LoginClientAsync("4491", "FerrMaius1!");

        var html = await GetPageHtmlAsync(client, IndexPath + "?criar=1");
        Assert.Contains("id=\"ferr-new-reference\" data-dmo-maiusculas", html, StringComparison.Ordinal);

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "c142"),
            ("lot", "7"),
            ("mf_reference", "t5810"),
            ("technical_name", "m107544mfnnv"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var tool = await db.Tools.AsNoTracking().SingleAsync(t => t.Lot == "7" && t.Type == DMO.Alpha.Core.Tools.ToolType.Cm);
        Assert.Equal("C142", tool.Reference);
        Assert.Equal("T5810", tool.AssociatedMfReference);
        Assert.Equal("M107544MFNNV", tool.TechnicalName);
    }

    // ------------------------------------------------------------------
    // Index — FATOS HUMANOS, filtros, zero anti-padrões.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField()
    {
        SeedUser("4410", "Ferramentas Owner", "provider-ferr-1", "FerrPass1!");
        using var client = await LoginClientAsync("4410", "FerrPass1!");
        var toolId = await SeedRealToolAsync();

        var html = await GetPageHtmlAsync(client, $"{IndexPath}?type=CM&reference=5447");

        // FATOS HUMANOS: «CM · 5447 · lote 3 · B1» — o que o Owner
        // reconhece no chão da fábrica.
        Assert.Contains("CM · 5447 · lote 3", html);
        Assert.Contains("B1", html);
        // P2-8: o texto direto — o botão diz o que abre («Ficha»), o
        // «Abrir» saiu (a regra do Owner).
        // A GRAMÁTICA GLOBAL (o Owner 2026-10-13: «tem botoes abrir ficha, quando
        // ja tinhamos falado no duplo click e no click carregar no botao fora»):
        // NAS TABELAS NÃO EXISTEM BOTÕES — a coluna «Ficha» morre; a linha
        // seleciona (o radio escondido + o invertido), o DUPLO abre a ficha,
        // sem botão «Abrir ficha» (16/10/2026: com duplo clique não há botão).
        Assert.DoesNotContain(">Ficha</a>", html, StringComparison.Ordinal);
        Assert.Contains("data-dmo-row", html, StringComparison.Ordinal);
        Assert.Contains("data-dmo-open=\"/Ferramentas/Ficha?tool_id=", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Abrir ficha</a>", html, StringComparison.Ordinal);

        // O tool_id vive no URL da ficha (identidade opaca do backend),
        // nunca como facto exibido da folha.
        Assert.Contains($"tool_id={toolId}", html);
        Assert.DoesNotContain($">T{toolId[1..]}<", html);

        // SEM campo Estado; a lista é o registo canónico, não um workflow.
        // A coluna «Estado» só mostra onde está a ferramenta (Diogo,
        // 26/10/2026: «deve apresentar percentagem e o estado»).
        Assert.DoesNotContain("name=\"estado\"", html, StringComparison.OrdinalIgnoreCase);

        // A criação contextual e os formatos-canónicos HINT estão na
        // folha (nunca recusa).
        // A TAB CRIAR (o Owner 2026-10-13: [um tab para criar e outro para ver o que existe]): o form vive no ?criar=1.
        var criarHtml = await client.GetStringAsync("/Ferramentas?criar=1");
        Assert.Contains("ferr-create-title", criarHtml);
        Assert.DoesNotContain("ferr-create-title", html);
        // A ajuda pede a referência toda (sweep de 19/10/2026, 3.15): a
        // regra «CM = dígitos» saiu a 17/10/2026.
        Assert.Contains("A referência toda, como no desenho: 1075C142, 5447T173 ou ST100.", criarHtml);
        Assert.DoesNotContain("CM = dígitos", criarHtml);

        // ZERO anti-padrões de mockup (precedente Boquilhas/Index): sem
        // rotas de mockup, sem JS demo, sem sessionStorage. 2026-10-11 (port
        // H-3): a nav global do shell carrega a rota REAL da Boquilhas
        // (/31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html — o @page atual
        // do build; o corte F muda-a) e o layout as scripts comuns do F0 —
        // os asserts aplicam à FOLHA das Ferramentas (o conteúdo após a
        // nav), não à navegação global do shell.
        var navEnd = html.IndexOf("</nav>", StringComparison.OrdinalIgnoreCase);
        var folhaHtml = navEnd >= 0 ? html[(navEnd + 6)..] : html;
        Assert.DoesNotContain("sessionStorage", folhaHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localStorage", folhaHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VISUAL_AUTHORITY", folhaHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".html\"", folhaHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", folhaHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIndex_FiltersOnlyReduceCandidates_ExcludingDummy()
    {
        SeedUser("4411", "Ferramentas Owner", "provider-ferr-2", "FerrPass2!");
        using var client = await LoginClientAsync("4411", "FerrPass2!");
        await SeedRealToolAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.AddRange(
                new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
                new Tool { ToolId = "Tdef7654321", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
            await db.SaveChangesAsync();
        }

        var byReference = await GetPageHtmlAsync(client, $"{IndexPath}?reference=5447");
        Assert.Contains("CM · 5447 · lote 3", byReference);
        Assert.Contains("<main data-dmo-troca", byReference);
        Assert.Contains("data-dmo-fica data-dmo-parcial", byReference);
        Assert.Contains("data-dmo-regiao", byReference);
        Assert.Contains("<nav data-dmo-parcial class=\"ferr-pagination\"", byReference);
        Assert.DoesNotContain("BQ · T194 · lote 12", byReference);

        // Sem filtros, a lista não carrega (Diogo, 17/10/2026: «não deve
        // existir lista até procurar»).
        var empty = await GetPageHtmlAsync(client, IndexPath);
        Assert.DoesNotContain("CM · 5447 · lote 3", empty);
        Assert.DoesNotContain("BQ · T194 · lote 12", empty);
        Assert.Contains("id=\"ferrResultados\" aria-labelledby=\"ferr-list-title\" hidden=\"hidden\"", empty);

        // Só o tipo não carrega a lista: aparece ao começar a escrever
        // (Diogo, 18/10/2026), e parte da referência já encontra («T1»).
        var bqOnlyType = await GetPageHtmlAsync(client, $"{IndexPath}?type=BQ");
        Assert.DoesNotContain("BQ · T194 · lote 12", bqOnlyType);
        var bq = await GetPageHtmlAsync(client, $"{IndexPath}?type=BQ&reference=t1");
        Assert.Contains("BQ · T194 · lote 12", bq);

        // A linha DUMMY (REMOVED) nunca é candidata: excluída por
        // constante na query canónica.
        var cm = await GetPageHtmlAsync(client, $"{IndexPath}?type=CM&reference=E");
        Assert.DoesNotContain("REMOVED · REMOVED", cm);
        cm = await GetPageHtmlAsync(client, $"{IndexPath}?type=CM&reference=54");
        Assert.Contains("CM · 5447 · lote 3", cm);
        Assert.DoesNotContain("REMOVED · REMOVED", cm);
        Assert.DoesNotContain("lote REMOVED", cm);
    }

    [Fact]
    public async Task Pagination_PreservesFilters_AndExportsBothPagesForBrowserChecks()
    {
        SeedUser("4499", "Paginação", "provider-pagination", "FerrPagination1!");
        using var client = await LoginClientAsync("4499", "FerrPagination1!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            for (var i = 1; i <= 12; i++)
                db.Tools.Add(new Tool { ToolId = $"page-tool-{i}", Type = ToolType.Cm, Reference = "PAGING", Lot = $"{i:00}" });
            await db.SaveChangesAsync();
        }
        var first = await GetPageHtmlAsync(client, $"{IndexPath}?reference=PAGING&por_pagina=10");
        var second = await GetPageHtmlAsync(client, $"{IndexPath}?reference=PAGING&por_pagina=10&pagina=2");
        Assert.Contains(">1 de 2</span>", first);
        Assert.Contains(">2 de 2</span>", second);
        Assert.Equal(10, System.Text.RegularExpressions.Regex.Matches(first, "name=\"ferr_tool\"").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(second, "name=\"ferr_tool\"").Count);
        var directory = Environment.GetEnvironmentVariable("DMO_UI_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ferr-page1.html"), first);
            File.WriteAllText(Path.Combine(directory, "ferr-page2.html"), second);
        }
    }

    [Fact]
    public async Task Results_ReturnOnlyCandidates_WithoutLoadingMachines_AndPreserveContext()
    {
        int machineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            for (var i = 1; i <= 12; i++)
            {
                var tool = new Tool { ToolId = $"api-{i:00}", Type = ToolType.Cm, Reference = "API", Lot = $"{i:00}" };
                db.Tools.Add(tool);
                db.ToolMachineLinks.Add(new ToolMachineLink { Tool = tool, Machine = machine });
            }
            await db.SaveChangesAsync();
            machineId = machine.Id;
        }
        using var anonymous = CreateClient();
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync($"{IndexPath}?handler=Resultados&type=CM")).StatusCode);
        SeedUser("4498", "Pesquisa parcial", "provider-ferr-resultados", "FerrResultados1!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IMachineListQuery>(_ => new ForbiddenMachineListQuery())));
        using var client = await LoginClientAsync("4498", "FerrResultados1!", factory);
        var template = Uri.EscapeDataString("/JobOn?cm_tool_id={tool_id}");
        var response = await client.GetAsync($"{IndexPath}?handler=Resultados&type=CM&mf_reference=API&machine_id={machineId}&pagina=2&por_pagina=10&return={template}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ferrResultados", response.Headers.GetValues("X-Dmo-Region").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "name=\"ferr_tool\"").Count);
        Assert.Contains("api-11", html);
        Assert.Contains("api-12", html);
        Assert.Contains("data-href-template=\"/JobOn?cm_tool_id={id}\"", html);
        // Vindo do Job On, o «Selecionar ferramenta» fica no canto superior
        // direito do cartão, na linha do título, e não por baixo da tabela
        // (regra 5 do deep sweep de 18/10/2026).
        Assert.Matches("<div class=\"dmo-card-topo\">\\s*<h3 id=\"ferr-list-title\">Ferramentas registadas[\\s\\S]*?</h3>\\s*"
            + "<div class=\"dmo-card-topo-acoes\" data-dmo-for=\"ferrToolList\">\\s*<a class=\"dmo-button\" data-dmo-action[^>]*>Selecionar ferramenta</a>", html);
        Assert.True(html.IndexOf("Selecionar ferramenta", StringComparison.Ordinal) < html.IndexOf("id=\"ferrToolList\"", StringComparison.Ordinal));
        Assert.DoesNotContain("ferr-actions", html);
        Assert.Contains($"machine_id={machineId}", html);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Filtrar ferramentas registadas", html);
        var last = await client.GetStringAsync($"{IndexPath}?handler=Resultados&type=CM&reference=API&pagina=2147483647&por_pagina=-1&return=https://example.org");
        Assert.Contains(">2 de 2</span>", last);
        Assert.DoesNotContain("data-href-template", last);
        var empty = await client.GetStringAsync($"{IndexPath}?handler=Resultados");
        Assert.Contains("hidden=\"hidden\"", empty);
        Assert.DoesNotContain("name=\"ferr_tool\"", empty);
    }

    private sealed class ForbiddenMachineListQuery : IMachineListQuery
    {
        public Task<IReadOnlyList<MachineOption>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The results handler must not load the filter's machine list.");
    }

    [Fact]
    public async Task PostCreate_RegistersRealTool_PrgToFicha_MintedBackendId()
    {
        SeedUser("4412", "Ferramentas Owner", "provider-ferr-3", "FerrPass3!");
        using var client = await LoginClientAsync("4412", "FerrPass3!");

        int machineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();
            machineId = machine.Id;
        }

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "3"),
            ("machine_ids", machineId.ToString()),
            ("__RequestVerificationToken", token)));

        // PRG: a ficha da nova ferramenta abre com a nota de criação.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        Assert.Contains("created=1", location, StringComparison.Ordinal);

        // O tool_id foi MINTADO pelo backend («T» + 10 hex minúsculos).
        var minted = location.Split("tool_id=")[1].Split('&')[0];
        Assert.Matches("^T[0-9a-f]{10}$", minted);

        var ficha = await GetPageHtmlAsync(client, location);
        Assert.Contains("Ferramenta registada", ficha);
        Assert.Contains("CM · 5447 · lote 3", ficha);
        Assert.Contains("B1", ficha);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var persisted = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == minted);
            Assert.Equal(ToolType.Cm, persisted.Type);
            Assert.Equal("5447", persisted.Reference);
            Assert.Equal("3", persisted.Lot);
            Assert.Equal(1, await db.ToolMachineLinks.AsNoTracking().CountAsync(l => l.ToolId == minted));
        }
    }

    [Fact]
    public async Task Ficha_SemMaquinas_AvisaComLinkParaOEditar()
    {
        // Diogo, 19/10/2026: sem máquinas a ferramenta conta em todas para a
        // ferramenta repetida, com um aviso e um link para onde se muda.
        SeedUser("4421", "Ferramentas Owner", "provider-ferr-12", "FerrPass12!");
        using var client = await LoginClientAsync("4421", "FerrPass12!");
        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var created = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "MF"),
            ("reference", "1075"),
            ("lot", "4"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var toolId = created.Headers.Location!.ToString().Split("tool_id=")[1].Split('&')[0];

        var ficha = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains("Sem máquinas: conta como estando em todas", ficha);
        Assert.Contains("href=\"#ferrEditar\" id=\"ferr-sem-maquinas-editar\">Acrescentar as máquinas</a>", ficha);
        Assert.Contains("id=\"ferrEditar\"", ficha);
    }

    [Fact]
    public async Task PostCreate_Repetida_EBloqueada_AExistenteFicaComAvisoQueSeLimpa()
    {
        // FERRAMENTA REPETIDA (o proprietário, 18/10/2026): os mesmos (tipo,
        // referência, lote) já não abrem a existente; o registo é recusado
        // a vermelho com o link para ela, e ela fica com um aviso na ficha
        // que se limpa depois de confirmar que está certa. Um lote
        // diferente é a criação nova de sempre.
        SeedUser("4420", "Ferramentas Owner", "provider-ferr-11", "FerrPass11!");
        using var client = await LoginClientAsync("4420", "FerrPass11!");

        int b1Id;
        int b2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var b2 = new Machine { Code = "B2" };
            db.Machines.Add(b1);
            db.Machines.Add(b2);
            await db.SaveChangesAsync();
            b1Id = b1.Id;
            b2Id = b2.Id;
        }

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");

        var first = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "3"),
            ("machine_ids", b1Id.ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var toolId = first.Headers.Location!.ToString().Split("tool_id=")[1].Split('&')[0];

        // A segunda com os MESMOS factos: recusada, nada registado.
        await RegistosAntigos.EnvelhecerAsync(_factory.Services);
        var repeat = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "3"),
            ("machine_ids", b1Id.ToString()),
            ("machine_ids", b2Id.ToString()),
            ("return", "/JobOn?jobon_id=5&editar=1&cm_tool_id={tool_id}"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, repeat.StatusCode);
        var repeatLocation = repeat.Headers.Location!.ToString();
        Assert.StartsWith("/Ferramentas?", repeatLocation, StringComparison.Ordinal);
        Assert.Contains("create_refusal=ToolAlreadyExists", repeatLocation, StringComparison.Ordinal);
        Assert.Contains($"existing_tool_id={toolId}", repeatLocation, StringComparison.Ordinal);

        var refusal = await GetPageHtmlAsync(client, repeatLocation);
        Assert.Contains("data-dmo-toast=\"erro\"", refusal);
        Assert.Contains("Já existe uma ferramenta com este tipo, referência e lote numa das mesmas máquinas. Nenhuma ferramenta foi registada", refusal);
        Assert.Contains($"href=\"/Ferramentas/Ficha?tool_id={toolId}\">Abrir a ferramenta existente</a>", refusal);
        // Vindo do Job On: usar a existente volta lá com ela.
        Assert.Contains($"href=\"/JobOn?jobon_id=5&amp;editar=1&amp;cm_tool_id={toolId}\">Usar a existente</a>", refusal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(1, await db.Tools.AsNoTracking().CountAsync(t => t.Reference == "5447" && t.Lot == "3"));
            Assert.Equal(1, await db.ToolMachineLinks.AsNoTracking().CountAsync(l => l.ToolId == toolId));
            Assert.NotNull((await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId)).DuplicateWarningAtUtc);
        }

        // A ficha da existente mostra o aviso com quem tentou e o «Limpar aviso».
        var ficha = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains("Possível ferramenta repetida", ficha);
        Assert.Contains("Ferramentas Owner tentou registar outra ferramenta CM · 5447 · lote 3", ficha);
        Assert.Contains("id=\"ferr-repetida-limpar\">Limpar aviso</button>", ficha);
        // Com máquinas não há o aviso de «Sem máquinas».
        Assert.DoesNotContain("ferr-sem-maquinas", ficha);

        var clearToken = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var cleared = await client.PostAsync($"{FichaPath}?handler=ClearDuplicateWarning", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("__RequestVerificationToken", clearToken)));
        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&guardado=aviso", cleared.Headers.Location!.ToString());
        var limpa = await GetPageHtmlAsync(client, cleared.Headers.Location!.ToString());
        Assert.Contains("Aviso de ferramenta repetida limpo.", limpa);
        Assert.DoesNotContain("Possível ferramenta repetida", limpa);

        // O lote DIFERENTE é a criação nova: outra ferramenta legítima.
        var otherLot = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "4"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, otherLot.StatusCode);
        var otherLotLocation = otherLot.Headers.Location!.ToString();
        Assert.Contains("created=1", otherLotLocation, StringComparison.Ordinal);
        Assert.DoesNotContain($"tool_id={toolId}", otherLotLocation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostCreate_TypedRefusalByPrg_MessageShown_NoWrite()
    {
        SeedUser("4413", "Ferramentas Owner", "provider-ferr-4", "FerrPass4!");
        using var client = await LoginClientAsync("4413", "FerrPass4!");

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "XX"),
            ("reference", "T194"),
            ("lot", "3"),
            ("__RequestVerificationToken", token)));

        // Recusa tipada por PRG: o tipo desconhecido (o formulário só
        // oferece CM/BQ/MF) é UnknownTypeToken — nunca texto inventado.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("create_refusal=UnknownTypeToken", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.ToString());
        Assert.Contains("Tipo desconhecido", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Tools.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.ToolMachineLinks.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task PostCreate_ContextualReturnTemplate_GetsMintedToolIdBack()
    {
        // O fluxo contextual (blueprint «Contextual selection and
        // creation»): o chamador abre o Ferramentas com ?return=…{tool_id}…
        // e recebe o tool_id resultante — o Job On nunca minta identidade
        // privada.
        SeedUser("4414", "Ferramentas Owner", "provider-ferr-5", "FerrPass5!");
        using var client = await LoginClientAsync("4414", "FerrPass5!");

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1&return=/Controlo/Peso%3Fcm%3D%7Btool_id%7D");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "9"),
            ("return", "/Controlo/Peso?cm={tool_id}"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Controlo/Peso?cm=T", location, StringComparison.Ordinal);
        Assert.DoesNotContain("{tool_id}", location, StringComparison.Ordinal);
        Assert.Matches("^/Controlo/Peso\\?cm=T[0-9a-f]{10}$", location);
    }

    [Fact]
    public async Task PostCreate_WithTechnicalValues_SavesThemOnTheNewTool_AndReturnsToTheFlow()
    {
        // O Diogo, 17/10/2026: «nao temos forma de inserir informaçao tecnica
        // nas ferramentas». O registo tem a informação técnica opcional;
        // cada tipo grava só os seus campos (a marisa de uma CM é ignorada).
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-tec-1", "FerrTec1!");
        using var client = await LoginClientAsync("4418", "FerrTec1!");

        var form = await GetPageHtmlAsync(client, $"{IndexPath}?criar=1&type=BQ");
        Assert.Contains("<summary><span class=\"dmo-caret\" aria-hidden=\"true\"></span>Informação técnica (opcional)</summary>", form);
        Assert.Contains("data-ferr-tech-tipo=\"CM\" hidden=\"hidden\"", form);
        Assert.Contains("data-ferr-tech-tipo=\"BQ\">", form);

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "7"),
            ("process_token", "PS"),
            ("nominal_weight", "310,5"),
            ("volume_puncao", "30.2"),
            ("volume_marisa", "9"),
            ("diametro_gargalo", "26"),
            ("tp_diametro", ""),
            ("return", "/JobOn?novo=1&cm_tool_id={tool_id}"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/JobOn?novo=1&cm_tool_id=T", location, StringComparison.Ordinal);
        var toolId = location["/JobOn?novo=1&cm_tool_id=".Length..];

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var stored = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(CmProcess.Ps, stored.Process);
        Assert.Equal(310.5m, stored.NominalWeightGrams);
        Assert.Equal(30.2m, stored.VolumePuncaoCm3);
        Assert.Null(stored.VolumeMarisaCm3);
        Assert.Equal(26m, stored.DiametroGargaloMm);
        Assert.Null(stored.TpDiametroMm);
    }

    [Fact]
    public async Task PostCreate_UnreadableTechnicalValue_SavesTheTool_AndTheSheetWarns()
    {
        SeedUser("4419", "Ferramentas Owner", "provider-ferr-tec-2", "FerrTec2!");
        using var client = await LoginClientAsync("4419", "FerrTec2!");

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "BQ"),
            ("reference", "T194"),
            ("lot", "4"),
            ("volume_marisa", "abc"),
            ("diametro_gargalo", "1,8"),
            ("return", "/JobOn?novo=1&bq_tool_id={tool_id}"),
            ("__RequestVerificationToken", token)));

        // Com um campo ilegível a ficha abre primeiro, com o aviso e o link
        // para continuar no Job On.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync(location));
        Assert.Contains("Marisa (cm³) «abc»", html);
        Assert.Contains("Continuar no fluxo que abriu o Ferramentas", html);
    }

    [Fact]
    public async Task PostCreate_Refusal_KeepsTheTechnicalValues_AndOpensThem()
    {
        SeedUser("4420", "Ferramentas Owner", "provider-ferr-tec-3", "FerrTec3!");
        using var client = await LoginClientAsync("4420", "FerrTec3!");

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", ""),
            ("lot", "3"),
            ("nominal_weight", "30,2"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location!.ToString());
        Assert.Contains("<details class=\"ferr-create-tech\" open=\"open\">", html);
        Assert.Contains("name=\"nominal_weight\" type=\"text\" inputmode=\"decimal\" value=\"30,2\"", html);
    }

    [Fact]
    public async Task PostCreate_InvalidReturnTemplate_Ignored_LocalOnly()
    {
        // Validação LOCAL-ONLY do return: um template que não começa por
        // "/" ou que começa por "//" é IGNORADO (nunca recusa) — o
        // pós-criação segue para a ficha.
        SeedUser("4415", "Ferramentas Owner", "provider-ferr-6", "FerrPass6!");
        using var client = await LoginClientAsync("4415", "FerrPass6!");

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5447"),
            ("lot", "3"),
            ("return", "https://external.example/steal?tool={tool_id}"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        Assert.DoesNotContain("external.example", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/\\external.example/{tool_id}")]
    [InlineData("/JobOn")]
    public async Task CreateTool_BackslashOrNoToolIdReturn_OpensTheSheet(string returnTemplate)
    {
        SeedUser("4416", "Ferramentas Owner", "provider-ferr-6b", "FerrPass6b!");
        using var client = await LoginClientAsync("4416", "FerrPass6b!");

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"),
            ("reference", "5448"),
            ("lot", "3"),
            ("return", returnTemplate),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTool_Refusal_KeepsTheTechnicalName()
    {
        SeedUser("4417", "Ferramentas Owner", "provider-ferr-6c", "FerrPass6c!");
        using var client = await LoginClientAsync("4417", "FerrPass6c!");

        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");
        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "XX"),
            ("reference", "5449"),
            ("lot", "3"),
            ("technical_name", "M107544MFNNV"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location!.ToString());
        Assert.Contains("value=\"M107544MFNNV\"", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Ficha — composição, edição, duplicação, eliminação.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetFicha_UnknownToolId_CleanWarning_NothingInvented()
    {
        SeedUser("4416", "Ferramentas Owner", "provider-ferr-7", "FerrPass7!");
        using var client = await LoginClientAsync("4416", "FerrPass7!");

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id=Taaaaaaaaaa");
        Assert.Contains("A ferramenta indicada não existe", html);
        Assert.DoesNotContain("id=\"ferr-edit-facts-title\"", html);
    }

    [Fact]
    public async Task GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered()
    {
        SeedUser("4417", "Ferramentas Owner", "provider-ferr-8", "FerrPass8!");
        using var client = await LoginClientAsync("4417", "FerrPass8!");
        var toolId = await SeedRealToolAsync();

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");

        // Factos humanos + máquinas por código.
        Assert.Contains("CM · 5447 · lote 3", html);
        Assert.Contains("B1", html);
        Assert.Contains("NNPB", html);

        // O tampão e a punção saíram da ficha da CM (Diogo, 26/10/2026): o
        // tampão escreve-se na Folha do Job On e a punção no Peso; os valores
        // gravados ficam na base, sem aparecer.
        Assert.DoesNotContain("name=\"tp_diametro\"", html);
        Assert.DoesNotContain("name=\"tp_sagitta\"", html);
        Assert.DoesNotContain("name=\"volume_puncao\"", html);
        Assert.DoesNotContain("name=\"tp_raio\"", html);
        Assert.DoesNotContain("Raio (mm)", html);
        Assert.DoesNotContain("Volume do tampão (cm³)", html);
        Assert.DoesNotContain("14.225", html);
        Assert.DoesNotContain("28.84061800082953", html);
        Assert.DoesNotContain("1438.3860504277206", html);
        Assert.DoesNotContain("name=\"tp_volume\"", html);
        Assert.DoesNotContain("name=\"tp_raio_base\"", html);
        Assert.DoesNotContain("name=\"tp_raio_esfera\"", html);

        // Os forms canónicos: factos (full-replace), valores técnicos,
        // duplicação e eliminação. A1: as DUAS SECÇÕES da ficha —
        // «Geral» (os factos) e «Técnica» (os valores técnicos, com os
        // RÓTULOS HUMANOS — o P2-9: nunca snake_case na folha).
        Assert.Contains(">Editar</h3>", html);
        Assert.Contains("Geral — CM · 5447 · lote 3", html);
        Assert.Contains("Informação técnica", html);
        Assert.DoesNotContain("Punção (cm³)", html);
        Assert.Contains("<legend class=\"ferr-tech-titulo\">Gargalo<", html);
        Assert.Contains("Informação técnica", html);
        Assert.DoesNotContain("<legend class=\"ferr-tech-titulo\">Tampão<", html);
        Assert.DoesNotContain("Calote (mm)", html);
        Assert.DoesNotContain("Marisa (cm³)", html);
        Assert.Contains(">Guardar</button>", html);
        Assert.DoesNotContain("Guardar valores técnicos", html);

        // A informação técnica abre só quando o utilizador a pede, mesmo com
        // dados em falta: o aviso fica nos módulos que os usam (Diogo, 17/10/2026).
        Assert.Contains("<details>\n      <summary id=\"ferrTecnicaTitulo\" class=\"dmo-card-topo\" data-dmo-regiao><h3 id=\"ferr-tech-title\"", html.Replace("\r\n", "\n"));
        Assert.DoesNotContain("dados técnicos:", html);
        var aberta = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&tecnica=1");
        Assert.Contains("<details open=\"open\">", aberta);
        // Os RÓTULOS VISÍVEIS sem snake_case (o atributo name do input é
        // o contrato do POST, invisível na folha — o rótulo é que é
        // humano).
        Assert.DoesNotContain(">volume_puncao", html);
        Assert.DoesNotContain(">diametro_gargalo", html);
        // Duplicar e Eliminar no «Opções» do canto do Geral, cada um numa janela
        // pequena (Diogo, 17/10/2026); já não são secções da página.
        Assert.Contains("<summary class=\"dmo-button\" aria-label=\"Opções da ferramenta", html);
        Assert.Contains("data-dmo-dialog=\"ferr-duplicate-dialog\">Duplicar para lote novo</button>", html);
        Assert.Contains("data-dmo-dialog=\"ferr-delete-dialog\">Eliminar ferramenta</button>", html);
        Assert.Contains("<dialog id=\"ferr-duplicate-dialog\"", html);
        Assert.DoesNotContain("Eliminar esta ferramenta", html);
        // Eliminar: só o aviso curto, sem as contagens (Diogo, 17/10/2026).
        Assert.Contains("<dialog id=\"ferr-delete-dialog\"", html);
        Assert.DoesNotContain("data-dmo-dialog-aberta=\"1\"", html);
        Assert.Contains("Quer mesmo eliminar", html);
        Assert.DoesNotContain("Pesos reatribuídos", html);
    }

    [Fact]
    public async Task PostEditFacts_FullReplaceSameToolId_PrgPersisted()
    {
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-9", "FerrPass9!");
        using var client = await LoginClientAsync("4418", "FerrPass9!");
        var toolId = await SeedRealToolAsync();

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("type_token", "CM"),
            ("reference", "5448"),
            ("lot", "4"),
            ("mf_reference", "5811"),
            ("__RequestVerificationToken", token)));

        // PRG à ficha do MESMO tool_id: a identidade nunca muda; a ficha
        // confirma no canto que os dados gerais ficaram guardados.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&guardado=geral", response.Headers.Location?.ToString());

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains("CM · 5448 · lote 4", html);
        Assert.Contains("5811", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var persisted = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal("5448", persisted.Reference);
            Assert.Equal("4", persisted.Lot);
            Assert.Equal("5811", persisted.AssociatedMfReference);
        }
    }

    [Fact]
    public async Task PostEdit_SavedMeanwhileByAnotherUser_RefusesFactsAndTechnical_NothingSaved()
    {
        // Edições ao mesmo tempo (varredura de 18/10/2026, B4): os dois
        // formulários levam a versão aberta (no InMemory é sempre 0); com
        // outra versão nada se grava e a ficha mostra o que ficou gravado.
        SeedUser("4431", "Ferramentas Owner", "provider-ferr-b4", "FerrPassB4!");
        using var client = await LoginClientAsync("4431", "FerrPassB4!");
        var toolId = await SeedRealToolAsync();

        var ficha = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&tecnica=1");
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(
            ficha, "<input type=\"hidden\" name=\"versao\" value=\"0\" />").Count);

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var facts = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("type_token", "CM"),
            ("reference", "5448"),
            ("lot", "4"),
            ("versao", "7"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, facts.StatusCode);
        var factsLocation = facts.Headers.Location!.ToString();
        Assert.Contains("facts_refusal=ChangedByAnotherUser", factsLocation);
        Assert.Contains("Outra pessoa gravou esta ferramenta depois de a abrires.", await GetPageHtmlAsync(client, factsLocation));

        var technical = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("nominal_weight", "999"),
            ("versao", "7"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, technical.StatusCode);
        var technicalLocation = technical.Headers.Location!.ToString();
        Assert.Contains("technical_refusal=ChangedByAnotherUser", technicalLocation);
        Assert.DoesNotContain("999", technicalLocation);
        var html = await GetPageHtmlAsync(client, technicalLocation);
        Assert.Contains("Outra pessoa gravou a informação técnica depois de abrires a ficha.", html);
        Assert.DoesNotContain("value=\"999\"", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var persisted = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.NotEqual("5448", persisted.Reference);
        var stored = await db.ToolTechnicalContexts.AsNoTracking().SingleOrDefaultAsync(t => t.ToolId == toolId);
        Assert.NotEqual(999m, stored?.NominalWeightGrams);
    }

    [Fact]
    public async Task PostEditTechnical_GenesisSheetValues_PrgPersistedExactly()
    {
        SeedUser("4419", "Ferramentas Owner", "provider-ferr-10", "FerrPass10!");
        using var client = await LoginClientAsync("4419", "FerrPass10!");
        var toolId = await SeedRealToolAsync();

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("process_token", "NNPB"),
            ("nominal_weight", "520.5"),
            ("tp_diametro", "28.45"),
            ("tp_sagitta", "4.5"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&tecnica=1&guardado=tecnica", response.Headers.Location?.ToString());

        // Persistido na precisão EXATA; os derivados antigos já gravados
        // (raio, R base, R esfera, volume) ficam intactos — nada se perde.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var persisted = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal(520.5m, persisted.NominalWeightGrams);
            Assert.Equal(28.45m, persisted.TpDiametroMm);
            Assert.Equal(24.11m, persisted.TpRaioMm);
            Assert.Equal(4.5m, persisted.TpSagittaMm);
            Assert.Equal(14.225m, persisted.TpRaioBaseMm);
            Assert.Equal(28.84061800082953m, persisted.TpRaioEsferaMm);
            Assert.Equal(1438.3860504277206m, persisted.TpVolumeMm3);
        }
    }

    [Theory]
    [InlineData("nominal_weight", "abc")]
    [InlineData("tp_sagitta", "1.234,5")]
    [InlineData("tp_sagitta", "1,234.5")]
    [InlineData("volume_puncao", "12..5")]
    public async Task PostEditTechnical_UnreadableField_SavesTheReadableOnes_KeepsTheOldValue_AndWarns(string field, string invalid)
    {
        // beta.63 (A9): um campo ilegível já não recusa os sete. Os legíveis
        // gravam (a vírgula decimal também), no ilegível fica o valor que já
        // estava gravado e a ficha avisa qual foi.
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-edit", "FerrPass8!");
        using var client = await LoginClientAsync("4418", "FerrPass8!");
        var toolId = await SeedRealToolAsync();
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var values = new Dictionary<string, string>
        {
            ["tool_id"] = toolId, ["process_token"] = "PS", ["nominal_weight"] = "520.5",
            ["tp_diametro"] = "29.75", ["tp_sagitta"] = "4.6", ["diametro_gargalo"] = "70,10",
            [field] = invalid, ["__RequestVerificationToken"] = token
        };
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical",
            HtmlFormHelpers.BuildFormPost(values.Select(x => (x.Key, x.Value)).ToArray()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.DoesNotContain("technical_refusal", location);
        Assert.Contains("tecnica=1", location);
        Assert.Contains($"ilegivel_{field}=", location);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var stored = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(CmProcess.Ps, stored.Process);
        Assert.Equal(29.75m, stored.TpDiametroMm);
        Assert.Equal(70.10m, stored.DiametroGargaloMm);
        Assert.Equal(1438.3860504277206m, stored.TpVolumeMm3);
        // O ilegível manteve o valor anterior (o da folha genesis, ou vazio).
        Assert.Equal(field == "nominal_weight" ? null : 520.5m, stored.NominalWeightGrams);
        Assert.Equal(field == "tp_sagitta" ? 4.5m : 4.6m, stored.TpSagittaMm);
        Assert.Null(stored.VolumePuncaoCm3);
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("data-dmo-toast=\"aviso\" class=\"ferr-aviso warn\"", html);
        Assert.Contains("Não foi possível ler", html);
        Assert.Contains($"«{invalid}»", html);
        Assert.Contains("70,10 ou 70.10", html);
        Assert.DoesNotContain("Nenhum valor técnico foi alterado", html);
    }

    [Theory]
    [InlineData("volume_puncao", "-70")]
    [InlineData("nominal_weight", "0")]
    [InlineData("tp_sagitta", "-1")]
    public async Task PostEditTechnical_ImpossibleValue_KeepsTheOldValue_AndWarns(string field, string impossible)
    {
        // Diogo, 18/10/2026 (deep sweep I3-M-4): 0 ou menos é fisicamente
        // impossível. Como o ilegível: os outros gravam, fica o anterior e a
        // ficha avisa.
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-impossivel", "FerrPass8!");
        using var client = await LoginClientAsync("4418", "FerrPass8!");
        var toolId = await SeedRealToolAsync();
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var values = new Dictionary<string, string>
        {
            ["tool_id"] = toolId, ["process_token"] = "PS", ["nominal_weight"] = "520.5",
            ["tp_diametro"] = "29.75", ["tp_sagitta"] = "4.6", ["volume_puncao"] = "70.1",
            [field] = impossible, ["__RequestVerificationToken"] = token
        };
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical",
            HtmlFormHelpers.BuildFormPost(values.Select(x => (x.Key, x.Value)).ToArray()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains($"impossivel_{field}=", location);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var stored = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal(29.75m, stored.TpDiametroMm);
            Assert.Equal(field == "volume_puncao" ? null : 70.1m, stored.VolumePuncaoCm3);
            Assert.Equal(field == "nominal_weight" ? null : 520.5m, stored.NominalWeightGrams);
            Assert.Equal(field == "tp_sagitta" ? 4.5m : 4.6m, stored.TpSagittaMm);
        }
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Valor impossível", html);
        Assert.Contains($"«{impossible}»", html);
    }

    [Fact]
    public async Task PostEditTechnical_FieldOfAnotherType_IsNotWarnedAbout()
    {
        // Um campo que a ferramenta não grava (a punção numa BQ) não aparece
        // na ficha; ilegível ou não, não gera aviso.
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-edit-bq", "FerrPass8!");
        using var client = await LoginClientAsync("4418", "FerrPass8!");
        string toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = new Tool { ToolId = "Tbq20611401", Type = ToolType.Bq, Reference = "T206", Lot = "115" };
            db.Tools.Add(tool);
            await db.SaveChangesAsync();
            toolId = tool.ToolId;
        }
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("volume_marisa", "31,2"), ("volume_puncao", "abc"), ("__RequestVerificationToken", token)));
        Assert.Equal($"{FichaPath}?tool_id={toolId}&tecnica=1&guardado=tecnica", response.Headers.Location?.OriginalString);
        using var check = _factory.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<DmoDbContext>().ToolTechnicalContexts
            .AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(31.2m, stored.VolumeMarisaCm3);
    }

    [Fact]
    public async Task GetFicha_OldRefusalLinks_StillShowTheirMessages()
    {
        // Links antigos (antes da beta.63) com recusas que já não são
        // devolvidas continuam a abrir a página com a mensagem; o valor
        // morto BqTooManyMachines já não existe e é simplesmente ignorado.
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-old-links", "FerrPass8!");
        using var client = await LoginClientAsync("4418", "FerrPass8!");
        var toolId = await SeedRealToolAsync();

        var technical = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&technical_refusal=InvalidNumericValue&nominal_weight=abc");
        Assert.Contains("70,10 ou 70.10", technical);
        Assert.Contains("value=\"abc\"", technical);
        Assert.DoesNotContain("Use . como separador decimal", technical);

        var duplicate = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&duplicate_refusal=LotAlreadyExists");
        Assert.Contains("Esse lote já está registado", duplicate);

        var facts = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&facts_refusal=MissingLot");
        Assert.Contains("O lote não pode ficar em branco", facts);

        var create = await GetPageHtmlAsync(client, $"{IndexPath}?create_refusal=MissingLot");
        Assert.Contains("Indique o lote da boquilha: é preciso para identificar a ferramenta.", create);

        var dead = await GetPageHtmlAsync(client, $"{IndexPath}?create_refusal=BqTooManyMachines");
        Assert.DoesNotContain("no máximo UMA", dead);
        var deadFacts = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&facts_refusal=BqTooManyMachines");
        Assert.DoesNotContain("no máximo UMA", deadFacts);
    }

    [Fact]
    public async Task PostEditTechnical_EmptyValue_IntentionallyClearsIt()
    {
        SeedUser("4418", "Ferramentas Owner", "provider-ferr-clear", "FerrPass8!");
        using var client = await LoginClientAsync("4418", "FerrPass8!");
        var toolId = await SeedRealToolAsync();
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("process_token", "NNPB"), ("tp_sagitta", ""), ("__RequestVerificationToken", token)));
        Assert.Equal($"{FichaPath}?tool_id={toolId}&tecnica=1&guardado=tecnica", response.Headers.Location?.OriginalString);
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().ToolTechnicalContexts
            .AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Null(stored.TpSagittaMm);
        // O volume antigo gravado não é apagado por omissão (o campo saiu da ficha).
        Assert.Equal(1438.3860504277206m, stored.TpVolumeMm3);
    }

    [Fact]
    public async Task PostDuplicate_NewLotRequired_OpensNewToolFicha_SourceIntact()
    {
        SeedUser("4420", "Ferramentas Owner", "provider-ferr-11", "FerrPass11!");
        using var client = await LoginClientAsync("4420", "FerrPass11!");
        var toolId = await SeedRealToolAsync();

        // Lote em branco: recusa tipada por PRG, fonte intacta.
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var refused = await client.PostAsync($"{FichaPath}?handler=Duplicate", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("new_lot", " "),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Contains("duplicate_refusal=MissingNewLot", refused.Headers.Location?.ToString(), StringComparison.Ordinal);
        var refusalHtml = await GetPageHtmlAsync(client, refused.Headers.Location!.ToString());
        Assert.Contains("O lote novo é obrigatório", refusalHtml);

        // Duplicação com lote novo: a ficha da NOVA ferramenta abre.
        var duplicated = await client.PostAsync($"{FichaPath}?handler=Duplicate", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("new_lot", "8"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, duplicated.StatusCode);
        var location = duplicated.Headers.Location?.ToString() ?? string.Empty;
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        Assert.Contains("created=1", location, StringComparison.Ordinal);
        Assert.NotEqual(toolId, location.Split("tool_id=")[1].Split('&')[0]);

        // A nova ficha mostra o lote novo com os factos e a geometria
        // herdados como defaults; a FONTE continua no registo.
        var newHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("CM · 5447 · lote 8", newHtml);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var source = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal("3", source.Lot);
            Assert.Equal(2, await db.Tools.AsNoTracking().CountAsync());
            Assert.Equal(2, await db.ToolTechnicalContexts.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task PostDuplicate_LotAlreadyRegistered_IsRefused_AndTheExistingGetsTheWarning()
    {
        // Duplicar para um lote já registado é recusado (18/10/2026; antes
        // abria a existente): a janela mostra a recusa com o link para a
        // existente, nada é copiado, e a existente fica com o aviso.
        SeedUser("4420", "Ferramentas Owner", "provider-ferr-dup-existing", "FerrPass11!");
        using var client = await LoginClientAsync("4420", "FerrPass11!");
        var toolId = await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "Tabc1234568", Type = ToolType.Cm, Reference = "5447", Lot = "8" });
            await db.SaveChangesAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=Duplicate", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("new_lot", " 8 "), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&duplicate_refusal=LotAlreadyExists&existing_tool_id=Tabc1234568", location);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Esse lote já está registado para esta referência numa das mesmas máquinas. Nenhuma duplicação foi feita", html);
        Assert.Contains("href=\"/Ferramentas/Ficha?tool_id=Tabc1234568\">Abrir a ferramenta existente</a>", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(2, await db.Tools.AsNoTracking().CountAsync());
            // Nada foi copiado para a existente (a fonte tem valores técnicos).
            Assert.Equal(1, await db.ToolTechnicalContexts.AsNoTracking().CountAsync());
            Assert.NotNull((await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "Tabc1234568")).DuplicateWarningAtUtc);
        }

        Assert.Contains("Possível ferramenta repetida", await GetPageHtmlAsync(client, $"{FichaPath}?tool_id=Tabc1234568"));
    }

    [Fact]
    public async Task PostCreate_EmptyLot_SavesNoLotWithWarning_AndASecondOneIsRefused()
    {
        // beta.63 (A12): o lote vazio grava «s/lote» e a ficha avisa; uma
        // segunda criação sem lote com o mesmo tipo e referência é a mesma
        // ferramenta e é recusada (ferramenta repetida, 18/10/2026).
        SeedUser("4422", "Ferramentas Owner", "provider-ferr-nolot", "FerrPass22!");
        using var client = await LoginClientAsync("4422", "FerrPass22!");
        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");

        var first = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "MF"), ("reference", "5810"), ("lot", ""), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var firstLocation = first.Headers.Location!.ToString();
        Assert.Contains("created=1", firstLocation, StringComparison.Ordinal);
        Assert.Contains("sem_lote=1", firstLocation, StringComparison.Ordinal);
        var toolId = firstLocation.Split("tool_id=")[1].Split('&')[0];

        var html = await GetPageHtmlAsync(client, firstLocation);
        Assert.Contains("MF · 5810 · lote s/lote", html);
        Assert.Contains("data-dmo-toast=\"aviso\" class=\"ferr-aviso warn\"", html);
        Assert.Contains("a ferramenta ficou gravada com «s/lote»", html);

        await RegistosAntigos.EnvelhecerAsync(_factory.Services);
        var second = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "MF"), ("reference", "5810"), ("lot", "  "), ("__RequestVerificationToken", token)));
        var secondLocation = second.Headers.Location!.ToString();
        Assert.Contains("create_refusal=ToolAlreadyExists", secondLocation, StringComparison.Ordinal);
        Assert.Contains($"existing_tool_id={toolId}", secondLocation, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var persisted = await db.Tools.AsNoTracking().SingleAsync();
        Assert.Equal("s/lote", persisted.Lot);
    }

    [Fact]
    public async Task PostCreate_WarningWithContextualReturn_OpensTheSheetWithAContinueLink()
    {
        // Com avisos, o regresso ao fluxo contextual passa pela ficha (o
        // aviso não se perde) e a ficha oferece continuar.
        SeedUser("4423", "Ferramentas Owner", "provider-ferr-nolot-return", "FerrPass23!");
        using var client = await LoginClientAsync("4423", "FerrPass23!");
        var token = await GetVerificationTokenAsync(client, IndexPath + "?criar=1");

        var response = await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
            ("type_token", "CM"), ("reference", "5447"), ("lot", ""), ("bq_quantity", "doze"),
            ("return", "/Controlo/Peso?cm={tool_id}"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/Ferramentas/Ficha?tool_id=T", location, StringComparison.Ordinal);
        var toolId = location.Split("tool_id=")[1].Split('&')[0];

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("a ferramenta ficou gravada com «s/lote»", html);
        Assert.Contains("A quantidade «doze» não é um número inteiro e ficou de fora", html);
        Assert.Contains($"href=\"/Controlo/Peso?cm={toolId}\"", html);
        Assert.Contains("Continuar no fluxo que abriu o Ferramentas", html);

        // Um continuar externo nunca vira link.
        var external = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&continuar=https%3A%2F%2Fexternal.example%2F");
        Assert.DoesNotContain("external.example", external);
    }

    [Fact]
    public async Task PostEditFacts_UnreadableQuantity_KeepsTheStoredOne_SavesTheRest_AndWarns()
    {
        // beta.63: a quantidade ilegível na edição mantém a gravada, grava
        // os outros factos e avisa; a negativa continua recusada.
        SeedUser("4425", "Ferramentas Owner", "provider-ferr-edit-qty", "FerrPass25!");
        using var client = await LoginClientAsync("4425", "FerrPass25!");
        var toolId = await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
            tool.BqAccountedQuantity = 120;
            await db.SaveChangesAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5448"), ("lot", "3"),
            ("bq_quantity", "doze"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.DoesNotContain("facts_refusal", location, StringComparison.Ordinal);
        Assert.Contains("quantidade_ilegivel=doze", location, StringComparison.Ordinal);
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("data-dmo-toast=\"aviso\" class=\"ferr-aviso warn\"", html);
        Assert.Contains("A quantidade «doze» não é um número inteiro: manteve-se a quantidade anterior", html);
        Assert.Contains("CM · 5448 · lote 3", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var persisted = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal("5448", persisted.Reference);
            Assert.Equal(120, persisted.BqAccountedQuantity);
        }

        var negative = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5449"), ("lot", "3"),
            ("bq_quantity", "-5"), ("__RequestVerificationToken", token)));
        Assert.Contains("facts_refusal=InvalidLotQuantity", negative.Headers.Location!.ToString(), StringComparison.Ordinal);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var persisted = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
            Assert.Equal("5448", persisted.Reference);
            Assert.Equal(120, persisted.BqAccountedQuantity);
        }
    }

    [Fact]
    public async Task PostEditFacts_EmptyLot_SavesNoLotWithWarning_CollisionLinksTheExistingTool()
    {
        SeedUser("4424", "Ferramentas Owner", "provider-ferr-edit-nolot", "FerrPass24!");
        using var client = await LoginClientAsync("4424", "FerrPass24!");
        var toolId = await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "Tabc1234569", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
            await db.SaveChangesAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", ""),
            ("mf_reference", "5811"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&sem_lote=1&guardado=geral", response.Headers.Location?.ToString());
        var html = await GetPageHtmlAsync(client, response.Headers.Location!.ToString());
        Assert.Contains("CM · 5447 · lote s/lote", html);
        Assert.Contains("data-dmo-toast=\"aviso\" class=\"ferr-aviso warn\"", html);
        Assert.Contains("ficou gravada com «s/lote»", html);
        Assert.Contains("5811", html);

        // A colisão com outra ferramenta continua recusada (B) e oferece abrir a existente.
        var collision = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", "Tabc1234569"), ("type_token", "CM"), ("reference", "5447"), ("lot", ""),
            ("__RequestVerificationToken", token)));
        var collisionLocation = collision.Headers.Location!.ToString();
        Assert.Contains("facts_refusal=LotAlreadyExists", collisionLocation, StringComparison.Ordinal);
        Assert.Contains($"existing_tool_id={toolId}", collisionLocation, StringComparison.Ordinal);
        var refusal = await GetPageHtmlAsync(client, collisionLocation);
        Assert.Contains("Já existe outra ferramenta com este tipo, referência e lote", refusal);
        Assert.Contains($"href=\"/Ferramentas/Ficha?tool_id={toolId}\"", refusal);
        Assert.Contains("Abrir a ferramenta existente", refusal);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("4", (await checkDb.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "Tabc1234569")).Lot);
    }

    [Fact]
    public async Task PostEditFacts_Refused_FormComesBackWithEverythingTyped_NothingSaved()
    {
        // Deep sweep, 18/10/2026, B-ferramentas-6: o lote que já existe
        // recusava e o formulário voltava com o gravado; perdiam-se a
        // quantidade e as máquinas escritas. Agora volta com tudo o que se
        // escreveu, para corrigir só o lote.
        SeedUser("4432", "Ferramentas Owner", "provider-ferr-b6", "FerrPassB6!");
        using var client = await LoginClientAsync("4432", "FerrPassB6!");
        var toolId = await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "Tabc1234570", Type = ToolType.Cm, Reference = "5447", Lot = "7" });
            var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
            tool.BqAccountedQuantity = 116;
            await db.SaveChangesAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        var response = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "7"),
            ("mf_reference", "5811"), ("bq_quantity", "999"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("facts_refusal=LotAlreadyExists", location, StringComparison.Ordinal);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Já existe outra ferramenta com este tipo, referência e lote", html);
        Assert.Contains("id=\"ferr-edit-lot\" name=\"lot\" value=\"7\"", html);
        Assert.Contains("name=\"mf_reference\" value=\"5811\"", html);
        Assert.Contains("inputmode=\"numeric\" value=\"999\"", html);
        // A B1 foi desmarcada: volta desmarcada.
        Assert.DoesNotMatch("name=\"machine_ids\" value=\"[0-9]+\"\\s+checked", html);
        // O cabeçalho da ficha continua a mostrar o que está gravado.
        Assert.Contains("CM · 5447 · lote 3", html);

        // Com máquinas marcadas, voltam marcadas.
        int machineId;
        using (var scope = _factory.Services.CreateScope())
            machineId = (await scope.ServiceProvider.GetRequiredService<DmoDbContext>().Machines.AsNoTracking().SingleAsync()).Id;
        var withMachine = await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("type_token", "CM"), ("reference", "5447"), ("lot", "7"),
            ("bq_quantity", "500"), ("machine_ids", machineId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("__RequestVerificationToken", token)));
        var withMachineHtml = await GetPageHtmlAsync(client, withMachine.Headers.Location!.ToString());
        Assert.Contains("inputmode=\"numeric\" value=\"500\"", withMachineHtml);
        Assert.Matches($"name=\"machine_ids\" value=\"{machineId}\"\\s+checked", withMachineHtml);

        // Sem recusa, o formulário mostra o gravado.
        var plain = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains("id=\"ferr-edit-lot\" name=\"lot\" value=\"3\"", plain);
        Assert.Contains("inputmode=\"numeric\" value=\"116\"", plain);
        Assert.Matches("name=\"machine_ids\" value=\"[0-9]+\"\\s+checked", plain);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        var persisted = await checkDb.Tools.AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal("3", persisted.Lot);
        Assert.Equal(116, persisted.BqAccountedQuantity);
        Assert.Null(persisted.AssociatedMfReference);
        Assert.Equal(1, await checkDb.ToolMachineLinks.CountAsync(l => l.ToolId == toolId));
    }

    [Fact]
    public async Task DeleteFlow_WarningShowsImpact_ExplicitConfirm_PRGToIndex_HistoryPreserved()
    {
        SeedUser("4421", "Ferramentas Owner", "provider-ferr-12", "FerrPass12!");
        using var client = await LoginClientAsync("4421", "FerrPass12!");
        var toolId = await SeedRealToolAsync();

        // Produção que referencia a tool + registo de consumo + a linha
        // DUMMY canónica (a migração #7 semeia-a no PostgreSQL; na seam
        // InMemory do host de testes semeia-se à mão, como ela).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = await db.Machines.AsNoTracking().SingleAsync();
            var jobOn = new JobOn { Reference = "5810", ProductionNumber = "J10", MachineId = machine.Id };
            db.JobOns.Add(jobOn);
            db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = toolId });
            db.Tools.Add(new Tool
            {
                ToolId = ToolRegistryConstants.RemovedToolId,
                Type = ToolType.Cm,
                Reference = "REMOVED",
                Lot = "REMOVED"
            });
            db.Pesos.Add(new Peso
            {
                CmToolId = toolId,
                BqToolId = toolId,
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 2.5m,
                VolumePuncaoCm3 = 1.4m,
                GlassDensityGramsPerCm3 = 2.5m
            });
            await db.SaveChangesAsync();
        }

        // (1) O aviso curto ANTES da confirmação (Diogo, 17/10/2026: «basta
        // um warning se quer mesmo eliminar») — o histórico nunca recusa.
        var warning = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&confirm_delete=1");
        Assert.Contains("Quer mesmo eliminar CM · 5447 · lote 3?", warning);
        Assert.DoesNotContain("Produções que a referem", warning);
        Assert.Contains(">Eliminar</button>", warning);
        Assert.Contains("<dialog id=\"ferr-delete-dialog\"", warning);
        Assert.Contains("data-dmo-dialog-aberta=\"1\"", warning);

        // (2) A confirmação EXPLÍCITA por POST (antiforgery): PRG à
        // lista com a nota de eliminação.
        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}&confirm_delete=1");
        var response = await client.PostAsync($"{FichaPath}?handler=Delete", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Ferramentas?deleted=1", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        var index = await GetPageHtmlAsync(client, response.Headers.Location!.ToString());
        Assert.Contains("Ferramenta eliminada", index);

        // (3) A tool desapareceu FISICAMENTE; a produção continua lá,
        // agora a apontar para a linha da ferramenta eliminada (a base
        // liga as produções às ferramentas desde a AddToolFksToProduction),
        // tal como o Peso.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Null(await db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.ToolId == toolId));
            var cmContext = await db.CmContexts.AsNoTracking().SingleAsync();
            Assert.Equal(ToolRegistryConstants.RemovedToolId, cmContext.ToolId);
            var peso = await db.Pesos.AsNoTracking().SingleAsync();
            Assert.Equal(ToolRegistryConstants.RemovedToolId, peso.CmToolId);
            Assert.Equal(ToolRegistryConstants.RemovedToolId, peso.BqToolId);
        }
    }
    [Fact]
    public async Task Registry_PagesTenOrTwentyRows_AndNavigationPreservesFiltersAndReturn()
    {
        SeedUser("4991", "Lista paginada", "paged-registry-provider", "PagePass1!");
        int machineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
            for (var i = 1; i <= 25; i++)
            {
                var tool = new Tool { ToolId = $"paged-{i:00}", Type = ToolType.Cm, Reference = "6018", Lot = i.ToString("00") };
                db.Tools.Add(tool);
                db.ToolMachineLinks.Add(new ToolMachineLink { Tool = tool, Machine = machine });
            }
            await db.SaveChangesAsync();
            machineId = machine.Id;
        }
        using var client = await LoginClientAsync("4991", "PagePass1!");
        var path = $"/Ferramentas?type=CM&reference=6018&machine_id={machineId}&return=%2FJobOn%3Fcm_tool_id%3D%7Btool_id%7D&pagina=2";
        var html = await client.GetStringAsync(path);
        Assert.Equal(10, System.Text.RegularExpressions.Regex.Matches(html, "name=\"ferr_tool\"").Count);
        Assert.Contains(">2 de 3</span>", html);
        var next = System.Text.RegularExpressions.Regex.Match(html, "href=\"([^\"]*)\"\\s+aria-label=\"Página seguinte\"").Groups[1].Value;
        next = WebUtility.HtmlDecode(next);
        Assert.Contains("type=CM", next);
        Assert.Contains("reference=6018", next);
        Assert.Contains($"machine_id={machineId}", next);
        Assert.Contains("pagina=3", next);
        Assert.Contains("return=", next);
        var last = await client.GetStringAsync(next);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(last, "name=\"ferr_tool\"").Count);
        var twenty = await client.GetStringAsync(path.Replace("pagina=2", "pagina=1&por_pagina=20"));
        Assert.Equal(20, System.Text.RegularExpressions.Regex.Matches(twenty, "name=\"ferr_tool\"").Count);
    }

    [Fact]
    public async Task CreateTool_UsesMachineDefaultWithoutJavaScript_AndKeepsManualOverride()
    {
        SeedUser("4992", "Quantidade sugerida", "quantity-registry-provider", "QuantityPass1!");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        await db.SaveChangesAsync();
        var key = ToolLotQuantityKeys.For(ToolType.Bq, "B1");
        db.ModuleSettings.Add(new DMO.Alpha.Core.Controlo.ModuleSetting { SettingKey = key, SettingValue = "120" });
        await db.SaveChangesAsync();
        try
        {
            using var client = await LoginClientAsync("4992", "QuantityPass1!");
            async Task<HttpResponseMessage> Create(string lot, string quantity, string manual)
            {
                var token = await GetVerificationTokenAsync(client, "/Ferramentas?criar=1");
                return await client.PostAsync("/Ferramentas?handler=Create", HtmlFormHelpers.BuildFormPost(
                    ("type_token", "BQ"), ("reference", "Y232"), ("lot", lot), ("bq_quantity", quantity),
                    ("quantity_manual", manual), ("machine_ids", b1.Id.ToString()), ("__RequestVerificationToken", token)));
            }
            Assert.Equal(HttpStatusCode.Redirect, (await Create("1", "", "0")).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Create("2", "110", "1")).StatusCode);
            db.ChangeTracker.Clear();
            Assert.Equal(120, (await db.Tools.SingleAsync(t => t.Lot == "1")).BqAccountedQuantity);
            Assert.Equal(110, (await db.Tools.SingleAsync(t => t.Lot == "2")).BqAccountedQuantity);
            var response = await client.GetAsync($"/Ferramentas?handler=LotQuantity&type=BQ&machine_ids={b1.Id}");
            response.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(120, json.RootElement.GetProperty("quantity").GetInt32());
            // beta.63 (A11): a quantidade ilegível grava a ferramenta sem
            // quantidade (nem a sugestão da máquina) e a ficha avisa.
            var unreadable = await Create("3", "bad", "1");
            var unreadableLocation = unreadable.Headers.Location!.OriginalString;
            Assert.DoesNotContain("InvalidLotQuantity", unreadableLocation);
            Assert.Contains("quantidade_ilegivel=bad", unreadableLocation);
            var warned = await client.GetStringAsync(unreadableLocation);
            Assert.Contains("data-dmo-toast=\"aviso\" class=\"ferr-aviso warn\"", warned);
            Assert.Contains("A quantidade «bad» não é um número inteiro e ficou de fora", warned);
            db.ChangeTracker.Clear();
            Assert.Null((await db.Tools.SingleAsync(t => t.Lot == "3")).BqAccountedQuantity);

            // A negativa continua recusada, com o valor preservado no form.
            var invalid = await Create("4", "-5", "1");
            Assert.Contains("InvalidLotQuantity", invalid.Headers.Location!.OriginalString);
            var draft = await client.GetStringAsync(invalid.Headers.Location);
            Assert.Contains("value=\"-5\"", draft);
            Assert.Equal(3, await db.Tools.CountAsync(t => t.Reference == "Y232"));
        }
        finally
        {
            db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey == key));
            await db.SaveChangesAsync();
        }
    }


    [Fact]
    public async Task GetFicha_Bq_ShowsOnlyBqTechnicalFields_AndPostKeepsCmValuesOffTheBq()
    {
        SeedUser("4498", "Ferramentas Owner", "provider-ferr-bq", "FerrPassB!");
        using var client = await LoginClientAsync("4498", "FerrPassB!");
        string toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = new Tool { ToolId = "Tbq20611400", Type = ToolType.Bq, Reference = "T206", Lot = "114" };
            db.Tools.Add(tool);
            db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = tool.ToolId, VolumeMarisaCm3 = 31.1m });
            await db.SaveChangesAsync();
            toolId = tool.ToolId;
        }

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}");
        Assert.Contains("Marisa (cm³)", html);
        Assert.Contains("<legend class=\"ferr-tech-titulo\">Gargalo<", html);
        Assert.DoesNotContain("name=\"process_token\"", html);
        Assert.DoesNotContain("name=\"volume_puncao\"", html);
        Assert.DoesNotContain("name=\"tp_diametro\"", html);
        Assert.DoesNotContain("<legend class=\"ferr-tech-titulo\">Tampão<", html);

        var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
        using var response = await client.PostAsync($"{FichaPath}?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", toolId), ("volume_marisa", "31.2"), ("volume_puncao", "99"), ("tp_diametro", "28.45"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var check = _factory.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<DmoDbContext>().ToolTechnicalContexts
            .AsNoTracking().SingleAsync(t => t.ToolId == toolId);
        Assert.Equal(31.2m, stored.VolumeMarisaCm3);
        Assert.Null(stored.VolumePuncaoCm3);
        Assert.Null(stored.TpDiametroMm);
    }

    [Fact]
    public async Task PostEditFacts_MudarOTipoDeUmaBqUsada_GravaEAvisaComLinkParaOEditar()
    {
        // Sweep de 19/10/2026, F-1: mudar o tipo de uma BQ já usada gravava
        // sem aviso. Continua a gravar, e a ficha avisa com as produções que
        // a usaram como BQ; voltar a BQ grava sem aviso.
        SeedUser("4441", "Ferramentas Owner", "provider-ferr-f1", "FerrPassF1!");
        using var client = await LoginClientAsync("4441", "FerrPassF1!");
        const string toolId = "Tf1b0000001";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
            db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "T194", Lot = "12" });
            var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = "J1", Machine = machine };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            db.BqContexts.Add(new BqContext { JobOnId = jobOn.Id, ToolId = toolId });
            await db.SaveChangesAsync();
        }

        async Task<HttpResponseMessage> EditarAsync(string tipo)
        {
            var token = await GetVerificationTokenAsync(client, $"{FichaPath}?tool_id={toolId}");
            return await client.PostAsync($"{FichaPath}?handler=EditFacts", HtmlFormHelpers.BuildFormPost(
                ("tool_id", toolId), ("type_token", tipo), ("reference", "T194"), ("lot", "12"),
                ("__RequestVerificationToken", token)));
        }

        var paraCm = await EditarAsync("CM");
        Assert.Equal(HttpStatusCode.Redirect, paraCm.StatusCode);
        var location = paraCm.Headers.Location!.ToString();
        Assert.Contains("tipo_anterior=BQ&producoes=1", location);
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("CM · T194 · lote 12", html);
        Assert.Contains("Esta ferramenta já foi usada em 1 produção como BQ: o tipo novo muda o que ela e as Boquilhas mostram. " +
            "Se foi engano, volte a pôr BQ em Editar: fica tudo como estava.", html);
        Assert.Contains("<a class=\"dmo-aviso-link\" href=\"#ferrEditar\">Corrigir em Editar</a>", html);

        var deVolta = await EditarAsync("BQ");
        Assert.Equal($"/Ferramentas/Ficha?tool_id={toolId}&guardado=geral", deVolta.Headers.Location!.ToString());
        // O link do aviso, aberto outra vez já com o tipo anterior, não avisa.
        Assert.DoesNotContain("Esta ferramenta já foi usada", await GetPageHtmlAsync(client, location));
    }

    [Fact]
    public async Task Ficha_CamposTecnicos_NoMaximoDuasCasas_SemZerosNoFim()
    {
        // Diogo, 25/10/2026: o Peso nominal aparecia «140.000» no campo da ficha.
        SeedUser("4476", "Ferramentas Owner", "provider-ferr-dec", "FerrPassDec1!");
        using var client = await LoginClientAsync("4476", "FerrPassDec1!");
        var toolId = await SeedRealToolAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tecnica = db.ToolTechnicalContexts.Single(t => t.ToolId == toolId);
            tecnica.NominalWeightGrams = 140.000m;
            tecnica.DiametroGargaloMm = 36.850m;
            await db.SaveChangesAsync();
        }

        var html = await GetPageHtmlAsync(client, $"{FichaPath}?tool_id={toolId}&tecnica=1");

        Assert.Contains("name=\"nominal_weight\" type=\"text\" inputmode=\"decimal\" value=\"140\"", html);
        Assert.Contains("name=\"diametro_gargalo\" type=\"text\" inputmode=\"decimal\" value=\"36.85\"", html);
        Assert.DoesNotContain("name=\"tp_diametro\"", html);
        Assert.DoesNotContain("140.000", html);
    }
}
