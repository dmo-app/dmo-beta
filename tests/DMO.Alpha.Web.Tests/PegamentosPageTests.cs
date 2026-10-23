using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
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
/// Prova o slice vertical nativo da folha de Pegamentos (corte
/// production-scoped): a folha por controlo_id mostra o contexto herdado
/// do Job On (as tools NUNCA são selecionadas — o operador mede a
/// combinação CM+BQ+MF do contexto ativo), o gargalo em falta é
/// completado no fluxo e grava no tool_id owner, a primeira medição cria
/// o registo durável (pegamentos_id) com a tolerância congelada, o POST
/// regista o gesto em cruz com PRG e todos os cálculos (ovalização com
/// sinal, média, corredor, mapa de limites) vêm do backend; o MF é
/// OBRIGATÓRIO na criação do Job On (regra do Owner 2026-10-10: «jobon
/// necessita sempre de MF, senao nao faz garrafas ou frascos» — o molde
/// final dá a forma do produto e a base 4-dígitos da referência), pelo
/// que o J10 real nasce com o seu MF-TOOL-9 (o desenho) e a folha mostra
/// o cartão Molde final herdado do contexto; o Resumo liga à folha
/// pelo controlo_id (edição mínima: o link de navegação); o anónimo é
/// desafiado pelo [Authorize] ANTES de qualquer leitura/escrita.
/// </summary>
public partial class PegamentosPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Pegamentos";

    private readonly CustomWebApplicationFactory _factory;

    public PegamentosPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    /// <summary>
    /// A base InMemory é PARTILHADA por todas as classes de teste do host
    /// (nome fixo); cada teste limpa o estado de domínio que tocou ao
    /// sair, para que as classes seguintes encontrem o mesmo estado limpo
    /// (as tabelas de Pegamentos ANTES de Tools/Controlos — a ordem
    /// dependente→principal das FKs Restrict).
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
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
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

    private void SeedUser(string operatorId, string name, string providerUserId, string password, bool ferramentas = false)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        // O user operacional com O SEU TEMPLATE (a regra do corte I: «User
        // sem Template = sem operacional»): os POSTs do Controlo exigem a
        // capability (o modo chefe: o approve não cria).
        var templateKey = ferramentas ? "tpl-teste-operacional-ferramentas" : "tpl-teste-operacional";
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == templateKey);
        if (template is null)
        {
            template = new DMO.Alpha.Core.Admin.Template
            {
                TemplateKey = templateKey,
                Name = ferramentas ? "Operacional de teste com Ferramentas" : "Operacional de teste",
                Capabilities = ferramentas
                    ? new[]
                    {
                        DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate,
                        DMO.Alpha.Core.Admin.DmoCapabilities.ControloCreate,
                        DMO.Alpha.Core.Admin.DmoCapabilities.Ferramentas
                    }
                    : new[]
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
    /// Semeia a produção real (o J10 da base): B1, T3 (CM 5447 lote 3,
    /// NNPB sem gargalo), BQ-1 (BQ T194 12 sem gargalo) e o MF-TOOL-9
    /// (MF 5810 lote 1 — o molde final OBRIGATÓRIO, regra do Owner
    /// 2026-10-10; a referência do desenho bate com a base 4-dígitos da
    /// referência 5810 da produção). O Job On nasce pela porta canónica
    /// CreateJobOn — o comando recebe MfToolId="MF-TOOL-9" — com os
    /// contextos CM/BQ/MF e o controlo associado na mesma escrita.
    /// </summary>
    private async Task<int> SeedRealProductionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb });
        await db.SaveChangesAsync();

        // O Job On real pela porta canónica: o MF é obrigatório (regra do
        // Owner 2026-10-10) e o CreateJobOn recebe MfToolId="MF-TOOL-9" —
        // o controlo_id nasce na MESMA escrita atómica da produção.
        var createJobOn = scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
        var created = await createJobOn.HandleAsync(new CreateJobOn(
            "J10",
            "5810",
            machine.Id,
            "T3",
            "MF-TOOL-9",
            "BQ-1"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);

        return await db.Controlos.AsNoTracking()
            .Where(c => c.JobOnId == created.JobOn!.JobOnId)
            .Select(c => c.Id)
            .SingleAsync();
    }

    private async Task<HttpResponseMessage> PostMeasurementAsync(
        HttpClient client,
        string pageUrl,
        int controloId,
        string component,
        string costura,
        string? contraCostura = null,
        string? gargalo = null)
    {
        var token = await GetVerificationTokenAsync(client, pageUrl);
        var fields = new List<(string, string)>
        {
            ("controlo_id", controloId.ToString()),
            ("component", component),
            ("costura", costura),
            ("__RequestVerificationToken", token)
        };
        if (contraCostura is not null)
        {
            fields.Add(("contra_costura", contraCostura));
        }
        if (gargalo is not null)
        {
            fields.Add(("gargalo", gargalo));
        }

        return await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(fields.ToArray()));
    }

    [Fact]
    public async Task AnonymousRequests_ChallengedBeforeAnyReadOrWrite()
    {
        // O [Authorize] desafia o anónimo ANTES de qualquer leitura/escrita:
        // nem a folha é servida, nem o controlo é resolvido.
        var controloId = await SeedRealProductionAsync();

        using var client = CreateClient();
        var response = await client.GetAsync($"{PagePath}?controlo_id={controloId}");

        // O [Authorize] desafia o anónimo ANTES de qualquer leitura/escrita
        // (o Location pode vir como URI absoluto com o ReturnUrl — padrão
        // do teste irmão do Resumo).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Pegamentos.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.PegamentosMeasurements.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task GetWithoutControloId_ShowsProvenanceState_NothingInvented()
    {
        // Sem controlo_id: os Pegamentos novos, sem Job On (as ferramentas
        // à mão) ou associados a um Job On — nenhum contexto inventado.
        SeedUser("4400", "Pegamentos Operador", "provider-peg-1", "PegPass1!");
        using var client = await LoginClientAsync("4400", "PegPass1!");

        var html = await GetPageHtmlAsync(client, PagePath);

        Assert.Contains("Sem Job On", html);
        Assert.Contains("Associar Job On", html);
        Assert.Contains("id=\"peg-tools-title\">Ferramentas<", html);
        AssertModoNoCartao(html, "peg-tools-title", jobOn: false);
        AssertModoNoCartao(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon"), "peg-jobon-title", jobOn: true);
        Assert.DoesNotContain("Criar Pegamentos", html);
        Assert.DoesNotContain("Contexto da produção", html);
    }

    [Fact]
    public async Task GetUnknownControloId_ShowsCleanWarning_NoSheetInvented()
    {
        SeedUser("4401", "Pegamentos Operador", "provider-peg-2", "PegPass2!");
        using var client = await LoginClientAsync("4401", "PegPass2!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id=999");

        Assert.Contains("O controlo indicado não existe", html);
        Assert.DoesNotContain("Contexto do Job On", html);
    }

    [Fact]
    public async Task GetFolha_ControloId_ShowsInheritedContext_GargaloMissing_MfAlwaysInherited()
    {
        // A folha por controlo_id: o contexto canónico herdado (T3/BQ-1 e o
        // MF-TOOL-9 obrigatório — o J10 real agora com o seu molde final,
        // regra do Owner 2026-10-10), o gargalo em falta visível como
        // completion (CM, BQ e MF — a ferramenta MF nova também está em
        // falta) e o estado de ausência do registo (ainda sem medições) —
        // a folha mostra-se pronta a medir.
        SeedUser("4402", "Pegamentos Operador", "provider-peg-3", "PegPass3!", ferramentas: true);
        using var client = await LoginClientAsync("4402", "PegPass3!");

        var controloId = await SeedRealProductionAsync();
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        Assert.Contains("Contexto da produção", html);
        // Visual alignment: backend-derived Job On context and components
        // use the Prototype presentation, without prototype demo data.
        Assert.Contains("css/dmo-pegamentos-prototype.css", html);
        Assert.Contains("peg-section-head", html);
        Assert.Contains("peg-context-grid", html);
        Assert.Contains("peg-ferramenta-linha", html);
        Assert.Contains("peg-readings-title", html);
        Assert.Contains("peg-registo-tabela", html);
        Assert.Contains("peg-map-heading", html);
        Assert.Contains("J10", html);
        Assert.Contains("5810", html);
        // P2-3: os ids de backend NUNCA como factos da folha — as tools
        // pelos FATOS HUMANOS (a referência + o lote das folhas); os
        // hex só vivem no registo (o padrão declarado pelo próprio
        // Ferramentas).
        Assert.DoesNotContain(">T3<", html);
        Assert.Contains("5447", html);
        Assert.Contains("T194", html);
        // O cartão MF (cor #0891B2 no mapa, nome «MF») — herdado
        // do Job On com a sua tool, referência do desenho e lote: o MF
        // dá a forma do produto e a base 4-dígitos da referência.
        Assert.Contains("id=\"peg-comp-Mf-title\" data-peg-titulo>MF ", html);
        Assert.DoesNotContain(">MF-TOOL-9<", html);
        Assert.Contains(" · Lote 1<", html);
        // O gargalo em falta: campo de completion no fluxo — também no MF
        // (a ferramenta nova só tem o gargalo como valor técnico — regra
        // do Owner).
        // Sem nominal não há Pegamentos (Diogo, 17/10/2026): o nominal
        // preenche-se na ficha da ferramenta, que volta a esta folha, e a
        // linha de registo só aparece depois.
        Assert.Contains("não tem nominal do gargalo", html);
        Assert.Contains("Abrir a ficha da ferramenta", html);
        Assert.Contains($"/Ferramentas/Ficha?tool_id=MF-TOOL-9&amp;tecnica=1&amp;continuar=%2FControlo%2FPegamentos%3Fcontrolo_id%3D{controloId}", html);
        var cartaoMf = Section(html, "data-peg-component=\"MF\"");
        Assert.Contains("não tem nominal do gargalo", cartaoMf);
        Assert.DoesNotContain("name=\"gargalo\"", cartaoMf);
        Assert.DoesNotContain("id=\"costura-Mf\"", html);
        // O registo ausente é o estado válido antes da primeira medição: sem texto e sem «Extrair PDF».
        Assert.DoesNotContain("Ainda sem medições nesta produção.", html);
        Assert.DoesNotContain("id=\"pegExtrairPdf\"", html);
        // O default do Owner exibido como tolerância efetiva corrente.
        Assert.Contains("nominal ± 0.20 mm", html);
        // O form da medição em cruz presente por componente.
        Assert.Contains("Costura (0°) mm", html);
        Assert.Contains("Contra costura (90°) mm", html);
        // As tools NUNCA são selecionáveis na folha (spec: nada de
        // re-selecionar o contexto já estabelecido).
        Assert.DoesNotContain("Abrir ferramentas", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostMeasurement_CreatesRegistro_PRG_BackToShowWithBackendCalculations(bool decimalComma)
    {
        string Input(string value) => decimalComma ? value.Replace('.', ',') : value;
        // A primeira medição (CM em cruz com o gargalo completado no
        // fluxo): PRG de volta à folha, o registo durável nasce com a
        // tolerância congelada, o gargalo grava no tool_id OWNER (a T3
        // ganha diametro_gargalo 70.00) e a folha mostra os cálculos do
        // backend — ovalização com sinal, média, corredor e o mapa.
        SeedUser("4403", "Pegamentos Operador", "provider-peg-4", "PegPass4!");
        using var client = await LoginClientAsync("4403", "PegPass4!");

        var controloId = await SeedRealProductionAsync();
        var sheetUrl = $"{PagePath}?controlo_id={controloId}";

        var posted = await PostMeasurementAsync(
            client, sheetUrl, controloId, "CM", Input("70.10"), contraCostura: Input("69.90"), gargalo: Input("70.00"));

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Equal(sheetUrl, posted.Headers.Location!.ToString());

        // O backend persistiu: UM registo do controlo com a tolerância
        // congelada e UMA leitura com o nominal congelado; a T3 ganhou o
        // gargalo no tool_id owner (nunca no registo de Pegamentos).
        int registroId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var registro = await db.Pegamentos.AsNoTracking().SingleAsync();
            registroId = registro.Id;
            Assert.Equal(controloId, registro.ControloId);
            Assert.Equal("T3", registro.CmToolId);
            Assert.Equal("BQ-1", registro.BqToolId);
            // O MF obrigatório do J10 congela no registo com os factos
            // humanos do desenho (CORTE A: referência/lote à criação).
            Assert.Equal("MF-TOOL-9", registro.MfToolId);
            Assert.Equal("5810", registro.MfToolReferenceFrozen);
            Assert.Equal("1", registro.MfToolLotFrozen);
            Assert.Equal(0.20m, registro.ToleranceMm);

            var measurement = await db.PegamentosMeasurements.AsNoTracking().SingleAsync();
            Assert.Equal(registro.Id, measurement.PegamentosId);
            Assert.Equal(1, measurement.RowPosition);
            Assert.Equal(PegamentosComponent.Cm, measurement.Component);
            Assert.Equal(70.10m, measurement.CosturaMm);
            Assert.Equal(69.90m, measurement.ContraCosturaMm);
            Assert.Equal(70.00m, measurement.NominalGargaloMm);

            var t3 = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "T3");
            Assert.Equal(70.00m, t3.DiametroGargaloMm);
            Assert.Equal(CmProcess.Nnpb, t3.Process);
        }

        // A folha reaberta: os cálculos do backend com formato pt-PT
        // (ovalização +0,20 com sinal preservado, média 70,00, estado
        // fora porque a ovalização toca a tolerância 0,20 — Diogo,
        // 19/10/2026, F8), o corredor nominal±0,20, o registo durável
        // visível e o mapa de limites projetado.
        var html = await GetPageHtmlAsync(client, sheetUrl);
        // A ovalização com sinal preservado: o HtmlEncoder escreve o «+»
        // como entidade &#x2B; (o sinal é o fato do Owner — «+0,3 e −0,3
        // são fatos distintos»); o negativo renderiza com o hífen literal.
        Assert.Contains("&#x2B;0.20", html);
        Assert.Contains("70.00", html);
        Assert.Contains(">fora<", html);
        Assert.Contains("69.80", html);
        Assert.Contains("70.20", html);
        // A regra do Owner — ids fora das folhas: o registo é provado pela
        // ausência do estado vazio, nunca pelo pegamentos_id visível.
        Assert.DoesNotContain("Ainda sem medições nesta produção.", html);
        Assert.DoesNotContain("pegamentos_id", html);
        Assert.Contains("Mapa de limites", html);
        Assert.Contains("1 medição fora da tolerância", html);
        // A folha com medições vai para o Chromium (tests/visual/ecra_pequeno_browser.py).
        ExportarParaOBrowser.Gravar("pegamentos-folha.html", html);
        // O gargalo completado deixou de estar em falta na T3 (o input de
        // completion da CM desapareceu, substituído pelo facto herdado) — a
        // BQ-1 e o MF-TOOL-9 continuam em falta, o estado truthful delas
        // (a ferramenta MF nova só ganha o gargalo quando o operador a
        // medir e completar no fluxo).
        Assert.Contains("id=\"costura-Cm\"", html);
        Assert.DoesNotContain("id=\"costura-Bq\"", html);
        Assert.DoesNotContain("id=\"costura-Mf\"", html);
    }

    [Fact]
    public async Task PostMeasurement_SingleAxis_OvalizacaoUndefined_MediaEqualsCostura()
    {
        // A BQ em eixo único (a geometria real não permite o 90°): a
        // medição regista-se sem bloqueio, a ovalização fica indefinida
        // (—) e a média = costura; o gargalo da BQ-1 completado no fluxo.
        SeedUser("4404", "Pegamentos Operador", "provider-peg-5", "PegPass5!");
        using var client = await LoginClientAsync("4404", "PegPass5!");

        var controloId = await SeedRealProductionAsync();
        var sheetUrl = $"{PagePath}?controlo_id={controloId}";

        var posted = await PostMeasurementAsync(
            client, sheetUrl, controloId, "BQ", "59.90", gargalo: "60.00");

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        var html = await GetPageHtmlAsync(client, sheetUrl);
        Assert.Contains("59.90", html);
        // A linha da BQ: ovalização «—» (indefinida) — o estado truthful
        // do eixo único, nunca um segundo eixo fabricado.
        Assert.Contains("—", html);
        Assert.Contains("id=\"peg-comp-Bq-title\" data-peg-titulo>BQ ", html);
        // O contexto no título, sem repetir no cartão (Diogo, 24/10/2026).
        Assert.Matches("data-peg-titulo>BQ (<a[^>]*>)?T194(</a>)? · Lote 12<", html);
        Assert.DoesNotContain("<dt>Lote</dt>", html);
        Assert.DoesNotContain("<dt>Referência</dt>", html);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var measurement = await db.PegamentosMeasurements.AsNoTracking().SingleAsync();
            Assert.Equal(PegamentosComponent.Bq, measurement.Component);
            Assert.Null(measurement.ContraCosturaMm);
            Assert.Equal(60.00m, measurement.NominalGargaloMm);

            var bq = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "BQ-1");
            Assert.Equal(60.00m, bq.DiametroGargaloMm);
        }
    }

    [Fact]
    public async Task PostMeasurement_MfInProduction_RegistersWithInFlowGargaloCompletion()
    {
        // O MF do J10 real (o molde final OBRIGATÓRIO — regra do Owner
        // 2026-10-10: sem ele não se fazem garrafas nem frascos): a
        // medição em cruz do MF regista-se como a de qualquer componente
        // herdado do contexto; o gargalo em falta na ferramenta nova é
        // completado no fluxo e grava no tool_id OWNER (a MF-TOOL-9 ganha
        // diametro_gargalo 59.00) — nenhuma tool inventada, o facto é da
        // ferramenta.
        SeedUser("4405", "Pegamentos Operador", "provider-peg-6", "PegPass6!");
        using var client = await LoginClientAsync("4405", "PegPass6!");

        var controloId = await SeedRealProductionAsync();
        var sheetUrl = $"{PagePath}?controlo_id={controloId}";

        var posted = await PostMeasurementAsync(
            client, sheetUrl, controloId, "MF", "59.10", contraCostura: "58.90", gargalo: "59.00");

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Equal(sheetUrl, posted.Headers.Location!.ToString());

        // O backend persistiu: a leitura do MF com o nominal congelado e a
        // completion gravada no tool_id owner MF-TOOL-9 (nunca no registo
        // de Pegamentos).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var registro = await db.Pegamentos.AsNoTracking().SingleAsync();
            Assert.Equal("MF-TOOL-9", registro.MfToolId);

            var measurement = await db.PegamentosMeasurements.AsNoTracking().SingleAsync();
            Assert.Equal(registro.Id, measurement.PegamentosId);
            Assert.Equal(PegamentosComponent.Mf, measurement.Component);
            Assert.Equal(59.00m, measurement.NominalGargaloMm);

            var mf = await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "MF-TOOL-9");
            Assert.Equal(59.00m, mf.DiametroGargaloMm);
        }

        // A folha reaberta: o cartão do MF com a sua leitura, o
        // mapa com a lane do MF na cor do componente (#0891B2 — cor do
        // Owner) e o gargalo completado — o form de completion do MF
        // desapareceu (a BQ-1 continua em falta, o estado truthful dela).
        var html = await GetPageHtmlAsync(client, sheetUrl);
        Assert.Contains("id=\"peg-comp-Mf-title\" data-peg-titulo>MF ", html);
        Assert.Contains("59.00", html);
        Assert.Contains("#0891B2", html);
        Assert.Contains("id=\"costura-Mf\"", html);
        Assert.DoesNotContain("id=\"costura-Bq\"", html);
    }

    [Fact]
    public async Task MissingNominal_RefusesMeasurement_AndPointsToTheToolSheet()
    {
        // Diogo, 17/10/2026: «não dá para fazer Pegamentos sem nominal do
        // gargalo». Sem nominal na ferramenta, a folha manda à ficha da
        // ferramenta e a medição é recusada sem gravar nada.
        SeedUser("4498", "Pegamentos Operador", "provider-peg-missing-nominal", "PegMissing1!");
        using var client = await LoginClientAsync("4498", "PegMissing1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var html = await GetPageHtmlAsync(client, url);
        if (Environment.GetEnvironmentVariable("DMO_FORM_EVIDENCE_DIR") is { } root)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "pegamentos-missing-nominal.html"), html);
        }

        Assert.Contains("peg-card-sem-nominal", html);
        Assert.Contains("A ferramenta 5447 não tem nominal do gargalo.", html);
        // Sem o módulo Ferramentas, a folha não liga à ficha: pede a quem a gere.
        Assert.Contains("Peça a quem gere as Ferramentas para o preencher", html);
        Assert.DoesNotContain("/Ferramentas/Ficha", html);
        // Nenhum cartão tem campo para o gargalo: o nominal escreve-se só na
        // ficha da ferramenta.
        foreach (var token in new[] { "CM", "BQ", "MF" })
        {
            var cartao = Section(html, $"data-peg-component=\"{token}\"");
            Assert.Contains("não tem nominal do gargalo", cartao);
            Assert.DoesNotContain("name=\"gargalo\"", cartao);
        }
        Assert.DoesNotContain("Cinza = não avaliável", html);

        foreach (var component in new[] { "CM", "BQ", "MF" })
        {
            using var refused = await PostMeasurementAsync(client, url, controloId, component, "70.10", "69.90");
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            var location = refused.Headers.Location!.ToString();
            Assert.Contains("measurement_refusal=MissingGargalo", location);
            var warned = await GetPageHtmlAsync(client, location);
            Assert.Contains("sem nominal não se registam Pegamentos", warned);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.PegamentosMeasurements.CountAsync());
            Assert.Equal(0, await db.Pegamentos.CountAsync());
            Assert.Null((await db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "T3")).DiametroGargaloMm);
        }

        // Com o nominal na ferramenta, a medição regista e é avaliada.
        using var completed = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00");
        Assert.Equal(url, completed.Headers.Location!.ToString());
        var reopened = await GetPageHtmlAsync(client, url);
        Assert.Contains("id=\"costura-Cm\"", reopened);
        Assert.DoesNotContain("A ferramenta 5447 não tem nominal", reopened);
        using var finalScope = _factory.Services.CreateScope();
        var finalSheet = await finalScope.ServiceProvider.GetRequiredService<IPegamentosQuery>().FindSheetByControloAsync(controloId);
        var cmReading = Assert.Single(finalSheet!.Pegamentos!.Measurements);
        Assert.Equal(70m, cmReading.NominalGargaloMm);
        Assert.False(cmReading.Evaluation.IsNotEvaluable);
    }

    [Fact]
    public async Task PostMeasurement_SeveralLines_RecordsEachFilledLine()
    {
        // «+ Linha» (Diogo, 17/10/2026): várias linhas gravam-se de uma vez;
        // as linhas vazias ignoram-se.
        SeedUser("4487", "Pegamentos Operador", "provider-peg-lines", "PegLines1!");
        using var client = await LoginClientAsync("4487", "PegLines1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var html = await GetPageHtmlAsync(client, url);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var posted = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "CM"), ("gargalo", "70.00"),
            ("costura", "70.10"), ("contra_costura", "69.90"),
            ("costura", ""), ("contra_costura", ""),
            ("costura", "70,05"), ("contra_costura", ""),
            ("__RequestVerificationToken", token)));
        Assert.Equal(url, posted.Headers.Location!.ToString());

        token = await GetVerificationTokenAsync(client, url);
        using var refused = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "CM"),
            ("costura", "70.20"), ("costura", "abc"),
            ("__RequestVerificationToken", token)));
        Assert.Contains("measurement_refusal=InvalidMeasurementValue", refused.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
            .AsNoTracking().OrderBy(m => m.RowPosition).ToListAsync();
        Assert.Equal(new[] { 70.10m, 70.05m }, rows.Select(r => r.CosturaMm));
        Assert.Equal(new decimal?[] { 69.90m, null }, rows.Select(r => r.ContraCosturaMm));
        Assert.All(rows, r => Assert.Equal(70m, r.NominalGargaloMm));
    }

    [Fact]
    public async Task Readings_EditByClickingTheValue_DeleteWithTheX_Renumbers()
    {
        // Diogo, 17/10/2026: sem botões nas linhas; carregar num valor abre a
        // correção e o pequeno × apaga a leitura.
        SeedUser("4489", "Pegamentos Operador", "provider-peg-correct", "PegCorrect1!");
        using var client = await LoginClientAsync("4489", "PegCorrect1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.30", "69.80")) { }
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.05")) { }

        int[] ids;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            ids = await db.PegamentosMeasurements.OrderBy(m => m.RowPosition).Select(m => m.Id).ToArrayAsync();
        }

        var html = await GetPageHtmlAsync(client, url);
        Assert.Contains("data-peg-mais-linha hidden>+ Linha</button>", html);
        // O operador só preenche o lugar da máquina nos Pegamentos sem Job On.
        Assert.DoesNotContain("<span>Operador</span>", html);
        // Sem âncora: a correção abre no lugar da linha (uma âncora levava-a
        // para o topo do ecrã, por baixo da barra fixa).
        Assert.Contains($"editar_medicao={ids[1]}&amp;campo=contra\"", html);
        Assert.DoesNotContain("#peg-linha-", html);
        Assert.Contains($"value=\"{ids[1]}\" class=\"peg-x\"", html);
        var tbody = System.Text.RegularExpressions.Regex.Match(html,
            "data-peg-component=\"CM\"[\\s\\S]*?<tbody>([\\s\\S]*?)</tbody>").Groups[1].Value;
        Assert.DoesNotContain("dmo-button", tbody);

        // Corrigir: a linha abre com o valor carregado em foco.
        var editUrl = $"{url}&editar_medicao={ids[1]}&campo=contra";
        var editing = await GetPageHtmlAsync(client, editUrl);
        Assert.Matches($"<input id=\"editar-contra-{ids[1]}\"[^>]*autofocus", editing);
        Assert.Contains("Enter guarda · Esc cancela", editing);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(editing)!;
        using var edited = await client.PostAsync($"{PagePath}?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", ids[1].ToString()),
            ("costura", "70,20"), ("contra_costura", "70.00"), ("__RequestVerificationToken", token)));
        Assert.Equal(url, edited.Headers.Location!.ToString());

        // Apagar a primeira: as restantes ficam 1 e 2.
        token = await GetVerificationTokenAsync(client, url);
        using var deleted = await client.PostAsync($"{PagePath}?handler=DeleteMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", ids[0].ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(url, deleted.Headers.Location!.ToString());

        // Uma leitura de outra folha não se apaga por esta.
        token = await GetVerificationTokenAsync(client, url);
        using var foreign = await client.PostAsync($"{PagePath}?handler=DeleteMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", (controloId + 999).ToString()), ("measurement_id", ids[1].ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Contains("correcao=nao-encontrada", foreign.Headers.Location!.ToString());

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var rows = await finalDb.PegamentosMeasurements.AsNoTracking().OrderBy(m => m.RowPosition).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.RowPosition));
        Assert.Equal(ids[1], rows[0].Id);
        Assert.Equal(70.20m, rows[0].CosturaMm);
        Assert.Equal(70.00m, rows[0].ContraCosturaMm);
        Assert.Equal(70m, rows[0].NominalGargaloMm);
    }

    [Fact]
    public async Task Correcao_ContraCosturaIlegivel_Recusa_SemApagarAGuardada()
    {
        // Os campos da correção são de texto: «70..05» chegava ao servidor,
        // que gravava a leitura sem contra costura e sem aviso. Só a contra
        // costura vazia a retira.
        SeedUser("4410", "Pegamentos Operador", "provider-peg-contra", "PegContra1!");
        using var client = await LoginClientAsync("4410", "PegContra1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.12", "70.05", "70.00")) { }
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements.Select(m => m.Id).SingleAsync();
        }

        var token = await GetVerificationTokenAsync(client, $"{url}&editar_medicao={id}&campo=contra");
        using var refused = await client.PostAsync($"{PagePath}?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", id.ToString()),
            ("costura", "70.12"), ("contra_costura", "70..05"), ("__RequestVerificationToken", token)));
        Assert.Equal($"{url}&editar_medicao={id}&campo=contra&correcao=contra-invalida", refused.Headers.Location!.ToString());
        var recusa = await GetPageHtmlAsync(client, refused.Headers.Location!.ToString());
        Assert.Contains("Indique a contra costura com um valor numérico", recusa);
        Assert.Matches($"<input id=\"editar-contra-{id}\"[^>]*autofocus", recusa);
        using (var scope = _factory.Services.CreateScope())
        {
            var guardada = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements.AsNoTracking().SingleAsync();
            Assert.Equal(70.05m, guardada.ContraCosturaMm);
        }

        // Vazia, retira-a.
        token = await GetVerificationTokenAsync(client, url);
        using var cleared = await client.PostAsync($"{PagePath}?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", id.ToString()),
            ("costura", "70.12"), ("contra_costura", " "), ("__RequestVerificationToken", token)));
        Assert.Equal(url, cleared.Headers.Location!.ToString());

        using var finalScope = _factory.Services.CreateScope();
        var row = await finalScope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(70.12m, row.CosturaMm);
        Assert.Null(row.ContraCosturaMm);
    }

    [Fact]
    public async Task RegistarEApagarNoutroCartao_MantemACorrecaoAberta()
    {
        // Uma correção aberta (e escrita) num cartão fica aberta quando se
        // regista ou apaga noutro: o servidor volta com ela, por isso esse
        // cartão não muda e o que lá está escrito fica.
        SeedUser("4411", "Pegamentos Operador", "provider-peg-aberta", "PegAberta1!");
        using var client = await LoginClientAsync("4411", "PegAberta1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }
        using (await PostMeasurementAsync(client, url, controloId, "BQ", "25.00", null, "25.00")) { }
        int cmId, bqId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            cmId = await db.PegamentosMeasurements.Where(m => m.Component == PegamentosComponent.Cm).Select(m => m.Id).SingleAsync();
            bqId = await db.PegamentosMeasurements.Where(m => m.Component == PegamentosComponent.Bq).Select(m => m.Id).SingleAsync();
        }

        var aberta = $"{url}&editar_medicao={cmId}&campo=contra";
        var token = await GetVerificationTokenAsync(client, aberta);
        using var registered = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "BQ"), ("costura", "25.02"),
            ("editar_medicao", cmId.ToString()), ("campo", "contra"), ("__RequestVerificationToken", token)));
        Assert.Equal(aberta, registered.Headers.Location!.ToString());
        Assert.Matches($"<input id=\"editar-contra-{cmId}\"[^>]*autofocus", await GetPageHtmlAsync(client, aberta));

        token = await GetVerificationTokenAsync(client, aberta);
        using var deleted = await client.PostAsync($"{PagePath}?handler=DeleteMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("measurement_id", bqId.ToString()),
            ("editar_medicao", cmId.ToString()), ("campo", "contra"), ("__RequestVerificationToken", token)));
        Assert.Equal(aberta, deleted.Headers.Location!.ToString());

        // Sem correção aberta, o registo volta à folha como antes.
        token = await GetVerificationTokenAsync(client, url);
        using var plain = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "BQ"), ("costura", "25.03"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(url, plain.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Folha_GravarSemRecarregar_UmaZonaPorCartao()
    {
        // Gravar sem recarregar (Diogo, 17/10/2026: «atualizar só o que
        // muda»): a raiz data-dmo-troca; zonas para o contexto, as mensagens,
        // cada cartão e o mapa; registar, corrigir e apagar são formulários
        // dentro do cartão, que é data-dmo-parcial. O PDF segue o caminho de
        // sempre (o contexto não é parcial).
        SeedUser("4408", "Pegamentos Operador", "provider-peg-troca", "PegTroca1!");
        using var client = await LoginClientAsync("4408", "PegTroca1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }

        var html = await GetPageHtmlAsync(client, url);
        var main = System.Text.RegularExpressions.Regex.Match(html, "<main[\\s\\S]*?</main>").Value;
        Assert.StartsWith("<main class=\"peg-page dmo-workspace\" aria-labelledby=\"peg-title\" data-dmo-troca>", main);
        foreach (var zona in new[] { "pegContexto", "pegAvisos", "pegCartaoCM", "pegCartaoBQ", "pegCartaoMF", "pegMapa" })
        {
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(main, $"<div id=\"{zona}\" class=\"dmo-zona\" data-dmo-regiao>"));
        }

        var cm = System.Text.RegularExpressions.Regex.Match(main, "<div id=\"pegCartaoCM\"[\\s\\S]*?</section>").Value;
        Assert.Contains("data-peg-component=\"CM\" data-dmo-parcial data-dmo-zonas=\"pegAvisos pegMapa\">", cm);
        Assert.Contains("id=\"costura-Cm\"", cm);
        Assert.Contains("<form id=\"peg-registar-CM\" method=\"post\"", cm);
        Assert.Contains("<form id=\"peg-apagar-CM\" method=\"post\"", cm);
        Assert.Contains("data-peg-component=\"BQ\" data-dmo-parcial data-dmo-zonas=\"pegAvisos pegMapa\">", main);

        var contexto = System.Text.RegularExpressions.Regex.Match(main, "<div id=\"pegContexto\"[\\s\\S]*?</section>").Value;
        Assert.Contains("handler=GeneratePdf", contexto);
        Assert.DoesNotContain("data-dmo-parcial", contexto);

        // Nada na folha obriga o componente a abrir a página inteira.
        foreach (var marca in new[] { "<script", "<dialog", "data-dmo-script", "data-dmo-suggest", "data-dmo-autosubmit", "data-dmo-procura", "data-dmo-valor" })
        {
            Assert.DoesNotContain(marca, main);
        }

        // O × confirma numa só janela da app, fora das zonas que trocam.
        Assert.Contains("<dialog id=\"peg-apagar-janela\" class=\"dmo-dialog\"", html[html.IndexOf("</main>", StringComparison.Ordinal)..]);
        Assert.Contains("data-peg-apagar-confirmar>Apagar</button>", html);

        // Abrir a correção é um link dentro do cartão; a recusa vem na zona das mensagens.
        Assert.Matches($"<a class=\"peg-valor\" href=\"/Controlo/Pegamentos\\?controlo_id={controloId}&amp;editar_medicao=\\d+&amp;campo=costura\"", cm);
        var recusa = await GetPageHtmlAsync(client, $"{url}&correcao=nao-encontrada");
        Assert.Matches("<div id=\"pegAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<div data-dmo-toast=\"erro\" class=\"peg-aviso\" role=\"alert\">Essa leitura já não existe", recusa);
    }

    [Fact]
    public async Task MissingNominal_ToolSheet_SavesGargalo_AndReturnsToTheSheet()
    {
        SeedUser("4488", "Pegamentos Operador", "provider-peg-ficha", "PegFicha1!", ferramentas: true);
        using var client = await LoginClientAsync("4488", "PegFicha1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        var ficha = $"/Ferramentas/Ficha?tool_id=T3&tecnica=1&continuar={Uri.EscapeDataString(url)}";
        var fichaHtml = await GetPageHtmlAsync(client, ficha);
        Assert.Contains($"name=\"continuar\" value=\"{url.Replace("?", "?").Replace("&", "&amp;")}\"", fichaHtml);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(fichaHtml)!;
        using var saved = await client.PostAsync("/Ferramentas/Ficha?handler=EditTechnical", HtmlFormHelpers.BuildFormPost(
            ("tool_id", "T3"), ("diametro_gargalo", "70.00"), ("continuar", url),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(url, saved.Headers.Location!.ToString());

        var html = await GetPageHtmlAsync(client, url);
        Assert.Contains("id=\"costura-Cm\"", html);
        Assert.DoesNotContain("A ferramenta 5447 não tem nominal", html);
    }

    [Theory]
    [InlineData("CM", "T3", ToolType.Cm, "5447")]
    [InlineData("BQ", "BQ-1", ToolType.Bq, "T194")]
    [InlineData("MF", "MF-TOOL-9", ToolType.Mf, "5810")]
    public async Task Completion_UsesToolRootFromProductionContext_NotAnotherLot(
        string component, string toolId, ToolType toolType, string reference)
    {
        SeedUser("4496", "Pegamentos Operador", "provider-peg-root", "PegMissing1!");
        using var client = await LoginClientAsync("4496", "PegMissing1!");
        var controloId = await SeedRealProductionAsync();
        int cmId, bqId, mfId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            cmId = await db.CmContexts.Select(c => c.Id).SingleAsync();
            bqId = await db.BqContexts.Select(c => c.Id).SingleAsync();
            mfId = await db.MfContexts.Select(c => c.Id).SingleAsync();
            db.Tools.Add(new Tool { ToolId = "other-lot", Type = toolType, Reference = reference, Lot = "99" });
            db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "other-lot", DiametroGargaloMm = 55m });
            await db.SaveChangesAsync();
        }

        var url = $"{PagePath}?controlo_id={controloId}";
        using var completed = await PostMeasurementAsync(client, url, controloId, component, "70.10", "69.90", "70.00");
        Assert.Equal(url, completed.Headers.Location!.ToString());
        // The next measurement reuses the root's value without asking again.
        using var next = await PostMeasurementAsync(client, url, controloId, component, "70.12", "69.92");
        Assert.Equal(url, next.Headers.Location!.ToString());
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(70m, (await finalDb.ToolTechnicalContexts.SingleAsync(t => t.ToolId == toolId)).DiametroGargaloMm);
        Assert.Equal(55m, (await finalDb.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "other-lot")).DiametroGargaloMm);
        Assert.All(await finalDb.PegamentosMeasurements.ToListAsync(), m => Assert.Equal(70m, m.NominalGargaloMm));
        Assert.Equal(cmId, await finalDb.CmContexts.Select(c => c.Id).SingleAsync());
        Assert.Equal(bqId, await finalDb.BqContexts.Select(c => c.Id).SingleAsync());
        Assert.Equal(mfId, await finalDb.MfContexts.Select(c => c.Id).SingleAsync());
        Assert.Equal("T3", (await finalDb.CmContexts.SingleAsync()).ToolId);
        Assert.Equal("BQ-1", (await finalDb.BqContexts.SingleAsync()).ToolId);
        Assert.Equal("MF-TOOL-9", (await finalDb.MfContexts.SingleAsync()).ToolId);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("70,0.1")]
    public async Task MissingNominal_UnreadableGargalo_IsRefused_ToolUntouched(string gargalo)
    {
        // Sem nominal não há Pegamentos (Diogo, 17/10/2026): um gargalo
        // ilegível numa ferramenta sem nominal recusa a medição, mantém as
        // leituras escritas e não toca na ferramenta.
        SeedUser("4497", "Pegamentos Operador", "provider-peg-invalid-nominal", "PegMissing1!");
        using var client = await LoginClientAsync("4497", "PegMissing1!");
        var controloId = await SeedRealProductionAsync();
        using var response = await PostMeasurementAsync(client, $"{PagePath}?controlo_id={controloId}", controloId,
            "CM", "70.10", "69.90", gargalo);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("measurement_refusal=MissingGargalo", location);

        var refused = await GetPageHtmlAsync(client, location);
        Assert.Contains("sem nominal não se registam Pegamentos", refused);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PegamentosMeasurements.CountAsync());
        Assert.Null((await db.ToolTechnicalContexts.SingleAsync(t => t.ToolId == "T3")).DiametroGargaloMm);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task SavedPesoNavigation_UsesItsOwnProduction_AndDoesNotAssociateToolScopedPeso(bool productionScoped, bool reviewOnly)
    {
        SeedUser("4492", "Controlo Operador", "provider-peso-navigation", "PesoNavigation1!");
        using var client = await LoginClientAsync("4492", "PesoNavigation1!");
        var controloId = await SeedRealProductionAsync();
        int pesoId, jobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            jobOnId = await db.Controlos.Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
            var other = await scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>().HandleAsync(new CreateJobOn(
                "J20", "5810", await db.Machines.Select(m => m.Id).SingleAsync(), "T3", "MF-TOOL-9", "BQ-1"));
            Assert.Equal(CreateJobOnResult.Success, other.Result);
            var peso = new Peso
            {
                ControloId = productionScoped ? controloId : null, CmToolId = "T3", BqToolId = "BQ-1",
                Process = "NNPB", WaterTemperatureC = 20, VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesoId = peso.Id;
        }
        if (reviewOnly)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
                var userId = await db.Users.Where(u => u.OperatorId == "4492").Select(u => u.Id).SingleAsync();
                db.TemplateAssignments.RemoveRange(db.TemplateAssignments.Where(a => a.UserId == userId));
                var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-peso-navigation-review", Name = "Revisão", Capabilities = new[] { DmoCapabilities.ControloApprove } };
                db.Templates.Add(template);
                db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = userId, TemplateId = template.Id });
                await db.SaveChangesAsync();
            }
            // O Peso é do Controlo Criar: quem só tem o Controlo Aprovar
            // entra no seu próprio módulo, e a revisão abre o mesmo Peso.
            using var opened = await client.GetAsync($"/Controlo/Peso?peso_id={pesoId}");
            Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);
            Assert.Equal("/Controlo/Approve", opened.Headers.Location!.ToString());
            var detail = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
            Assert.Contains("J10", WebUtility.HtmlDecode(detail));
            using var finalReviewScope = _factory.Services.CreateScope();
            var dbAfterReview = finalReviewScope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(controloId, (await dbAfterReview.Pesos.SingleAsync(p => p.Id == pesoId)).ControloId);
            Assert.Equal(2, await dbAfterReview.Controlos.CountAsync());
            return;
        }
        var html = await GetPageHtmlAsync(client, $"/Controlo/Peso?peso_id={pesoId}");
        var navigation = System.Text.RegularExpressions.Regex.Match(html,
            "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
        string Link(string label) => System.Text.RegularExpressions.Regex.Matches(navigation,
            "<a\\b[^>]*href=\"([^\"]*)\"[^>]*>([^<]*)</a>")
            .Cast<System.Text.RegularExpressions.Match>()
            .Where(m => WebUtility.HtmlDecode(m.Groups[2].Value).Trim() == label)
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Single();
        Assert.Equal(productionScoped ? $"/Controlo/Resumo?jobon_id={jobOnId}" : "/Controlo/Resumo", Link("Resumo"));
        Assert.Equal(productionScoped ? $"/Controlo/Pegamentos?controlo_id={controloId}" : "/Controlo/Pegamentos", Link("Pegamentos"));
        if (productionScoped)
        {
            Assert.Contains("Produção J10 · 5810 · B1", WebUtility.HtmlDecode(html));
            var resumo = await GetPageHtmlAsync(client, Link("Resumo"));
            Assert.Contains($"type=\"hidden\" name=\"jobon_id\" value=\"{jobOnId}\"", resumo);
            var pegamentos = await GetPageHtmlAsync(client, Link("Pegamentos"));
            Assert.Contains($"name=\"controlo_id\" value=\"{controloId}\"", pegamentos);
        }
        else
        {
            Assert.Contains("sem associação a produção", WebUtility.HtmlDecode(html));
        }
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var saved = await finalDb.Pesos.SingleAsync(p => p.Id == pesoId);
        Assert.Equal(productionScoped ? controloId : (int?)null, saved.ControloId);
        Assert.Equal(1, await finalDb.Pesos.CountAsync());
        Assert.Equal(2, await finalDb.Controlos.CountAsync());
    }

    [Fact]
    public async Task MixedAxes_SummaryAndPdfUseRowMeans_WithoutHidingAlert()
    {
        SeedUser("4493", "Pegamentos Operador", "provider-peg-mixed-axes", "PegMixed1!");
        using var client = await LoginClientAsync("4493", "PegMixed1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        foreach (var row in new[] { ("70.10", "69.90"), ("69.90", "70.10"), ("70.30", (string?)null) })
        {
            using var saved = await PostMeasurementAsync(client, url, controloId, "CM", row.Item1, row.Item2, "70.00");
            Assert.Equal(url, saved.Headers.Location!.ToString());
        }
        var html = await GetPageHtmlAsync(client, url);
        var avg = WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html, "<tr class=\"peg-average-row\">[\\s\\S]*?</tr>").Value);
        Assert.Contains("<td>+0.00</td>", avg);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(avg, "<td>70.10</td>").Count);
        Assert.Contains("3 fora", avg); // 70.30 e as ovalizações ±0,20 (F8)
        Assert.Contains("peg-cell-bad\">70.30", html);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var response = await client.PostAsync($"{PagePath}?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        if (Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { } root)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(Path.Combine(root, "pegamentos-mixed-axes.pdf"), bytes);
        }
    }

    [Fact]
    public async Task HistoricalToolFacts_RemainOnReadingCardAndPdf_AfterCatalogEdit()
    {
        SeedUser("4494", "Pegamentos Operador", "provider-peg-history-facts", "PegHistory1!");
        using var client = await LoginClientAsync("4494", "PegHistory1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using var registered = await PostMeasurementAsync(client, url, controloId, "CM", "70.50", "70.00", "70.00");
        Assert.Equal(url, registered.Headers.Location!.ToString());
        using (var scope = _factory.Services.CreateScope())
        {
            var edited = await scope.ServiceProvider.GetRequiredService<IEditToolFactsHandler>()
                .HandleAsync(new EditToolFacts("T3", "CM", "5447-CORR", "33"));
            Assert.Equal(EditToolFactsResult.Success, edited.Result);
        }

        var html = await GetPageHtmlAsync(client, url);
        var card = System.Text.RegularExpressions.Regex.Match(html,
            "data-peg-component=\"CM\"[\\s\\S]*?</section>").Value;
        Assert.Contains("data-peg-titulo>CM 5447 · Lote", card);
        Assert.Contains(" · Lote 3<", card);
        Assert.Contains("peg-cell-bad\">70.50", card);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var response = await client.PostAsync($"{PagePath}?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        if (Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { } root)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(Path.Combine(root, "pegamentos-historical-identity.pdf"), bytes);
        }
    }

    [Theory]
    [InlineData("costura", "abc")]
    [InlineData("costura", "")]
    [InlineData("costura", "70,1.0")]
    public async Task InvalidCostura_StillRefused_PreservesDraftAndSavedReadings_ThenAllowsCorrection(string field, string invalid)
    {
        // A costura é o facto base da leitura (NOT NULL): continua recusada.
        SeedUser("4495", "Pegamentos Operador", "provider-peg-invalid-reading", "PegReading1!");
        using var client = await LoginClientAsync("4495", "PegReading1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using var initial = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00");
        Assert.Equal(url, initial.Headers.Location!.ToString());

        using var invalidPost = await PostMeasurementAsync(client, url, controloId, "CM",
            field == "costura" ? invalid : "70.12", field == "contra_costura" ? invalid : "69.92");
        Assert.Equal(HttpStatusCode.Redirect, invalidPost.StatusCode);
        Assert.Contains("measurement_refusal=InvalidMeasurementValue", invalidPost.Headers.Location!.ToString());
        var refused = await GetPageHtmlAsync(client, invalidPost.Headers.Location.ToString());
        Assert.Contains("Indique a costura com um valor numérico maior que 0 (aceita vírgula ou ponto decimal", refused);
        Assert.DoesNotContain("com ponto decimal.", refused);
        var inputId = field == "costura" ? "costura-Cm" : "contra-Cm";
        var input = System.Text.RegularExpressions.Regex.Match(refused, $"<input[^>]*id=\"{inputId}\"[^>]*>").Value;
        Assert.NotEmpty(input);
        var echoed = System.Text.RegularExpressions.Regex.Match(input, "\\bvalue=\"([^\"]*)\"").Groups[1].Value;
        Assert.Equal(invalid, echoed); // Razor omits the value attribute for an empty draft.
        if (invalid.Length > 0) Assert.Contains("type=\"text\"", input);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var saved = await db.PegamentosMeasurements.SingleAsync();
            Assert.Equal(70.10m, saved.CosturaMm);
            Assert.Equal(69.90m, saved.ContraCosturaMm);
            Assert.Equal(70m, saved.NominalGargaloMm);
        }

        using var corrected = await PostMeasurementAsync(client, url, controloId, "CM", "70.12", "69.92");
        Assert.Equal(url, corrected.Headers.Location!.ToString());
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var measurements = await finalDb.PegamentosMeasurements.OrderBy(m => m.RowPosition).ToListAsync();
        Assert.Equal(2, measurements.Count);
        Assert.Equal(2, measurements[1].RowPosition);
        Assert.Equal(70.12m, measurements[1].CosturaMm);
        Assert.Equal(69.92m, measurements[1].ContraCosturaMm);
    }

    [Theory]
    [InlineData("abc", "70.00")]
    [InlineData("69,9.0", "70,00")]
    [InlineData("1 234", "70.00")]
    public async Task UnreadableContraCostura_SavesCosturaOnly_WithWarning(string invalidContra, string? gargalo)
    {
        // Owner 16/10/2026: a contra costura ilegível já não recusa a
        // medição toda — grava a costura, a contra costura fica vazia e a
        // folha avisa. Sem gargalo, o aviso «não avaliável» junta-se.
        SeedUser("4494", "Pegamentos Operador", "provider-peg-invalid-contra", "PegContra1!");
        using var client = await LoginClientAsync("4494", "PegContra1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";

        using var posted = await PostMeasurementAsync(client, url, controloId, "CM", "70,12", invalidContra, gargalo);
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        var location = posted.Headers.Location!.ToString();
        Assert.DoesNotContain("measurement_refusal", location);
        Assert.Contains("ContraCosturaIgnored", location);
        Assert.Equal(gargalo is null, location.Contains("NotEvaluable"));

        var warned = await GetPageHtmlAsync(client, location);
        // O aviso vai para a pilha fixa do canto: não empurra os cartões. É
        // «aviso» (não fecha sozinho, como as «ok»): fica até se fechar ou
        // até à ação seguinte no cartão.
        Assert.Contains("data-dmo-toast=\"aviso\" class=\"peg-aviso warn\" role=\"status\"", warned);
        Assert.DoesNotContain("data-dmo-toast=\"ok\" class=\"peg-aviso warn\"", warned);
        Assert.Contains("Medição registada sem contra costura: o valor escrito não é um número legível", warned);
        Assert.Equal(gargalo is null, warned.Contains("Medição registada como «não avaliável»"));
        Assert.DoesNotContain("role=\"alert\"", warned);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var saved = await db.PegamentosMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(70.12m, saved.CosturaMm);
        Assert.Null(saved.ContraCosturaMm);
        Assert.Equal(gargalo is null ? null : 70m, saved.NominalGargaloMm);
    }

    /// <summary>
    /// E11 (beta.189): depois de registar uma leitura, o GET do redirect leva
    /// X-Dmo-Zonas e o servidor desenha só o cartão desse componente, as
    /// mensagens, o mapa e, antes da primeira medição, o contexto (o cartão
    /// declara-o em data-dmo-zonas). Sem o contexto, as ferramentas não se
    /// leem (as referências e os lotes só o contexto os mostra). As zonas
    /// são as mesmas da página inteira; uma zona desconhecida desenha a
    /// página inteira; o anónimo é desafiado como na página.
    /// </summary>
    [Fact]
    public async Task RegistarLeitura_ZonasPedidas_DesenhaSoAsZonas_SemLerAsFerramentas()
    {
        SeedUser("4498", "Pegamentos Zonas", "provider-peg-zonas", "PegZonas1!");
        var leituras = new LeiturasContadas();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IToolLookupQuery>(sp => new ToolLookupContado(
                ActivatorUtilities.CreateInstance<DMO.Alpha.Infrastructure.Tools.ToolLookupQuery>(sp), leituras));
            services.AddScoped<IPegamentosQuery>(sp => new PegamentosQueryContado(
                ActivatorUtilities.CreateInstance<DMO.Alpha.Infrastructure.Controlo.PegamentosQuery>(sp), leituras));
        }));
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";

        using (var anonymous = CreateClient(factory))
        {
            var refused = await anonymous.SendAsync(ZonasGravacao.Pedido(url, "pegCartaoCM pegAvisos pegMapa"));
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Contains("/login", refused.Headers.Location?.ToString(), StringComparison.Ordinal);
            Assert.Null(ZonasGravacao.Regiao(refused));
        }

        using var client = await LoginClientAsync("4498", "PegZonas1!", factory);
        var page = await GetPageHtmlAsync(client, url);
        // Antes da primeira medição, o cartão pede também o contexto.
        Assert.Contains("data-peg-component=\"CM\" data-dmo-parcial data-dmo-zonas=\"pegAvisos pegMapa pegContexto\"", page);

        var primeira = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", gargalo: "70.00");
        Assert.Equal(HttpStatusCode.Redirect, primeira.StatusCode);
        var volta = primeira.Headers.Location!.ToString();
        leituras.Zerar();
        const string comContexto = "pegCartaoCM pegAvisos pegMapa pegContexto";
        var resposta = await client.SendAsync(ZonasGravacao.Pedido(volta, comContexto));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(comContexto, ZonasGravacao.Regiao(resposta));
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Equal(comContexto.Split(' '), ZonasGravacao.Raizes(html));
        Assert.Matches("dmo-card-topo-acoes\">\\s*<form[^>]*GeneratePdf[\\s\\S]*?<button type=\"submit\" class=\"dmo-button\" id=\"pegExtrairPdf\">Extrair PDF</button>", html);
        Assert.DoesNotContain("Descarregar", html);
        Assert.Contains("5447", html);
        Assert.Equal(1, leituras.FolhasPegamentos);
        Assert.Equal(3, leituras.Ferramentas);

        // Com medições, o contexto já não muda: o cartão deixa de o pedir.
        page = await GetPageHtmlAsync(client, url);
        Assert.Contains("data-peg-component=\"CM\" data-dmo-parcial data-dmo-zonas=\"pegAvisos pegMapa\"", page);
        var segunda = await PostMeasurementAsync(client, url, controloId, "CM", "70.30", "69.80");
        Assert.Equal(HttpStatusCode.Redirect, segunda.StatusCode);
        leituras.Zerar();
        const string zonas = "pegCartaoCM pegAvisos pegMapa";
        resposta = await client.SendAsync(ZonasGravacao.Pedido(segunda.Headers.Location!.ToString(), zonas));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(zonas, ZonasGravacao.Regiao(resposta));
        Assert.Contains("X-Dmo-Zonas", resposta.Headers.Vary);
        html = await resposta.Content.ReadAsStringAsync();
        Assert.Equal(zonas.Split(' '), ZonasGravacao.Raizes(html));
        Assert.Matches("70[.,]30", html);
        Assert.Contains("peg-map-card", html);
        Assert.DoesNotContain("pegCartaoBQ", html);
        Assert.DoesNotContain("Contexto da produção", html);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, leituras.FolhasPegamentos);
        Assert.Equal(0, leituras.Ferramentas);

        // As zonas são as que a página inteira desenha (os mesmos partials).
        var inteira = ZonasGravacao.SemTokens(await GetPageHtmlAsync(client, url));
        foreach (var zona in System.Text.RegularExpressions.Regex.Split(ZonasGravacao.SemTokens(html).Trim(), "(?=<div id=\"peg(?:Avisos|Mapa)\")"))
        {
            Assert.Contains(zona.Trim(), inteira);
        }

        // Uma zona desconhecida: a página inteira, sem X-Dmo-Region.
        var desconhecida = await client.SendAsync(ZonasGravacao.Pedido(url, "pegCartaoCM pegOutra"));
        Assert.Null(ZonasGravacao.Regiao(desconhecida));
        Assert.Contains("Contexto da produção", await desconhecida.Content.ReadAsStringAsync());

        // Um POST com o cabeçalho grava como sempre (redirect); só o GET o usa.
        var token = await GetVerificationTokenAsync(client, url);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{PagePath}?handler=RegisterMeasurement")
        {
            Content = HtmlFormHelpers.BuildFormPost(("controlo_id", controloId.ToString()), ("component", "CM"),
                ("costura", "70.20"), ("__RequestVerificationToken", token))
        };
        request.Headers.Add("X-Dmo-Zonas", zonas);
        var post = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Null(ZonasGravacao.Regiao(post));
    }

    [Fact]
    public async Task Pdf_WithoutMeasurements_NoButton_AndPostIsRefused_WithoutWriting()
    {
        // Diogo, 24/10/2026: ninguém preenche Pegamentos à mão — sem medições
        // não há «Extrair PDF» e o POST direto não gera folha em branco.
        SeedUser("4492", "Pegamentos Operador", "provider-peg-blank-pdf", "PegBlank1!");
        using var client = await LoginClientAsync("4492", "PegBlank1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";

        var html = await GetPageHtmlAsync(client, url);
        Assert.DoesNotContain("id=\"pegExtrairPdf\"", html);
        Assert.DoesNotContain("em branco", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var response = await client.PostAsync($"{PagePath}?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("pdf_refusal=sem-medicoes", response.Headers.Location!.ToString());
        var aviso = await GetPageHtmlAsync(client, response.Headers.Location!.ToString());
        Assert.Contains("Ainda não há medições", System.Net.WebUtility.HtmlDecode(aviso));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pegamentos.CountAsync());
        Assert.Equal(0, await db.PegamentosMeasurements.CountAsync());
    }

    [Fact]
    public async Task PostMeasurement_UnusableCostura_NoWrite_AsksForCorrection()
    {
        // Sem costura utilizável não existe leitura a registar — a costura
        // é o facto base do gesto em cruz e nenhum valor é inventado (o
        // folha pede correção e conserva o rascunho, sem escrita).
        SeedUser("4406", "Pegamentos Operador", "provider-peg-7", "PegPass7!");
        using var client = await LoginClientAsync("4406", "PegPass7!");

        var controloId = await SeedRealProductionAsync();
        var sheetUrl = $"{PagePath}?controlo_id={controloId}";

        var posted = await PostMeasurementAsync(client, sheetUrl, controloId, "CM", "abc");

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Contains("measurement_refusal=InvalidMeasurementValue", posted.Headers.Location!.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Pegamentos.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.PegamentosMeasurements.AsNoTracking().CountAsync());
        }
    }

    [Fact]
    public async Task ResumoPage_LinksToPegamentosSheet_WithEnsuredControloId()
    {
        // A edição mínima do Resumo: o botão «Pegamentos» (o nome real —
        // P2-8, sem «Abrir» a mais) liga a folha ao controlo_id
        // assegurado da produção selecionada (a LEI: controlo_id →
        // contexto todo para pegamentos).
        SeedUser("4407", "Controlo Operador", "provider-peg-8", "PegPass8!");
        using var client = await LoginClientAsync("4407", "PegPass8!");

        var controloId = await SeedRealProductionAsync();
        var html = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={(await GetJobOnIdAsync())}");

        Assert.Contains($"/Controlo/Pegamentos?controlo_id={controloId}", html);
        // O TEXTO DIRETO (o Owner 2026-10-13: «o abrir nos botoes esta a
        // mais é redundante») — o botão diz o destino.
        Assert.Contains(">Pegamentos</a>", html);
        // A navegação secundária do módulo ganhou a função Pegamentos.
        Assert.Contains("href=\"/Controlo/Pegamentos?controlo_id=", html);
        Assert.Contains(">Pegamentos</a>", html);
    }

    private async Task<int> GetJobOnIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.JobOns.AsNoTracking().Select(j => j.Id).SingleAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealTarget_IndividualCells_OutsideCorridor_EachPaintedWithItsOwnState(bool decimalComma)
    {
        string Input(string value) => decimalComma ? value.Replace('.', ',') : value;
        // A REGRA DO OWNER na RENDERIZAÇÃO (2026-10-10: «qualquer uma das
        // mediçoes que ultrapassa o minimo ou maximo deve dar alerta e nao
        // apenas o delta/media») provada com o worked example REAL do
        // PDF-alvo (5447T173, produção 202610 — dmo-pdf\pegamentos-real):
        // a CM com o gargalo REAL 24,80 (corredor 24,60–25,00 — o upper é
        // o nominal da BQ, fronteira partilhada). A linha nº 45 do alvo
        // (25,08/25,04 → média 25,06 ⚠: todas as células fora); a linha
        // nº 3 do alvo (25,01/24,97 → média 24,99 DENTRO, mas a costura
        // individual 25,01 CRUZOU o limite 25,00 — o caso que o próprio
        // alvo não marca); a linha nº 21 (24,84/24,81 → 24,82: dentro).
        // Cada célula pinta SOZINHA o seu estado — a média dentro nunca
        // oculta uma medição individual fora do corredor.
        SeedUser("4409", "Pegamentos Operador", "provider-peg-real", "PegReal1!");
        using var client = await LoginClientAsync("4409", "PegReal1!");

        var controloId = await SeedRealProductionAsync();
        var sheetUrl = $"{PagePath}?controlo_id={controloId}";

        // A linha nº 21 do alvo (24,84/24,81 → 24,82): tudo dentro — o
        // gargalo REAL 24,80 completado no fluxo grava no tool_id owner.
        await PostMeasurementAsync(client, sheetUrl, controloId, "CM", Input("24.84"), Input("24.81"), gargalo: Input("24.80"));
        // A linha nº 3 do alvo (25,01/24,97 → 24,99): a costura individual fora.
        await PostMeasurementAsync(client, sheetUrl, controloId, "CM", Input("25.01"), Input("24.97"));
        // A linha nº 45 do alvo (25,08/25,04 → 25,06 ⚠): todas as células fora.
        await PostMeasurementAsync(client, sheetUrl, controloId, "CM", Input("25.08"), Input("25.04"));

        var html = await GetPageHtmlAsync(client, sheetUrl);

        // A linha nº 21: NENHUMA célula pintada (24,84 · 24,81 · média exata
        // 24,825 exibida como 24,83 — o decimal exato do backend).
        Assert.DoesNotContain("peg-cell-bad\">24.84", html);
        Assert.DoesNotContain("peg-cell-bad\">24.81", html);
        Assert.DoesNotContain("peg-cell-bad\">24.83", html);

        // A linha nº 3: SÓ a costura 25,01 pinta (a regra do Owner — a
        // contra 24,97 e a média 24,99 estão DENTRO e ficam limpas).
        Assert.Contains("peg-cell-bad\">25.01", html);
        Assert.DoesNotContain("peg-cell-bad\">24.97", html);
        Assert.DoesNotContain("peg-cell-bad\">24.99", html);

        // A linha nº 45: costura, contra costura E média, todas pintadas.
        Assert.Contains("peg-cell-bad\">25.08", html);
        Assert.Contains("peg-cell-bad\">25.04", html);
        Assert.Contains("peg-cell-bad\">25.06", html);

        // O estado por linha acompanha (informativo, nunca bloqueante):
        // as linhas 3 e 45 em alerta, a 21 dentro.
        Assert.Contains(">fora<", html);
        Assert.Contains(">dentro<", html);
    }

    [Fact]
    public async Task PdfDownloads_UsePersistedModels_AllComponents_AndDoNotWriteDatabaseOrServerFiles()
    {
        SeedUser("4499", "Operador PDF", "provider-pdf-recovery", "PdfRecovery1!");
        using var client = await LoginClientAsync("4499", "PdfRecovery1!");
        var controloId = await SeedRealProductionAsync();
        var jobOnId = await GetJobOnIdAsync();
        var pageUrl = $"{PagePath}?controlo_id={controloId}";
        foreach(var reading in new[] {
            ("CM","34.55","34.50","34.50"), ("CM","34.70","34.51","34.50"), ("CM","34.54","34.51","34.50"),
            ("BQ","34.70","34.70","34.70"), ("BQ","34.73","34.72","34.70"), ("BQ","34.70","34.71","34.70"), ("BQ","34.71","34.70","34.70"),
            ("MF","34.94","35.01","34.90"), ("MF","34.92","34.80","34.90"), ("MF","35.03","34.95","34.90") })
        {
            using var registered = await PostMeasurementAsync(client,pageUrl,controloId,reading.Item1,reading.Item2,reading.Item3,reading.Item4);
            Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
        }
        int historicalId;
        int previousPesoId;
        using(var scope=_factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Peso AddPeso(int? control, decimal offset)
            {
                var peso=new Peso { CmToolId="T3", BqToolId="BQ-1", ControloId=control, Process="NNPB", WaterTemperatureC=25,
                    GlassDensityGramsPerCm3=2.5m,VolumeMarisaCm3=10,VolumePuncaoCm3=24.42m,NominalWeightGrams=138 };
                int pos=0;
                foreach(var r in new[] { ("61",71.10m), ("95",71.40m), ("63",71.20m), ("36",71.80m) })
                    peso.Measurements.Add(new PesoMeasurement { RowPosition=++pos,CmSubject=r.Item1,WaterWeightGrams=r.Item2+offset });
                db.Pesos.Add(peso); db.SaveChanges(); return peso;
            }
            AddPeso(controloId,0);
            var create=scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>();
            var machineId=await db.Machines.Select(m=>m.Id).SingleAsync();
            var previous=await create.HandleAsync(new CreateJobOn("J09","5810",machineId,"T3","MF-TOOL-9","BQ-1",new(2026,3,5),new(2026,3,8)));
            Assert.Equal(CreateJobOnResult.Success,previous.Result);
            var previousControl=await db.Controlos.Where(c=>c.JobOnId==previous.JobOn!.JobOnId).Select(c=>c.Id).SingleAsync();
            historicalId=AddPeso(previousControl,.2m).Id;
            previousPesoId=AddPeso(null,.8m).Id;
        }
        // Load the actual pages to obtain valid antiforgery tokens before measuring writes.
        var paths = new[] { (Name:"pegamentos",Page:pageUrl,Handler:"/Controlo/Pegamentos",Field:"controlo_id",Id:controloId),
            (Name:"peso",Page:$"/Controlo/Peso?controlo_id={controloId}",Handler:"/Controlo/Peso",Field:"controlo_id",Id:controloId),
            (Name:"resumo-completo",Page:$"/Controlo/Resumo?jobon_id={jobOnId}",Handler:"/Controlo/Resumo",Field:"jobon_id",Id:jobOnId),
            (Name:"resumo",Page:$"/Controlo/Resumo?jobon_id={jobOnId}",Handler:"/Controlo/Resumo",Field:"jobon_id",Id:jobOnId) };
        foreach(var target in paths)
        {
            var token=await GetVerificationTokenAsync(client,target.Page);
            using var scope=_factory.Services.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var before = await db.ControloViewings.AsNoTracking().Select(v=>v.ViewedAtUtc).ToListAsync();
            var controls=await db.Controlos.CountAsync();
            var readings=await db.PegamentosMeasurements.CountAsync();
            using var response=await client.PostAsync(target.Handler+"?handler=GeneratePdf",HtmlFormHelpers.BuildFormPost(
                (target.Field,target.Id.ToString()),("comparar",historicalId.ToString()),("comparar_peso",previousPesoId.ToString()),
                ("apenas_resumo",(target.Name=="resumo").ToString()),("__RequestVerificationToken",token)));
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Equal("application/pdf",response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment",response.Content.Headers.ContentDisposition?.DispositionType);
            // A subpasta <referência>/<produção> para a pasta dos PDFs do
            // computador (Diogo, 17/10/2026); o nome do ficheiro tem as duas.
            var pasta=Uri.UnescapeDataString(response.Headers.GetValues("X-Dmo-Pdf-Pasta").Single());
            Assert.Matches("^[^/]+/[^/]+$",pasta);
            Assert.Contains(pasta.Replace('/','-'),response.Content.Headers.ContentDisposition!.FileName ?? response.Content.Headers.ContentDisposition.FileNameStar);
            var bytes=await response.Content.ReadAsByteArrayAsync();
            Assert.StartsWith("%PDF-",System.Text.Encoding.ASCII.GetString(bytes[..5]));
            Assert.Equal(controls,await db.Controlos.CountAsync());
            Assert.Equal(readings,await db.PegamentosMeasurements.CountAsync());
            Assert.Equal(before,await db.ControloViewings.AsNoTracking().Select(v=>v.ViewedAtUtc).ToListAsync());
            if(Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { } root)
            {
                Directory.CreateDirectory(root);
                File.WriteAllBytes(Path.Combine(root,"app-"+target.Name+".pdf"),bytes);
            }
        }
    }

    /// <summary>A escolha do registo vive dentro do cartão e marca a opção ativa (Diogo, 17/10/2026).</summary>
    private static void AssertModoNoCartao(string html, string cardTitleId, bool jobOn)
    {
        var card = System.Text.RegularExpressions.Regex.Match(html,
            $"<section[^>]*aria-labelledby=\"{cardTitleId}\"[\\s\\S]*?</section>").Value;
        Assert.Contains("data-dmo-modo", card);
        Assert.Contains(jobOn ? "aria-current=\"page\">Associar Job On</a>" : "aria-current=\"page\">Sem Job On</a>", card);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-dmo-modo"));
    }
}
