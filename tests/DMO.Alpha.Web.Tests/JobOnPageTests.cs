using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova o slice vertical nativo da página Job On REFUNDADA pelo mockup
/// (a autoridade visual e de comportamento — o layout beta-light-layout
/// com o rail das linhas, o dmo-calendar do mês, a lista do dia, a folha
/// com as tools por pesquisa e o histórico com as associações humanas):
/// o surface Razor injeta os read models (records com as tools pelos
/// nomes com lote, o catálogo por tipo, as máquinas) por window inline,
/// cria o jobon_id com os contextos aplicáveis (cm_id/mf_id/bq_id) via
/// handler canónico e redireciona ao RESUMO da produção (o comportamento
/// canónico submit do js); o MF é OBRIGATÓRIO (regra do Owner
/// 2026-10-10: «jobon necessita sempre de MF, senao nao faz garrafas ou
/// frascos»): sem ele a criação é recusada tipada MissingMfTool e nada
/// é criado; os slots CM/BQ sem seleção são simplesmente não
/// aplicáveis — nenhum contexto desse tipo é criado. O duplo clique no
/// dia já escolhido do calendário tira a seleção ou, com uma produção
/// escolhida e confirmação, retira-a das datas (sem botão);
/// /Planeamento redireciona ao /JobOn (a grelha morreu). A mutação
/// nativa exige sessão autenticada; as recusas tipadas voltam por query
/// (PRG) preservando os valores introduzidos. A EDIÇÃO: o POST Update
/// (a folha em modo edição — o jobon_edit_id) grava a produção
/// RE-PLANEADA (a janela, a linha, as tools) SEM criar outra — o
/// jobon_id é o mesmo, a identidade imutável e a recusa tipada (o MF
/// obrigatório herda; o jobon inexistente é JobOnNotFound limpa)
/// preserva o formulário submetido com a BD inalterada.
/// </summary>
public partial class JobOnPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/JobOn";

    private readonly CustomWebApplicationFactory _factory;

    public JobOnPageTests(CustomWebApplicationFactory factory)
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
        // Os Controlos são removidos ANTES dos JobOns: a FK JobOnId →
        // JobOns é ON DELETE RESTRICT (uma produção referenciada por um
        // controlo_id não desaparece silenciosamente), pelo que a limpeza
        // segue a mesma ordem dependente→principal das outras tabelas.
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
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
        var user = new User
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            Name = name,
            ProviderUserId = providerUserId
        };
        db.Users.Add(user);

        // O GATE DO G2 (a varredura 2026-10-12): os POSTs do JobOn agora
        // validam jobon.create — os utilizadores destes testes PRECISAM do
        // pacote com a chave da oficina (o módulo JobOn pede a sua
        // ferramenta, o template entrega a chave; a analogia do Owner:
        // oficinas → chaves → portas).
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-test-planejador");
        if (template is null)
        {
            template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-test-planejador",
                Name = "Planejador de teste — Job On",
                Capabilities = new[] { DmoCapabilities.JobOnCreate }
            };
            db.Templates.Add(template);
        }

        db.TemplateAssignments.Add(new TemplateAssignment
        {
            Id = Guid.NewGuid(),
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
        // O Planeamento leva à folha nova (Diogo, 23/10/2026: escolher uma
        // produção, o Criar e o Duplicar abrem /JobOn/Folha): o token lê-se
        // onde o redirect chega.
        if (response.StatusCode == System.Net.HttpStatusCode.Redirect
            && response.Headers.Location?.ToString() is { } destino
            && destino.StartsWith("/JobOn/Folha", StringComparison.Ordinal))
        {
            response = await client.GetAsync(destino);
        }
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        // A ficha antiga do Planeamento deixou de existir (Diogo, 23/10/2026):
        // /JobOn?jobon_id=N leva à folha nova, que é a página que se lê.
        if (response.StatusCode == HttpStatusCode.Redirect
            && response.Headers.Location?.ToString() is { } folha
            && folha.StartsWith("/JobOn/Folha", StringComparison.Ordinal))
        {
            response = await client.GetAsync(folha);
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<(int MachineB1Id, int MachineB2Id)> SeedRegistryAsync()
    {
        // O registo real para o fluxo: duas máquinas e as Tools canónicas —
        // DUAS candidatos CM (a mesma referência 5447, lotes diferentes) para
        // provar que a seleção é explícita entre candidatos distinguíveis —
        // e o MF-TOOL-9 (MF 5810 lote 1 — o desenho; o MF é obrigatório em
        // todo o Job On, regra do Owner 2026-10-10).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        db.Machines.Add(b1);
        db.Machines.Add(b2);

        db.Tools.Add(new Tool { ToolId = "T1", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        db.Tools.Add(new Tool { ToolId = "T2", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "T-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "3" });

        await db.SaveChangesAsync();
        return (b1.Id, b2.Id);
    }

    private async Task<int> SeedExistingJobOnAsync(DateOnly? inicioEm = null, DateOnly? fimEm = null)
    {
        // Um Job On já existente com contextos CM e MF (o MF é obrigatório
        // em todo o Job On — regra do Owner 2026-10-10) — a consulta da
        // página tem de o mostrar com o seu contexto (a janela temporal é
        // opcional para provar os sem dia).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        var jobOn = new JobOn
        {
            Reference = "5447",
            ProductionNumber = "PN-EXISTENTE-1",
            Machine = machine,
            InicioEm = inicioEm,
            FimEm = fimEm
        };
        var cm = new CmContext { JobOn = jobOn, ToolId = "T1" };
        var mf = new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-9" };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(cm);
        db.MfContexts.Add(mf);
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    private async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string tokenPath,
        string productionNumber,
        string reference,
        string machineId,
        string cmToolId,
        string mfToolId,
        string bqToolId,
        string? inicioEm = null,
        string? fimEm = null)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        return await client.PostAsync(
            $"{PagePath}?handler=Create",
            HtmlFormHelpers.BuildFormPost(
                ("production_number", productionNumber),
                ("reference", reference),
                ("machine_id", machineId),
                ("cm_tool_id", cmToolId),
                ("mf_tool_id", mfToolId),
                ("bq_tool_id", bqToolId),
                ("inicio_em", inicioEm ?? string.Empty),
                ("fim_em", fimEm ?? string.Empty),
                ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia()
    {
        // Diogo, 22/10/2026: «Criar Job On» à frente das caixas da referência
        // e da produção, na mesma linha, e abre a folha nova com o dia escolhido.
        SeedUser("4296", "Job On Operador", "provider-jobon-criar-1", "JobOnCriar1!");
        using var client = await LoginClientAsync("4296", "JobOnCriar1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4");
        var bloco = html[html.IndexOf("class=\"jobon-procura-bloco\"", StringComparison.Ordinal)..];
        bloco = bloco[..bloco.IndexOf("</form>", StringComparison.Ordinal)];
        var criar = bloco.IndexOf(">Criar Job On</a>", StringComparison.Ordinal);
        Assert.True(criar >= 0, "O «Criar Job On» não está na barra da procura.");
        var referencia = bloco.IndexOf("id=\"lightSearch\"", StringComparison.Ordinal);
        var producao = bloco.IndexOf("id=\"lightProcura\"", StringComparison.Ordinal);
        Assert.True(referencia >= 0, "Falta a caixa da referência na barra da procura.");
        Assert.True(producao >= 0, "Falta a caixa da produção na barra da procura.");
        Assert.True(criar > referencia && criar > producao, "O «Criar Job On» tem de vir à direita das caixas (Diogo, 22/10).");
        // Abre a folha nova vazia, em Editar (Diogo, 23/10/2026), com o dia como início.
        Assert.Matches("<a id=\"jobonCriar\" class=\"dmo-button\" href=\"/JobOn/Folha\\?novo=1&amp;inicio_em=2026-03-04\"", bloco);
        // Um só botão na página: o do título saiu.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, ">Criar Job On</a>"));
        // A ajuda da produção encostada à caixa (Diogo, 24/10/2026); o Chromium mede-a
        // (tests/visual/ajuda_junto_browser.py).
        Assert.Matches("<span class=\"dmo-com-ajuda\"><span id=\"lightProcura\"[\\s\\S]*?</span><span class=\"dmo-help\"", bloco);
        ExportarParaOBrowser.Gravar("jobon-ajuda.html", html);

        // Sem dia escolhido, abre sem data de início.
        var semDia = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=0");
        Assert.Contains("<a id=\"jobonCriar\" class=\"dmo-button\" href=\"/JobOn/Folha?novo=1\"", semDia);
    }

    [Fact]
    public async Task CreateFlow_ExplicitSelections_EstablishesQueryableContext()
    {
        var (machineB1Id, _) = await SeedRegistryAsync();
        var existingJobOnId = await SeedExistingJobOnAsync();
        SeedUser("4200", "Job On Operador", "provider-jobon-1", "JobOnPass1!");
        using var client = await LoginClientAsync("4200", "JobOnPass1!");

        // A página refundada pelo mockup: o layout beta-light com o
        // calendário do mês, a lista do dia e a folha; as máquinas para a
        // linha e os records com o Job On existente e o seu contexto humano.
        var html = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("Selecione uma produção para consultar ou preparar as ferramentas.", html);
        Assert.Contains("Criar Job On", html);
        Assert.DoesNotContain("Retirar do dia", html);
        Assert.Contains("dmo-calendar", html);          // o dmo-calendar do mês
        // O RAIL do JobOn é DA PÁGINA e diz «O QUE VAI ENTRAR» (a C1 — o
        // sidepanel pedido pelo Owner; e o comentário da própria folha: «o
        // shared das Boquilhas serve a folha DELES — a semântica aqui é a do
        // planeamento»). O id bqCurrentLines é o aside DAS BOQUILHAS (as
        // correntes em máquina); exigi-lo aqui era pedir ao planeamento a
        // vista de outro módulo.
        Assert.Contains("beta-production-rail", html);
        Assert.Contains("O que vai entrar", html);
        Assert.Contains("B1", html);
        Assert.Contains("B2", html);
        // As ferramentas procuram-se ao escrever: o catálogo não vem na página.
        Assert.DoesNotContain("\"T1\"", html);
        Assert.DoesNotContain("\"T2\"", html);
        // O histórico só é desenhado (e lido) na sua tab.
        Assert.DoesNotContain("Histórico de Job On", html);
        var historyHtml = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447");
        Assert.Contains("PN-EXISTENTE-1", historyHtml);
        Assert.Contains("Histórico de Job On", historyHtml);

        // Criar a produção real: o utilizador escolhe EXPLICITAMENTE a
        // máquina B1, a CM T2 (lote 3, entre os dois candidatos), o MF
        // obrigatório do desenho (MF-TOOL-9) e a BQ; a produção nasce
        // com os três contextos aplicáveis.
        var created = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-2025-050",
            "5810",
            machineB1Id.ToString(),
            "T2",
            "MF-TOOL-9",
            "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var createdLocation = created.Headers.Location!.ToString();
        Assert.Contains("jobon_id=", createdLocation, StringComparison.Ordinal);

        // Este template só dá o Job On (sem o Controlo Criar): o GUARDAR
        // fica no Job On com a produção gravada selecionada, em vez de ir ao
        // Resumo, cuja entrada levaria a pessoa para outra página e o
        // registo pareceria perdido (A7, decisão do proprietário 16/10/2026).
        Assert.StartsWith($"{PagePath}?jobon_id=", createdLocation, StringComparison.Ordinal);
        Assert.DoesNotContain("/Controlo", createdLocation, StringComparison.Ordinal);

        var jobOnId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(createdLocation, @"jobon_id=(\d+)").Groups[1].Value);
        Assert.NotEqual(existingJobOnId, jobOnId);

        // O Job On aberto (?jobon_id): os read models injectados mostram
        // a produção com os factos humanos — as tools PELOS NOMES COM LOTE
        // (o contrato: «CM ST100 lote 12 · MF 5447 lote 08 · BQ T173
        // lote 04»), nunca os tool_ids como factos da folha.
        // Abre na folha nova (Diogo, 23/10/2026), com o lote sem o zero à esquerda.
        var detailHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.Contains("PN-2025-050", detailHtml);
        Assert.Contains("5810", detailHtml);
        Assert.Contains("aria-label=\"Contra molde 5447 lote 3\"", detailHtml);   // a CM escolhida (T2), humano
        Assert.Contains("aria-label=\"Molde final 5810 lote 1\"", detailHtml);   // o MF do desenho, humano
        Assert.Contains("aria-label=\"Boquilha T194 lote 3\"", detailHtml);  // a BQ escolhida, humano
        Assert.Contains("B1", detailHtml);

        // O histórico encontra as duas produções pela referência de cada
        // uma, identificadas pelos números de produção e pelas associações.
        var listHtml = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5810");
        Assert.Contains("PN-2025-050", listHtml);
        Assert.Contains("<td>5447 · lote 3</td>", listHtml);
        Assert.Contains("<td>5810 · lote 1</td>", listHtml);
        Assert.Contains("<td>T194 · lote 3</td>", listHtml);
        Assert.Contains("PN-EXISTENTE-1", await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447"));

        // Verdade persistida: o novo Job On com os TRÊS contextos aplicáveis,
        // cada um vinculado a este jobon_id — e o existente intocado.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(2, await db.JobOns.AsNoTracking().CountAsync());

            var cm = await db.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("T2", cm.ToolId);
            var mf = await db.MfContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("MF-TOOL-9", mf.ToolId);
            var bq = await db.BqContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("T-BQ1", bq.ToolId);
        }
    }

    [Fact]
    public async Task Create_LowercaseReference_IsSavedInUppercase()
    {
        // Pedido do proprietário (16/10/2026): «5447t173» grava «5447T173»,
        // sem ter de voltar atrás para corrigir as letras.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4291", "Job On Maiúsculas", "provider-jobon-maius", "JobOnMaius1!");
        using var client = await LoginClientAsync("4291", "JobOnMaius1!");

        var html = await GetPageHtmlAsync(client, PagePath + "?novo=1");
        Assert.Contains("id=\"lightReference\" data-dmo-maiusculas", html, StringComparison.Ordinal);

        var created = await PostCreateAsync(client, $"{PagePath}?novo=1", "PN-MAIUS-1", "5447t173", machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "PN-MAIUS-1");
        Assert.Equal("5447T173", jobOn.Reference);
    }

    [Theory]
    [InlineData("", "MF-TOOL-9", "T-BQ1", "MissingCmTool", "Falta a CM (contra molde): um Job On precisa de CM, BQ e MF.")]
    [InlineData("T1", "MF-TOOL-9", "", "MissingBqTool", "Falta a BQ (boquilha): um Job On precisa de CM, BQ e MF.")]
    [InlineData("T1", "", "T-BQ1", "MissingMfTool", "O MF (molde final) é obrigatório")]
    public async Task Create_SemCmOuSemBq_ERecusado_ENadaEGravado(string cm, string mf, string bq, string recusa, string mensagem)
    {
        // Diogo, 22/10/2026 (bloqueio autorizado): um Job On não se grava
        // sem CM, BQ e MF. Antes, a BQ por escolher criava o Job On sem BQ.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4201", "Job On Operador", "provider-jobon-2", "JobOnPass2!");
        using var client = await LoginClientAsync("4201", "JobOnPass2!");

        var refused = await PostCreateAsync(
            client, $"{PagePath}?novo=1", "PN-2025-051", "5810", machineB1Id.ToString(), cm, mf, bq);

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains($"create_refusal={recusa}", location, StringComparison.Ordinal);
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains(mensagem, html);
        Assert.Contains("Nenhum Job On foi criado.", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.False(await db.JobOns.AsNoTracking().AnyAsync());
        Assert.False(await db.BqContexts.AsNoTracking().AnyAsync());
    }

    [Fact]
    public async Task Edit_SemBq_ERecusado_ENadaMuda()
    {
        // O mesmo bloqueio na edição: um Job On antigo sem BQ só grava completo.
        var (machineB1Id, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(
            inicioEm: new DateOnly(2026, 3, 2),
            fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4241", "Job On Operador", "provider-jobon-sembq", "JobOnPassSB!");
        using var client = await LoginClientAsync("4241", "JobOnPassSB!");

        var refused = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T1", "MF-TOOL-9", "", "2026-04-12", "");

        var location = refused.Headers.Location!.ToString();
        Assert.Contains("update_refusal=MissingBqTool", location, StringComparison.Ordinal);
        Assert.Contains("Falta a BQ (boquilha): um Job On precisa de CM, BQ e MF.", await GetPageHtmlAsync(client, location));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(new DateOnly(2026, 3, 2), (await db.JobOns.AsNoTracking().SingleAsync()).InicioEm);
    }

    [Fact]
    public async Task Edit_PorABqNumJobOnSemBq_NaoEMudancaDeBoquilhas()
    {
        // Diogo, 22/10/2026: pôr a BQ num Job On que não tinha completa-o; não
        // é uma mudança de boquilhas (sem registo em «O que mudou», sem aviso).
        // Só uma BQ que substitui outra é mudança.
        var (machineB1Id, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(
            inicioEm: new DateOnly(2026, 3, 2),
            fimEm: new DateOnly(2026, 3, 9));
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "T-BQ2", Type = ToolType.Bq, Reference = "5810T194", Lot = "4" });
            await db.SaveChangesAsync();
        }
        SeedUser("4242", "Job On Operador", "provider-jobon-pobq", "JobOnPassPB!");
        using var client = await LoginClientAsync("4242", "JobOnPassPB!");

        var completed = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T1", "MF-TOOL-9", "T-BQ1", "2026-03-02", "2026-03-09");
        Assert.Equal($"{PagePath}?jobon_id={jobOnId}", completed.Headers.Location!.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal("T-BQ1", (await db.BqContexts.AsNoTracking().SingleAsync(b => b.JobOnId == jobOnId)).ToolId);
            Assert.False(await db.JobOnChanges.AsNoTracking().AnyAsync(c => c.JobOnId == jobOnId && c.Field == JobOnChangeFields.Bq));
        }

        // Trocar essa BQ por outra já é mudança de boquilhas.
        var replaced = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T1", "MF-TOOL-9", "T-BQ2", "2026-03-02", "2026-03-09");
        Assert.Equal(HttpStatusCode.Redirect, replaced.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var change = Assert.Single(await db.JobOnChanges.AsNoTracking().Where(c => c.JobOnId == jobOnId && c.Field == JobOnChangeFields.Bq).ToListAsync());
            Assert.Equal("T194 · lote 3", change.OldValueFrozen);
        }
    }

    [Fact]
    public async Task Create_ComOControloCriar_FicaNoJobOn_NaoVaiAoResumo()
    {
        // Diogo, 22/10/2026: depois de criar o Job On fica-se no Job On do
        // Planeamento; já não se é levado ao Resumo do Controlo.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4243", "Job On Operador", "provider-jobon-fica", "JobOnPassFi!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = await db.Users.SingleAsync(u => u.OperatorId == "4243");
            var template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = "tpl-jobon-controlo-fica",
                Name = "Job On e Controlo",
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            db.TemplateAssignments.Add(new DMO.Alpha.Core.Admin.TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4243", "JobOnPassFi!");

        var created = await PostCreateAsync(
            client, $"{PagePath}?novo=1", "PN-FICA-1", "5810", machineB1Id.ToString(), "T1", "MF-TOOL-9", "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        Assert.StartsWith($"{PagePath}?jobon_id=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("Controlo", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRefusal_IsTypedAndRedirectsWithPreservedValues()
    {
        // A recusa tipada volta por query (PRG) com os valores introduzidos
        // preservados: a CM selecionada não está registada no Ferramentas —
        // nenhum Job On é criado.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4202", "Job On Operador", "provider-jobon-3", "JobOnPass3!");
        using var client = await LoginClientAsync("4202", "JobOnPass3!");

        var refused = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-2025-052",
            "5810",
            machineB1Id.ToString(),
            "T-DESCONHECIDA",
            "MF-TOOL-9",
            "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("create_refusal=CmToolNotFound", location, StringComparison.Ordinal);
        Assert.Contains("production_number=PN-2025-052", location, StringComparison.Ordinal);
        Assert.Contains("cm_tool_id=T-DESCONHECIDA", location, StringComparison.Ordinal);

        // A mensagem tipada é apresentada com os valores preservados no
        // formulário (o banner da folha + o estado injectado).
        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("A ferramenta CM selecionada não está registada no Ferramentas", refusalHtml);
        Assert.Contains("PN-2025-052", refusalHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_DoubleClick_SecondSubmitOpensTheSavedJobOn()
    {
        // Duplo clique (Diogo, 18/10/2026): o segundo envio, com os mesmos
        // dados, abre a produção que o primeiro gravou — nunca «já existe».
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4211", "Job On Operador", "provider-jobon-11", "JobOnPass11!");
        using var client = await LoginClientAsync("4211", "JobOnPass11!");

        var first = await PostCreateAsync(client, $"{PagePath}?novo=1", "PN-DUPLO-1", "5447t194", machineB1Id.ToString(), "T1", "MF-TOOL-9", "T-BQ1");
        var second = await PostCreateAsync(client, $"{PagePath}?novo=1", "PN-DUPLO-1", "5447T194", machineB1Id.ToString(), "T1", "MF-TOOL-9", "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.DoesNotContain("create_refusal", second.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(first.Headers.Location, second.Headers.Location);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_DuplicateProductionNumber_RefusedTyped()
    {
        // Já existe um Job On da mesma referência com o mesmo nº: a recusa
        // é tipada e nenhum Job On novo é criado (a referência + o nº
        // identificam a ocorrência).
        // O MF obrigatório está selecionado — a recusa é mesmo a do
        // duplicado, nunca a do MF em falta.
        var (machineB1Id, _) = await SeedRegistryAsync();
        await SeedExistingJobOnAsync();
        SeedUser("4203", "Job On Operador", "provider-jobon-4", "JobOnPass4!");
        using var client = await LoginClientAsync("4203", "JobOnPass4!");

        var refused = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-EXISTENTE-1",
            "5447",
            machineB1Id.ToString(),
            "T1",
            "MF-TOOL-9",
            "T-BQ1");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("create_refusal=DuplicateProductionNumber", location, StringComparison.Ordinal);

        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("Já existe um Job On desta referência com este nº de produção", refusalHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Create_WithoutMfSelection_RefusedTyped_NothingCreated()
    {
        // A regra do Owner 2026-10-10: «jobon necessita sempre de MF, senao
        // nao faz garrafas ou frascos» — sem MF selecionado a criação é
        // recusada tipadamente (MissingMfTool por query, PRG), a mensagem
        // explica o porquê com os valores preservados e NADA é criado: a
        // BD fica sem o jobon.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4204", "Job On Operador", "provider-jobon-5", "JobOnPass5!");
        using var client = await LoginClientAsync("4204", "JobOnPass5!");

        var refused = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-2025-053",
            "5810",
            machineB1Id.ToString(),
            "T1",
            "",           // MF não selecionado — obrigatório
            "");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("create_refusal=MissingMfTool", location, StringComparison.Ordinal);
        Assert.Contains("production_number=PN-2025-053", location, StringComparison.Ordinal);
        Assert.Contains("cm_tool_id=T1", location, StringComparison.Ordinal);

        // A recusa renderiza a mensagem da regra do Owner — nada é criado.
        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("O MF (molde final) é obrigatório — sem ele não se fazem garrafas nem frascos", refusalHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.JobOns.AsNoTracking().CountAsync());
    }

    private async Task<HttpResponseMessage> PostUpdateAsync(
        HttpClient client,
        string jobOnEditId,
        string machineId,
        string cmToolId,
        string mfToolId,
        string bqToolId,
        string? inicioEm = null,
        string? fimEm = null,
        string versao = "")
    {
        // O POST da folha em MODO EDIÇÃO: o jobon_edit_id + os factos
        // mutáveis (a linha, a janela, as tools). O nº de produção e a
        // referência NÃO vão — a identidade é imutável na edição e o
        // handler nem os lê.
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        return await client.PostAsync(
            $"{PagePath}?handler=Update",
            HtmlFormHelpers.BuildFormPost(
                ("jobon_edit_id", jobOnEditId),
                ("machine_id", machineId),
                ("cm_tool_id", cmToolId),
                ("mf_tool_id", mfToolId),
                ("bq_tool_id", bqToolId),
                ("inicio_em", inicioEm ?? string.Empty),
                ("fim_em", fimEm ?? string.Empty),
                ("versao", versao),
                ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task EditFlow_SavedMeanwhileByAnotherUser_RefusesAndShowsWhatWasSaved()
    {
        // Edições ao mesmo tempo (varredura de 18/10/2026, B4): a folha leva
        // a versão aberta; com outra versão nada se grava e a folha reabre
        // com o que ficou gravado (os valores escritos não voltam).
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4231", "Job On Operador", "provider-jobon-b4", "JobOnPassB4!");
        using var client = await LoginClientAsync("4231", "JobOnPassB4!");
        var created = await PostCreateAsync(
            client, $"{PagePath}?novo=1", "PN-B4-1", "5810", machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-03-09");
        var jobOnId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(created.Headers.Location!.ToString(), @"jobon_id=(\d+)").Groups[1].Value);

        // No InMemory a versão é sempre 0; no PostgreSQL é o xmin da linha.
        var folha = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains("<input type=\"hidden\" name=\"versao\" value=\"0\" />", folha);

        var refused = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-04-01", versao: "7");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Equal($"/JobOn?jobon_id={jobOnId}&editar=1&update_refusal=ChangedByAnotherUser", location);
        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Outra pessoa gravou esta produção depois de a abrires.", html);
        Assert.Contains("value=\"2026-03-09\"", html);
        Assert.DoesNotContain("value=\"2026-04-01\"", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(new DateOnly(2026, 3, 9), (await db.JobOns.AsNoTracking().SingleAsync()).FimEm);

        // Com a versão aberta grava.
        var saved = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-04-01", versao: "0");
        Assert.DoesNotContain("update_refusal", saved.Headers.Location!.ToString());
    }

    [Fact]
    public async Task EditFlow_TheSameSaveTwice_GoesWhereTheFirstWent_WithoutTheRefusal()
    {
        // Duplo clique em «Guardar alterações» (sweep do Codex de 19/10/2026,
        // J-1): o segundo envio leva a versão de antes da primeira gravação e
        // os mesmos dados. Vai para onde foi o primeiro, sem «Outra pessoa
        // gravou», e «O que mudou» fica com uma só linha.
        var (machineB1Id, _) = await SeedRegistryAsync();
        SeedUser("4235", "Job On Operador", "provider-jobon-j1", "JobOnPassJ1!");
        using var client = await LoginClientAsync("4235", "JobOnPassJ1!");
        var created = await PostCreateAsync(
            client, $"{PagePath}?novo=1", "PN-J1-1", "5810", machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-03-09");
        var jobOnId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(created.Headers.Location!.ToString(), @"jobon_id=(\d+)").Groups[1].Value);

        var primeiro = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-04-01", versao: "0");
        // No InMemory a versão fica sempre 0: a de antes é outra qualquer.
        var segundo = await PostUpdateAsync(
            client, jobOnId.ToString(), machineB1Id.ToString(), "T2", "MF-TOOL-9", "T-BQ1",
            fimEm: "2026-04-01", versao: "7");

        Assert.Equal(HttpStatusCode.Redirect, segundo.StatusCode);
        Assert.Equal(primeiro.Headers.Location, segundo.Headers.Location);
        Assert.DoesNotContain("update_refusal", segundo.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Single(await db.JobOnChanges.AsNoTracking().Where(c => c.JobOnId == jobOnId).ToListAsync());
    }

    [Fact]
    public async Task EditFlow_ReplansTheSameProduction_WindowLineAndTools()
    {
        // A EDIÇÃO DO JOB ON: criar → editar mudando a janela temporal,
        // a linha e as tools SEM criar outra produção — o jobon_id é o
        // MESMO (o redirect ao Resumo dele), o nº de produção não
        // duplica, o controlo_id idem; a BQ que não existia nasce na
        // edição e a CM troca o ToolId NO MESMO contexto.
        var (machineB1Id, machineB2Id) = await SeedRegistryAsync();
        SeedUser("4209", "Job On Operador", "provider-jobon-10", "JobOnPass10!");
        using var client = await LoginClientAsync("4209", "JobOnPass10!");

        var created = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-EDIT-1",
            "5810",
            machineB1Id.ToString(),
            "T2",
            "MF-TOOL-9",
            "T-BQ1");
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var createdLocation = created.Headers.Location!.ToString();
        var jobOnId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(createdLocation, @"jobon_id=(\d+)").Groups[1].Value);

        // O [Editar] DA FICHA leva MESMO à folha de edição (a C1): o contrato
        // da folha é «?editar=1&jobon_id=N abre-a EM EDIÇÃO» — o link da ficha
        // tinha de levar o editar=1, senão o botão reabria a ficha FECHADA e
        // ninguém conseguia emendar uma produção. Afirma-se o HREF, não só o
        // resultado: é o link que está em causa.
        // A ficha é agora a folha nova (Diogo, 23/10/2026): o Editar dela.
        var fichaHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}");
        Assert.Contains(
            $"href=\"/JobOn/Folha?jobon_id={jobOnId}&amp;editar=1\" data-jof-editar>Editar</a>",
            fichaHtml,
            StringComparison.Ordinal);

        // A FOLHA DE EDIÇÃO server-rendered (sem js para abrir): o hidden do
        // jobon_edit_id (o POST Update), o lightSave e o rótulo da casa no
        // modo edição.
        var pageHtml = await GetPageHtmlAsync(client, $"{PagePath}?jobon_id={jobOnId}&editar=1");
        Assert.Contains("jobon_edit_id", pageHtml);
        Assert.Contains("lightSave", pageHtml);
        Assert.Contains("Guardar alterações", pageHtml);

        var updated = await PostUpdateAsync(
            client,
            jobOnId.ToString(),
            machineB2Id.ToString(),
            "T1",
            "MF-TOOL-9",
            "T-BQ1",
            "2026-04-12",
            "2026-04-18");

        // Sem o Controlo Criar no template, o GUARDAR fica na folha da
        // MESMA produção (o padrão do Create): o jobon_id não muda na
        // edição e a página abre-se sem ser desviada.
        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        var updatedLocation = updated.Headers.Location!.ToString();
        Assert.Equal($"{PagePath}?jobon_id={jobOnId}", updatedLocation);
        // O Planeamento abre a produção na folha nova (Diogo, 23/10/2026).
        var savedSheet = await client.GetAsync(updatedLocation);
        Assert.Equal(HttpStatusCode.Redirect, savedSheet.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={jobOnId}", savedSheet.Headers.Location!.ToString());

        // Verdade persistida: UM jobon (o nº não duplica) com a
        // identidade IMUTÁVEL e os factos novos — a linha B2, a janela
        // 12→18 de abril, a CM trocada e a BQ acrescentada.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(1, await db.JobOns.AsNoTracking().CountAsync());

            var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
            Assert.Equal(jobOnId, jobOn.Id);
            Assert.Equal("PN-EDIT-1", jobOn.ProductionNumber);
            Assert.Equal("5810", jobOn.Reference);
            Assert.Equal(machineB2Id, jobOn.MachineId);
            Assert.Equal(new DateOnly(2026, 4, 12), jobOn.InicioEm);
            Assert.Equal(new DateOnly(2026, 4, 18), jobOn.FimEm);

            var cm = await db.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("T1", cm.ToolId);
            var mf = await db.MfContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("MF-TOOL-9", mf.ToolId);
            var bq = await db.BqContexts.AsNoTracking().SingleAsync(c => c.JobOnId == jobOnId);
            Assert.Equal("T-BQ1", bq.ToolId);

            // O controlo_id da produção idem: UM controlo, o mesmo de
            // sempre — a edição não cria outro.
            Assert.Equal(1, await db.Controlos.AsNoTracking().CountAsync());
            Assert.Equal(jobOnId, (await db.Controlos.AsNoTracking().SingleAsync()).JobOnId);
        }

        // A folha reabre com a combinação NOVA (os factos humanos): a
        // CM trocada (lote 2, já não a lote 3) e a BQ acrescentada.
        var detailHtml = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5810");
        Assert.Contains("<td>5447 · lote 2</td>", detailHtml);
        Assert.Contains("<td>T194 · lote 3</td>", detailHtml);
        Assert.DoesNotContain("<td>5447 · lote 3</td>", detailHtml);
    }

    [Fact]
    public async Task EditFlow_UnknownJobOn_RefusedTypedWithPreservedForm()
    {
        // O jobon inexistente: a recusa tipada JobOnNotFound volta por
        // query (PRG) com o formulário preservado — a máquina, as tools
        // e a janela submetidas — SEM jobon_id (não há folha de edição
        // a reabrer) e a BD fica inalterada.
        var (machineB1Id, _) = await SeedRegistryAsync();
        await SeedExistingJobOnAsync();
        SeedUser("4210", "Job On Operador", "provider-jobon-11", "JobOnPass11!");
        using var client = await LoginClientAsync("4210", "JobOnPass11!");

        var refused = await PostUpdateAsync(
            client,
            "999999",
            machineB1Id.ToString(),
            "T1",
            "MF-TOOL-9",
            "T-BQ1",
            "2026-04-12",
            "2026-04-18");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("update_refusal=JobOnNotFound", location, StringComparison.Ordinal);
        Assert.Contains("machine_id=", location, StringComparison.Ordinal);
        Assert.Contains("cm_tool_id=T1", location, StringComparison.Ordinal);
        Assert.Contains("mf_tool_id=MF-TOOL-9", location, StringComparison.Ordinal);
        Assert.Contains("inicio_em=2026-04-12", location, StringComparison.Ordinal);
        Assert.Contains("fim_em=2026-04-18", location, StringComparison.Ordinal);
        Assert.DoesNotContain("jobon_id=", location, StringComparison.Ordinal);

        // A mensagem tipada + o estado preservado na FOLHA por RAZOR (o
        // window injectado do beta-jobon.js morto saiu — a lei «tudo
        // razor»): a folha reabre nos valores que o utilizador tentou
        // guardar — a janela submetida nos inputs date, a máquina e as
        // tools pedidas pré-selecionadas nos selects.
        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("A produção indicada não existe. Nenhuma alteração foi guardada.", refusalHtml);
        Assert.Contains("value=\"2026-04-12\"", refusalHtml);
        Assert.Contains("value=\"2026-04-18\"", refusalHtml);
        Assert.Contains($"value=\"{machineB1Id}\" selected=\"selected\"", refusalHtml);
        Assert.Contains("value=\"T1\" selected=\"selected\"", refusalHtml);
        Assert.Contains("value=\"MF-TOOL-9\" selected=\"selected\"", refusalHtml);

        // A BD inalterada: a produção existente fica exatamente como
        // estava (zero escritas parciais).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal("PN-EXISTENTE-1", jobOn.ProductionNumber);
        Assert.Equal("T1", (await db.CmContexts.AsNoTracking().SingleAsync()).ToolId);
    }

    [Fact]
    public async Task EditFlow_MissingMfTool_RefusedTyped_JobOnUntouched()
    {
        // O MF vazio na edição: a regra do Owner herda — MissingMfTool
        // por query (PRG) com o jobon_id (a folha reabre em MODO EDIÇÃO
        // com a produção e os valores submetidos) e a BD inalterada.
        var (machineB1Id, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(
            inicioEm: new DateOnly(2026, 3, 2),
            fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4211", "Job On Operador", "provider-jobon-12", "JobOnPass12!");
        using var client = await LoginClientAsync("4211", "JobOnPass12!");

        var refused = await PostUpdateAsync(
            client,
            jobOnId.ToString(),
            machineB1Id.ToString(),
            "T1",
            "",           // o MF vazio — obrigatório, herda na edição
            "T-BQ1",
            "2026-04-12",
            "");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("update_refusal=MissingMfTool", location, StringComparison.Ordinal);
        Assert.Contains($"jobon_id={jobOnId}", location, StringComparison.Ordinal);
        Assert.Contains("inicio_em=2026-04-12", location, StringComparison.Ordinal);

        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("O MF (molde final) é obrigatório", refusalHtml);
        Assert.Contains("Nenhuma alteração foi guardada", refusalHtml);

        // A BD inalterada: a janela da produção é a de ANTES da edição
        // recusada — a máquina e os contextos idem.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(new DateOnly(2026, 3, 2), jobOn.InicioEm);
        Assert.Equal(new DateOnly(2026, 3, 9), jobOn.FimEm);
        var mf = await db.MfContexts.AsNoTracking().SingleAsync();
        Assert.Equal("MF-TOOL-9", mf.ToolId);
    }

    [Fact]
    public async Task EditFlow_Refused_ReopensTheSheetInEditMode_NotAsANewProduction()
    {
        // Bug da varredura (16/10/2026): a recusa na edição reabria a folha
        // em criação, e guardar de novo criava um Job On duplicado. A folha
        // tem de voltar em edição (jobon_edit_id + handler Update), com a
        // referência e o nº da produção e os valores submetidos.
        var (machineB1Id, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(
            inicioEm: new DateOnly(2026, 3, 2),
            fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4212", "Job On Operador", "provider-jobon-13", "JobOnPass13!");
        using var client = await LoginClientAsync("4212", "JobOnPass13!");

        var refused = await PostUpdateAsync(
            client,
            jobOnId.ToString(),
            machineB1Id.ToString(),
            "T1",
            "",
            "T-BQ1",
            "2026-04-12",
            "");
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("editar=1", location, StringComparison.Ordinal);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("handler=Update", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Create", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"jobon_edit_id\" value=\"{jobOnId}\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"5447\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"PN-EXISTENTE-1\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"2026-04-12\"", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"T1\" selected=\"selected\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"MF-TOOL-9\" selected=\"selected\">", html, StringComparison.Ordinal);

        // Guardar de novo, agora com o MF, edita a mesma produção.
        var saved = await PostUpdateAsync(
            client,
            jobOnId.ToString(),
            machineB1Id.ToString(),
            "T1",
            "MF-TOOL-9",
            "T-BQ1",
            "2026-04-12",
            "");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(jobOnId, jobOn.Id);
        Assert.Equal(new DateOnly(2026, 4, 12), jobOn.InicioEm);
    }

    [Fact]
    public async Task PlaneamentoRedirectsToJobOn_TheGridIsGone()
    {
        // A refunda do calendário: a página da grelha máquinas×31-dias
        // morre — /Planeamento redireciona ao /JobOn (um 302 por pedido,
        // nunca um 301 cached).
        var (_, _) = await SeedRegistryAsync();
        SeedUser("4205", "Job On Operador", "provider-jobon-6", "JobOnPass6!");
        using var client = await LoginClientAsync("4205", "JobOnPass6!");

        var response = await client.GetAsync("/Planeamento");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/JobOn", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task HistoryTabListsProductionsWithHumanToolAssociations()
    {
        // A tab do histórico (?historico=1 — o padrão do Controlo/Peso): as
        // produções anteriores com as associações das tools pelos nomes com
        // lote + os SEM DIA.
        var (_, _) = await SeedRegistryAsync();
        await SeedExistingJobOnAsync(inicioEm: new DateOnly(2026, 2, 10), fimEm: new DateOnly(2026, 2, 20));
        await SeedExistingJobOnAsync(); // o segundo fica SEM dia (undated)
        SeedUser("4206", "Job On Operador", "provider-jobon-7", "JobOnPass7!");
        using var client = await LoginClientAsync("4206", "JobOnPass7!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447");
        Assert.Contains("Histórico de Job On", html);
        Assert.Contains("Produções anteriores e respetivas associações de ferramentas.", html);
        // A associação humana, em colunas alinhadas: CM 5447 lote 2, MF 5810 lote 1.
        Assert.Contains("<td>5447 · lote 2</td>", html);
        Assert.Contains("<td>5810 · lote 1</td>", html);
        Assert.Contains("<td>10/02/2026 – 20/02/2026</td>", html);
        Assert.DoesNotContain(">Procurar</button>", html);
        // O SEM DIA (undated) aparece na coluna das datas.
        Assert.Contains("Sem dia associado", html);
        // A sub-tab do histórico está ligada à função ?historico=1 da shell.
        Assert.Contains("href=\"/JobOn?historico=1\"", html);
        // A secção do histórico não pode ficar dentro da secção do Job On,
        // que está escondida nesta tab (a página aparecia em branco).
        Assert.Equal(
            System.Text.RegularExpressions.Regex.Matches(html, "<section[\\s>]").Count,
            System.Text.RegularExpressions.Regex.Matches(html, "</section>").Count);
    }

    [Fact]
    public async Task SelectedCalendarDay_ClickUnselects_AndNoDayShowsOnlyTheHint()
    {
        // Diogo, 18/10/2026: um clique entra no dia do calendário, outro no
        // que já está escolhido tira-o («desnecessário 2») — sem botão. O dia
        // escolhido leva o link sem dia (dia=0); sem dia, o quadro fica
        // vazio com a indicação e nenhum dia aparece escolhido.
        var (_, _) = await SeedRegistryAsync();
        await SeedExistingJobOnAsync(inicioEm: new DateOnly(2026, 3, 2), fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4207", "Job On Operador", "provider-jobon-8", "JobOnPass8!");
        using var client = await LoginClientAsync("4207", "JobOnPass8!");

        var withDay = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4");
        Assert.Contains("Produções de 04/03/2026", withDay);
        Assert.Contains("PN-EXISTENTE-1", withDay);
        Assert.Matches("data-dmo-calendar-unselect=\"[^\"]*dia=0", withDay);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(withDay, "data-dmo-calendar-unselect="));
        // A linha da produção: um clique abre a ficha, outro na mesma tira-a.
        Assert.Matches("data-dmo-fechar=\"[^\"]*dia=4", withDay);
        Assert.DoesNotContain("data-dmo-seleciona", withDay);

        var noDay = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=0");
        Assert.Contains("Escolha um dia no calendário.", noDay);
        Assert.DoesNotContain("PN-EXISTENTE-1", noDay);
        Assert.DoesNotContain("data-dmo-calendar-unselect=", noDay);
        Assert.DoesNotContain("dmo-calendar-day has selected", noDay);
        Assert.DoesNotContain("Retirar do dia", noDay);
    }

    [Fact]
    public async Task Calendario_ELinhasDoDia_TemIdsEstaveis_ParaOFocoFicarNoSitio()
    {
        // Deep sweep, 18/10/2026, B-jobon-9: depois de escolher com o Enter
        // uma produção, um dia ou a seta do mês, a troca devolve o foco ao
        // elemento com o mesmo id; sem id, o foco ia para o body.
        var (_, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(inicioEm: new DateOnly(2026, 3, 2), fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4208", "Job On Operador", "provider-jobon-foco", "JobOnPass9!");
        using var client = await LoginClientAsync("4208", "JobOnPass9!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4");
        Assert.Contains("id=\"dmoCalendarioAnterior\" aria-label=\"Mês anterior\"", html);
        Assert.Contains("id=\"dmoCalendarioSeguinte\" aria-label=\"Mês seguinte\"", html);
        for (var dia = 1; dia <= 31; dia++)
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, $"id=\"dmoDia-{dia}\""));
        Assert.Matches("class=\"dmo-calendar-day[^\"]*selected[^\"]*\" id=\"dmoDia-4\"", html);
        // (A semente deste teste tem duas máquinas B1, por isso a linha aparece em ambas.)
        Assert.Contains($"id=\"jobonLinha-{jobOnId}\" data-dmo-row data-id=\"{jobOnId}\"", html);
    }

    [Fact]
    public async Task ProximaMudanca_FicaANegrito()
    {
        // Diogo, 19/10/2026: «na informação da mudança meter em bold para
        // salientar mais». As duas regras da «Mudança …» (linha com produção
        // e linha vazia) levam o negrito.
        using var client = _factory.CreateClient();
        var css = await client.GetStringAsync("/beta-production-overview.css");
        Assert.Contains(".beta-light-productions .beta-light-production>.beta-light-next{font-size:12.5px;font-weight:700;", css);
        Assert.Contains(".beta-light-machine-empty-linha>.beta-light-next{margin-left:auto;padding-right:12px;white-space:nowrap;font-size:12.5px;font-weight:700;", css);
    }

    [Fact]
    public async Task Calendario_SoMarcaOsDiasEmQueUmaLinhaMuda()
    {
        // Diogo, 19/10/2026: os pontos em todos os dias carregavam o
        // calendário; o sinal fica só nos dias em que uma produção entra ou
        // sai de uma linha. Os dias do meio da produção ficam sem sinal.
        var (_, _) = await SeedRegistryAsync();
        await SeedExistingJobOnAsync(inicioEm: new DateOnly(2026, 3, 2), fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4209", "Job On Operador", "provider-jobon-mudancas", "JobOnPass10!");
        using var client = await LoginClientAsync("4209", "JobOnPass10!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=0");
        Assert.Contains("class=\"dmo-calendar-day has\" id=\"dmoDia-2\"", html);
        Assert.Contains("class=\"dmo-calendar-day has\" id=\"dmoDia-10\"", html);
        foreach (var dia in new[] { 1, 3, 4, 9, 11 })
            Assert.Contains($"class=\"dmo-calendar-day\" id=\"dmoDia-{dia}\"", html);
        Assert.Contains("Mudanças numa linha", html);
        Assert.DoesNotContain("Com marcações", html);
    }

    [Fact]
    public async Task CalendarDay_NeverRemovesTheProductionFromItsDates()
    {
        // Bug 18/10/2026 (o Diogo: «se tiver um Job On selecionado e
        // selecionar o dia do calendário remove a produção, isso é
        // perigoso»): o clique no dia escolhido com a ficha aberta tirava as
        // datas da produção. Esse gesto acabou: o calendário só escolhe ou
        // tira o dia. A página já não traz o formulário escondido, e o POST
        // que ele enviava não muda nada na base.
        var (_, _) = await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(
            inicioEm: new DateOnly(2026, 3, 2),
            fimEm: new DateOnly(2026, 3, 9));
        SeedUser("4207", "Job On Operador", "provider-jobon-8", "JobOnPass8!");
        using var client = await LoginClientAsync("4207", "JobOnPass8!");

        var open = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4&jobon_id={jobOnId}");
        Assert.DoesNotContain("lightRemoveForm", open);
        Assert.DoesNotContain("RemoveDay", open);
        Assert.DoesNotContain("dmo:calendar-unselect", open);

        // Mudar de dia, tirar o dia e voltar, com a produção aberta.
        foreach (var dia in new[] { "4", "0", "5", "4" })
        {
            await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia={dia}&jobon_id={jobOnId}");
        }

        var token = await GetVerificationTokenAsync(client, $"{PagePath}?novo=1");
        await client.PostAsync(
            $"{PagePath}?handler=RemoveDay",
            HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()),
                ("ano", "2026"), ("mes", "3"), ("dia", "4"),
                ("__RequestVerificationToken", token)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var jobOn = await db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(new DateOnly(2026, 3, 2), jobOn.InicioEm);
        Assert.Equal(new DateOnly(2026, 3, 9), jobOn.FimEm);
        Assert.Empty(await db.JobOnChanges.AsNoTracking().Where(c => c.JobOnId == jobOnId).ToListAsync());
    }

    [Fact]
    public async Task UnauthenticatedRequests_ReceiveNoPageAndNoMutation()
    {
        var (machineB1Id, _) = await SeedRegistryAsync();

        // Sem sessão autenticada a página nativa não é servida: o
        // [Authorize] desafia para o login antes de qualquer leitura.
        var anonymous = CreateClient();
        var response = await anonymous.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        // E o POST de criação também não: desafiado antes de escrever
        // qualquer coisa — o par token/cookie vem da página de login, a
        // única superfície anónima que o emite.
        var token = await GetVerificationTokenAsync(anonymous, "/login");
        var refused = await anonymous.PostAsync(
            $"{PagePath}?handler=Create",
            HtmlFormHelpers.BuildFormPost(
                ("production_number", "PN-ANON"),
                ("reference", "5810"),
                ("machine_id", machineB1Id.ToString()),
                ("cm_tool_id", "T1"),
                ("mf_tool_id", ""),
                ("bq_tool_id", ""),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.JobOns.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateFlow_WithOverlapOnSameMachine_SavesWithoutBanner()
    {
        // A3 — O AVISO AMARELO DO OVERLAP (o Owner: «overlap da maquina
        // deve dar um warning, nao deve bloquear, o warning deve dizer
        // detalhadamente o que esta mal como resolver»): a produção
        // existente na B1 ocupa 10/04–15/04; a nova na MESMA máquina
        // 12/04–20/04. A produção é GRAVADA na mesma (o mantra — nunca
        // um bloqueio, nunca a recusa vermelha) e o redirect FICA na
        // folha do Job On com o banner AMARELO detalhado (as janelas, o
        // período exato, o como-resolver) — o Resumo fica a um clique.
        var (machineB1Id, _) = await SeedRegistryAsync();

        // A produção existente NA MESMA máquina do registo (o id âncora
        // — o overlap é por MachineId, não por código).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = new JobOn
            {
                Reference = "5447",
                ProductionNumber = "PN-EXISTENTE-1",
                MachineId = machineB1Id,
                InicioEm = new DateOnly(2026, 4, 10),
                FimEm = new DateOnly(2026, 4, 15)
            };
            var cm = new CmContext { JobOn = jobOn, ToolId = "T1" };
            var mf = new MfContext { JobOn = jobOn, ToolId = "MF-TOOL-9" };
            db.JobOns.Add(jobOn);
            db.CmContexts.Add(cm);
            db.MfContexts.Add(mf);
            await db.SaveChangesAsync();
        }

        SeedUser("4215", "Job On Operador", "provider-jobon-15", "JobOnPass15!");
        using var client = await LoginClientAsync("4215", "JobOnPass15!");

        var created = await PostCreateAsync(
            client,
            $"{PagePath}?novo=1",
            "PN-OVERLAP-1",
            "5810",
            machineB1Id.ToString(),
            "T1",
            "MF-TOOL-9",
            "T-BQ1",
            inicioEm: "2026-04-12",
            fimEm: "2026-04-20");

        // GRAVOU: o redirect é o PRG do aviso — a folha do Job On com o
        // jobon_id e o overlap=1 (o comportamento canónico ao Resumo é
        // para a gravação SEM conflito).
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        Assert.StartsWith("/JobOn", location, StringComparison.Ordinal);
        Assert.DoesNotContain("overlap=1", location, StringComparison.Ordinal);
        Assert.Contains("jobon_id=", location, StringComparison.Ordinal);

        // A verdade persistida: DUAS produções na B1 (a nova está lá — o
        // aviso nunca bloqueou a gravação).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(2, await db.JobOns.AsNoTracking().CountAsync());
        }

        // Sem banner de sobreposição entre produções da linha (Diogo,
        // 25/10/2026: «so se aplica as bqs»).
        var bannerHtml = await GetPageHtmlAsync(client, location);
        Assert.DoesNotContain("sobrepõe-se", bannerHtml);
        Assert.DoesNotContain("12/04/2026–15/04/2026", bannerHtml);
    }

    [Fact]
    public async Task ToolCandidates_FilterByMachineAndProduction_AndShowAllRestoresAlternatives()
    {
        SeedUser("4831", "Picker User", "picker-provider", "PickerPass1!");
        var (b1, b2) = await SeedRegistryAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Single(t => t.ToolId == "T1").AssociatedMfReference = "5810";
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T1", MachineId = b1 });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T1", MachineId = b2 });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T2", MachineId = b2 });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4831", "PickerPass1!");
        using var json = await GetJsonAsync(client, $"/JobOn?handler=ToolCandidates&type=CM&machine_id={b1}&reference=5810");
        var cm = json.RootElement.GetProperty("items");
        Assert.Single(cm.EnumerateArray());
        Assert.Equal("T1", cm[0].GetProperty("toolId").GetString());
        Assert.Equal("B1 · B2", cm[0].GetProperty("machineCodes").GetString());
        Assert.Equal("Filtrado por máquina e referência", json.RootElement.GetProperty("filtro").GetString());
        using var allJson = await GetJsonAsync(client, $"/JobOn?handler=ToolCandidates&type=CM&machine_id={b1}&reference=UNKNOWN&all=true");
        Assert.Equal(2, allJson.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("Catálogo completo", allJson.RootElement.GetProperty("filtro").GetString());
        // Sem tipo, a procura não sabe que campo serve.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/JobOn?handler=ToolCandidates")).StatusCode);
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/JobOn?handler=ToolCandidates&type=CM")).StatusCode);
    }

    [Fact]
    public async Task ToolCandidates_SearchAsYouType_FindsByPartialReferenceOrLot()
    {
        SeedUser("4832", "Picker Search", "picker-search", "PickerPass2!");
        await SeedRegistryAsync();
        using var client = await LoginClientAsync("4832", "PickerPass2!");

        using var byReference = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=CM&q=544&all=true");
        Assert.Equal(new[] { "T1", "T2" }, ToolIds(byReference));
        using var lowerCase = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=BQ&q=t19&all=true");
        Assert.Equal(new[] { "T-BQ1" }, ToolIds(lowerCase));
        using var byLot = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=CM&q=3&all=true");
        Assert.Equal(new[] { "T2" }, ToolIds(byLot));
        using var none = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=MF&q=XYZ&all=true");
        Assert.Empty(ToolIds(none));
    }

    [Fact]
    public async Task ToolCandidates_Cm_FollowsTheChosenMfAndMachine_AndFallsBackToTheProductionReference()
    {
        // Duplicar um Job On e trocar a CM: a MF e a máquina da folha
        // sugerem as CM dessa MF (a referência ou a associação explícita).
        SeedUser("4833", "Picker MF", "picker-mf", "PickerPass3!");
        var (b1, b2) = await SeedRegistryAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Single(t => t.ToolId == "T1").AssociatedMfReference = "5810";
            db.Tools.Single(t => t.ToolId == "T2").AssociatedMfReference = "5810";
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T1", MachineId = b1 });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T2", MachineId = b2 });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4833", "PickerPass3!");

        using var byMf = await GetJsonAsync(client,
            $"/JobOn?handler=ToolCandidates&type=CM&machine_id={b1}&reference=OUTRA&mf_tool_id=MF-TOOL-9");
        Assert.Equal(new[] { "T1" }, ToolIds(byMf));
        Assert.Equal("Filtrado por máquina e pela MF 5810", byMf.RootElement.GetProperty("filtro").GetString());

        // Nenhuma CM da MF nesta máquina: volta à referência da produção.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Single(t => t.ToolId == "T1").AssociatedMfReference = null;
            await db.SaveChangesAsync();
        }
        using var fallback = await GetJsonAsync(client,
            $"/JobOn?handler=ToolCandidates&type=CM&machine_id={b1}&reference=5447&mf_tool_id=MF-TOOL-9");
        Assert.Equal(new[] { "T1" }, ToolIds(fallback));
        Assert.Equal("Filtrado por máquina e referência", fallback.RootElement.GetProperty("filtro").GetString());
    }

    [Fact]
    public async Task ToolCandidates_Mf_ShowsOnlyTheMfOfTheChosenCm_AndTheMachine()
    {
        // O Diogo (17/10/2026): com a CM associada, a lista da MF mostra só
        // as MF que trabalham com ela (a referência MF associada da CM, ou a
        // referência da CM) e, com máquina, só as dessa máquina.
        SeedUser("4836", "Picker CM", "picker-cm", "PickerPass6!");
        var (b1, b2) = await SeedRegistryAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "CM-ST100", Type = ToolType.Cm, Reference = "ST100", Lot = "5", AssociatedMfReference = "5810" });
            db.Tools.Add(new Tool { ToolId = "MF-5810-2", Type = ToolType.Mf, Reference = "5810", Lot = "2" });
            db.Tools.Add(new Tool { ToolId = "MF-5447", Type = ToolType.Mf, Reference = "5447", Lot = "1" });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "MF-TOOL-9", MachineId = b1 });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "MF-5810-2", MachineId = b2 });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4836", "PickerPass6!");

        using var byCm = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=MF&cm_tool_id=CM-ST100");
        Assert.Equal(new[] { "MF-TOOL-9", "MF-5810-2" }, ToolIds(byCm));
        Assert.Equal("Filtrado pela CM 5810", byCm.RootElement.GetProperty("filtro").GetString());

        using var byCmAndMachine = await GetJsonAsync(client, $"/JobOn?handler=ToolCandidates&type=MF&machine_id={b2}&cm_tool_id=CM-ST100");
        Assert.Equal(new[] { "MF-5810-2" }, ToolIds(byCmAndMachine));

        // A CM 5447 (sem associação) só encaixa na MF 5447; sem volta à
        // referência da produção quando nada corresponde.
        using var byReference = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=MF&cm_tool_id=T1&reference=5810");
        Assert.Equal(new[] { "MF-5447" }, ToolIds(byReference));
        // Nunca esconder (Diogo, 23/10/2026: «nas ferramentas estão lá mas o Job On não filtra»):
        // nenhuma MF da CM ligada à linha → as da CM sem o filtro de linha, com o aviso.
        using var none = await GetJsonAsync(client, $"/JobOn?handler=ToolCandidates&type=MF&machine_id={b1}&cm_tool_id=T1&reference=5810");
        Assert.Equal(new[] { "MF-5447" }, ToolIds(none));
        Assert.Equal("Filtrado por máquina e pela CM 5447 (nenhuma ligada a esta linha: sem filtro de linha)", none.RootElement.GetProperty("filtro").GetString());

        // «Mostrar todas» ignora a CM.
        using var all = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=MF&cm_tool_id=CM-ST100&all=true");
        Assert.Equal(3, ToolIds(all).Length);
    }

    [Fact]
    public async Task ToolCandidates_ReturnAtMost20_WithTheTotal()
    {
        SeedUser("4834", "Picker Limit", "picker-limit", "PickerPass4!");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            for (var i = 1; i <= 25; i++)
            {
                db.Tools.Add(new Tool { ToolId = $"BQ-{i:00}", Type = ToolType.Bq, Reference = "T173", Lot = i.ToString() });
            }
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4834", "PickerPass4!");
        using var page = await GetJsonAsync(client, "/JobOn?handler=ToolCandidates&type=BQ&all=true");
        Assert.Equal(20, page.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(25, page.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Sheet_RendersOnlyTheChosenTools_NeverTheWholeCatalog()
    {
        SeedUser("4835", "Picker Sheet", "picker-sheet", "PickerPass5!");
        await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync();
        using var client = await LoginClientAsync("4835", "PickerPass5!");

        var nova = await GetPageHtmlAsync(client, "/JobOn?novo=1");
        Assert.DoesNotContain("<option value=\"T1\"", nova, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"T-BQ1\"", nova, StringComparison.Ordinal);

        // Duplicar: a folha traz as ferramentas do Job On, e só essas.
        var duplicar = await GetPageHtmlAsync(client, $"/JobOn?jobon_id={jobOnId}&duplicar=1");
        Assert.Contains("<option value=\"T1\" selected=\"selected\">5447 · Lote 2", duplicar, StringComparison.Ordinal);
        Assert.Contains("<option value=\"MF-TOOL-9\" selected=\"selected\">5810 · Lote 1", duplicar, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"T2\"", duplicar, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"T-BQ1\"", duplicar, StringComparison.Ordinal);
    }

    private static async Task<System.Text.Json.JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] ToolIds(System.Text.Json.JsonDocument json) =>
        json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("toolId").GetString()!).ToArray();


    [Fact]
    public async Task Historico_Mostra25ProducoesPorPagina_MaisRecentesPrimeiro()
    {
        // O Owner 2026-10-16: «aplicar paginaçao nos historicos».
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B1" };
            for (var i = 1; i <= 30; i++)
            {
                db.JobOns.Add(new JobOn { Reference = "5447", ProductionNumber = $"PAG-{i:00}", Machine = machine });
            }
            await db.SaveChangesAsync();
        }
        SeedUser("4290", "Job On Operador", "provider-jobon-paginas", "JobOnPaginas1!");
        using var client = await LoginClientAsync("4290", "JobOnPaginas1!");

        var page1 = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447");
        var page2 = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&pagina=2");

        // As 30 mais recentes: PAG-30..PAG-06 na página 1, PAG-05.. na 2.
        Assert.Contains("<td>PAG-30</td>", page1);
        Assert.Contains("<td>PAG-06</td>", page1);
        Assert.DoesNotContain("<td>PAG-05</td>", page1);
        Assert.Contains("Página 1 de", page1);
        Assert.Contains("historico=1&amp;referencia=5447&amp;pagina=2", page1);
        Assert.Contains("id=\"jobonHistoricoResultados\" data-dmo-regiao", page1);
        Assert.Contains("<nav data-dmo-parcial class=\"dmo-list-pager\"", page1);
        Assert.Contains("<td>PAG-05</td>", page2);
        Assert.DoesNotContain("<td>PAG-06</td>", page2);
        // A tabela «Produções desta referência» era da ficha antiga, que
        // deixou de existir (Diogo, 23/10/2026): ver
        // Ficha_ProducoesDaReferencia_SaiComAFichaAntiga_AFolhaTemODropdown.
    }

    [Fact]
    public async Task Procura_EmDoisCampos_AReferenciaEncheODropdownDasProducoes()
    {
        // O Diogo, 17/10/2026: «ao escrever a referência na primeira, a segunda
        // mostra produção e máquina»; sem lista por baixo do campo («o problema
        // era ter as duas formas de filtrar a produção»). A referência completa
        // enche o dropdown (20/10/2026), com as 5 produções mais recentes;
        // escolher uma abre a folha.
        SeedHistoryJobOns(
            ("6018Y232", "202601", null, null), ("6018Y232", "202602", null, null),
            ("6018Y232", "202603", null, null), ("6018Y232", "202604", null, null),
            ("6018Y232", "202605", null, null), ("6018Y232", "202606", null, null),
            ("6018Y232", "202607", null, null), ("6018Y232", "202608", null, null),
            ("6018Y232", "202610", null, null), ("6018Y232", "202611", null, null),
            ("6018Y232", "202612", null, null), ("5804T200", "202609", null, null));
        int newestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            newestId = db.JobOns.Single(j => j.ProductionNumber == "202612").Id;
        }
        SeedUser("4295", "Job On Operador", "provider-jobon-procura-1", "JobOnProc1!");
        using var client = await LoginClientAsync("4295", "JobOnProc1!");

        // Sem procura: o campo e, à frente, o dropdown vazio e desativado.
        var empty = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4");
        Assert.Contains("id=\"lightSearch\"", empty);
        Assert.Contains("data-dmo-procura=\"/JobOn?ano=2026&amp;mes=3&amp;dia=4&amp;handler=ProcurarProducao\" data-dmo-procura-alvo=\"lightProcura\" data-dmo-procura-sempre", empty);
        Assert.DoesNotContain(">Procurar</button>", empty);
        Assert.Contains("<select id=\"lightProcuraProducoes\" aria-label=\"Produção (escreva primeiro a referência)\" disabled>", empty);
        Assert.DoesNotContain("lightProcuraReferencias", empty);
        // A página troca só as zonas que mudam: os avisos e as folhas; a
        // procura fica como o operador a deixou.
        Assert.Contains("class=\"beta-light-layout\" data-dmo-troca", empty);
        Assert.Contains("id=\"jobonAvisos\" data-dmo-regiao", empty);
        Assert.Contains("id=\"jobonFolhas\" data-dmo-regiao", empty);
        Assert.Contains("class=\"jobon-procura-bloco\" data-dmo-fica data-dmo-parcial", empty);

        // O meio da referência, a produção ou outra coisa: o dropdown fica vazio, sem lista.
        foreach (var termo in new[] { "018", "202609", "ZZZZ", "" })
        {
            var parcial = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&ano=2026&mes=3&dia=4&q={termo}");
            Assert.Contains("disabled>", parcial);
            Assert.DoesNotContain("<option value=\"/JobOn", parcial);
            Assert.DoesNotContain("<table", parcial);
        }

        // A referência completa (sem distinção de maiúsculas): as 5 mais recentes, «produção máquina».
        var dropdown = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&ano=2026&mes=3&dia=4&q=6018y232");
        // Com produções, o dropdown destaca-se (Diogo, 19/10/2026: enchia-se sem se ver).
        Assert.Contains("<select id=\"lightProcuraProducoes\" class=\"jobon-procura-com-resultados\" aria-label=\"Últimas produções de 6018y232\" data-dmo-ir>", dropdown);
        Assert.Contains("<option value=\"\" selected>Escolher produção</option>", dropdown);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(dropdown, "<option").Count);   // «Produção» e as 5
        Assert.Matches(@">202612 [A-Z]\d+</option>", dropdown);
        Assert.Matches(@">202607 [A-Z]\d+</option>", dropdown);
        Assert.DoesNotContain(">202606 ", dropdown);
        Assert.DoesNotContain(">202609 ", dropdown);
        // Escolher uma produção abre a folha nova, com o dia escolhido (Diogo, 23/10/2026).
        Assert.Contains($"value=\"/JobOn/Folha?jobon_id={newestId}&amp;dia=2026-03-04\"", dropdown);

        // O início não enche o dropdown (Diogo, 20/10/2026: «primeiro campo
        // filtra apenas referências completas, segundo aparece dropdown das
        // produções 202601 202602»): fica para as sugestões por baixo do campo.
        foreach (var termo in new[] { "6018", "601", "t2" })
        {
            var inicio = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&ano=2026&mes=3&dia=4&q={termo}");
            Assert.Contains("disabled>", inicio);
            Assert.DoesNotContain("<option value=\"/JobOn", inicio);
        }
        Assert.DoesNotContain("6018Y232 2026", dropdown);

        // Sem script, o Enter enche o dropdown pelo servidor e o campo fica com a referência.
        var entered = await GetPageHtmlAsync(client, $"{PagePath}?procura=5804T200");
        Assert.Contains("value=\"5804T200\"", entered);
        Assert.Matches(@">202609 [A-Z]\d+</option>", entered);
        Assert.DoesNotContain("lightProcuraReferencias", entered);
    }

    [Fact]
    public async Task Procura_SugereAsReferenciasPorBaixo_ParaOBrowser()
    {
        // O Diogo, 20/10/2026: «já pedi para arranjar o filtro do Planeamento
        // 5 vezes»; ao escrever «60» não aparecia nada por baixo do campo. O
        // campo sugere as referências como os outros campos de referência e
        // escolher uma enche o dropdown. A página, as sugestões e o dropdown
        // vão para o Chromium (tests/visual/nok_agente_browser.py).
        SeedHistoryJobOns(("6018Y232", "202601", null, null), ("6018Y232", "202602", null, null), ("5804T200", "202609", null, null));
        SeedUser("4296", "Job On Operador", "provider-jobon-procura-sugere", "JobOnProc3!");
        using var client = await LoginClientAsync("4296", "JobOnProc3!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?ano=2026&mes=3&dia=4");
        Assert.Matches("id=\"lightSearch\"[^>]*data-dmo-suggest=\"referencias\"", html);
        var sugestoes = await client.GetStringAsync("/Sugestoes?fonte=referencias&q=60");
        Assert.Contains("6018Y232", sugestoes);
        Assert.DoesNotContain("5804T200", sugestoes);
        var producoes = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&ano=2026&mes=3&dia=4&q=6018Y232");
        Assert.Matches(@">202602 [A-Z]\d+</option>", producoes);
        // Com o início, o dropdown espera pela referência escolhida (20/10/2026).
        var inicio = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&ano=2026&mes=3&dia=4&q=60");
        Assert.Contains("disabled>", inicio);

        ExportarParaOBrowser.Gravar("jobon-procura.html", html);
        ExportarParaOBrowser.Gravar("jobon-sugestoes-60.json", sugestoes);
        ExportarParaOBrowser.Gravar("jobon-producoes-6018Y232.html", producoes);
        ExportarParaOBrowser.Gravar("jobon-producoes-60.html", inicio);
    }

    [Fact]
    public async Task Procura_EmDoisCampos_NaoEncontraPelaFerramenta()
    {
        // A produção 5447 usa o MF 5810 lote 1: escrever «5810» não enche o
        // dropdown (só pela referência); «5447» enche-o com essa produção.
        await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9));
        SeedUser("4296", "Job On Lote", "provider-jobon-procura-2", "JobOnProc2!");
        using var client = await LoginClientAsync("4296", "JobOnProc2!");

        var byTool = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&q=5810");
        Assert.DoesNotContain($"data-id=\"{jobOnId}\"", byTool);
        var byReference = await GetPageHtmlAsync(client, $"{PagePath}?handler=ProcurarProducao&q=5447");
        Assert.Contains($"data-id=\"{jobOnId}\"", byReference);
    }

    [Fact]
    public async Task Procura_AoEscrever_SemLogin_ERecusada()
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{PagePath}?handler=ProcurarProducao&q=6018");
        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ficha_ProducoesDaReferencia_SaiComAFichaAntiga_AFolhaTemODropdown()
    {
        await SeedRegistryAsync();
        var jobOnId = await SeedExistingJobOnAsync(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9));
        SeedHistoryJobOns(("5447", "PN-ANTIGA", null, null), ("5447X", "PN-OUTRA-REF", null, null));
        var reads = new HistoryReads();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IJobOnListQuery>(sp =>
                new CountingJobOnListQuery(new JobOnListQuery(sp.GetRequiredService<DmoDbContext>()), reads))));
        SeedUser("4296", "Job On Operador", "provider-jobon-procura-2", "JobOnProc2!");
        using var client = await LoginClientAsync("4296", "JobOnProc2!", factory);

        // A FICHA ANTIGA SAIU (Diogo, 23/10/2026: «esta versão do Job On já não
        // deveria existir»): o link antigo da tabela leva à folha nova, sem ler
        // a tabela; as produções da referência estão no dropdown da folha.
        var antigo = await client.GetAsync($"{PagePath}?jobon_id={jobOnId}&producoes_ref=1");
        Assert.Equal(HttpStatusCode.Redirect, antigo.StatusCode);
        Assert.Equal($"/JobOn/Folha?jobon_id={jobOnId}", antigo.Headers.Location!.ToString());
        Assert.Equal(0, reads.ReferencePages);

        var folha = await GetPageHtmlAsync(client, antigo.Headers.Location!.ToString());
        Assert.DoesNotContain("id=\"fichaProducoes\"", folha);
        Assert.DoesNotContain("Ficha da produção", folha);
        var dropdown = folha[folha.IndexOf("id=\"jofProducoes\"", StringComparison.Ordinal)..];
        dropdown = dropdown[..dropdown.IndexOf("</select>", StringComparison.Ordinal)];
        Assert.Contains($"value=\"/JobOn/Folha?jobon_id={jobOnId}\" selected=\"selected\">PN-EXISTENTE-1", dropdown);
        Assert.DoesNotContain("PN-OUTRA-REF", dropdown);
        Assert.Equal(0, reads.ReferencePages);
    }

    private void SeedHistoryJobOns(params (string Reference, string ProductionNumber, DateOnly? InicioEm, DateOnly? FimEm)[] jobOns)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        foreach (var j in jobOns)
        {
            db.JobOns.Add(new JobOn { Reference = j.Reference, ProductionNumber = j.ProductionNumber, Machine = machine, InicioEm = j.InicioEm, FimEm = j.FimEm });
        }
        db.SaveChanges();
    }

    /// <summary>Conta as leituras do histórico, por cima da query real.</summary>
    private sealed class CountingJobOnListQuery(IJobOnListQuery inner, HistoryReads reads) : IJobOnListQuery
    {
        public Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads.Windows);
            return inner.ListWindowAsync(de, ate, cancellationToken);
        }

        public Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(DateOnly dia, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads.NextPerMachine);
            return inner.ListNextPerMachineAsync(dia, cancellationToken);
        }

        public Task<PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(JobOnHistoryFilter filter, int? pageNumber, int pageSize = ListPaging.DefaultPageSize, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads.Searches);
            return inner.SearchHistoryPageAsync(filter, pageNumber, pageSize, cancellationToken);
        }

        public Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(string reference, int? machineId = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads.Productions);
            return inner.ListProductionsOfReferenceAsync(reference, machineId, cancellationToken);
        }

        public Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(string reference, string? machine = null, CancellationToken cancellationToken = default) =>
            inner.ListByReferenceAsync(reference, machine, cancellationToken);

        public Task<PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(string reference, int? pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads.ReferencePages);
            return inner.ListPageOfReferenceAsync(reference, pageNumber, pageSize, cancellationToken);
        }
    }

    private sealed class HistoryReads
    {
        public int Searches;
        public int Productions;
        public int ReferencePages;
        public int Windows;
        public int NextPerMachine;
    }

    [Fact]
    public async Task Historico_RespostaParcial_PaginaSemLerCalendarioOuRail_EPreservaFiltros()
    {
        SeedHistoryJobOns(Enumerable.Range(1, 30).Select(i => ("5447", $"PAR-{i:00}", (DateOnly?)null, (DateOnly?)null)).ToArray());
        var reads = new HistoryReads();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IJobOnListQuery>(sp =>
                new CountingJobOnListQuery(new JobOnListQuery(sp.GetRequiredService<DmoDbContext>()), reads))));
        SeedUser("4298", "Histórico parcial", "provider-jobon-parcial", "JobOnParcial1!");
        using var anonymous = CreateClient();
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync($"{PagePath}?handler=HistoricoResultados&historico=1&referencia=5447")).StatusCode);
        using var client = await LoginClientAsync("4298", "JobOnParcial1!", factory);
        var response = await client.GetAsync($"{PagePath}?handler=HistoricoResultados&historico=1&referencia=5447&pagina=2&desde=invalida");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("jobonHistoricoResultados", response.Headers.GetValues("X-Dmo-Region").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("PAR-05", html);
        Assert.DoesNotContain("PAR-06", html);
        Assert.Contains("Desde", html);
        Assert.Contains("historico=1", html);
        Assert.DoesNotContain("handler=HistoricoResultados", html);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dmo-calendar", html);
        Assert.Equal(1, reads.Searches);
        Assert.Equal(1, reads.Productions);
        Assert.Equal(0, reads.Windows);
        Assert.Equal(0, reads.NextPerMachine);
        var empty = await client.GetStringAsync($"{PagePath}?handler=HistoricoResultados&historico=1");
        Assert.Contains(DMO.Alpha.Web.Pages.JobOn.IndexModel.HistoryEmptyStateMessage, empty);
        Assert.Equal(1, reads.Searches);
        Assert.Equal(0, reads.Windows);
    }

    [Fact]
    public async Task Historico_AbreEmBranco_SoComOsFiltros_SemLerProducoes()
    {
        // O proprietário, 16/10/2026: «Jobon historico continua fazer load de
        // tudo, nao é necessario». Sem filtros, a tab mostra só os filtros e
        // não lê nenhuma produção; a primeira procura lê.
        SeedHistoryJobOns(("5447", "PN-HIST-1", null, null), ("5810", "PN-HIST-2", new DateOnly(2026, 3, 2), null));
        var reads = new HistoryReads();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IJobOnListQuery>(sp =>
                new CountingJobOnListQuery(new JobOnListQuery(sp.GetRequiredService<DmoDbContext>()), reads))));
        SeedUser("4291", "Job On Operador", "provider-jobon-hist-1", "JobOnHist1!");
        using var client = await LoginClientAsync("4291", "JobOnHist1!", factory);

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.Equal(0, reads.Searches);
        Assert.Equal(0, reads.Productions);
        Assert.Contains(DMO.Alpha.Web.Pages.JobOn.IndexModel.HistoryEmptyStateMessage, html);
        Assert.Contains("name=\"referencia\"", html);
        Assert.Contains("data-dmo-suggest=\"referencias\"", html);
        Assert.Contains("name=\"desde\"", html);
        Assert.Contains("name=\"ate\"", html);
        Assert.Contains("name=\"maquina\"", html);
        Assert.DoesNotContain("name=\"producao\"", html);   // o dropdown só aparece com a referência
        // O painel lateral mostra as produções em curso pelas datas (Diogo,
        // 22/10/2026); o histórico em si continua sem produções.
        var semPainel = System.Text.RegularExpressions.Regex.Replace(html, "<aside class=\"beta-light-rail beta-production-rail\"[\\s\\S]*?</aside>", string.Empty);
        Assert.DoesNotContain("PN-HIST-1", semPainel);
        Assert.DoesNotContain("PN-HIST-2", semPainel);
        Assert.DoesNotContain("Página 1 de", html);

        var searched = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447");
        Assert.Equal(1, reads.Searches);
        Assert.Equal(1, reads.Productions);
        Assert.Contains("<td>PN-HIST-1</td>", searched);
        Assert.DoesNotContain("<td>PN-HIST-2</td>", searched);
        Assert.DoesNotContain(DMO.Alpha.Web.Pages.JobOn.IndexModel.HistoryEmptyStateMessage, searched);
    }

    [Fact]
    public async Task Historico_PorReferencia_DropdownComAsProducoesDela_EFiltraPelaEscolhida()
    {
        SeedHistoryJobOns(
            ("5447", "PN-A", null, null),
            ("5447", "PN-B", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            ("5810", "PN-C", null, null));
        SeedUser("4292", "Job On Operador", "provider-jobon-hist-2", "JobOnHist2!");
        using var client = await LoginClientAsync("4292", "JobOnHist2!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447");
        Assert.Contains("id=\"jobonHistProducao\"", html);
        Assert.Contains("<option value=\"PN-A\">PN-A</option>", html);
        Assert.Contains("<option value=\"PN-B\">PN-B</option>", html);
        Assert.DoesNotContain("value=\"PN-C\"", html);
        Assert.Contains("<td>PN-A</td>", html);
        Assert.Contains("<td>PN-B</td>", html);
        Assert.DoesNotContain("<td>PN-C</td>", html);

        var chosen = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&producao=PN-A");
        Assert.Contains("<option value=\"PN-A\" selected=\"selected\">PN-A</option>", chosen);
        Assert.Contains("<td>PN-A</td>", chosen);
        Assert.DoesNotContain("<td>PN-B</td>", chosen);

        // Uma produção de outra referência (a referência mudou) não filtra.
        var stale = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5810&producao=PN-A");
        Assert.Contains("<td>PN-C</td>", stale);
        Assert.DoesNotContain("<td>PN-A</td>", stale);
    }

    [Fact]
    public async Task Historico_ReferenciaEMaquina_DropdownSoComAsProducoesDessaMaquina()
    {
        // A regra do proprietário (16/10/2026): primeiro a referência (e a
        // máquina, quando faz sentido), depois o dropdown só com as produções
        // dessa referência, lido só para ela.
        int b2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var b2 = new Machine { Code = "B2" };
            db.JobOns.Add(new JobOn { Reference = "5447", ProductionNumber = "PN-M1", Machine = b1 });
            db.JobOns.Add(new JobOn { Reference = "5447", ProductionNumber = "PN-M2", Machine = b2 });
            db.SaveChanges();
            b2Id = b2.Id;
        }
        SeedUser("4295", "Job On Operador", "provider-jobon-hist-5", "JobOnHist5!");
        using var client = await LoginClientAsync("4295", "JobOnHist5!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&maquina={b2Id}");
        Assert.Contains($"<option value=\"{b2Id}\" selected=\"selected\">B2</option>", html);
        Assert.Contains("<option value=\"PN-M2\">PN-M2</option>", html);
        Assert.DoesNotContain("value=\"PN-M1\"", html);
        Assert.Contains("<td>PN-M2</td>", html);
        Assert.DoesNotContain("<td>PN-M1</td>", html);

        // Uma máquina que não existe não filtra.
        var unknown = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&maquina=999999");
        Assert.Contains("<td>PN-M1</td>", unknown);
        Assert.Contains("<td>PN-M2</td>", unknown);
    }

    [Fact]
    public async Task Historico_PorData_MostraAsProducoesCujaJanelaTocaOPeriodo()
    {
        SeedHistoryJobOns(
            ("5447", "PN-JAN", new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10)),
            ("5447", "PN-MAR", new DateOnly(2026, 2, 27), new DateOnly(2026, 3, 4)),
            ("5447", "PN-SEMDIA", null, null));
        SeedUser("4293", "Job On Operador", "provider-jobon-hist-3", "JobOnHist3!");
        using var client = await LoginClientAsync("4293", "JobOnHist3!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&desde=2026-03-01&ate=2026-03-31");
        Assert.Contains("<td>PN-MAR</td>", html);
        Assert.DoesNotContain("<td>PN-JAN</td>", html);
        Assert.DoesNotContain("<td>PN-SEMDIA</td>", html);
        Assert.Contains("value=\"2026-03-01\"", html);
        Assert.Contains("value=\"2026-03-31\"", html);

        var none = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&desde=2026-06-01");
        Assert.Contains("Sem produções para esta procura.", none);
    }

    [Fact]
    public async Task Historico_DataInvalida_IgnoradaComAviso_NaoRecusa()
    {
        SeedHistoryJobOns(("5447", "PN-AVISO", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 4)));
        SeedUser("4294", "Job On Operador", "provider-jobon-hist-4", "JobOnHist4!");
        using var client = await LoginClientAsync("4294", "JobOnHist4!");

        // A data ilegível é ignorada com um aviso; a procura pela referência segue.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&desde=31-13-2026");
        Assert.Contains("jobon-aviso warn", html);
        Assert.Contains("«Desde» ignorada: «31-13-2026» não é uma data válida.", html);
        Assert.Contains("<td>PN-AVISO</td>", html);

        // Só com uma data ilegível não há procura: o histórico fica em branco.
        var onlyBad = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&ate=amanha");
        Assert.Contains("«Até» ignorada: «amanha» não é uma data válida.", onlyBad);
        Assert.Contains(DMO.Alpha.Web.Pages.JobOn.IndexModel.HistoryEmptyStateMessage, onlyBad);
        Assert.DoesNotContain("<td>PN-AVISO</td>", onlyBad);

        // Desde depois de Até: as datas trocam-se, com aviso.
        var swapped = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&desde=2026-03-31&ate=2026-03-01");
        Assert.Contains("as datas foram trocadas", swapped);
        Assert.Contains("<td>PN-AVISO</td>", swapped);
    }

    [Fact]
    public async Task Historico_Filtros_TrocamSoOsResultadosEAsProducoes()
    {
        // Deep sweep, 18/10/2026 (n.º 57): cada filtro do histórico
        // recarregava a página toda. Os filtros ficam (data-dmo-fica) e o
        // DmoTroca troca só os resultados e o dropdown das produções; para
        // isso, fora das zonas, a página é igual em todas as escolhas.
        int b2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var b2 = new Machine { Code = "B2" };
            db.JobOns.Add(new JobOn { Reference = "5447", ProductionNumber = "PN-Z1", Machine = b1, InicioEm = new DateOnly(2026, 3, 2), FimEm = new DateOnly(2026, 3, 4) });
            db.JobOns.Add(new JobOn { Reference = "5447", ProductionNumber = "PN-Z2", Machine = b2 });
            db.JobOns.Add(new JobOn { Reference = "5810", ProductionNumber = "PN-Z3", Machine = b2 });
            db.SaveChanges();
            b2Id = b2.Id;
        }
        SeedUser("4297", "Job On Operador", "provider-jobon-hist-zonas", "JobOnHistZ1!");
        using var client = await LoginClientAsync("4297", "JobOnHistZ1!");

        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.Contains("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>", vazio);
        Assert.Contains("id=\"jobonHistFiltros\"", vazio);
        // Sem referência, a zona das produções existe, escondida e vazia.
        Assert.Contains("<div id=\"jobonHistProducaoZona\" class=\"dmo-field\" data-dmo-regiao hidden=\"hidden\">", vazio);
        Assert.DoesNotContain("name=\"producao\"", vazio);

        var fora = Controlo.PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[]
        {
            "referencia=5447",
            $"referencia=5447&maquina={b2Id}",
            "referencia=5447&producao=PN-Z1",
            "referencia=5810&producao=PN-Z1",
            "desde=2026-03-01&ate=2026-03-31",
            "referencia=5447&desde=31-13-2026",
            "ate=amanha",
            "referencia=NAOEXISTE",
            "referencia=5447&pagina=2",
        })
        {
            var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&{filtros}");
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
        }

        // A referência traz o dropdown das produções dela; a máquina estreita-o.
        var referencia = Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447"), "jobonHistProducaoZona");
        Assert.DoesNotContain("hidden", referencia[..referencia.IndexOf('>')]);
        Assert.Contains("<option value=\"PN-Z1\">PN-Z1</option>", referencia);
        Assert.Contains("<option value=\"PN-Z2\">PN-Z2</option>", referencia);
        var maquina = Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&maquina={b2Id}"), "jobonHistProducaoZona");
        Assert.DoesNotContain("PN-Z1", maquina);
        Assert.Contains("<option value=\"PN-Z2\">PN-Z2</option>", maquina);

        // A referência aplica-se ao escrever (deep sweep 2, 18/10/2026, F-10):
        // antes ficava no campo sem Enter e a lista era a do filtro anterior.
        // Escrever outra referência tira a produção escolhida para a anterior.
        Assert.Matches("<form [^>]*id=\"jobonHistFiltros\" data-dmo-ao-escrever=\"jobonHistoricoResultados\"", vazio);
        Assert.Contains("<select id=\"jobonHistProducao\" name=\"producao\" data-dmo-autosubmit data-dmo-depende=\"jobonHistReferencia\">", referencia);
    }

    [Fact]
    public async Task Historico_ReferenciaPeloInicio_ODropdownLevaAReferenciaEFiltraSoEssaProducao()
    {
        // Deep sweep 2 (18/10/2026): a referência pelo início dela ou da parte
        // BQ, nunca a meio (F-2); o dropdown mostra as últimas 5 produções,
        // cada uma com a sua referência, e escolher uma filtra só essa, mesmo
        // com o mesmo nº noutra referência (F-17).
        SeedHistoryJobOns(
            ("5447T173", "PN-D1", null, null),
            ("5447Y232", "PN-D1", null, null),
            ("5447T173", "PN-D2", null, null),
            ("5447Y232", "PN-D2", null, null),
            ("5447T173", "PN-D3", null, null),
            ("5447Y232", "PN-D3", null, null),
            ("4996T350", "PN-D4", null, null),
            ("3540Y079", "PN-D5", null, null));
        SeedUser("4298", "Job On Operador", "provider-jobon-hist-inicio", "JobOnHistI1!");
        using var client = await LoginClientAsync("4298", "JobOnHistI1!");

        static int Linhas(string html, string numero) =>
            System.Text.RegularExpressions.Regex.Matches(html, $"<td>{numero}</td>").Count;

        var zona = Controlo.PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447"), "jobonHistProducaoZona");
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(zona, "<option value=\"PN-D").Count);
        Assert.Contains("<option value=\"PN-D3|5447Y232\">PN-D3 · 5447Y232</option>", zona);
        Assert.Contains("<option value=\"PN-D3|5447T173\">PN-D3 · 5447T173</option>", zona);
        Assert.Contains("<option value=\"PN-D1|5447Y232\">PN-D1 · 5447Y232</option>", zona);
        Assert.DoesNotContain("PN-D1|5447T173", zona);

        // A escolha leva a referência: só essa produção.
        var escolhida = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&producao=PN-D3%7C5447T173");
        Assert.Contains("<option value=\"PN-D3|5447T173\" selected=\"selected\">PN-D3 · 5447T173</option>", escolhida);
        Assert.Equal(1, Linhas(escolhida, "PN-D3"));
        Assert.Contains("<td><strong>5447T173</strong></td>", escolhida);
        Assert.DoesNotContain("<td><strong>5447Y232</strong></td>", escolhida);

        // Um link antigo só com o nº continua a filtrar, por uma só produção.
        var antigo = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&producao=PN-D3");
        Assert.Equal(1, Linhas(antigo, "PN-D3"));
        Assert.Contains("selected=\"selected\">PN-D3 · 5447Y232</option>", antigo);

        // Uma produção mais antiga que as últimas 5, pedida por link, entra no dropdown e filtra.
        var maisAntiga = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=5447&producao=PN-D1%7C5447T173");
        Assert.Contains("<option value=\"PN-D1|5447T173\" selected=\"selected\">PN-D1 · 5447T173</option>", maisAntiga);
        Assert.Equal(1, Linhas(maisAntiga, "PN-D1"));
        Assert.Contains("<td><strong>5447T173</strong></td>", maisAntiga);

        // «35» encontra a 3540Y079, nunca a 4996T350 com 35 a meio; «y2» a parte BQ.
        var meio = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=35");
        Assert.Equal(1, Linhas(meio, "PN-D5"));
        Assert.Equal(0, Linhas(meio, "PN-D4"));
        var parteBq = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=y2");
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(parteBq, "<td><strong>5447Y232</strong></td>").Count);
        Assert.DoesNotContain("<td><strong>5447T173</strong></td>", parteBq);
    }
}
