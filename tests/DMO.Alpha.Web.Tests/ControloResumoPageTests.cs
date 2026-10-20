using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova o slice vertical nativo do Resumo do Controlo (corte ensure +
/// Resumo): o browser «Procurar resumo» filtra por referência com
/// IGUALDADE EXATA aplicada no servidor (o contains/auto-open do mockup é
/// regra de protótipo, não copiada; a produção a abrir é sempre escolha
/// humana explícita pelo «Abrir»), a folha da produção selecionada mostra
/// o contexto pela query canónica EXISTENTE IProductionContextQuery (a
/// superfície que o Peso production-scoped consumirá) e assegura o
/// controlo_id de forma idempotente: um Job On criado antes deste corte
/// (estilo J10 real — jobon persistido sem controlo) ganha o seu
/// controlo_id UMA vez por backfill na abertura, e reabrir devolve o
/// MESMO controlo_id; um Job On criado pela página já nasce com o
/// controlo_id garantido na MESMA escrita. Estado do documento truthful:
/// áreas «não registado», nenhum OK/NOK inventado; o anónimo é desafiado
/// pelo [Authorize] ANTES de qualquer leitura/escrita; um jobon_id
/// inexistente é aviso limpo sem escrita nenhuma.
/// </summary>
public class ControloResumoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Resumo";

    private readonly CustomWebApplicationFactory _factory;

    public ControloResumoPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    /// <summary>
    /// A base InMemory é PARTILHADA por todas as classes de teste do host
    /// (nome fixo); cada teste limpa o estado de domínio que tocou ao sair,
    /// para que as classes seguintes (Peso, Boquilhas, …) encontrem o mesmo
    /// estado limpo que encontravam antes desta classe existir.
    /// </summary>
    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.BqMovements.RemoveRange(db.BqMovements);
        db.BqRepairTraces.RemoveRange(db.BqRepairTraces);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        // Controlos ANTES dos JobOns: a FK JobOnId → JobOns é ON DELETE
        // RESTRICT, a mesma ordem dependente→principal das outras tabelas.
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
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

    private void SeedUser(string operatorId, string name, string providerUserId, string password)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        // O user operacional com O SEU TEMPLATE (a regra do corte I: «User
        // sem Template = sem operacional»): os POSTs do Controlo exigem a
        // capability (o modo chefe: o approve não cria).
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = new[]
                {
                    DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate,
                    DMO.Alpha.Core.Admin.DmoCapabilities.ControloCreate
                }
            };
            db.Templates.Add(template);
            db.SaveChanges();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId
        };
        db.Users.Add(user);
        db.SaveChanges();

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

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Semeia uma produção no estado PRÉ-CORTE: Job On persistido com os
    /// contextos aplicáveis (o mf_id incluído — o MF é obrigatório em
    /// todo o Job On, regra do Owner 2026-10-10) mas SEM nenhum controlo —
    /// exactamente o estado da base real (o J10 foi criado antes deste
    /// corte existir).
    /// </summary>
    private async Task<(int JobOnId, int CmId, int BqId, int MfId)> SeedPreCutJobOnAsync(
        string productionNumber,
        string reference,
        string machineCode,
        string? cmToolId = null,
        string? mfToolId = null,
        string? bqToolId = null)
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

        CmContext? cm = null;
        MfContext? mf = null;
        BqContext? bq = null;
        if (cmToolId is not null)
        {
            cm = new CmContext { JobOn = jobOn, ToolId = cmToolId };
        }

        if (mfToolId is not null)
        {
            mf = new MfContext { JobOn = jobOn, ToolId = mfToolId };
        }

        if (bqToolId is not null)
        {
            bq = new BqContext { JobOn = jobOn, ToolId = bqToolId };
        }

        // A REFERÊNCIA TOTAL do cartão (o Owner 2026-10-12) compõe-se da
        // base + a referência da boquilha — os factos humanos exigem as
        // rows das Tools com as REFERÊNCIAS (a referência total é o
        // conjunto; a CM tem a SUA referência e o SEU lote: 1075C142 e
        // ST100 são factos distintos).
        foreach (var (toolId, type, toolReference, toolLot) in new[]
                 {
                     ("T3", ToolType.Cm, "5447", "3"),
                     ("MF-TOOL-9", ToolType.Mf, "5810", "1"),
                     ("BQ-1", ToolType.Bq, "T194", "3")
                 })
        {
            if (!db.Tools.Any(t => t.ToolId == toolId))
            {
                db.Tools.Add(new Tool { ToolId = toolId, Type = type, Reference = toolReference, Lot = toolLot });
            }
        }

        db.JobOns.Add(jobOn);
        if (cm is not null)
        {
            db.CmContexts.Add(cm);
        }

        if (mf is not null)
        {
            db.MfContexts.Add(mf);
        }

        if (bq is not null)
        {
            db.BqContexts.Add(bq);
        }

        await db.SaveChangesAsync();
        return (jobOn.Id, cm?.Id ?? 0, bq?.Id ?? 0, mf?.Id ?? 0);
    }

    private static int? ParseControloId(string html)
    {
        // C-2 (decisão do Owner): ids fora das folhas — o controlo_id não é
        // mostrado como facto; vive no URL/estado. O id lê-se do link
        // «Pegamentos» (o nome real do botão no Resumo — P2-8).
        var match = Regex.Match(html, "controlo_id=(\\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private async Task<int> CountControlosAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.Controlos.AsNoTracking().CountAsync();
    }

    [Fact]
    public async Task AnonymousRequests_ReceiveNoPageAndNoMutation()
    {
        // Uma produção real existe (pré-corte, sem controlo): se o ensure
        // corresse para o anónimo, criaria o controlo. O [Authorize]
        // desafia ANTES de qualquer leitura/escrita — nem a página é
        // servida, nem o browser consulta, nem o ensure escreve.
        var (jobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J10", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");

        var anonymous = CreateClient();
        var response = await anonymous.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        // E com um jobon_id escolhido também não: desafiado antes de
        // qualquer leitura — nenhum controlo é criado.
        var openResponse = await anonymous.GetAsync($"{PagePath}?jobon_id={jobOnId}");
        Assert.Equal(HttpStatusCode.Redirect, openResponse.StatusCode);
        Assert.Contains("/login", openResponse.Headers.Location?.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, await CountControlosAsync());
    }

    [Fact]
    public async Task Browser_ReferenceFilter_ServerSide_AndRecentesPrimeiro()
    {
        // O browser «Procurar resumo»: três produções — duas com a
        // referência 5810 (J10 e J12, criadas por esta ordem) e uma com
        // 5447 (J11). Todas com o MF obrigatório do desenho (MF-TOOL-9 —
        // regra do Owner 2026-10-10). O filtro é aplicado no servidor:
        // 5810 mostra J12 (o mais recente primeiro) e J10, nunca J11; a
        // procura ignora maiúsculas e aceita parte da referência ou o nº
        // de produção (H22-02).
        var (j10Id, _, _, _) = await SeedPreCutJobOnAsync(
            "J10", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        var (j11Id, _, _, _) = await SeedPreCutJobOnAsync("J11", "5447", "B2", mfToolId: "MF-TOOL-9");
        var (j12Id, _, _, _) = await SeedPreCutJobOnAsync("J12", "5810", "B3", cmToolId: "T4", mfToolId: "MF-TOOL-9");

        SeedUser("4300", "Controlo Operador", "provider-controlo-1", "ControloPass1!");
        using var client = await LoginClientAsync("4300", "ControloPass1!");

        // AS OPÇÕES SÓ COM A REFERÊNCIA (o Owner 2026-10-12: «procurar
        // resumo so deve mostrar as opçoes quando seleciono uma
        // referencia»): sem referência a home NÃO lista — só o campo e o
        // convite à procura; a ordem (recentes primeiro) prova-se dentro
        // do filtro abaixo.
        var allHtml = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("Procurar resumo", allHtml);
        Assert.Contains("Escreva a referência", allHtml);
        Assert.DoesNotContain("<td>J10", allHtml);
        Assert.DoesNotContain("<td>J11", allHtml);
        Assert.DoesNotContain("<td>J12", allHtml);

        // Igualdade exata ao valor persistido: 5810 mostra apenas as
        // produções com essa referência (J12 e J10), nunca a 5447 (J11).
        var filteredHtml = await GetPageHtmlAsync(client, $"{PagePath}?reference=5810");
        Assert.Contains("J10", filteredHtml);
        Assert.Contains("J12", filteredHtml);
        Assert.DoesNotContain("J11", filteredHtml);
        Assert.DoesNotContain("5447", filteredHtml);
        Assert.True(
            filteredHtml.IndexOf("J12", StringComparison.Ordinal) < filteredHtml.IndexOf("J10", StringComparison.Ordinal),
            "Dentro do filtro, o mais recente continua primeiro.");

        // Parte da referência encontra as mesmas produções (H22-02).
        var partialHtml = await GetPageHtmlAsync(client, $"{PagePath}?reference=581");
        Assert.Contains("<td>J10", partialHtml);
        Assert.Contains("<td>J12", partialHtml);
        Assert.DoesNotContain("<td>J11", partialHtml);

        // O nº de produção, em minúsculas, encontra a produção dele.
        var numberHtml = await GetPageHtmlAsync(client, $"{PagePath}?reference=j11");
        Assert.Contains("<td>J11", numberHtml);
        Assert.DoesNotContain("<td>J10", numberHtml);
        Assert.DoesNotContain("<td>J12", numberHtml);

        // Um texto que não existe continua a dar o estado vazio.
        var missingHtml = await GetPageHtmlAsync(client, $"{PagePath}?reference=9999");
        Assert.Contains("Nenhuma produção encontrada", missingHtml);
        Assert.DoesNotContain("<td>J10", missingHtml);
        Assert.DoesNotContain("<td>J11", missingHtml);
        Assert.DoesNotContain("<td>J12", missingHtml);

        // A consulta não escreve nada: sem jobon_id escolhido o ensure
        // não corre (nenhuma seleção automática, nenhum auto-open).
        Assert.Equal(0, await CountControlosAsync());

        // Nas tabelas não existem botões: um clique na linha abre a folha
        // (Diogo, 17/10/2026); sem botão «Procurar» nem «Abrir».
        Assert.Contains($@"data-dmo-open=""/Controlo/Resumo?jobon_id={j12Id}""", filteredHtml);
        Assert.Contains($@"data-dmo-open=""/Controlo/Resumo?jobon_id={j10Id}""", filteredHtml);
        Assert.Contains("data-dmo-abrir-clique", filteredHtml);
        Assert.Contains("data-dmo-row", filteredHtml);
        Assert.DoesNotContain("Abrir produção escolhida", filteredHtml);
        Assert.DoesNotContain(">Procurar</button>", filteredHtml);

        // A procura ao escrever: o handler devolve só a lista do texto.
        var live = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=j11");
        Assert.Contains("<td>J11</td>", live);
        Assert.DoesNotContain("<td>J10</td>", live);
        Assert.DoesNotContain("<html", live);
        Assert.Contains("data-dmo-abrir-clique", live);
        var liveMissing = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=9999");
        Assert.Contains("Nenhuma produção encontrada", liveMissing);
        Assert.Equal(0, await CountControlosAsync());
    }

    [Fact]
    public async Task SecondaryNav_ShowsPesoAndResumo_OnBothControloPages()
    {
        // Nav secundária Controlo: Peso | Resumo nas duas páginas — a
        // página Peso ganhou a entrada Resumo e a página Resumo lista
        // ambas as funções do módulo.
        await SeedPreCutJobOnAsync("J10", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        SeedUser("4301", "Controlo Operador", "provider-controlo-2", "ControloPass2!");
        using var client = await LoginClientAsync("4301", "ControloPass2!");

        var resumoHtml = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("Resumo do Controlo", resumoHtml);
        Assert.Contains("href=\"/Controlo/Peso\"", resumoHtml);
        Assert.Contains("href=\"/Controlo/Resumo\"", resumoHtml);
        Assert.Contains(">Peso</a>", resumoHtml);
        Assert.Contains(">Resumo</a>", resumoHtml);

        var pesoHtml = await GetPageHtmlAsync(client, "/Controlo/Peso");
        Assert.Contains("href=\"/Controlo/Peso\"", pesoHtml);
        Assert.Contains("href=\"/Controlo/Resumo\"", pesoHtml);
        Assert.Contains(">Peso</a>", pesoHtml);
        Assert.Contains(">Resumo</a>", pesoHtml);
    }

    [Fact]
    public async Task SecondaryNav_GroupsTheSheetsUnderFuncoes()
    {
        // Menos separadores (o Owner 16/10/2026): Resumo, «Funções» com o
        // Peso e os Pegamentos num dropdown, e depois Comparação e Histórico.
        SeedUser("4302", "Controlo Operador", "provider-controlo-funcoes", "ControloPass3!");
        using var client = await LoginClientAsync("4302", "ControloPass3!");

        foreach (var (path, active) in new[] { (PagePath, "Resumo"), ("/Controlo/Peso", "Funções"), ("/Controlo/Pegamentos", "Funções"), ("/Controlo/Puncoes", "Funções") })
        {
            var html = await GetPageHtmlAsync(client, path);
            var nav = System.Text.RegularExpressions.Regex.Match(html,
                "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
            var labels = System.Text.RegularExpressions.Regex.Matches(nav, "<(?:a|summary)\\b[^>]*>([^<]*)<")
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToArray();
            Assert.Equal(new[] { "Resumo", "Funções", "Peso", "Pegamentos", "PU NNPB", "Comparação", "Histórico", "Definições" }, labels);

            var menu = System.Text.RegularExpressions.Regex.Match(nav,
                "<details class=\"dmo-tab-menu[^\"]*\" data-dmo-tab-menu>[\\s\\S]*?</details>").Value;
            Assert.Contains("href=\"/Controlo/Peso\"", menu);
            Assert.Contains("href=\"/Controlo/Pegamentos\"", menu);
            Assert.Contains("href=\"/Controlo/Puncoes\"", menu);
            Assert.DoesNotContain("Comparação", menu);
            Assert.Equal(active == "Funções", menu.Contains("<summary class=\"active\">", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce()
    {
        // O cenário do J10 real: Job On #1 (ref 5810, máquina B1,
        // cm_id→T3, bq_id→BQ-1, mf_id→MF-TOOL-9 — o molde final
        // obrigatório do desenho, regra do Owner 2026-10-10) criado ANTES
        // deste corte — persistido sem controlo. Abrir o Resumo: a folha
        // mostra o contexto pela query canónica EXISTENTE e o controlo_id
        // é criado por backfill UMA vez; REABRIR devolve o MESMO
        // controlo_id.
        var (jobOnId, cmId, bqId, mfId) = await SeedPreCutJobOnAsync(
            "J10", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        Assert.Equal(0, await CountControlosAsync());

        SeedUser("4302", "Controlo Operador", "provider-controlo-3", "ControloPass3!");
        using var client = await LoginClientAsync("4302", "ControloPass3!");

        // A folha da produção selecionada: contexto canónico + controlo_id
        // assegurado pela abertura (bookkeeping idempotente, nunca um ato
        // de criação do utilizador).
        var folhaHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.Contains("Produção selecionada", folhaHtml);
        // Visual contract (beta.28): a folha segue a composição do PDF do
        // Resumo — a janela em máquina com o selo, os controlos associados,
        // a combinação e o «Visto por» — sobre os dados reais do Job On.
        Assert.Contains("css/dmo-resumo-prototype.css", folhaHtml);
        Assert.Contains("resumo-context-grid", folhaHtml);
        Assert.Contains("resumo-tools-grid", folhaHtml);
        Assert.Contains("resumo-areas", folhaHtml);
        Assert.Contains("resumo-linked-grid", folhaHtml);
        Assert.Contains("Controlos associados", folhaHtml);
        Assert.Contains("A combinação da produção", folhaHtml);
        Assert.Contains("<span>Início</span>", folhaHtml);
        Assert.DoesNotContain("Início em máquina", folhaHtml);
        // Menos clutter (Diogo, 17/10/2026): sem botões das folhas nem
        // seletores de comparação no topo; os PDFs, cada um em separado,
        // ficam no «Exportar»; a combinação vem antes dos controlos.
        Assert.Contains(">Exportar<", folhaHtml);
        Assert.Contains(">PDF do Resumo</button>", folhaHtml);
        // O PDF completo (Diogo, 19/10/2026): o primeiro do menu, sem
        // «apenas_resumo», junta o Resumo e os controlos com registos.
        Assert.Contains("<button type=\"submit\" class=\"dmo-options-item\">PDF completo</button>", folhaHtml);
        Assert.True(folhaHtml.IndexOf(">PDF completo<", StringComparison.Ordinal)
            < folhaHtml.IndexOf(">PDF do Resumo<", StringComparison.Ordinal));
        Assert.Contains(">Folha de Pegamentos em branco</button>", folhaHtml);
        Assert.DoesNotContain(">PDF do Peso</button>", folhaHtml);
        Assert.DoesNotContain("para comparar", folhaHtml);
        Assert.DoesNotContain("PDF único", folhaHtml);
        Assert.DoesNotContain("resumo-quick-links", folhaHtml);
        Assert.True(folhaHtml.IndexOf("A combinação da produção", StringComparison.Ordinal)
            < folhaHtml.IndexOf("Controlos associados", StringComparison.Ordinal));
        // O estado do documento: OK/NOK grava ao escolher; o comentário abre
        // numa janela; a nota explicativa vive no triângulo.
        Assert.Contains("data-dmo-dialog=\"resumo-area-dialog-CM\"", folhaHtml);
        Assert.Contains("<dialog id=\"resumo-area-dialog-CM\"", folhaHtml);
        Assert.DoesNotContain("<p class=\"resumo-hint\">Sem registo, a área", folhaHtml);
        Assert.Contains("Aprovado por", folhaHtml);
        Assert.Contains("resumo-people", folhaHtml);
        Assert.Contains("<th>Aprovado a</th>", folhaHtml);
        Assert.Contains("Visto por", folhaHtml);
        Assert.Contains("Crie o Peso desta produção na folha de Peso.", folhaHtml);
        Assert.Contains("J10", folhaHtml);
        Assert.Contains("5810", folhaHtml);
        Assert.Contains("B1", folhaHtml);
        // C-2/C-1: factos humanos. A REFERÊNCIA é o campo DO JOB ON (o
        // Owner 2026-10-12) — inserida COMO ESTÁ no cabeçalho da folha
        // (referência · nº · máquina, como no PDF).
        Assert.Contains("<span>5810</span><i aria-hidden=\"true\">·</i><span>J10</span><i aria-hidden=\"true\">·</i><span>B1</span>", folhaHtml);
        Assert.DoesNotContain("<span>Lote da CM</span>", folhaHtml);

        var firstControloId = ParseControloId(folhaHtml);
        Assert.NotNull(firstControloId);

        // Estado do documento truthful: controlo_id assegurado + áreas
        // CM/BQ/MF/PU/CS «não registado» — NENHUM OK/NOK inventado.
        Assert.Contains("Estado do documento", folhaHtml);
        // (O seletor OK/NOK do formulário existe; nenhuma área mostra estado.)
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(folhaHtml, "<strong>não registado</strong>").Count);
        Assert.DoesNotContain("resumo-area ok", folhaHtml);
        Assert.DoesNotContain("resumo-area nok", folhaHtml);
        Assert.DoesNotContain("<strong>OK</strong>", folhaHtml);
        Assert.DoesNotContain("<strong>NOK</strong>", folhaHtml);

        // Verdade persistida: UM controlo para ESTA produção, criado pelo
        // backfill da abertura.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal(jobOnId, controlo.JobOnId);
            Assert.Equal(firstControloId, controlo.Id);
        }

        // REABRIR a mesma folha: o MESMO controlo_id, sem segunda escrita
        // (abrir 2× = 1 controlo_id — idempotência material).
        var reopenedHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        var secondControloId = ParseControloId(reopenedHtml);
        Assert.NotNull(secondControloId);
        Assert.Equal(firstControloId, secondControloId);
        Assert.Equal(1, await CountControlosAsync());
    }

    /// <summary>
    /// GRAVAR SEM RECARREGAR (Diogo, 17/10/2026: «atualizar só o que muda»):
    /// a folha marca a raiz da troca, a procura que fica como o operador a
    /// deixou, uma zona por área (o OK/NOK, o comentário e o link do mcaliper,
    /// com as janelas), a zona da mensagem de recusa e a do «Visto por». Só as
    /// áreas gravam pela troca: os PDFs do «Exportar» descarregam como sempre.
    /// Gravar e a recusa do link voltam à MESMA página com a produção (PRG),
    /// que é o que a troca precisa; a recusa vem na zona das mensagens.
    /// </summary>
    [Fact]
    public async Task Folha_MarcaAsZonasDaTroca_EGravarVoltaAMesmaPagina()
    {
        var (jobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J30", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        SeedUser("4311", "Controlo Troca", "provider-controlo-troca", "ControloPass7!");
        using var client = await LoginClientAsync("4311", "ControloPass7!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.Contains("aria-labelledby=\"resumo-title\" data-dmo-troca>", html);
        Assert.Contains("aria-labelledby=\"resumo-browser-title\" data-dmo-fica>", html);
        Assert.Contains("<div id=\"resumoEstadoAvisos\" class=\"dmo-zona\" data-dmo-regiao>", html);
        Assert.Contains("<div class=\"resumo-areas\" data-dmo-parcial data-dmo-zonas=\"resumoEstadoAvisos resumoVistoPor\">", html);
        foreach (var area in new[] { "CM", "BQ", "MF", "PU", "CS" })
        {
            Assert.Contains($"<div id=\"resumo-area-{area}\" class=\"dmo-zona\" data-dmo-regiao>", html);
            Assert.Contains($"<select id=\"resumo-estado-{area}\" name=\"estado\" aria-label=\"Estado {area}\" data-dmo-autosubmit>", html);
            Assert.Contains($"<dialog id=\"resumo-area-dialog-{area}\" class=\"dmo-dialog\"", html);
        }
        Assert.Contains("<div id=\"resumoVistoPor\" class=\"resumo-viewings\" data-dmo-regiao>", html);
        Assert.Single(Regex.Matches(html, "data-dmo-parcial"));
        Assert.DoesNotContain("data-dmo-dialog-aberta", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var saved = await client.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("area", "CM"),
            ("estado", "NOK"),
            ("comentario", ""),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal($"{PagePath}?jobon_id={jobOnId}#estado-documento", saved.Headers.Location?.ToString());

        var refused = await client.PostAsync($"{PagePath}?handler=Mcaliper", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("area", "CM"),
            ("url", "ftp://mcaliper.local/x"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Equal($"{PagePath}?jobon_id={jobOnId}&mcaliper=invalido#estado-documento", refused.Headers.Location?.ToString());
        var after = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&mcaliper=invalido");
        Assert.Matches("<div id=\"resumoEstadoAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div data-dmo-toast=\"erro\"", after);
        Assert.Matches("<div id=\"resumo-area-CM\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div class=\"resumo-area nok\">", after);
    }

    /// <summary>
    /// Deep sweep (18/10/2026): cada formulário da área envia só o seu
    /// campo. Um posto com a folha aberta antes de outro gravar o NOK e o
    /// comentário muda o OK/NOK sem apagar esse comentário, e guardar o
    /// comentário não repõe um estado antigo.
    /// </summary>
    [Fact]
    public async Task AreaState_TwoStations_EachFormKeepsTheOtherField()
    {
        var (jobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J33", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        SeedUser("4314", "Posto A", "provider-posto-a", "PostoA2026!");
        SeedUser("4315", "Posto B", "provider-posto-b", "PostoB2026!");
        using var a = await LoginClientAsync("4314", "PostoA2026!");
        using var b = await LoginClientAsync("4315", "PostoB2026!");
        var url = $"{PagePath}?jobon_id={jobOnId}";

        // A abre a folha antes de B gravar.
        var htmlA = await GetPageHtmlAsync(a, url);
        var tokenA = HtmlFormHelpers.ExtractRequestVerificationToken(htmlA)!;
        var estadoForm = Regex.Match(htmlA,
            "<form method=\"post\"[^>]*handler=AreaState[^>]*class=\"resumo-area-form\">(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;
        Assert.DoesNotContain("name=\"comentario\"", estadoForm);
        var comentarioForm = Regex.Match(htmlA,
            "<dialog id=\"resumo-area-dialog-CM\".*?<form[^>]*>(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;
        Assert.DoesNotContain("name=\"estado\"", comentarioForm);

        var tokenB = HtmlFormHelpers.ExtractRequestVerificationToken(await GetPageHtmlAsync(b, url))!;
        using (var nok = await b.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("estado", "NOK"), ("__RequestVerificationToken", tokenB))))
        {
            Assert.Equal(HttpStatusCode.Redirect, nok.StatusCode);
        }
        using (var nota = await b.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("comentario", "Rebarba no gargalo"), ("__RequestVerificationToken", tokenB))))
        {
            Assert.Equal(HttpStatusCode.Redirect, nota.StatusCode);
        }
        Assert.Equal("NOK|Rebarba no gargalo", await LerCmAsync());

        // A, com a folha antiga, escolhe OK: o comentário de B fica.
        using (var ok = await a.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("estado", "OK"), ("__RequestVerificationToken", tokenA))))
        {
            Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        }
        Assert.Equal("OK|Rebarba no gargalo", await LerCmAsync());

        // B guarda outro comentário com a folha de antes do OK: o OK fica.
        using (var nota2 = await b.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("comentario", "Trocada"), ("__RequestVerificationToken", tokenB))))
        {
            Assert.Equal(HttpStatusCode.Redirect, nota2.StatusCode);
        }
        Assert.Equal("OK|Trocada", await LerCmAsync());

        async Task<string> LerCmAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var estado = await db.ControloAreaStates.AsNoTracking()
                .SingleAsync(x => x.Area == "CM" && x.Controlo!.JobOnId == jobOnId);
            return $"{estado.Status}|{estado.Comment}";
        }
    }

    /// <summary>
    /// E11 (beta.189): depois de gravar uma área, o GET do redirect leva
    /// X-Dmo-Zonas e o servidor desenha só o cartão dessa área, a mensagem e o
    /// «Visto por», com os mesmos partials da página e as mesmas verificações
    /// (sessão e entrada no módulo). Não lê as ferramentas, o Peso, os
    /// Pegamentos nem as PU NNPB, que essas zonas não mostram; a visita fica
    /// registada como na página inteira. Uma zona desconhecida, ou uma
    /// produção sem controlo, desenha a página inteira, sem X-Dmo-Region.
    /// </summary>
    [Fact]
    public async Task GravarArea_ZonasPedidas_DesenhaSoAsZonas_SemLerOQueNaoMostram()
    {
        var (jobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J31", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        var (semControlo, _, _, _) = await SeedPreCutJobOnAsync("J32", "5810", "B2", cmToolId: "T3");
        SeedUser("4313", "Controlo Zonas", "provider-controlo-zonas", "ControloZonas7!");
        var leituras = new LeiturasContadas();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IToolLookupQuery>(sp => new ToolLookupContado(
                ActivatorUtilities.CreateInstance<DMO.Alpha.Infrastructure.Tools.ToolLookupQuery>(sp), leituras));
            services.AddScoped<DMO.Alpha.Core.Controlo.IPesoQuery>(sp => new PesoQueryContado(
                ActivatorUtilities.CreateInstance<DMO.Alpha.Infrastructure.Controlo.PesoQuery>(sp), leituras));
            services.AddScoped<DMO.Alpha.Core.Controlo.IPegamentosQuery>(sp => new PegamentosQueryContado(
                ActivatorUtilities.CreateInstance<DMO.Alpha.Infrastructure.Controlo.PegamentosQuery>(sp), leituras));
        }));
        const string zonas = "resumo-area-CM resumoEstadoAvisos resumoVistoPor";
        var url = $"{PagePath}?jobon_id={jobOnId}";

        using (var anonymous = CreateClient(factory))
        {
            var refused = await anonymous.SendAsync(ZonasGravacao.Pedido(url, zonas));
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Contains("/login", refused.Headers.Location?.ToString(), StringComparison.Ordinal);
            Assert.Null(ZonasGravacao.Regiao(refused));
        }

        using var client = await LoginClientAsync("4313", "ControloZonas7!", factory);
        // A página inteira assegura o controlo (o ensure) e lê o que mostra.
        var page = await GetPageHtmlAsync(client, url);
        Assert.True(leituras.Ferramentas > 0 && leituras.Peso > 0 && leituras.Pegamentos > 0);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)!;
        var saved = await client.PostAsync($"{PagePath}?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("estado", "NOK"), ("comentario", "Rebarba"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var volta = saved.Headers.Location!.ToString();

        leituras.Zerar();
        var response = await client.SendAsync(ZonasGravacao.Pedido(volta, zonas));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(zonas, ZonasGravacao.Regiao(response));
        Assert.Contains("X-Dmo-Zonas", response.Headers.Vary);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(zonas.Split(' '), ZonasGravacao.Raizes(html));
        Assert.Matches("<div id=\"resumo-area-CM\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div class=\"resumo-area nok\">", html);
        Assert.Contains("Rebarba", html);
        Assert.Contains("Operador 4313", html);
        Assert.DoesNotContain("resumo-area-BQ", html);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Procurar resumo", html);
        Assert.DoesNotContain("A combinação da produção", html);
        Assert.Equal(0, leituras.Ferramentas);
        Assert.Equal(0, leituras.Peso);
        Assert.Equal(0, leituras.Pegamentos);

        // As zonas são as que a página inteira desenha (os mesmos partials).
        var inteira = ZonasGravacao.SemTokens(await GetPageHtmlAsync(client, url));
        foreach (var zona in Regex.Split(ZonasGravacao.SemTokens(html).Trim(), "(?=<div id=\"resumo(?:EstadoAvisos|VistoPor)\")"))
        {
            Assert.Contains(zona.Trim(), inteira);
        }

        // Uma zona desconhecida: a página inteira, sem X-Dmo-Region.
        var desconhecida = await client.SendAsync(ZonasGravacao.Pedido(url, "resumo-area-XX"));
        Assert.Null(ZonasGravacao.Regiao(desconhecida));
        Assert.Contains("Procurar resumo", await desconhecida.Content.ReadAsStringAsync());

        // Uma produção ainda sem controlo: a página inteira (que o assegura).
        var antes = await CountControlosAsync();
        var semZonas = await client.SendAsync(ZonasGravacao.Pedido($"{PagePath}?jobon_id={semControlo}", zonas));
        Assert.Null(ZonasGravacao.Regiao(semZonas));
        Assert.Contains("Procurar resumo", await semZonas.Content.ReadAsStringAsync());
        Assert.Equal(antes + 1, await CountControlosAsync());
    }

    /// <summary>
    /// E11: quem não tem o Controlo não recebe as zonas — a entrada no
    /// módulo é a mesma da página.
    /// </summary>
    [Fact]
    public async Task ZonasPedidas_SemOModulo_NaoDesenhaNada()
    {
        var (jobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J33", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        _factory.AuthProvider.AddAccount("4314@dmo.local", "provider-sem-controlo", "SemControlo7!", "4314@dmo.local");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-teste-so-jobon",
                Name = "Só Job On",
                Capabilities = new[] { DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate }
            };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = "4314", Name = "Sem Controlo", ProviderUserId = "provider-sem-controlo" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        using var client = await LoginClientAsync("4314", "SemControlo7!");
        var response = await client.SendAsync(ZonasGravacao.Pedido($"{PagePath}?jobon_id={jobOnId}", "resumo-area-CM resumoVistoPor"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(ZonasGravacao.Regiao(response));
        Assert.Equal(0, await CountControlosAsync());
    }

    [Fact]
    public async Task CreateJobOnViaPage_ControloGuaranteedBySameWrite()
    {
        // Um Job On criado pela página Job On já nasce com o controlo_id
        // garantido na MESMA escrita atómica (spec Job On L33): depois do
        // redirect da criação existe EXACTAMENTE um controlo para o novo
        // jobon_id — e abrir o Resumo devolve esse MESMO controlo_id (o
        // ensure encontra o que a criação já persistiu; nunca cria um
        // segundo).
        int machineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            db.Machines.Add(machine);
            db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
            db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
            db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "3" });
            await db.SaveChangesAsync();
            machineId = machine.Id;
        }

        SeedUser("4303", "Job On Operador", "provider-controlo-4", "ControloPass4!");
        using var client = await LoginClientAsync("4303", "ControloPass4!");

        var token = await GetVerificationTokenAsync(client, "/JobOn?novo=1");
        var created = await client.PostAsync(
            "/JobOn?handler=Create",
            HtmlFormHelpers.BuildFormPost(
                ("production_number", "J20"),
                ("reference", "5810"),
                ("machine_id", machineId.ToString()),
                ("cm_tool_id", "T3"),
                ("mf_tool_id", "MF-TOOL-9"),
                ("bq_tool_id", "BQ-1"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var createdLocation = created.Headers.Location!.ToString();
        var jobOnId = int.Parse(Regex.Match(createdLocation, @"jobon_id=(\d+)").Groups[1].Value);

        // Verdade persistida logo após a criação: 1 controlo por jobon,
        // vinculado ao jobon_id emitido (escrita atómica da criação).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal(jobOnId, controlo.JobOnId);
        }

        // Abrir o Resumo dessa produção: o MESMO controlo_id (o ensure é
        // get-or-create — encontra o que a criação já persistiu).
        var folhaHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.Contains("Produção selecionada", folhaHtml);
        var controloIdFromFolha = ParseControloId(folhaHtml);
        Assert.NotNull(controloIdFromFolha);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var controlo = await db.Controlos.AsNoTracking().SingleAsync();
            Assert.Equal(controloIdFromFolha, controlo.Id);
            Assert.Equal(1, await db.Controlos.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task NonExistentJobOn_ShowsWarning_NoControloCreated()
    {
        // Um jobon_id que não existe é aviso limpo: nenhuma folha é
        // inventada e o ensure não escreve nada (nunca é criado um
        // controlo para uma produção inexistente).
        var (seededJobOnId, _, _, _) = await SeedPreCutJobOnAsync(
            "J10", "5810", "B1", cmToolId: "T3", mfToolId: "MF-TOOL-9", bqToolId: "BQ-1");
        var nonExistentJobOnId = seededJobOnId + 1000;

        SeedUser("4304", "Controlo Operador", "provider-controlo-5", "ControloPass5!");
        using var client = await LoginClientAsync("4304", "ControloPass5!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={nonExistentJobOnId}");

        Assert.Contains("A produção indicada não existe", html);
        Assert.DoesNotContain("Produção selecionada", html);
        Assert.Equal(0, await CountControlosAsync());
    }

    /// <summary>
    /// DMO-044 — as sugestões ao escrever: o campo do Resumo declara o
    /// componente; o endpoint só responde a sessões autenticadas e devolve
    /// as produções por parte da referência, sem distinção de maiúsculas.
    /// </summary>
    [Fact]
    public async Task SearchPagination_ReturnsOnlyTheRequestedList_AndDoesNotOpenOrCreateAControlo()
    {
        int initialViewings;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            for (var i = 1; i <= 12; i++)
                db.JobOns.Add(new JobOn { Reference = "RP-REF", ProductionNumber = $"RP-{i:00}", Machine = machine });
            await db.SaveChangesAsync();
            initialViewings = await db.ControloViewings.CountAsync();
        }
        using var anonymous = CreateClient();
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync($"{PagePath}?handler=Procurar&q=RP&pagina=2")).StatusCode);
        SeedUser("4395", "Resumo paginado", "provider-resumo-paginas", "ResumoPaginas1!");
        using var client = await LoginClientAsync("4395", "ResumoPaginas1!");
        var first = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=RP&pagina=1");
        var second = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=RP&pagina=2");
        Assert.Contains("RP-12", first);
        Assert.DoesNotContain("RP-07", first);
        Assert.Contains("RP-07", second);
        Assert.Contains("RP-03", second);
        Assert.DoesNotContain("RP-08", second);
        Assert.DoesNotContain("<html", second, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resumo do Controlo", second);
        Assert.Contains("data-dmo-procura-pagina", second);
        var last = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=RP&pagina=2147483647");
        Assert.Contains("Página 3 de 3", last);
        Assert.Contains("RP-01", last);
        var negative = await GetPageHtmlAsync(client, $"{PagePath}?handler=Procurar&q=RP&pagina=-1");
        Assert.Contains("Página 1 de 3", negative);
        using var check = _factory.Services.CreateScope();
        var state = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Empty(state.Controlos);
        Assert.Equal(initialViewings, await state.ControloViewings.CountAsync());
    }

    [Fact]
    public async Task Suggestions_AuthenticatedOnly_AndResumoFieldUsesThem()
    {
        await SeedPreCutJobOnAsync("202616", "6018Y232", "C2", mfToolId: "MF-TOOL-9");

        var anonymous = CreateClient();
        var anonymousResponse = await anonymous.GetAsync("/Sugestoes?fonte=producoes&q=6018");
        Assert.NotEqual(HttpStatusCode.OK, anonymousResponse.StatusCode);

        SeedUser("4310", "Controlo Sugestões", "provider-controlo-sug", "ControloPass1!");
        using var client = await LoginClientAsync("4310", "ControloPass1!");

        var json = await client.GetStringAsync("/Sugestoes?fonte=producoes&q=y23");
        Assert.Contains("\"value\":\"6018Y232\"", json, StringComparison.Ordinal);
        Assert.Contains("6018Y232 \u00B7 202616 \u00B7 C2", json, StringComparison.Ordinal);

        // Um carácter chega, pelo início da referência (Diogo, 18/10/2026:
        // «0» sugere a 0132T173); um carácter a meio não encontra nada.
        Assert.Contains("\"value\":\"6018Y232\"", await client.GetStringAsync("/Sugestoes?fonte=producoes&q=6"), StringComparison.Ordinal);
        Assert.Equal("[]", await client.GetStringAsync("/Sugestoes?fonte=producoes&q=8"));
        Assert.Equal("[]", await client.GetStringAsync("/Sugestoes?fonte=producoes&q=232"));
        Assert.Equal("[]", await client.GetStringAsync("/Sugestoes?fonte=desconhecida&q=6018"));

        // O campo do Resumo passou à procura ao escrever com a lista por
        // baixo (Diogo, 17/10/2026): sem as sugestões em cima da lista.
        var html = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("data-dmo-procura=\"/Controlo/Resumo?handler=Procurar\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dmo-suggest=\"producoes\"", html, StringComparison.Ordinal);
    }
}
