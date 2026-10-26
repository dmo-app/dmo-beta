using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova o slice vertical nativo do Registo de Boquilhas: o surface Razor
/// existente recebe um bq_repair_trace_id canónico (?trace_id=N),
/// renderiza factos persistidos a partir de dados do servidor, liga o POST de
/// movimento ao IRegisterBqMovementHandler (discrepância computada pelo
/// backend) e volta ao mesmo contexto canónico por redirect. O contexto
/// nativo usa a seam de conta existente: sem sessão autenticada não há
/// leitura nem mutação nativa. A memória/mock do browser não é fonte de
/// verdade deste caminho: os valores semeados existem apenas na persistência.
/// </summary>
public partial class BoquilhasRegistoPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Boquilhas";

    private readonly CustomWebApplicationFactory _factory;

    public BoquilhasRegistoPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.BqMovements.RemoveRange(db.BqMovements);
        db.BqRepairTraces.RemoveRange(db.BqRepairTraces);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Machines.RemoveRange(db.Machines);
        db.Tools.RemoveRange(db.Tools);
        db.SaveChanges();
    }

    private HttpClient CreateClient(WebApplicationFactory<Program>? factory = null) => (factory ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password, bool withOperationalAccess = true, bool withFerramentas = false)
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
        // Com o módulo Ferramentas, o catálogo mostra «Criar boquilha».
        var templateKey = withFerramentas ? "tpl-teste-operacional-ferramentas" : "tpl-teste-operacional";
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == templateKey);
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = templateKey,
                // O mesmo nome: o cabeçalho do HTML exportado para o browser
                // fica com a mesma largura.
                Name = "Operacional de teste",
                Capabilities = withFerramentas
                    ? new[] { DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate, DMO.Alpha.Core.Admin.DmoCapabilities.Boquilhas, DMO.Alpha.Core.Admin.DmoCapabilities.Ferramentas }
                    : new[] { DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate, DMO.Alpha.Core.Admin.DmoCapabilities.Boquilhas }
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
        var client = CreateClient(factory);
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

    private async Task<Tool> SeedToolAsync(string toolId, string reference, string lot)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var tool = new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = reference, Lot = lot };
        db.Tools.Add(tool);
        await db.SaveChangesAsync();
        return tool;
    }

    private async Task<BqRepairTrace> SeedTraceAsync(string? toolId, int? bqContextId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var trace = new BqRepairTrace { ToolId = toolId, BqContextId = bqContextId };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        return trace;
    }

    private async Task<BqContext> SeedProductionAsync(
        string toolId,
        string reference,
        string productionNumber,
        string machineCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn { Reference = reference, ProductionNumber = productionNumber, Machine = machine };
        var bq = new BqContext { JobOn = jobOn, ToolId = toolId };
        db.JobOns.Add(jobOn);
        db.BqContexts.Add(bq);
        await db.SaveChangesAsync();
        return bq;
    }

    private async Task SeedMovementAsync(int traceId, string movementType, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRegisterBqMovementHandler>();
        var outcome = await handler.HandleAsync(new RegisterBqMovement(traceId, movementType, quantity));
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
    }

    private async Task<List<BqMovement>> ReadMovementsAsync(int traceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == traceId)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    private string TraceUrl(int traceId) => $"{PagePath}?trace_id={traceId}";

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> PostMovementAsync(
        HttpClient client,
        string tokenPath,
        int traceId,
        string movementType,
        int quantity,
        string? occurredAt = null)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        var fields = new List<(string name, string value)>
        {
            ("trace_id", traceId.ToString()),
            ("movement_type", movementType),
            ("quantity", quantity.ToString())
        };
        if (occurredAt is not null)
        {
            fields.Add(("occurred_at", occurredAt));
        }
        fields.Add(("__RequestVerificationToken", token));
        return await client.PostAsync(
            $"{PagePath}?handler=RegisterMovement",
            HtmlFormHelpers.BuildFormPost(fields.ToArray()));
    }

    // ------------------------------------------------------------------
    // Canonical page context + read model
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegisterMovement_UserWithoutTemplate_IsRefusedAndNothingIsWritten()
    {
        // O template dá módulos: sem o módulo Boquilhas não se entra na
        // página nem corre nenhum handler; o pedido vai para o início.
        await SeedToolAsync("BQ-NATIVE-NOACCESS", "RGT990", "1");
        var trace = await SeedTraceAsync("BQ-NATIVE-NOACCESS");
        SeedUser("4190", "Sem Template", "provider-registo-noaccess", "RegistoPass90!", withOperationalAccess: false);
        using var client = await LoginClientAsync("4190", "RegistoPass90!");

        var page = await client.GetAsync(TraceUrl(trace.Id));
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/Modules/Home", page.Headers.Location?.ToString());

        var response = await PostMovementAsync(client, "/change-password", trace.Id, "saida", 5);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Modules/Home", response.Headers.Location?.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.BqMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CanonicalTraceId_LoadsPersistedTrace()
    {
        await SeedToolAsync("BQ-NATIVE-1", "RGT997", "77");
        var trace = await SeedTraceAsync("BQ-NATIVE-1");
        SeedUser("4100", "Registo Native", "provider-registo-1", "RegistoPass1!");
        using var client = await LoginClientAsync("4100", "RegistoPass1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        // Read model resolvido por bq_repair_trace_id canónico — a folha
        // NATIVA mostra os FATOS HUMANOS (a referência/lote, os derivados
        // no zero; os ids nunca na folha).
        // Exactly one module-functions nav: the Razor shell owns the tabs.
        // The Prototype presentation applies to actual BQ repair-trace data.
        Assert.Contains("css/dmo-boquilhas-prototype.css", html);
        Assert.Equal(1, html.Split("class=\"tabs dmo-secondary-nav\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("beta-production-rail", html);
        // O TAB «DEFINIÇÕES» na navegação É O ÚNICO acesso (o Owner 14/10:
        // «o botao definiçoes devia ser um tab») — o link contextual da
        // folha SAIU (a lição das duplicações: uma função, um caminho).
        Assert.DoesNotContain("href=\"/Admin/Definicoes?definition=boquilhas\"", html);
        Assert.Contains("Registo — BQ RGT997 · Lote 77", html);
        // O PAINEL DAS QUANTIDADES CALCULADAS (o html): «Em reparação» é o
        // legitimamente fora; sem lote contabilístico, «—» truthful.
        Assert.Contains("Em reparação</span><strong>0</strong>", html);
        Assert.Contains("Ainda sem movimentos neste registo.", html);

        // The three module views use the single shared shell navigation,
        // never an extra Prototype tab bar inside the Razor page.
        foreach (var url in new[] { "/Boquilhas?ficheiros=1", "/Boquilhas?historico=1" })
        {
            var viewHtml = await GetPageHtmlAsync(client, url);
            Assert.Equal(1, viewHtml.Split("class=\"tabs dmo-secondary-nav\"", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("href=\"/Admin/Definicoes?definition=boquilhas\"", viewHtml);
        }
    }

    [Fact]
    public async Task PersistedToolAndContextFacts_RenderFromServerData()
    {
        await SeedToolAsync("BQ-NATIVE-2", "RGT998", "78");
        var bq = await SeedProductionAsync("BQ-NATIVE-2", "ZQREF-99", "PN-550011", "B7");
        var trace = await SeedTraceAsync("BQ-NATIVE-2", bqContextId: bq.Id);
        SeedUser("4101", "Registo Native", "provider-registo-2", "RegistoPass2!");
        using var client = await LoginClientAsync("4101", "RegistoPass2!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        // Estes valores existem apenas na persistência semeada — nada de
        // fixtures de browser os fornece. A FOLHA NATIVA: os fatos humanos
        // (a referência da tool, os cards da produção pelo bq_id).
        Assert.Contains("Registo — BQ RGT998 · Lote 78", html);
        Assert.Contains("PN-550011", html);
        // Os cartões do registo (Diogo, 17/10/2026): a referência e a
        // produção em cartões; a máquina já não aparece no cabeçalho.
        Assert.Contains("<span>Referência</span><strong>ZQREF-99</strong>", html, StringComparison.Ordinal);
        Assert.Matches("<a class=\"dmo-fact dmo-fact-link\" href=\"/JobOn\\?jobon_id=\\d+#lightFicha\" title=\"Abrir o Job On desta produção\"><span>Produção</span><strong>PN-550011</strong></a>", html);
    }

    // ------------------------------------------------------------------
    // A GRAMÁTICA GLOBAL DAS TABELAS (o Owner 2026-10-12, verbatim:
    // «botoes vivem sempre fora, inverter cor ao selecionar um row, e
    // duplo click na row abre a funçao se existir»): a linha da produção
    // corrente SELECIONA no 1.º clique (o radio escondido, o valor = o
    // BqContextId — o ?bq_id do registo), a linha escolhida veste o
    // BOTÃO INVERTIDO (o padrão exato do Resumo) e o DUPLO CLIQUE abre
    // o registo. O 1-clique-navega morreu; as linhas «Sem produção»
    // ficam não-clicáveis.
    // ------------------------------------------------------------------

    [Fact]
    public async Task MachinesPanel_DirectLinkOpensProductionRegisterAndKeepsSearchCollapsed()
    {
        await SeedToolAsync("BQ-NATIVE-12", "RGT989", "88");
        var bq = await SeedProductionAsync("BQ-NATIVE-12", "ZQREF-97", "PN-550013", "B9");

        // Uma máquina SEM produção: a linha fica não-clicável.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Machines.Add(new Machine { Code = "B10" });
            db.SaveChanges();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = await db.Tools.SingleAsync(t => t.ToolId == "BQ-NATIVE-12");
            tool.BqAccountedQuantity = 120;
            var machine = await db.Machines.SingleAsync(m => m.Code == "B9");
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = tool.ToolId, MachineId = machine.Id });
            await db.SaveChangesAsync();
        }

        SeedUser("4109", "Registo Native", "provider-registo-12", "RegistoPass12!");
        using var client = await LoginClientAsync("4109", "RegistoPass12!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas");

        // Um clique seleciona a máquina; o duplo clique (ou o Enter) abre o registo.
        Assert.Contains($"data-dmo-row data-dmo-open=\"/Boquilhas?bq_id={bq.Id}\"", html);
        Assert.DoesNotContain("dmo-rail-link", html, StringComparison.Ordinal);
        Assert.Contains("beta-light-rail beta-production-rail", html, StringComparison.Ordinal);
        Assert.Contains("beta-production-lines", html, StringComparison.Ordinal);
        Assert.Contains("beta-production-rail-head", html, StringComparison.Ordinal);

        // A seta ▸ (o indício do atalho antigo) continua morta.
        Assert.DoesNotContain("▸", html, StringComparison.Ordinal);

        // «Sem produção»: o botão da linha fica sem ação (o estado truthful).
        var semIdx = html.IndexOf("<span>Sem produ", StringComparison.Ordinal);
        Assert.True(semIdx > 0, "A linha «Sem produção» não renderizou.");
        var rowStart = html.LastIndexOf("<button type=\"button\" class=\"bq-line-empty\"", semIdx, StringComparison.Ordinal);
        var rowEnd = html.IndexOf("</button>", semIdx, StringComparison.Ordinal);
        var semRow = html[rowStart..rowEnd];
        Assert.DoesNotContain("location.href", semRow, StringComparison.Ordinal);

        var registerHtml = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.Contains("<span>Quantidade do lote</span><strong>120</strong>", registerHtml);
        // A máquina vem da linha do painel lateral, escondida: sem o campo
        // «Máquina em uso» (Diogo, 22/10/2026); tipo, campos e Registar numa linha.
        Assert.Matches("<input type=\"hidden\" id=\"movementMachine\" name=\"movement_machine_id\" value=\"\\d+\" />", registerHtml);
        Assert.DoesNotContain("<select id=\"movementMachine\"", registerHtml);
        Assert.DoesNotContain("· compatível", registerHtml);
        Assert.Contains("<div class=\"bq-movement-line\">", registerHtml);
        var uiEvidence = Environment.GetEnvironmentVariable("DMO_UI_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(uiEvidence))
        {
            Directory.CreateDirectory(uiEvidence);
            File.WriteAllText(Path.Combine(uiEvidence, "registo.html"), registerHtml);
        }
        var formIndex = registerHtml.IndexOf("class=\"bq-movement-form\"", StringComparison.Ordinal);
        var metricsIndex = registerHtml.IndexOf("class=\"card bq-metrics-card\"", StringComparison.Ordinal);
        var latestIndex = registerHtml.IndexOf("class=\"card bq-latest-card\"", StringComparison.Ordinal);
        Assert.True(formIndex > 0 && metricsIndex > formIndex && latestIndex > metricsIndex);
        Assert.Contains("name=\"movement_type\" value=\"saida\" checked=\"checked\"", registerHtml);

        // Sem «Procurar / trocar de boquilha ou produção» (Diogo, 22/10/2026:
        // a BQ abre-se sempre pela produção, no painel lateral).
        Assert.DoesNotContain("bq-register-search", html);
        Assert.DoesNotContain("Procurar / trocar", registerHtml);

    }

    // ------------------------------------------------------------------
    // OS VALORES VIVOS DO CARD (blueprint «Live machine-card values»):
    // em casa · fora · discrepância — derivados do lote contabilístico
    // da Tool + os movimentos do trace aplicável; SEM produção corrente
    // a linha fica como está («Sem produção», sem valores).
    // ------------------------------------------------------------------

    [Fact]
    public async Task MachinesCard_ShowsTheThreeLiveValues_WhenTheCurrentBqExists()
    {
        await SeedToolAsync("BQ-NATIVE-13", "RGT988", "90");
        var bq = await SeedProductionAsync("BQ-NATIVE-13", "ZQREF-96", "PN-550014", "B4");

        // O LOTE CONTABILÍSTICO da Tool (120 — o fato da Tool; os
        // movimentos NUNCA o mutam).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var lotTool = await db.Tools.SingleAsync(t => t.ToolId == "BQ-NATIVE-13");
            lotTool.BqAccountedQuantity = 120;
            await db.SaveChangesAsync();
        }

        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 2);

        SeedUser("4110", "Registo Native", "provider-registo-13", "RegistoPass13!");
        using var client = await LoginClientAsync("4110", "RegistoPass13!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas");

        // O RAIL DO 31: a B10 com a sua BQ corrente — os fatos no botão.
        Assert.Contains("beta-production-rail-head", html, StringComparison.Ordinal);

        // OS VALORES VIVOS: no registo aberto (o desenho do 31 — o
        // lot-data do card do registo): o lote 120 com 2 legitimamente
        // fora; sem discrepância anotada → «—» (em branco, nunca zero).
        var registoHtml = await GetPageHtmlAsync(client, $"/Boquilhas?bq_id={bq.Id}");
        Assert.Contains("<span>Em reparação</span><strong>2</strong>", registoHtml);
        Assert.Contains("Discrepância acumulada", registoHtml);
    }

    [Fact]
    public async Task MachinesCard_WithoutCurrentProduction_StaysAsIs_NoLiveValues()
    {
        await SeedToolAsync("BQ-NATIVE-14", "RGT987", "91");
        await SeedProductionAsync("BQ-NATIVE-14", "ZQREF-95", "PN-550015", "B5");

        // Uma máquina SEM produção nenhuma — a linha «Sem produção».
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Machines.Add(new Machine { Code = "B11" });
            db.SaveChanges();
        }

        SeedUser("4111", "Registo Native", "provider-registo-14", "RegistoPass14!");
        using var client = await LoginClientAsync("4111", "RegistoPass14!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas");

        // A linha da B11 no rail das máquinas (#bqCurrentLines): diz «Sem
        // produção» e SEM os valores vivos de um lote que não está em máquina.
        var semRow = LinhaDoRail(html, "B11");
        Assert.Contains("Sem produção", semRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Fabricação", semRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Em reparação", semRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Discrepância", semRow, StringComparison.Ordinal);
    }

    /// <summary>A linha (data-dmo-row) de uma máquina no rail #bqCurrentLines.</summary>
    private static string LinhaDoRail(string html, string machineCode)
    {
        var inicio = html.IndexOf("id=\"bqCurrentLines\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "O rail das máquinas não aparece.");
        var rail = html[inicio..html.IndexOf("</aside>", inicio, StringComparison.Ordinal)];
        var linha = Regex.Matches(rail, "<button\\b[^>]*\\bdata-dmo-row\\b[\\s\\S]*?</button>")
            .Select(m => m.Value)
            .SingleOrDefault(l => l.Contains($">{machineCode}<", StringComparison.Ordinal));
        Assert.True(linha is not null, $"A linha da {machineCode} não aparece no rail.");
        return WebUtility.HtmlDecode(linha);
    }

    // ------------------------------------------------------------------
    // Movement POST through IRegisterBqMovementHandler
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostSaida_PersistsThroughHandler()
    {
        await SeedToolAsync("BQ-NATIVE-3", "RGT999", "79");
        var trace = await SeedTraceAsync("BQ-NATIVE-3");
        SeedUser("4102", "Registo Native", "provider-registo-3", "RegistoPass3!");
        using var client = await LoginClientAsync("4102", "RegistoPass3!");

        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 5);

        // PRG: volta ao mesmo contexto canónico do trace.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
                Assert.Contains($"trace_id={trace.Id}", location);

        var movements = await ReadMovementsAsync(trace.Id);
        var persisted = Assert.Single(movements);
        Assert.Equal(BqMovementType.Saida, persisted.Type);
        Assert.Equal(5, persisted.Quantity);
        Assert.Null(persisted.Discrepancy);

        // Re-render nativo a partir da persistência (sem JS/browser memory);
        // a linguagem dos movimentos em PT na folha e o painel das
        // quantidades calculadas do backend.
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("<td>Saída</td>", html);
        Assert.Contains("<span>Em reparação</span><strong>5</strong>", html);
    }

    [Fact]
    public async Task PostEntrada_ServerComputedDiscrepancyReflected()
    {
        await SeedToolAsync("BQ-NATIVE-4", "RGT994", "80");
        var trace = await SeedTraceAsync("BQ-NATIVE-4");
        await SeedMovementAsync(trace.Id, "saida", 5);
        SeedUser("4103", "Registo Native", "provider-registo-4", "RegistoPass4!");
        using var client = await LoginClientAsync("4103", "RegistoPass4!");

        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 7);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var movements = await ReadMovementsAsync(trace.Id);
        Assert.Equal(2, movements.Count);
        var entrada = movements[1];
        Assert.Equal(BqMovementType.Entrada, entrada.Type);
        Assert.Equal(7, entrada.Quantity);
        // Discrepância computada pelo backend; o browser nunca a envia.
        Assert.Equal(-2, entrada.Discrepancy);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("<td>Entrada</td>", html);
        // A discrepância é o link que abre a janela de pôr a zero (17/10/2026).
        Assert.Contains("<span>Discrepância acumulada</span><strong><a class=\"bq-reset-link\"", html);
        Assert.Contains(">-2</a></strong>", html);
        Assert.Contains("<span>Em reparação</span><strong>0</strong>", html);
    }

    [Fact]
    public async Task PostEntradaSemReparacao_PersistsWithClosedSemantics()
    {
        await SeedToolAsync("BQ-NATIVE-5", "RGT995", "81");
        var trace = await SeedTraceAsync("BQ-NATIVE-5");
        await SeedMovementAsync(trace.Id, "saida", 5);
        SeedUser("4104", "Registo Native", "provider-registo-5", "RegistoPass5!");
        using var client = await LoginClientAsync("4104", "RegistoPass5!");

        var response = await PostMovementAsync(
            client, TraceUrl(trace.Id), trace.Id, "entrada_sem_reparacao", 3);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var movements = await ReadMovementsAsync(trace.Id);
        Assert.Equal(2, movements.Count);
        var semReparacao = movements[1];
        // Token distinto persistido; contabilidade idêntica à entrada:
        // 3 explicados sobre 5 fora, sem discrepância.
        Assert.Equal(BqMovementType.EntradaSemReparacao, semReparacao.Type);
        Assert.Equal(3, semReparacao.Quantity);
        Assert.Null(semReparacao.Discrepancy);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(
            "<td>Entrada sem reparação</td>",
            html);
        Assert.Contains("<span>Em reparação</span><strong>2</strong>", html);
    }

    [Fact]
    public async Task Historico_ReparadorColumn_ShowsTheFrozenNameOfTheRoutedSaida()
    {
        // O CAMINHO VERTICAL COMPLETO do bloco dos reparadores: a saída
        // POSTada pela página NUNCA pergunta o reparador — o handler
        // resolve PELA ROTA (a máquina B1 do contexto de produção → a
        // config Boquilhas:RepairerB1) e CONGELA o nome no movimento; a
        // coluna «Reparador» do Histórico mostra O NOME CONGELADO (o
        // fallback — o nº do operador — só quando não há entidade).
        await SeedToolAsync("BQ-NATIVE-15", "RGT986", "92");
        var bq = await SeedProductionAsync("BQ-NATIVE-15", "ZQREF-94", "PN-550016", "B1");
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);

        // A ENTIDADE REPARADORA + a rota da máquina B1 para ela.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var repairer = new BqRepairer { Name = "Vidroart" };
            db.BqRepairers.Add(repairer);
            db.ModuleSettings.Add(new DMO.Alpha.Core.Controlo.ModuleSetting
            {
                SettingKey = BqRepairerRouting.SettingKeyFor("B1"),
                SettingValue = repairer.Id.ToString()
            });
            await db.SaveChangesAsync();
        }

        SeedUser("4112", "Registo Native", "provider-registo-15", "RegistoPass15!");
        using var client = await LoginClientAsync("4112", "RegistoPass15!");

        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 3);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // O movimento persistiu com o reparador CONGELADO pela rota.
        var movements = await ReadMovementsAsync(trace.Id);
        var saida = Assert.Single(movements);
        Assert.Equal(BqMovementType.Saida, saida.Type);
        Assert.NotNull(saida.BqRepairerId);
        Assert.Equal("Vidroart", saida.BqRepairerNameFrozen);

        // A coluna «Reparador» do Histórico (a TABELA dos movimentos do
        // dia) mostra O NOME CONGELADO (o fallback — o nº do operador — só
        // quando não há entidade). O histórico abre em branco (decisão de
        // 16/10): os movimentos só aparecem depois de uma pesquisa.
        var blank = await GetPageHtmlAsync(client, "/Boquilhas?historico=1");
        Assert.DoesNotContain("<td>Vidroart</td>", blank);
        Assert.Contains("Escolha um período ou pesquise para ver os movimentos.", blank);
        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=RGT986");
        Assert.Contains("<td>Vidroart</td>", html);

        // O FILTRO por reparador encontra a saída PELO NOME CONGELADO
        // (o fato humano — não o Id).
        var filtered = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&reparador=vidro");
        Assert.Contains("<td>Vidroart</td>", filtered);

        var other = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&reparador=inexistente");
        Assert.DoesNotContain("<td>Vidroart</td>", other);
    }

    // ------------------------------------------------------------------
    // Clean refusals
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnknownTrace_RefusedCleanly()
    {
        await SeedToolAsync("BQ-NATIVE-6", "RGT996", "82");
        SeedUser("4105", "Registo Native", "provider-registo-6", "RegistoPass6!");
        using var client = await LoginClientAsync("4105", "RegistoPass6!");

        var html = await GetPageHtmlAsync(client, TraceUrl(99999));
        Assert.Contains("O registo indicado não existe", html);
        Assert.Contains("O registo indicado não existe.", html);

        // POST a trace desconhecido: recusa tipada por query, nada persistido.
        var response = await PostMovementAsync(client, "/login", 99999, "saida", 5);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("movement_refusal=TraceNotFound", location);

        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("O registo indicado não existe. Nenhum movimento foi registado.", refusalHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.BqMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task UnknownMovementType_RefusedViaTypedRefusal()
    {
        await SeedToolAsync("BQ-NATIVE-7", "RGT993", "83");
        var trace = await SeedTraceAsync("BQ-NATIVE-7");
        SeedUser("4106", "Registo Native", "provider-registo-7", "RegistoPass7!");
        using var client = await LoginClientAsync("4106", "RegistoPass7!");

        // "Irreparável" não é token canónico e não é mapeado.
        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "Irreparável", 4);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("movement_refusal=UnknownMovementType", location);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Tipo de movimento desconhecido. Use saída, entrada ou entrada sem reparação.", html);
        Assert.Empty(await ReadMovementsAsync(trace.Id));
    }

    // ------------------------------------------------------------------
    // Immutability of Job On / BqContext
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostMovement_DoesNotModifyJobOnOrBqContext()
    {
        await SeedToolAsync("BQ-NATIVE-8", "RGT992", "84");
        var bq = await SeedProductionAsync("BQ-NATIVE-8", "ZQREF-98", "PN-550012", "B8");
        // Estado canónico pós-associação: âncora direta tool_id LIMPA (null) e
        // bq_id resolvido; a Tool alcança-se via bq_id → BqContext.ToolId.
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        SeedUser("4107", "Registo Native", "provider-registo-8", "RegistoPass8!");
        using var client = await LoginClientAsync("4107", "RegistoPass8!");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOnBefore = await db.JobOns.AsNoTracking().SingleAsync(j => j.Id == bq.JobOnId);
            Assert.Equal("ZQREF-98", jobOnBefore.Reference);
            Assert.Equal("PN-550012", jobOnBefore.ProductionNumber);
        }

        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 2);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOnAfter = await db.JobOns.AsNoTracking().SingleAsync(j => j.Id == bq.JobOnId);
            Assert.Equal("ZQREF-98", jobOnAfter.Reference);
            Assert.Equal("PN-550012", jobOnAfter.ProductionNumber);
            Assert.NotEqual(0, jobOnAfter.MachineId);

            var contextAfter = await db.BqContexts.AsNoTracking().SingleAsync(c => c.Id == bq.Id);
            Assert.Equal(bq.JobOnId, contextAfter.JobOnId);
            Assert.Equal("BQ-NATIVE-8", contextAfter.ToolId);

            var traceAfter = await db.BqRepairTraces.AsNoTracking().SingleAsync(t => t.Id == trace.Id);
            Assert.Equal(bq.Id, traceAfter.BqContextId);
            // Âncora direta permanece LIMPA (null); registar um movimento não
            // a repõe. A Tool continua alcançável via bq_id → BqContext.ToolId.
            Assert.Null(traceAfter.ToolId);

            Assert.Single(await db.BqMovements.AsNoTracking()
                .Where(m => m.BqRepairTraceId == trace.Id).ToListAsync());
        }
    }

    // ------------------------------------------------------------------
    // Server persistence is the source of truth (not browser memory)
    // ------------------------------------------------------------------

    [Fact]
    public async Task NativePath_ServerDataIsSourceOfTruth()
    {
        // Referência exclusiva da persistência: nenhum fixture/mock do
        // browser a contém; se renderiza, veio do servidor.
        await SeedToolAsync("BQ-NATIVE-9", "RGT-UNIQ-5501", "85");
        var trace = await SeedTraceAsync("BQ-NATIVE-9");
        SeedUser("4108", "Registo Native", "provider-registo-9", "RegistoPass9!");
        using var client = await LoginClientAsync("4108", "RegistoPass9!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("RGT-UNIQ-5501", html);

        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 9);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var movements = await ReadMovementsAsync(trace.Id);
        var persisted = Assert.Single(movements);

        // O re-render após o POST vem do GET seguinte ao redirect: apenas
        // dados persistidos, sem qualquer execução de JavaScript.
        var reloaded = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("<td>Saída</td>", reloaded);
        Assert.Contains("<span>Em reparação</span><strong>9</strong>", reloaded);
    }

    // ------------------------------------------------------------------
    // Existing account seam gates the native read and the native mutation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Anonymous_NativeTraceRead_RedirectedToLogin()
    {
        await SeedToolAsync("BQ-NATIVE-10", "RGT991", "86");
        var trace = await SeedTraceAsync("BQ-NATIVE-10");

        using var client = CreateClient();
        var response = await client.GetAsync(TraceUrl(trace.Id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/login", location);
        Assert.Contains("ReturnUrl=", location);
    }

    [Fact]
    public async Task Anonymous_MovementPost_RedirectedToLogin_NothingPersisted()
    {
        await SeedToolAsync("BQ-NATIVE-11", "RGT990", "87");
        var trace = await SeedTraceAsync("BQ-NATIVE-11");

        using var client = CreateClient();
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync(
            $"{PagePath}?handler=RegisterMovement",
            HtmlFormHelpers.BuildFormPost(
                ("trace_id", trace.Id.ToString()),
                ("movement_type", "saida"),
                ("quantity", "5"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? string.Empty);
        Assert.Empty(await ReadMovementsAsync(trace.Id));
    }

    // ------------------------------------------------------------------
    // A DATA/HORA REAL DO MOVIMENTO (o backfill dos ~2 meses — o Owner
    // 2026-10-14): o form traz o campo «Data e hora reais» pre-preenchido
    // com o agora (o equivalente ao «Data e hora reais *» do
    // boquilhas.html); o POST transporta-o em hora local e o handler grava
    // a data real em UTC com o CreatedAtUtc no agora técnico do registo;
    // sem o campo (o uso diário), o AGORA DO BACKEND manda — o
    // comportamento atual fica intacto.
    // ------------------------------------------------------------------

    [Fact]
    public async Task MovementForm_RendersTheRealDateField_PreFilledWithNow()
    {
        await SeedToolAsync("BQ-NATIVE-16", "RGT985", "93");
        var trace = await SeedTraceAsync("BQ-NATIVE-16");
        SeedUser("4113", "Registo Native", "provider-registo-16", "RegistoPass16!");
        using var client = await LoginClientAsync("4113", "RegistoPass16!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        // O EQUIVALENTE do form do boquilhas.html: um datetime-local
        // obrigatório, pre-preenchido com o agora do servidor.
        var match = Regex.Match(
            html,
            @"name=""occurred_at""[^>]*type=""datetime-local""[^>]*value=""(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})""");
        Assert.True(match.Success, "O campo «Data e hora reais» não renderizou pre-preenchido.");

        var rendered = DateTime.ParseExact(
            match.Groups[1].Value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        Assert.True(
            Math.Abs((rendered - TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"))).TotalMinutes) <= 2,
            $"O campo devia estar pre-preenchido com o agora; veio {match.Groups[1].Value}.");
    }

    [Theory]
    [InlineData("2026-08-15T09:15", "2026-08-15T08:15:00Z")]
    [InlineData("2026-01-15T09:15", "2026-01-15T09:15:00Z")]
    public async Task PostMovement_WithRetroactiveRealDate_PersistsItAndTheAuditStaysNow(string formValue, string expectedInstant)
    {
        await SeedToolAsync("BQ-NATIVE-17", "RGT984", "94");
        var trace = await SeedTraceAsync("BQ-NATIVE-17");
        SeedUser("4114", "Registo Native", "provider-registo-17", "RegistoPass17!");
        using var client = await LoginClientAsync("4114", "RegistoPass17!");

        // The operator enters factory time; the expected UTC instant is fixed
        // independently of the server timezone, in summer and winter.
        var expectedUtc = DateTime.Parse(expectedInstant, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        var beforeUtc = DateTime.UtcNow;
        var response = await PostMovementAsync(
            client, TraceUrl(trace.Id), trace.Id, "saida", 4, formValue);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var persisted = Assert.Single(await ReadMovementsAsync(trace.Id));

        // A DATA REAL do movimento persistiu (convertida a UTC pela folha).
        Assert.Equal(expectedUtc, persisted.OccurredAtUtc);

        // A AUDITORIA técnica continua no agora do registo — nunca na data
        // retroativa (o comportamento CreatedAtUtc=UtcNow mantém-se).
        Assert.InRange(persisted.CreatedAtUtc, beforeUtc, DateTime.UtcNow);
        Assert.True(
            persisted.CreatedAtUtc > expectedUtc.AddMinutes(1),
            "O CreatedAtUtc deve ficar no agora do registo, não na data real retroativa.");
    }

    // A hora da mudança de hora (março) já não recusa: passa para a hora
    // seguinte e avisa — ver BoquilhasAvisosTests.

    [Fact]
    public async Task PostMovement_WithoutTheRealDateField_GravaComOAgoraDoBackend()
    {
        // O USO DIÁRIO intacto: o POST sem occurred_at grava com o AGORA DO
        // BACKEND (o handler mantém o seu default — o uso diário nunca é
        // bloqueado nem reescrito pela data).
        await SeedToolAsync("BQ-NATIVE-18", "RGT983", "95");
        var trace = await SeedTraceAsync("BQ-NATIVE-18");
        SeedUser("4115", "Registo Native", "provider-registo-18", "RegistoPass18!");
        using var client = await LoginClientAsync("4115", "RegistoPass18!");

        var beforeUtc = DateTime.UtcNow;
        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 2);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var persisted = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.InRange(persisted.OccurredAtUtc, beforeUtc, DateTime.UtcNow);
        Assert.Equal(persisted.CreatedAtUtc, persisted.OccurredAtUtc);
    }

    // ------------------------------------------------------------------
    // O POST «Associar Job On a este registo» (o fim do ciclo do backfill
    // — o Owner 14/10: «criar um jobon, e poder associar um registo ao
    // mesmo»): o POST pede APENAS FATOS HUMANOS (o nº de produção + a
    // referência completa); a página resolve o bq_id e o handler existente
    // faz a associação; os movimentos e as datas mantêm-se; as recusas
    // tipadas informam com a causa em PT; um bq_id nunca recebe um
    // segundo trace.
    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostAssociateJobOnAsync(
        HttpClient client,
        string tokenPath,
        int traceId,
        string? jobNumber,
        string? jobReference)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        return await client.PostAsync(
            $"{PagePath}?handler=AssociateJobOn",
            HtmlFormHelpers.BuildFormPost(
                ("trace_id", traceId.ToString()),
                ("job_number", jobNumber ?? string.Empty),
                ("job_reference", jobReference ?? string.Empty),
                ("__RequestVerificationToken", token)));
    }

    // O formulário da página: a produção escolhida na lista vai pelo seu Job On.
    private async Task<HttpResponseMessage> PostAssociateChosenJobOnAsync(
        HttpClient client,
        string tokenPath,
        int traceId,
        int jobOnId)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        return await client.PostAsync(
            $"{PagePath}?handler=AssociateJobOn",
            HtmlFormHelpers.BuildFormPost(
                ("trace_id", traceId.ToString()),
                ("job_on_id", jobOnId.ToString()),
                ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task UnassociatedRegister_ShowsTheAssociateJobOnForm_AssociatedDoesNot()
    {
        await SeedToolAsync("BQ-ASSOC-0", "RGT982", "96");
        var trace = await SeedTraceAsync("BQ-ASSOC-0");
        SeedUser("4116", "Registo Native", "provider-registo-19", "RegistoPass19!");
        using var client = await LoginClientAsync("4116", "RegistoPass19!");

        // O registo PRÉ-PRODUÇÃO (sem bq_id): o 1.º passo, a referência.
        var unassociated = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("Associar Job On a este registo", unassociated);
        Assert.Contains("name=\"associar_ref\"", unassociated);
        Assert.DoesNotContain("name=\"job_on_id\"", unassociated);

        // O 2.º passo: com a referência, o select do nº e o POST. O tag
        // helper do form gera a action com o handler (o literal
        // asp-page-handler nunca chega ao HTML).
        var bq = await SeedProductionAsync("BQ-ASSOC-0", "ZQREF-96", "PN-550022", "B6");
        var withReference = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=ZQREF-96");
        Assert.Contains($"<option value=\"{bq.JobOnId}\">", withReference);
        Assert.Contains("PN-550022", withReference);
        Assert.Contains("handler=AssociateJobOn", withReference);
        // Escolher a produção não associa: associa o botão «Associar», no
        // canto do card (Diogo, 18/10/2026, pergunta 1 do deep sweep).
        Assert.Contains("<select id=\"jobNumber\" name=\"job_on_id\" required aria-label", withReference);
        Assert.Contains("form=\"bqAssociarJobOn\" class=\"btn primary\">Associar</button>", withReference);

        // O registo JÁ ASSOCIADO: o card da produção manda — sem o form.
        var associated = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        var associatedHtml = await GetPageHtmlAsync(client, TraceUrl(associated.Id));
        Assert.DoesNotContain("Associar Job On a este registo", associatedHtml);
    }

    [Fact]
    public async Task PostAssociateJobOn_WithHumanFacts_AssociatesAndKeepsMovementsAndDates()
    {
        // O CASO BACKFILL E2E: o registo (com movimento gravado) existe
        // ANTES da produção; o Job On cria-se DEPOIS (o fluxo existente);
        // o POST pelos fatos humanos associa — os movimentos e as datas
        // mantêm-se e o histórico passa a mostrar o nº/referência.
        await SeedToolAsync("BQ-ASSOC-1", "RGT981", "97");
        var trace = await SeedTraceAsync("BQ-ASSOC-1");
        await SeedMovementAsync(trace.Id, "saida", 2);

        // A produção criada DEPOIS do registo (o nº + a referência + a BQ
        // com o MESMO tool_id do trace).
        var bq = await SeedProductionAsync("BQ-ASSOC-1", "ZQREF-ASSOC", "PN-550023", "B2");
        SeedUser("4117", "Registo Native", "provider-registo-20", "RegistoPass20!");
        using var client = await LoginClientAsync("4117", "RegistoPass20!");

        var beforeUtc = DateTime.UtcNow;
        var response = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "PN-550023", "ZQREF-ASSOC");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"trace_id={trace.Id}", location);
        Assert.DoesNotContain("association_refusal", location);

        // OS MOVIMENTOS E AS DATAS MANTÊM-SE: o mesmo movimento, nada
        // recriado nem copiado; a associação só define o contexto.
        var movements = await ReadMovementsAsync(trace.Id);
        var persisted = Assert.Single(movements);
        Assert.Equal(BqMovementType.Saida, persisted.Type);
        Assert.Equal(2, persisted.Quantity);
        Assert.InRange(persisted.CreatedAtUtc, beforeUtc.AddSeconds(-1), DateTime.UtcNow);

        // O registo associado mostra o card da produção e o form sai.
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("PN-550023", html);
        Assert.DoesNotContain("Associar Job On a este registo", html);

        // O FIM DO «EM BRANCO ATÉ SER ASSOCIADO»: o Histórico (a TABELA dos
        // movimentos do dia) mostra o nº/referência da produção no
        // movimento. O histórico abre em branco (decisão de 16/10): pesquisa pela referência.
        var historico = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=RGT981");
        Assert.Contains("<td>PN-550023 · ZQREF-ASSOC</td>", historico);
    }

    [Fact]
    public async Task PostAssociateJobOn_RefusalsTypedWithPtCause()
    {
        await SeedToolAsync("BQ-ASSOC-2", "RGT980", "98");
        var trace = await SeedTraceAsync("BQ-ASSOC-2");
        SeedUser("4118", "Registo Native", "provider-registo-21", "RegistoPass21!");
        using var client = await LoginClientAsync("4118", "RegistoPass21!");

        // SEM O Nº DE PRODUÇÃO: recusa tipada informada, nada associado.
        var missing = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "", "");
        Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
        Assert.Contains("association_refusal=MissingFacts", missing.Headers.Location!.ToString());

        // PRODUÇÃO NÃO ENCONTRADA pelo nº: o Job On retroativo ainda não
        // existe — a causa em PT informa o caminho (criar o Job On e voltar).
        var notFound = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "999999", "ZQREF-NENHUMA");
        Assert.Equal(HttpStatusCode.Redirect, notFound.StatusCode);
        Assert.Contains("association_refusal=ProductionNotFound", notFound.Headers.Location!.ToString());

        var refusalHtml = await GetPageHtmlAsync(
            client, notFound.Headers.Location!.OriginalString);
        Assert.Contains(
            "Não encontrámos nenhuma produção com esse número.",
            refusalHtml);

        // Nada foi associado: o trace continua pré-produção.
        var traceAfter = await ReadTraceAsync(trace.Id);
        Assert.Null(traceAfter!.BqContextId);
        Assert.Equal("BQ-ASSOC-2", traceAfter.ToolId);
    }

    [Fact]
    public async Task PostAssociateJobOn_BqNotInProduction_RefusedByToolCorrelation()
    {
        // A BQ do registo não é a BQ deste Job On: a produção existe, mas
        // o seu contexto BQ pertence a OUTRA boquilha (a correlação é pelo
        // mesmo tool_id canónico).
        await SeedToolAsync("BQ-ASSOC-3", "RGT979", "99");
        var trace = await SeedTraceAsync("BQ-ASSOC-3");
        await SeedProductionAsync("BQ-OUTRA-BQ", "ZQREF-OUTRA", "PN-550024", "B3");
        SeedUser("4119", "Registo Native", "provider-registo-22", "RegistoPass22!");
        using var client = await LoginClientAsync("4119", "RegistoPass22!");

        var response = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "PN-550024", "ZQREF-OUTRA");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("association_refusal=BqNotInProduction", response.Headers.Location!.ToString());

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(
            "A boquilha deste registo não é uma das boquilhas dessa produção.",
            html);
    }

    [Fact]
    public async Task PostAssociateJobOn_AlreadyTracedBqContext_RefusedOneTracePerBq()
    {
        // UM BQ_ID NUNCA RECEBE UM SEGUNDO TRACE: a produção já tem o seu
        // registo — a recusa tipada manda abrir o registo existente e o
        // trace novo fica pré-produção, intocado.
        await SeedToolAsync("BQ-ASSOC-4", "RGT978", "100");
        var bq = await SeedProductionAsync("BQ-ASSOC-4", "ZQREF-4", "PN-550025", "B4");
        var existing = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        var fresh = await SeedTraceAsync("BQ-ASSOC-4");
        SeedUser("4120", "Registo Native", "provider-registo-23", "RegistoPass23!");
        using var client = await LoginClientAsync("4120", "RegistoPass23!");

        var response = await PostAssociateJobOnAsync(
            client, TraceUrl(fresh.Id), fresh.Id, "PN-550025", "ZQREF-4");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("association_refusal=BqContextAlreadyTraced", response.Headers.Location!.ToString());

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(
            "Essa produção já tem um registo de reparação.",
            html);

        // O trace novo mantém-se pré-produção (a âncora intacta, sem
        // bq_id); o registo existente da produção fica como está.
        var freshAfter = await ReadTraceAsync(fresh.Id);
        Assert.Null(freshAfter!.BqContextId);
        Assert.Equal("BQ-ASSOC-4", freshAfter.ToolId);

        var existingAfter = await ReadTraceAsync(existing.Id);
        Assert.Equal(bq.Id, existingAfter!.BqContextId);
    }

    private async Task<BqRepairTrace?> ReadTraceAsync(int traceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.BqRepairTraces.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == traceId);
    }

    // ------------------------------------------------------------------
    // A3 · O REGISTO E A FICHA AO TRAJE DO DMO-HTML: o painel das
    // quantidades calculadas (todos os valores do backend — a quantidade
    // contabilística da Tool e o fold do trace; a Fabricação é a subtração
    // feita no servidor), a tabela dos ÚLTIMOS MOVIMENTOS com a Data/hora
    // REAL e o reparador congelado, o painel de escolha de registo (com e
    // sem Job On) e a ficha da ferramenta com o «+ Registo com data
    // antiga». Os ids nunca no texto da folha.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegisterMetrics_RenderFromTheBackend_NeverFromTheClient()
    {
        // O PAINEL DO HTML: «Quantidade do lote — Fabricação — Em reparação —
        // Discrepância acumulada — calculadas para este lote e contexto,
        // com base na sequência de movimentos; não são stock editável».
        await SeedToolAsync("BQ-A3-METRIC", "RGT977", "101");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = await db.Tools.SingleAsync(t => t.ToolId == "BQ-A3-METRIC");
            tool.BqAccountedQuantity = 120;
            await db.SaveChangesAsync();
        }
        var trace = await SeedTraceAsync("BQ-A3-METRIC");
        await SeedMovementAsync(trace.Id, "saida", 3);
        SeedUser("4121", "Registo Native", "provider-registo-24", "RegistoPass24!");
        using var client = await LoginClientAsync("4121", "RegistoPass24!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        Assert.Contains("<span>Quantidade do lote</span><strong>120</strong>", html);
        Assert.Contains("<span>Fabricação</span><strong>117</strong>", html);
        Assert.Contains("<span>Em reparação</span><strong>3</strong>", html);
        Assert.Contains("<span>Discrepância acumulada</span><strong>—</strong>", html);
        Assert.Contains("Quantidades calculadas para este lote e contexto, com base na sequência de movimentos; não são stock editável.", html);

        // Sem quantidade contabilística: «—» em tudo o que depende dela —
        // nunca zero inventado.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool2 = new Tool { ToolId = "BQ-A3-SEMLOTE", Type = ToolType.Bq, Reference = "RGT976", Lot = "102" };
            db.Tools.Add(tool2);
            await db.SaveChangesAsync();
        }
        var trace2 = await SeedTraceAsync("BQ-A3-SEMLOTE");
        var semLote = await GetPageHtmlAsync(client, TraceUrl(trace2.Id));
        Assert.Contains("<span>Quantidade do lote</span><strong>—</strong>", semLote);
        Assert.Contains("<span>Fabricação</span><strong>—</strong>", semLote);

        // Sem a quantidade total do lote aparece o aviso âmbar com o caminho
        // para a ficha (Diogo, 18/10/2026); com ela, não.
        Assert.Contains("id=\"bqLotQuantityMissing\"", semLote);
        // A quantidade do lote serve a contagem da reparação (Diogo, 22/10/2026).
        Assert.Contains("Falta a quantidade total do lote desta BQ. Sem ela, a contagem da reparação não é calculada.", semLote);
        Assert.DoesNotContain("a Fabricação não é calculada", semLote);
        // O link para a ficha só aparece a quem tem o módulo Ferramentas e
        // a ficha volta a este registo (Diogo, 19/10/2026: _DmoAvisoLink).
        Assert.DoesNotContain("href=\"/Ferramentas/Ficha?tool_id=BQ-A3-SEMLOTE", semLote);
        Assert.DoesNotContain("id=\"bqLotQuantityMissing\"", html);

        SeedUser("4122", "Registo Ferramentas", "provider-registo-25", "RegistoPass25!", withFerramentas: true);
        using var comFerramentas = await LoginClientAsync("4122", "RegistoPass25!");
        var comLink = await GetPageHtmlAsync(comFerramentas, TraceUrl(trace2.Id));
        Assert.Contains($"<a class=\"dmo-aviso-link\" href=\"/Ferramentas/Ficha?tool_id=BQ-A3-SEMLOTE&amp;continuar=%2FBoquilhas%3Ftrace_id%3D{trace2.Id}\">Preencher na ficha da ferramenta</a></div>", comLink);
    }

    [Fact]
    public async Task MachineInUse_ListsOnlyTheMachinesOfTheTool_AndQuantityComesFirst()
    {
        // Diogo, 18/10/2026: «se ela só trabalha na C2 como está na ficha da
        // ferramenta não deveria conseguir escolher B2».
        await SeedToolAsync("BQ-MAQ-C2", "RGT970", "110");
        int c2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var c2 = new Machine { Code = "QC2" };
            db.Machines.AddRange(c2, new Machine { Code = "QB2" });
            await db.SaveChangesAsync();
            c2Id = c2.Id;
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-MAQ-C2", MachineId = c2.Id });
            await db.SaveChangesAsync();
        }
        var trace = await SeedTraceAsync("BQ-MAQ-C2");
        SeedUser("4131", "Registo Máquinas", "provider-registo-maq", "RegistoMaq1!");
        using var client = await LoginClientAsync("4131", "RegistoMaq1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        // Sem o campo «Máquina em uso» (Diogo, 22/10/2026): a máquina vem da
        // linha do painel lateral; «Registar» é o primeiro, antes da «Saída».
        Assert.DoesNotContain("for=\"movementMachine\"", html);
        Assert.DoesNotContain("<select id=\"movementMachine\"", html);
        Assert.Contains("<input type=\"hidden\" id=\"movementMachine\" name=\"movement_machine_id\" />", html);
        Assert.True(Posicao(html, ">Registar</button>") < Posicao(html, "<span>↗ Saída</span>"),
            "O Registar vem antes da Saída.");
        Assert.True(Posicao(html, "<span>↗ Saída</span>") < Posicao(html, "for=\"quantity\""));
    }

    [Fact]
    public async Task LastMovements_ShowTheRealDateAndTheFrozenRepairer()
    {
        // O facto humano do html («Cada movimento mantém o lote, a máquina,
        // a data e o reparador») — a coluna Data/hora REAL (o occurredAt da
        // A1) e o reparador CONGELADO da saída pela rota. A MÁQUINA B77 é
        // exclusiva deste teste (a rota Boquilhas:RepairerB77 não colide
        // com as sementes dos outros testes).
        await SeedToolAsync("BQ-A3-DATA", "RGT975", "103");
        var bq = await SeedProductionAsync("BQ-A3-DATA", "ZQREF-A3", "PN-550026", "B77");
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var repairer = new BqRepairer { Name = "Vidroart" };
            db.BqRepairers.Add(repairer);
            db.ModuleSettings.Add(new DMO.Alpha.Core.Controlo.ModuleSetting
            {
                SettingKey = BqRepairerRouting.SettingKeyFor("B77"),
                SettingValue = repairer.Id.ToString()
            });
            await db.SaveChangesAsync();
        }

        var localNow = DmoBusinessTime.ToLocal(DateTime.UtcNow);
        var retroLocal = DateTime.SpecifyKind(
            new DateTime(localNow.Year, localNow.Month, localNow.Day, 8, 5, 0).AddDays(-45),
            DateTimeKind.Unspecified);
        var retroUtc = DmoBusinessTime.ToUtc(retroLocal);

        using (var scope = _factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IRegisterBqMovementHandler>();
            var outcome = await handler.HandleAsync(
                new RegisterBqMovement(trace.Id, "saida", 3, null, retroUtc));
            Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        }

        SeedUser("4122", "Registo Native", "provider-registo-25", "RegistoPass25!");
        using var client = await LoginClientAsync("4122", "RegistoPass25!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));

        Assert.Contains("Últimos movimentos desta boquilha / produção", html);
        Assert.Contains("Mais recentes primeiro", html);
        // A DATA/HORA REAL (formatada em pt-PT, a hora local do servidor —
        // a mesma conversão que a folha faz).
        var expectedDate = TimeZoneInfo.ConvertTimeFromUtc(retroUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"))
            .ToString("g", System.Globalization.CultureInfo.GetCultureInfo("pt-PT"));
        Assert.Contains($"<td>{expectedDate}</td>", html);
        // O REPARADOR CONGELADO e o SALDO em branco (a saída normal).
        Assert.Contains("<td>Vidroart</td>", html);
        Assert.Contains("O Saldo só aparece numa Entrada excecional", html);
    }

    [Fact]
    public async Task ToolSheet_ShowsTheFactsTheHistoricalFormAndTheRegisters()
    {
        // A FICHA DA FERRAMENTA (o toolsTab do html): os fatos, o «+ Registo
        // com data antiga» (a máquina como facto de leitura + a data/hora
        // do primeiro movimento) e os registos da BQ.
        await SeedToolAsync("BQ-A3-FICHA", "RGT973", "105");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = await db.Tools.SingleAsync(t => t.ToolId == "BQ-A3-FICHA");
            tool.BqAccountedQuantity = 60;
            await db.SaveChangesAsync();
            var machine = new Machine { Code = "B3" };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "BQ-A3-FICHA", MachineId = machine.Id });
            await db.SaveChangesAsync();
        }
        var bq = await SeedProductionAsync("BQ-A3-FICHA", "ZQREF-FICHA", "PN-550028", "B3");
        var associated = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        await SeedMovementAsync(associated.Id, "saida", 2);
        SeedUser("4124", "Registo Native", "provider-registo-27", "RegistoPass27!");
        using var client = await LoginClientAsync("4124", "RegistoPass27!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1&ferramenta=BQ-A3-FICHA");

        Assert.Contains("Ficha da boquilha — RGT973 · Lote 105", html);
        Assert.Contains("Máquinas compatíveis: B3", html);
        Assert.Contains("<span>Quantidade do lote</span><strong>60</strong>", html);
        // Sem o «+ Registo com data antiga» (abria o Registo, que é só para
        // um Job On): os movimentos sem Job On ficam na ficha (Diogo, 22/10/2026).
        Assert.DoesNotContain("Recuperar movimentos antigos", html);
        Assert.DoesNotContain("handler=OpenHistoricalRegister", html);
        Assert.Contains("<h3>Movimentos sem Job On", html);
        Assert.Contains("handler=RegistarSemJobOn", html);
        // A tabela dos registos da ficha (o strong da linha da produção).
        Assert.Contains("<td><strong>Produção PN-550028 · ZQREF-FICHA</strong></td>", html);
        // OS IDS NUNCA NO TEXTO: o tool_id cru não aparece na folha.
        Assert.DoesNotContain(">BQ-A3-FICHA<", html);
        Assert.DoesNotContain("BQ-A3-FICHA</", html);
    }

    [Fact]
    public async Task CatalogRows_OpenTheToolSheetOnDoubleClick()
    {
        await SeedToolAsync("BQ-A3-CARD", "RGT971", "107");
        SeedUser("4126", "Registo Native", "provider-registo-29", "RegistoPass29!");
        using var client = await LoginClientAsync("4126", "RegistoPass29!");

        // A lista abre vazia; só aparece depois de procurar.
        var empty = await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1");
        Assert.DoesNotContain("ferramenta=BQ-A3-CARD", empty);
        Assert.Contains("id=\"bq-tool-results\" hidden", empty);

        var html = await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1&q=RGT971");

        // A gramática da casa nas linhas: o duplo clique/Enter abre a
        // ficha (o tool_id só no URL — escapado).
        // Diogo, 26/10/2026: o clique na boquilha abre a Ficha da ferramenta.
        Assert.Contains(
            "data-dmo-open=\"/Ferramentas/Ficha?tool_id=BQ-A3-CARD\"",
            html, StringComparison.Ordinal);
        Assert.Contains("Cada ferramenta BQ/lote tem uma ficha única, reutilizável em vários Job Ons", html);
    }

    [Fact]
    public async Task Historico_ShowsTheDayMovementsTable_AndTheSidepanelSurvivesTheTabs()
    {
        // OS ACHADOS DO OWNER AO VIVO (14/10): «quando mexes nos tabs
        // desaparece a informaçao do sidepanel» + «o historico de movimentos
        // esta horrivel, deveria ser uma tabela e nao desse jeito parece
        // estar esprimida». O SIDEPANEL É FIXO: a vista Histórico serve as
        // correntes como as outras (o painel lateral persiste entre os
        // tabs); os movimentos do dia são TABELA da gramática da casa.
        await SeedToolAsync("BQ-A3-HIST", "RGT970", "108");
        var bq = await SeedProductionAsync("BQ-A3-HIST", "ZQREF-HIST", "PN-550029", "B9");
        var trace = await SeedTraceAsync(toolId: null, bqContextId: bq.Id);
        await SeedMovementAsync(trace.Id, "saida", 2);
        SeedUser("4127", "Registo Native", "provider-registo-30", "RegistoPass30!");
        using var client = await LoginClientAsync("4127", "RegistoPass30!");

        // O histórico abre em branco (decisão de 16/10): o sidepanel e o
        // formulário ficam; os movimentos esperam por uma pesquisa.
        var blank = await GetPageHtmlAsync(client, "/Boquilhas?historico=1");
        Assert.Contains("beta-production-rail", blank);
        Assert.Contains("Escolha um período ou pesquise para ver os movimentos.", blank);
        Assert.DoesNotContain("<td>Saída</td>", blank);
        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=RGT970");

        // O SIDEPANEL FIXO: a informação das correntes está NA VISTA
        // HISTÓRICO (o painel lateral não desaparece ao mudar de tab).
        Assert.Contains("beta-production-rail", html);
        Assert.Contains("da produção PN-550029 na B9", html);

        // A TABELA dos movimentos do dia (nunca os cards espremidos) com as
        // colunas do facto humano do html.
        Assert.Contains("<th>Data/hora</th>", html);
        Assert.Contains("<th>Boquilha / lote</th>", html);
        Assert.Contains("<th>Reparador</th>", html);
        Assert.Contains("<td>Saída</td>", html);
        Assert.DoesNotContain("movements-list", html);

        // E COM ?dia explícito o calendário compõe-se na mesma (a grelha
        // nunca colapsa — o dia escolhido veste-se no calendário).
        var comDia = await GetPageHtmlAsync(client, $"/Boquilhas?historico=1&dia={DmoBusinessTime.Now.Day}");
        Assert.Contains("Mês anterior", comDia);
        Assert.Contains("<th>Data/hora</th>", comDia);
    }
    [Fact]
    public async Task RegisterMovement_ChosenMachineIsSavedAndShownInTheRegister()
    {
        SeedUser("4981", "Máquina escolhida", "machine-choice-provider", "ChoicePass1!");
        await SeedToolAsync("BQ-CHOICE", "Y232", "66");
        var trace = await SeedTraceAsync("BQ-CHOICE");
        int machineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "C3" };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();
            machineId = machine.Id;
        }
        using var client = await LoginClientAsync("4981", "ChoicePass1!");
        var token = await GetVerificationTokenAsync(client, $"/Boquilhas?trace_id={trace.Id}");
        var response = await client.PostAsync("/Boquilhas?handler=RegisterMovement", HtmlFormHelpers.BuildFormPost(
            ("trace_id", trace.Id.ToString()), ("movement_type", "saida"), ("quantity", "4"),
            ("movement_machine_id", machineId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal("C3", (await scope.ServiceProvider.GetRequiredService<DmoDbContext>().BqMovements.SingleAsync()).MachineCodeFrozen);
        }
        var html = await client.GetStringAsync($"/Boquilhas?trace_id={trace.Id}");
        // Sem o campo «Máquina em uso» (Diogo, 22/10/2026); a máquina gravada aparece no registo.
        Assert.DoesNotContain("Máquina em uso", html);
        Assert.Contains("<td>C3</td>", html);
    }

}
