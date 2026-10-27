using System.Globalization;
using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova AS VISTAS DO HISTÓRICO das Boquilhas (a A4 — o historyTab do
/// dmo-html): a GRELHA do resumo diário · 6 máquinas (dia na vertical,
/// B1..C3 na horizontal, cada registo separado quando há mudança, E/S/SR
/// com zeros omitidos) servida pelo read model do backend — nada
/// calculado no cliente; os contadores do filtro (as somas de quantidade,
/// não saldos de stock); os filtros do html (a máquina, o tipo, o
/// reparador, a pesquisa) sobre a data REAL; a lista dos movimentos
/// detalhados em TABELA com o duplo clique a abrir o registo; o sidepanel
/// fixo; os placeholders do PDF (o gerador é a A5 — nesta tarefa não se
/// constroem geradores). Os ids vivem no URL/hidden — nunca no texto.
/// </summary>
public class BoquilhasHistoricoPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Boquilhas";

    private readonly CustomWebApplicationFactory _factory;

    public BoquilhasHistoricoPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.RemoveRange(db.ModuleSettings);
        db.BqRepairers.RemoveRange(db.BqRepairers);
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

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);
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

        // O módulo Boquilhas vem do template.
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-boquilhas-historico");
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-boquilhas-historico",
                Name = "Boquilhas de teste",
                Capabilities = new[] { DMO.Alpha.Core.Admin.DmoCapabilities.Boquilhas }
            };
            db.Templates.Add(template);
        }
        db.SaveChanges();

        db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment
        {
            UserId = user.Id,
            TemplateId = template.Id
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password)
    {
        var client = CreateClient();
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

    /// <summary>A produção com a TOOL canónica (os factos da grelha vêm da
    /// Tool — a referência/lote da célula) + a máquina + o Job On + o
    /// bq_id + o trace associado (a âncora limpa — o estado pós-A2).</summary>
    private async Task<BqRepairTrace> SeedProductionAsync(
        string toolId,
        string machineCode,
        string reference,
        string productionNumber,
        string lot = "12")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = machineCode };
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            Machine = machine
        };
        var bq = new BqContext { JobOn = jobOn, ToolId = toolId };
        db.Tools.Add(new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = reference,
            Lot = lot
        });
        db.JobOns.Add(jobOn);
        db.BqContexts.Add(bq);
        // A GRAVAÇÃO EM DUAS FASES (o padrão da casa nos testes da A3): o
        // bq_id emitido pelo backend É REAL antes do trace o referenciar —
        // o trace nasce associado (a âncora direta limpa, o estado pós-A2).
        await db.SaveChangesAsync();
        var trace = new BqRepairTrace { ToolId = null, BqContextId = bq.Id };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        return trace;
    }

    private async Task<int> SeedMovementAsync(
        int traceId, string movementType, int quantity, DateTime? occurredUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRegisterBqMovementHandler>();
        var outcome = await handler.HandleAsync(new RegisterBqMovement(
            traceId, movementType, quantity, null, occurredUtc));
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        Assert.NotNull(outcome.Movement);
        return outcome.Movement!.MovementId;
    }

    [Fact]
    public async Task DailySummaryGrid_RendersDayByMachine_FromTheBackend()
    {
        // A GRELHA do resumo diário: dia na vertical, B1..C3 na horizontal;
        // a célula = o registo com E/S/SR (zeros omitidos) e o JO/Sem Job
        // On; o duplo clique abre o registo (o id só no URL).
        var traceA = await SeedProductionAsync("BQ-A4-A", "B1", "9389T194", "202601");
        await SeedMovementAsync(traceA.Id, "saida", 2);
        await SeedMovementAsync(traceA.Id, "entrada", 1);
        var traceB = await SeedProductionAsync("BQ-A4-B", "C2", "5447T173", "202602", "24/33");
        await SeedMovementAsync(traceB.Id, "saida", 3);
        SeedUser("4200", "Histórico A4", "provider-hist-1", "HistPass1!");
        using var client = await LoginClientAsync("4200", "HistPass1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=matriz");

        // A GRELHA: o cabeçalho do html com as 6 máquinas.
        Assert.Contains("<th>Dia</th>", html);
        foreach (var machine in new[] { "B1", "B2", "B3", "C1", "C2", "C3" })
        {
            Assert.Contains($"<th>{machine}</th>", html);
        }
        Assert.Contains("Resumo diário — B1, B2, B3, C1, C2, C3", html);

        // A CÉLULA do registo A na B1: a referência, o JO e as quantidades de
        // BQs (saiu 2, entrou 1); o SR de 0 não aparece; o tfoot mostra E e S
        // sempre, o SR só quando existe.
        Assert.Contains(">9389T194</strong>", html);
        Assert.Contains("BQ 9389T194 · Lote 12 · JO 202601", html);
        Assert.Contains("entry-e\">E 1</span>", html);
        Assert.Contains("entry-s\">S 2</span>", html);
        Assert.DoesNotContain("entry-r\">SR 0", html);
        Assert.DoesNotContain("entry-r\">SR 0</span>", html);
        Assert.Contains("entry-e\">E 0</span>", html); // o tfoot do html
        Assert.Contains("<strong>Total</strong>", html);

        // A CÉLULA do registo B na C2: a grelha mostra quantidades de BQs,
        // como o PDF (o proprietário, 16/10/2026), não o número de movimentos.
        Assert.Contains("BQ 5447T173 · Lote 24/33 · JO 202602", html);
        Assert.Contains("entry-s\">S 3</span>", html);

        // O DUPLO CLIQUE abre o registo (o trace_id só no URL).
        Assert.Contains($"data-dmo-open=\"/Boquilhas?trace_id={traceA.Id}\"", html, StringComparison.Ordinal);

        // A LEGENDA do html aterrada — e VESTE OS MESMOS SELOS da grelha (a A4
        // repair: o Owner pediu cor nos selos E/S/SR; se a legenda continuar em
        // prosa sem cor, o operador descodifica a grelha à sorte. A letra e a
        // cor da legenda são as mesmas da célula, nunca uma tradução à parte).
        Assert.Contains("<span class=\"entry-e\">E</span> Entradas de reparação", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"entry-s\">S</span> Saídas", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"entry-r\">SR</span> Entradas sem reparação", html, StringComparison.Ordinal);
        // Todos os dias do período aparecem, mesmo sem movimentos (como a versão antiga).
        Assert.Equal(DateTime.DaysInMonth(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.Year, DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.Month) + 1,
            System.Text.RegularExpressions.Regex.Matches(html, "<td><strong>(\\d\\d/\\d\\d|Total)</strong>").Count);

        // O PERÍODO da grelha (o matrixPeriod).
        Assert.Contains("Período:", html);

        // OS IDS NUNCA NO TEXTO: o tool_id cru não aparece.
        Assert.DoesNotContain("BQ-A4-A", html);
    }

    [Fact]
    public async Task FilterCounters_AreQuantitySums_NotStockBalances()
    {
        // «Totais de movimentos do filtro, não saldos de stock»: as somas
        // de QUANTIDADE por tipo sobre a lista filtrada.
        var traceA = await SeedProductionAsync("BQ-A4-C", "B2", "RGT960", "202603");
        await SeedMovementAsync(traceA.Id, "saida", 3);
        await SeedMovementAsync(traceA.Id, "entrada", 2);
        await SeedMovementAsync(traceA.Id, "entrada_sem_reparacao", 1);
        SeedUser("4201", "Histórico A4", "provider-hist-2", "HistPass2!");
        using var client = await LoginClientAsync("4201", "HistPass2!");

        // O histórico abre em branco (decisão de 16/10): os movimentos só
        // carregam depois de uma pesquisa ou de um filtro.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&pesquisa=RGT960");

        Assert.Contains("<span>Saídas</span><strong>3</strong>", html);
        Assert.Contains("<span>Entradas</span><strong>2</strong>", html);
        Assert.Contains("<span>Entradas sem reparação</span><strong>1</strong>", html);
        Assert.Contains("3 movimentos", html);
        Assert.Contains("Totais de movimentos do filtro, não saldos de stock.", html);
    }

    [Fact]
    public async Task Filters_TypeMachineTextAndRange_WorkOverTheRealDate()
    {
        // OS FILTROS do html sobre a DATA REAL: o tipo, a máquina (o
        // select B1..C3) e a pesquisa de texto.
        var traceA = await SeedProductionAsync("BQ-A4-D", "B1", "9389T194", "202604");
        var traceB = await SeedProductionAsync("BQ-A4-E", "B2", "5447T173", "202605");
        await SeedMovementAsync(traceA.Id, "saida", 2);
        await SeedMovementAsync(traceA.Id, "entrada", 1);
        await SeedMovementAsync(traceB.Id, "saida", 4);
        SeedUser("4202", "Histórico A4", "provider-hist-3", "HistPass3!");
        using var client = await LoginClientAsync("4202", "HistPass3!");

        // O TIPO: só as saídas (2 registos, 2 linhas).
        var saidas = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&tipo=saida");
        Assert.Equal(2, saidas.Split("<td>Saída</td>", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("<td>Entrada</td>", saidas);

        // A MÁQUINA (o select): só a B1.
        var b1 = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&maquina=B1");
        Assert.Contains("<td>9389T194", b1);
        Assert.DoesNotContain("<td>5447T173", b1);

        // A PESQUISA de texto: a referência (o case-insensitive).
        var pesquisa = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&pesquisa=9389t194");
        Assert.Contains("<td>9389T194", pesquisa);
        Assert.DoesNotContain("<td>5447T173", pesquisa);

        // O INTERVALO (desde/até): amanhã → a tabela fica vazia com a
        // recusa informada da folha (nunca bloqueia).
        var amanha = DmoBusinessTime.Now.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&desde={amanha}");
        Assert.Contains("Nenhum movimento encontrado com os filtros selecionados.", vazio);
    }

    [Fact]
    public async Task DetailedMovements_AreATableWithTheHumanFacts_AndTheDoubleClickOpens()
    {
        // A LISTA DOS MOVIMENTOS DETALHADOS em TABELA da casa — nunca os
        // cards espremidos (o Owner 14/10) — com as colunas do facto
        // humano e o duplo clique a abrir o REGISTO (o trace_id, nunca o
        // id do movimento).
        var traceA = await SeedProductionAsync("BQ-A4-F", "B3", "9389T194", "202606");
        var movementId = await SeedMovementAsync(traceA.Id, "saida", 2);
        SeedUser("4203", "Histórico A4", "provider-hist-4", "HistPass4!");
        using var client = await LoginClientAsync("4203", "HistPass4!");

        // O histórico abre em branco (decisão de 16/10): os movimentos só
        // carregam depois de uma pesquisa ou de um filtro.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&pesquisa=9389T194");

        // A TABELA (as colunas do facto humano do html).
        Assert.Contains("<th>Data/hora</th>", html);
        Assert.Contains("<th>Boquilha / lote</th>", html);
        Assert.Contains("<th>Movimento</th>", html);
        Assert.Contains("<th>Qtd.</th>", html);
        Assert.Contains("<th>Reparador</th>", html);
        Assert.Contains("<th>Linha</th>", html);
        Assert.Contains("<th>Produção</th>", html);
        Assert.Contains("<th>Saldo</th>", html);

        // A LINHA: a produção em branco até ser associada — associada,
        // mostra o nº · a referência; um clique abre a ficha da ferramenta
        // (Diogo, 18/10/2026), não o registo dos movimentos.
        Assert.Contains("<td>202606 · 9389T194</td>", html);
        Assert.Contains("data-dmo-open=\"/Ferramentas/Ficha?tool_id=BQ-A4-F\"", html, StringComparison.Ordinal);

        Assert.DoesNotContain("movements-list", html);

        // A AVULSA (sem produção): a produção «—» (em branco até ser
        // associada — o uso do html).
        var traceAvulso = await SeedTraceAvulsoAsync("BQ-A4-F");
        await SeedMovementAsync(traceAvulso.Id, "saida", 1);
        var comAvulso = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&pesquisa=RGT950");
        Assert.Contains("<td>—</td>", comAvulso);
    }

    private async Task<BqRepairTrace> SeedTraceAvulsoAsync(string toolId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
        tool.Reference = "RGT950";
        await db.SaveChangesAsync();
        var trace = new BqRepairTrace { ToolId = toolId, BqContextId = null };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        return trace;
    }

    [Fact]
    public async Task MonthJump_RebuildsTheCalendarAndTheMatrixPeriod()
    {
        // O SELETOR DE MÊS (o histMonthQuick do html): o calendário e a
        // grelha saltam para o mês pedido.
        var traceA = await SeedProductionAsync("BQ-A4-G", "B1", "9389T194", "202607");
        await SeedMovementAsync(traceA.Id, "saida", 2, DmoBusinessTime.ToUtc(
            DateTime.SpecifyKind(new DateTime(2026, 8, 20, 9, 0, 0), DateTimeKind.Unspecified)));
        SeedUser("4204", "Histórico A4", "provider-hist-5", "HistPass5!");
        using var client = await LoginClientAsync("4204", "HistPass5!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=matriz&mes_ano=2026-08");

        // O CALENDÁRIO (o shared da casa) no mês pedido (um link antigo com
        // mes_ano; o seletor saiu da página a 18/10) + a grelha com o movimento do dia real (20/08) na
        // B1 (a célula mostra S 2, a quantidade de BQs; os zeros são
        // omitidos na célula; o tfoot mostra E e S sempre).
        Assert.Contains("name=\"mes\" value=\"8\"", html);
        Assert.Contains(">20/08</strong>", html);
        Assert.Contains("S 2", html);
        Assert.Contains("Período: 01/08/2026 — 31/08/2026", html);
    }

    [Fact]
    public async Task ViewDropdown_AndContextualPdfButton_AreWiredToTheGenerators()
    {
        // A DECISÃO DO OWNER 14/10 (verbatim: «basta ter dropdown com as
        // vistas que quer, e sim se quiser criar pdf»): UM DROPDOWN com as
        // vistas (extensível) + O ÚNICO botão «Imprimir / PDF (filtros
        // atuais)» — sempre o handler PdfHistorico, que segue a vista e os
        // filtros: a lista dos movimentos ou a grelha.
        var traceA = await SeedProductionAsync("BQ-A5-BTN", "B1", "9389T194", "202609");
        await SeedMovementAsync(traceA.Id, "saida", 2);
        SeedUser("4207", "Histórico A4", "provider-hist-8", "HistPass8!");
        using var client = await LoginClientAsync("4207", "HistPass8!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");

        // Três vistas, num dropdown «Vista» antes da referência (Diogo,
        // 18/10): «Por linha / Job On» (a predefinida), «Resumo diário» e
        // «Movimentos detalhados» (de volta a 19/10). Sem «Selecionar mês».
        Assert.Matches("<select id=\"vista\" name=\"vista\" data-dmo-autosubmit>\\s*<option value=\"grupos\" selected=\"selected\">Por linha / Job On</option>", html);
        Assert.Contains("<option value=\"matriz\">Resumo diário · 6 máquinas</option>", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("id=\"vista\"", StringComparison.Ordinal) < html.IndexOf("id=\"pesquisa\"", StringComparison.Ordinal));
        Assert.DoesNotContain("bq-history-tab", html);
        Assert.DoesNotContain("Selecionar mês", html);
        Assert.Contains("<option value=\"movimentos\">Movimentos detalhados</option>", html, StringComparison.Ordinal);
        // O ÚNICO botão de PDF (sem os 3 botões de vistas do descarte).
        Assert.Contains("Imprimir / PDF (filtros atuais)", html);
        Assert.DoesNotContain("PDF · Grelha", html);
        Assert.DoesNotContain("PDF · Máquina", html);
        Assert.DoesNotContain("PDF · Mês", html);
        // O routing da vista movimentos sem máquina: o PDF do mês.
        Assert.Contains("handler=PdfHistorico", html, StringComparison.Ordinal);

        // A VISTA MATRIZ: o dropdown vestido na matriz e o PDF da grelha.
        var matriz = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=matriz");
        Assert.Contains("<option value=\"matriz\" selected=\"selected\">Resumo diário · 6 máquinas</option>", matriz, StringComparison.Ordinal);
        Assert.Contains("vista=matriz", matriz, StringComparison.Ordinal);
        Assert.Contains("handler=PdfHistorico", matriz, StringComparison.Ordinal);

        // COM a máquina no filtro: o PDF de uma máquina (o fato, nunca um id).
        var comMaquina = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&maquina=B1");
        Assert.Contains("handler=PdfHistorico", comMaquina, StringComparison.Ordinal);
        Assert.Contains("maquina=B1", comMaquina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdfHistorico_MatrixView_ReturnsTheGridPdf()
    {
        // O PDF DA GRELHA: o botão «Imprimir / PDF» na vista matriz usa o
        // MESMO read model da vista e o gerador SÓ FORMATA — o papel sai
        // com Saiu/entrou por dia×máquina.
        var traceA = await SeedProductionAsync("BQ-A5-GRELHA", "B1", "9389T194", "202610");
        await SeedMovementAsync(traceA.Id, "saida", 21);
        await SeedMovementAsync(traceA.Id, "entrada", 21);
        SeedUser("4208", "Histórico A4", "provider-hist-9", "HistPass9!");
        using var client = await LoginClientAsync("4208", "HistPass9!");

        var response = await client.GetAsync($"{PagePath}?historico=1&vista=matriz&handler=PdfHistorico");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("BOQUILHAS-matriz.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        Assert.True(bytes.Length > 1000, "o PDF tem corpo");
    }

    [Fact]
    public async Task MachineFilterWithoutPeriod_ShowsOnlyTheMostRecent_AndTheListAndPdfAgree()
    {
        // Só «Máquina B1», sem período (18/10/2026): a lista não lê todos os
        // movimentos de sempre; mostra os 500 mais recentes e explica na dica.
        var trace = await SeedProductionAsync("BQ-LIMITE", "B1", "9389T194", "202612");
        var start = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            for (var i = 0; i < DMO.Alpha.Web.Pages.Boquilhas.IndexModel.MaxListMovements + 5; i++)
            {
                db.BqMovements.Add(new BqMovement
                {
                    BqRepairTraceId = trace.Id,
                    Type = BqMovementType.Saida,
                    Quantity = 1,
                    MachineCodeFrozen = "B1",
                    OccurredAtUtc = start.AddHours(i),
                    CreatedAtUtc = start.AddHours(i)
                });
            }

            await db.SaveChangesAsync();
        }

        SeedUser("4231", "Histórico limite", "provider-hist-31", "HistPass31!");
        using var client = await LoginClientAsync("4231", "HistPass31!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&maquina=B1"));
        Assert.Contains($"Os {DMO.Alpha.Web.Pages.Boquilhas.IndexModel.MaxListMovements} mais recentes", html);
        Assert.Contains("Escolha um período para ver os anteriores.", html);

        // Com um período escolhido, a lista é a do período, sem limite.
        var period = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&maquina=B1&desde=2025-01-01&ate=2025-12-31"));
        Assert.Contains($"{DMO.Alpha.Web.Pages.Boquilhas.IndexModel.MaxListMovements + 5} movimentos", period);

        var pdf = await client.GetAsync($"{PagePath}?historico=1&handler=PdfHistorico&maquina=B1");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PdfHistorico_WithTheMachineFilter_ReturnsTheMachinePdf()
    {
        // O PDF DE UMA MÁQUINA (o Owner: «um para apenas mostrar uma
        // maquina»): o PDF dos filtros atuais com a máquina escolhida.
        var traceA = await SeedProductionAsync("BQ-A5-MAQ", "B1", "9389T194", "202611");
        await SeedMovementAsync(traceA.Id, "saida", 2);
        SeedUser("4209", "Histórico A4", "provider-hist-10", "HistPass10!");
        using var client = await LoginClientAsync("4209", "HistPass10!");

        var response = await client.GetAsync($"{PagePath}?historico=1&handler=PdfHistorico&maquina=B1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("BOQUILHAS-grupos.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
    }

    [Fact]
    public async Task PdfHistorico_WithoutTheMachine_ExportsAllMachines()
    {
        // Sem máquina no filtro, o PDF sai com os movimentos de todas as
        // máquinas — nunca recusa.
        var traceA = await SeedProductionAsync("BQ-A5-TODAS", "B2", "9389T195", "202613");
        await SeedMovementAsync(traceA.Id, "saida", 2);
        SeedUser("4210", "Histórico A4", "provider-hist-11", "HistPass11!");
        using var client = await LoginClientAsync("4210", "HistPass11!");

        var response = await client.GetAsync($"{PagePath}?historico=1&handler=PdfHistorico");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("BOQUILHAS-grupos.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
    }

    [Fact]
    public void ClampMatrixPeriod_CutsAtTheFirst366DaysAndExplains()
    {
        var start = new DateOnly(2025, 1, 1);

        // 366 dias cabem: sem corte nem aviso.
        var fits = DMO.Alpha.Web.Pages.Boquilhas.IndexModel.ClampMatrixPeriod(start, start.AddDays(365));
        Assert.Equal((start, start.AddDays(365)), (fits.Start, fits.End));
        Assert.Null(fits.Warning);

        // Mais de 366: corta no 366.º dia e diz até onde mostrou.
        var cut = DMO.Alpha.Web.Pages.Boquilhas.IndexModel.ClampMatrixPeriod(start, new DateOnly(2026, 6, 30));
        Assert.Equal(start, cut.Start);
        Assert.Equal(new DateOnly(2026, 1, 1), cut.End);
        Assert.Equal(
            "O resumo diário mostra no máximo 366 dias. O período pedido ia até 30/06/2026; " +
            "mostramos de 01/01/2025 a 01/01/2026. Para ver o resto, escolha um intervalo a partir de 02/01/2026.",
            cut.Warning);
    }

    [Fact]
    public async Task Matrix_LongerThan366Days_ShowsTheFirst366AndWarns()
    {
        SeedUser("4250", "Histórico A4", "provider-hist-250", "HistPass250!");
        using var client = await LoginClientAsync("4250", "HistPass250!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=matriz&desde=2025-01-01&ate=2026-06-30");

        Assert.Contains("Período: 01/01/2025 — 01/01/2026", html);
        Assert.Contains("<div class=\"jobon-aviso warn\" role=\"status\" style=\"margin:12px 20px\">O resumo diário mostra no máximo 366 dias.", html);
        Assert.Contains("data-dmo-list=\"Resumo diário por máquina\"", html);
        Assert.DoesNotContain("Seleciona um intervalo menor", html);
    }

    [Fact]
    public async Task PdfHistorico_MatrixLongerThan366Days_GeneratesTheFirst366Days()
    {
        SeedUser("4251", "Histórico A4", "provider-hist-251", "HistPass251!");
        using var client = await LoginClientAsync("4251", "HistPass251!");

        var historico = await client.GetAsync($"{PagePath}?handler=PdfHistorico&historico=1&vista=matriz&desde=2025-01-01&ate=2026-06-30");
        Assert.Equal(HttpStatusCode.OK, historico.StatusCode);
        Assert.Equal("application/pdf", historico.Content.Headers.ContentType?.MediaType);
        var bytes = await historico.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
    }

    [Fact]
    public async Task PdfHistorico_ForAMonth_ReturnsTheMonthPdf()
    {
        // O PDF DE UM MÊS (o Owner: «outro para mostrar movimentos num
        // mes»): o PDF dos filtros atuais com o período do mês.
        var traceA = await SeedProductionAsync("BQ-A5-MES", "B1", "9389T194", "202612");
        await SeedMovementAsync(traceA.Id, "saida", 3, DmoBusinessTime.ToUtc(
            DateTime.SpecifyKind(new DateTime(2026, 8, 20, 9, 0, 0), DateTimeKind.Unspecified)));
        SeedUser("4211", "Histórico A4", "provider-hist-12", "HistPass12!");
        using var client = await LoginClientAsync("4211", "HistPass12!");

        var response = await client.GetAsync($"{PagePath}?historico=1&handler=PdfHistorico&desde=2026-08-01&ate=2026-08-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        Assert.True(bytes.Length > 1000, "o PDF do mês tem corpo");
    }

    [Fact]
    public async Task PdfHistorico_WithoutPeriodOrFilter_ExportsTheCalendarMonth()
    {
        // Teste de carga (17/10/2026): sem período nem filtro, o PDF exportava
        // todos os movimentos de sempre (808 páginas com 5 anos de dados). Agora
        // sai o mês do calendário; os meses antigos ficam de fora.
        var trace = await SeedProductionAsync("BQ-PDF-MES", "B1", "9389T196", "202614");
        static DateTime Lisboa(int y, int m, int d) => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(new DateTime(y, m, d, 9, 0, 0), DateTimeKind.Unspecified),
            DMO.Alpha.Core.Runtime.DmoBusinessTime.Zone);
        await SeedMovementAsync(trace.Id, "saida", 1, Lisboa(2026, 8, 20));
        for (var i = 0; i < 80; i++)
        {
            await SeedMovementAsync(trace.Id, i % 2 == 0 ? "saida" : "entrada", 1, Lisboa(2025, 3, 1 + i % 28));
        }
        SeedUser("4212", "Histórico A4", "provider-hist-13", "HistPass13!");
        using var client = await LoginClientAsync("4212", "HistPass13!");

        static async Task<int> PagesAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = System.Text.Encoding.Latin1.GetString(await response.Content.ReadAsByteArrayAsync());
            return System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page[^s]").Count;
        }

        var oldMonth = await PagesAsync(await client.GetAsync(
            $"{PagePath}?historico=1&handler=PdfHistorico&desde=2025-03-01&ate=2025-03-31"));
        var calendarMonth = await PagesAsync(await client.GetAsync(
            $"{PagePath}?historico=1&handler=PdfHistorico&vista=grupos&ano=2026&mes=8"));

        Assert.True(oldMonth > 1, $"80 movimentos ocupam mais de uma página ({oldMonth})");
        Assert.Equal(1, calendarMonth);
    }

    [Fact]
    public async Task Sidepanel_SurvivesTheHistoryViews()
    {
        // O SIDEPANEL É FIXO (o Owner 14/10): a informação das correntes
        // está na vista do histórico, matriz ou detalhada.
        var traceA = await SeedProductionAsync("BQ-A4-H", "B1", "9389T194", "202608");
        await SeedMovementAsync(traceA.Id, "saida", 1);
        SeedUser("4206", "Histórico A4", "provider-hist-7", "HistPass7!");
        using var client = await LoginClientAsync("4206", "HistPass7!");

        foreach (var url in new[]
                 {
                     $"{PagePath}?historico=1",
                     $"{PagePath}?historico=1&vista=matriz"
                 })
        {
            var html = await GetPageHtmlAsync(client, url);
            Assert.Contains("beta-production-rail", html);
            Assert.Contains("da produção 202608 na B1", html);
        }
    }

    [Fact]
    public async Task DetailedMovements_AEntradaSemReparacao_NuncaVaiCruParaOEcran()
    {
        // A A4/A5 repair (o Owner 14/10 apanhou «EntradaSemReparacao» cru no
        // PDF). O buraco era de VOCABULÁRIO, não de acentos: o
        // BqToolHistoryQuery entrega o NOME do enum (mov.Type.ToString()) e a
        // folha só conhecia os TOKENS de armazenamento (entrada_sem_reparacao).
        // Os nomes de uma só palavra — Saida, Entrada — passavam por sorte
        // porque minúsculos batem certo; o de três palavras caía no fallback
        // e ia CRU para o <td>. Prova pelos dois lados: o rótulo certo
        // presente E o cru ausente (só o primeiro não apanhava a regressão).
        var trace = await SeedProductionAsync("BQ-A4-SR", "C1", "9389T194", "202609");
        await SeedMovementAsync(trace.Id, "saida", 2);
        await SeedMovementAsync(trace.Id, "entrada_sem_reparacao", 1);
        SeedUser("4213", "Histórico A4", "provider-hist-14", "HistPass14!");
        using var client = await LoginClientAsync("4213", "HistPass14!");

        // O histórico abre em branco (decisão de 16/10): os movimentos só
        // carregam depois de uma pesquisa ou de um filtro.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&pesquisa=9389T194");

        // OS RÓTULOS em PT da casa, nas duas palavras que sobreviveram e na que
        // rebentava.
        Assert.Contains("<td>Saída</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>Entrada sem reparação</td>", html, StringComparison.Ordinal);

        // O CRU NUNCA aparece — nem como rótulo, nem escondido no markup.
        Assert.DoesNotContain("EntradaSemReparacao", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>Saida</td>", html, StringComparison.Ordinal);
    }
    [Fact]
    public async Task GroupedHistory_KeepsLotsSeparateAndUsesFrozenMovementMachine()
    {
        var traceA = await SeedProductionAsync("BQ-GROUP-A", "B1", "T173", "202601", "8");
        var traceB = await SeedProductionAsync("BQ-GROUP-B", "C2", "T173", "202601", "9");
        var at = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        await SeedMovementAsync(traceA.Id, "saida", 4, at);
        var moved = await SeedMovementAsync(traceA.Id, "entrada", 2, at.AddHours(1));
        await SeedMovementAsync(traceB.Id, "saida", 7, at);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.BqMovements.SingleAsync(m => m.Id == moved)).MachineCodeFrozen = "C3";
            await db.SaveChangesAsync();
        }
        SeedUser("4291", "Histórico agrupado", "provider-group-1", "HistGroup1!");
        using var client = await LoginClientAsync("4291", "HistGroup1!");
        // O histórico abre em branco (decisão de 16/10): os movimentos só
        // carregam depois de uma pesquisa ou de um filtro.
        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=grupos&ano=2026&mes=10&desde=2026-10-12&ate=2026-10-12");
        Assert.Contains("<input type=\"hidden\" name=\"vista\" value=\"grupos\" />", html);
        Assert.Contains("Máquina C3", html);
        Assert.Contains("BQ T173 · Lote 8", html);
        Assert.Contains("BQ T173 · Lote 9", html);
        // Um clique na linha abre a ficha da ferramenta (Diogo, 18/10/2026).
        Assert.Contains("data-dmo-open=\"/Ferramentas/Ficha?tool_id=BQ-GROUP-A\"", html);
        Assert.Contains("data-dmo-open=\"/Ferramentas/Ficha?tool_id=BQ-GROUP-B\"", html);
        var response = await client.GetAsync("/Boquilhas?handler=PdfHistorico&historico=1&vista=grupos&maquina=C3&desde=2026-10-12&ate=2026-10-12");
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..5]));
    }

    [Fact]
    public async Task DetailedMovementsView_ListsOneMovementPerLine_WithoutGroupHeaders_AndItsPdf()
    {
        // «Movimentos detalhados» de volta (Diogo, 19/10/2026): a terceira
        // vista, um movimento por linha, sem os cabeçalhos por máquina e Job On.
        var traceA = await SeedProductionAsync("BQ-DET-A", "B1", "T173", "202601", "8");
        var traceB = await SeedProductionAsync("BQ-DET-B", "C2", "T173", "202601", "9");
        var at = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        await SeedMovementAsync(traceA.Id, "saida", 4, at);
        await SeedMovementAsync(traceA.Id, "entrada", 2, at.AddHours(1));
        await SeedMovementAsync(traceB.Id, "saida", 7, at);
        SeedUser("4293", "Histórico detalhado", "provider-det-1", "HistDet1!");
        using var client = await LoginClientAsync("4293", "HistDet1!");

        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=movimentos&ano=2026&mes=10&desde=2026-10-12&ate=2026-10-12");

        Assert.Contains("<option value=\"movimentos\" selected=\"selected\">Movimentos detalhados</option>", html, StringComparison.Ordinal);
        Assert.Contains("Movimentos — um por linha", html);
        Assert.Contains("3 movimentos", html);
        var rows = System.Text.RegularExpressions.Regex.Matches(html, "<tr data-dmo-row").Count;
        Assert.Equal(3, rows);
        Assert.DoesNotContain("bq-machine-group", html);
        Assert.DoesNotContain("Movimentos agrupados por máquina e Job On", html);
        // Os links do calendário e do PDF mantêm a vista.
        Assert.Contains("vista=movimentos", html);

        var pdf = await client.GetAsync("/Boquilhas?handler=PdfHistorico&historico=1&vista=movimentos&desde=2026-10-12&ate=2026-10-12");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("BOQUILHAS-movimentos.pdf", pdf.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task HistoryDay_ReplacesRangePreservesFiltersAndExportsTheSameSelection()
    {
        var trace = await SeedProductionAsync("BQ-DAY-A", "B1", "T173", "202601", "8");
        await SeedMovementAsync(trace.Id, "saida", 4, new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc));
        await SeedMovementAsync(trace.Id, "entrada", 2, new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        SeedUser("4292", "Histórico diário", "provider-day-1", "HistDay1!");
        using var client = await LoginClientAsync("4292", "HistDay1!");
        var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=grupos&ano=2026&mes=8&dia=12&maquina=B1&tipo=saida&pesquisa=T173");
        Assert.Contains("1 movimento</span>", html);
        // O dia mostra as fichas de BQ usadas, uma linha por máquina na cor dela.
        Assert.Contains("Registo da 12/08/2026", html);
        Assert.DoesNotContain("</strong> · —</span>", html);
        Assert.Contains("sem fichas neste dia\"><strong>B2</strong></span>", html);
        Assert.DoesNotContain("movimento(s)", html);
        Assert.Contains("beta-light-machine-row mch-B1", html);
        Assert.Contains($"<input type=\"radio\" name=\"bq_day_trace\" value=\"{trace.Id}\"", html);
        Assert.Contains("<span>T173 · Lote 8</span>", html);
        Assert.Contains("<span>Saídas</span><strong>4</strong>", html);
        Assert.Contains("dia=12", html);
        Assert.Contains("handler=PdfHistorico", html);
        Assert.Contains("tipo=saida", html);
        var response = await client.GetAsync("/Boquilhas?handler=PdfHistorico&vista=grupos&ano=2026&mes=8&dia=12&maquina=B1&tipo=saida&pesquisa=T173");
        foreach (var view in new[] { "matriz", "grupos" })
        {
            var viewHtml = await GetPageHtmlAsync(client, $"/Boquilhas?historico=1&vista={view}&ano=2026&mes=8&dia=12&tipo=saida");
            Assert.Contains("1 movimento", viewHtml);
            Assert.Contains("<span>Saídas</span><strong>4</strong>", viewHtml);
            var pdf = await client.GetAsync($"/Boquilhas?handler=PdfHistorico&vista={view}&ano=2026&mes=8&dia=12&tipo=saida");
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var ranged = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=grupos&ano=2026&mes=8&desde=2026-08-01&ate=2026-08-31&tipo=saida");
        var calendar = ranged[ranged.IndexOf("class=\"card dmo-calendar\"", StringComparison.Ordinal)..];
        var firstDayLink = System.Text.RegularExpressions.Regex.Match(calendar, "<a class=\"dmo-calendar-day[^>]+>").Value;
        Assert.Contains("tipo=saida", firstDayLink);
        Assert.DoesNotContain("desde=", firstDayLink);
        Assert.DoesNotContain("ate=", firstDayLink);
    }

    [Fact]
    public async Task History_OpensBlank_UntilAReferenceOrAPeriodIsSearched()
    {
        // Decisão de 16/10: o histórico abre em branco; pesquisa pela referência e/ou desde–até.
        var trace = await SeedProductionAsync("BQ-BLANK-A", "B1", "T188", "202611", "8");
        await SeedMovementAsync(trace.Id, "saida", 4, new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc));
        SeedUser("4293", "Histórico em branco", "provider-blank-1", "HistBlank1!");
        using var client = await LoginClientAsync("4293", "HistBlank1!");

        foreach (var view in new[] { "grupos" })
        {
            var blank = await GetPageHtmlAsync(client, $"/Boquilhas?historico=1&vista={view}");
            Assert.Contains("Escolha um período ou pesquise para ver os movimentos.", blank);
            Assert.Contains("<label for=\"pesquisa\">Referência</label>", blank);
            Assert.Contains("<label for=\"lote\">Lote</label>", blank);
            Assert.DoesNotContain("Referência / pesquisa", blank);
            Assert.Contains("name=\"desde\" type=\"date\"", blank);
            Assert.Contains("name=\"ate\" type=\"date\"", blank);
            Assert.DoesNotContain("<td>Saída</td>", blank);
            // A BQ em máquina aparece nas linhas do topo (Diogo, 27/10/2026); a lista fica vazia.
            var lista = blank[blank.IndexOf("id=\"bqHistLista\"", StringComparison.Ordinal)..];
            Assert.DoesNotContain("BQ T188 · Lote 8", lista);
            Assert.Contains("<span>Saídas</span><strong>0</strong>", blank);
            // O calendário do mês continua lá.
            Assert.Contains("class=\"card dmo-calendar\"", blank);
        }

        var byReference = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&pesquisa=T188");
        Assert.Contains("<td>Saída</td>", byReference);
        Assert.DoesNotContain("Escolha um período ou pesquise para ver os movimentos.", byReference);

        var byPeriod = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&desde=2026-08-01&ate=2026-08-31");
        Assert.Contains("<td>Saída</td>", byPeriod);
        Assert.Contains("1 movimento", byPeriod);

        // A grelha mantém-se como estava: carrega o mês sem pesquisa.
        var matriz = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=matriz&ano=2026&mes=8");
        Assert.Contains("BQ T188 · Lote 8", matriz);
    }


    [Fact]
    public async Task History_CalendarDay_ChangesOnlyTheZones()
    {
        // Diogo, 18/10: «faz flash ao escolher dia do calendário». Escolher um
        // dia, o «Hoje», o «Todos os dias» ou outro mês só muda as zonas
        // (data-dmo-regiao): o resto da página é igual e o DmoTroca troca-as
        // sem recarregar.
        var trace = await SeedProductionAsync("BQ-DIA-Z", "B1", "T188", "202612", "4");
        await SeedMovementAsync(trace.Id, "saida", 2, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
        await SeedMovementAsync(trace.Id, "entrada", 2, new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        SeedUser("4295", "Histórico zonas", "provider-zonas-1", "HistZon1!");
        using var client = await LoginClientAsync("4295", "HistZon1!");

        var dia10 = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&ano=2026&mes=9&dia=10");
        var dia20 = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&ano=2026&mes=9&dia=20");
        var semDia = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&ano=2026&mes=9&dia=0");
        var outubro = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&ano=2026&mes=10");

        Assert.Contains("<section id=\"historico\" class=\"view active\" data-dmo-troca>", dia10);
        foreach (var zona in new[] { "bqHistContexto", "bqHistContagem", "bqHistAcoes", "bqHistDesdeZona", "bqHistAteZona", "bqHistDiaCartao", "bqHistDia", "bqHistDiaAcoes", "bqHistTotais", "bqHistLista", "dmoCalendario" })
            Assert.Matches($"<div [^>]*id=\"{zona}\"[^>]*data-dmo-regiao", dia10);
        // «Hoje» e «Todos os dias» no canto superior direito do cartão do dia,
        // por cima do calendário e não por baixo (regra 39 do deep sweep de
        // 18/10/2026).
        Assert.Matches("<div id=\"bqHistDiaAcoes\" class=\"dmo-card-topo-acoes bq-calendar-actions\" data-dmo-regiao data-dmo-parcial><a class=\"btn\" href=\"[^\"]*\">Hoje</a><a class=\"btn\" href=\"[^\"]*\">Todos os dias</a></div>\\s*"
            + "<div class=\"card dmo-calendar\" id=\"dmoCalendario\"", dia10);
        // O dia muda o mês e o dia escondidos e limpa as datas: são zonas
        // dentro dos filtros, que ficam (deep sweep, 18/10/2026).
        Assert.Contains("<div id=\"bqHistFiltros\" class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>", dia10);
        Assert.NotEqual(Controlo.PesoZonas.Zona(dia10, "bqHistContexto"), Controlo.PesoZonas.Zona(dia20, "bqHistContexto"));
        Assert.NotEqual(Controlo.PesoZonas.Zona(dia10, "bqHistDia"), Controlo.PesoZonas.Zona(dia20, "bqHistDia"));
        var fora = Controlo.PesoZonas.ForaDasZonas(dia10);
        Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(dia20));
        Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(semDia));
        Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(outubro));
    }

    [Fact]
    public async Task History_Filters_ChangeOnlyTheZones()
    {
        // Deep sweep, 18/10/2026 (B-boquilhas-6): cada filtro do histórico
        // recarregava a página toda. Agora o DmoTroca pede a página com fetch
        // e troca só as zonas; para isso, fora das zonas e dos campos (que
        // ficam), a página tem de ser igual em todas as escolhas, também ao
        // mudar de vista e com as datas.
        var trace = await SeedProductionAsync("BQ-FIL-Z", "B1", "T189", "202612", "4");
        await SeedMovementAsync(trace.Id, "saida", 2, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
        await SeedMovementAsync(trace.Id, "entrada", 2, new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        SeedUser("4384", "Histórico filtros", "provider-filtros-1", "HistFil1!");
        using var client = await LoginClientAsync("4384", "HistFil1!");

        var semFiltros = await GetPageHtmlAsync(client, "/Boquilhas?historico=1");
        Assert.Contains("<div id=\"bqHistFiltros\" class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>", semFiltros);
        // «Limpar filtros» e o PDF abrem como sempre.
        Assert.Contains("<div id=\"bqHistAcoes\" class=\"bq-history-actions\" data-dmo-regiao data-dmo-completa>", semFiltros);
        // A pesquisa e o reparador aplicam-se ao escrever (deep sweep 2,
        // 18/10/2026, F-10): antes o texto sem Enter ficava no campo, mas a
        // lista, a contagem e o PDF eram do filtro anterior.
        Assert.Matches("<form [^>]*id=\"bqHistoryFilters\" data-dmo-ao-escrever=\"bqHistLista\"", semFiltros);
        Assert.Contains("a pesquisa e o reparador aplicam-se ao escrever", System.Net.WebUtility.HtmlDecode(semFiltros));
        var fora = Controlo.PesoZonas.ForaDasZonas(semFiltros);
        foreach (var filtros in new[]
        {
            "maquina=B1",
            "maquina=B1&tipo=saida",
            "tipo=entrada_sem_reparacao",
            "vista=matriz",
            "vista=matriz&maquina=B1&ano=2026&mes=9",
            "vista=grupos&desde=2026-09-01&ate=2026-09-30",
            "pesquisa=T189",
            "pesquisa=sem-resultados",
            "reparador=Hist",
            "maquina=B1&ano=2026&mes=9&dia=10",
        })
        {
            var html = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&" + filtros);
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
        }

        // A lista e os totais mudam com o filtro.
        var b1 = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&desde=2026-09-01&ate=2026-09-30");
        var saidas = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&maquina=B1&tipo=saida&desde=2026-09-01&ate=2026-09-30");
        Assert.NotEqual(Controlo.PesoZonas.Zona(b1, "bqHistTotais"), Controlo.PesoZonas.Zona(saidas, "bqHistTotais"));
        Assert.Contains("2 movimentos", Controlo.PesoZonas.Zona(b1, "bqHistContagem"));
        Assert.Contains("1 movimento", Controlo.PesoZonas.Zona(saidas, "bqHistContagem"));
    }

    [Fact]
    public async Task History_MonthOnly_ListsThatMonthsMovements()
    {
        // Bug do Diogo (17/10): «Filtrar» só com o mês escolhido não mostrava nada.
        var trace = await SeedProductionAsync("BQ-MES-A", "B1", "T177", "202612", "3");
        await SeedMovementAsync(trace.Id, "saida", 2, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
        await SeedMovementAsync(trace.Id, "entrada", 2, new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc));
        SeedUser("4294", "Histórico mês", "provider-mes-1", "HistMes1!");
        using var client = await LoginClientAsync("4294", "HistMes1!");

        // O seletor de mês saiu da página (Diogo, 18/10); um link antigo com
        // mes_ano continua a mostrar esse mês.
        var blank = await GetPageHtmlAsync(client, "/Boquilhas?historico=1");
        Assert.DoesNotContain("name=\"mes_ano\" value=", blank);

        var october = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&vista=grupos&mes_ano=2026-10&desde=&ate=&reparador=&pesquisa=&maquina=&tipo=");
        Assert.Contains("1 movimento", october);
        Assert.Contains("<td>Entrada</td>", october);
        Assert.DoesNotContain("<td>Saída</td>", october);

        // As datas desde/até continuam a mandar sobre o mês.
        var byDates = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&mes_ano=2026-10&desde=2026-09-01&ate=2026-09-30");
        Assert.Contains("<td>Saída</td>", byDates);
        Assert.DoesNotContain("<td>Entrada</td>", byDates);
    }

    [Fact]
    public async Task Matrix_MovimentosSemMaquina_AvisaComLinkParaALista()
    {
        // Sweep de 19/10/2026, B-2: a grelha não tem coluna para os
        // movimentos sem máquina, mas o cartão dos totais conta-os; os dois
        // totais não batiam sem nada o dizer.
        var agosto = DmoBusinessTime.ToUtc(
            DateTime.SpecifyKind(new DateTime(2026, 8, 12, 10, 0, 0), DateTimeKind.Unspecified));
        var naB1 = await SeedProductionAsync("BQ-B2-A", "B1", "9389T194", "202610");
        await SeedMovementAsync(naB1.Id, "saida", 2, agosto);
        SeedUser("4295", "Histórico B-2", "provider-hist-b2", "HistB2!");
        using var client = await LoginClientAsync("4295", "HistB2!");

        const string grelha = PagePath + "?historico=1&vista=matriz&mes_ano=2026-08";
        Assert.DoesNotContain("sem máquina não aparece", await GetPageHtmlAsync(client, grelha));

        // Um registo avulso de uma BQ sem máquinas na ficha: o movimento
        // fica sem máquina.
        await SeedProductionAsync("BQ-B2-B", "B2", "5447T173", "202611");
        var avulso = await SeedTraceAvulsoAsync("BQ-B2-B");
        await SeedMovementAsync(avulso.Id, "saida", 1, agosto);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, grelha));
        Assert.Contains("<div><span>Saídas</span><strong>3</strong></div>", html);
        Assert.Contains(
            "1 movimento sem máquina não aparece na grelha, mas conta nos totais. <a class=\"dmo-aviso-link\" href=\"/Boquilhas?historico=1&ano=2026&mes=8&vista=grupos&desde=2026-08-01&ate=2026-08-31\">Ver os movimentos</a>",
            html);

        // O link abre a lista do mesmo período, onde o movimento aparece.
        var lista = await GetPageHtmlAsync(client, "/Boquilhas?historico=1&ano=2026&mes=8&vista=grupos&desde=2026-08-01&ate=2026-08-31");
        Assert.Contains("<h3>Máquina Por indicar</h3>", lista);
        Assert.Contains("BQ RGT950", lista);
        Assert.Contains("<h3>Máquina B1</h3>", lista);
    }
}
