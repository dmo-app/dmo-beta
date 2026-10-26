using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// Prova as comparações TODAS do Peso no slice vertical nativo (o dmo-html
/// controlo-v04): a superfície própria «Comparação de Peso» (o tab
/// ?comparacao=1 — os selects da produção/peso inicial, as novas leituras
/// dos contra-moldes, o «Comparar pesos» ao vivo SEM gravar e o «Guardar
/// comparação» que cria o registo do histórico tipo Comparação), o
/// «Comparar Pesos guardados» do histórico (a comparação por ids no
/// backend) e a procura unificada do histórico (referência/produção/
/// ferramenta/tipo/data). Os cálculos são SEMPRE do backend; os ids nunca
/// são factos da folha; as recusas são tipadas em PT e nunca bloqueiam.
/// </summary>
public partial class PesoComparacaoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoComparacaoPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose()
    {
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoComparacaoMeasurements.RemoveRange(db.PesoComparacaoMeasurements);
        db.PesoComparacoes.RemoveRange(db.PesoComparacoes);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.PegamentosMeasurements.RemoveRange(db.PegamentosMeasurements);
        db.Pegamentos.RemoveRange(db.Pegamentos);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private void SeedUser(string operatorId, string name, string providerUserId, string password, bool comCapability = true)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = comCapability
                    ? new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
                    : new[] { DmoCapabilities.JobOnCreate }
            };
            db.Templates.Add(template);
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

        db.TemplateAssignments.Add(new TemplateAssignment
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

    /// <summary>
    /// O worked example: a produção 202601 (1075C142, B1) com a CM 5447
    /// (lote 3, NNPB) e a BQ T194; o Peso inicial com DUAS leituras (199,64
    /// g e 99,82 g a 20 °C → 450,52 g e 200,26 g de vidro — a casa).
    /// </summary>
    private async Task<(int ControloId, int JobOnId, int PesoInicialId)> SeedProducaoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        var machine = new Machine { Code = "B1" };
        var cmTool = new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        var bqTool = new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" };
        db.Machines.Add(machine);
        db.Tools.Add(cmTool);
        db.Tools.Add(bqTool);
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            VolumePuncaoCm3 = 30m
        });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "BQ-TOOL-9",
            VolumeMarisaCm3 = 10m
        });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });

        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = machine
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
        await db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();

        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id
        };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 199.64m });
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 2, CmSubject = "21", WaterWeightGrams = 99.82m });
        await db.SaveChangesAsync();

        return (controlo.Id, jobOn.Id, peso.Id);
    }

    /// <summary>
    /// A geometria do tampão na CM (para o «Supomos» e o apoio TP), a calote
    /// do Job On e outra produção da mesma referência com Peso, para comparar.
    /// </summary>
    private async Task<int> SeedOutraComGeometriaAsync(int jobOnId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var tecnico = db.ToolTechnicalContexts.Single(t => t.ToolId == "T3");
        tecnico.TpDiametroMm = 50m;
        tecnico.TpRaioMm = 40m;
        tecnico.TpSagittaMm = 4m;
        db.JobOns.Single(j => j.Id == jobOnId).CaloteMm = 3m;
        var job = new JobOn { Reference = "1075C142", ProductionNumber = "202602", MachineId = db.Machines.Single().Id };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = job.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        var outro = new Peso
        {
            ControloId = controlo.Id, CmToolId = "T3", BqToolId = "BQ-TOOL-9",
            Process = "NNPB", WaterTemperatureC = 20, VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m
        };
        db.Pesos.Add(outro);
        await db.SaveChangesAsync();
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = outro.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 198.64m });
        await db.SaveChangesAsync();
        return outro.Id;
    }

    /// <summary>
    /// O Peso aberto pelo histórico (?peso_id) compara-se na própria folha
    /// com outra produção da mesma referência, e o «supomos» da calote do
    /// tampão mostra a diferença — sem gravar nada (o Peso, as leituras e a
    /// calote do Job On ficam iguais). O dropdown só oferece as produções
    /// anteriores à do Peso (deep sweep 2, F-12): a 202602, registada depois,
    /// só aparece pedida (um link ou uma comparação já guardada).
    /// </summary>
    [Fact]
    public async Task PesoAbertoPeloHistorico_ComparaNaFolhaESupoeCaloteSemGravar()
    {
        var (_, jobOnId, pesoId) = await SeedProducaoAsync();
        var outroId = await SeedOutraComGeometriaAsync(jobOnId);
        await DefinirInicioAsync(jobOnId, new DateOnly(2026, 1, 12));
        var anteriorId = await SeedProducaoComPesoAsync("202552", new DateOnly(2025, 12, 15));

        SeedUser("4318", "Peso Operador", "provider-cmp-18", "PesoCmp18!");
        using var client = await LoginClientAsync("4318", "PesoCmp18!");

        // Sem escolha, a folha mostra o dropdown com as anteriores da
        // referência e não compara; a 202602 (posterior) não está lá.
        var semEscolha = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoId}");
        Assert.Contains("Produção a comparar (1075C142)", semEscolha);
        Assert.Contains("Procurar produção…", semEscolha);
        Assert.Contains($"<option value=\"{anteriorId}\"", semEscolha);
        Assert.DoesNotContain($"<option value=\"{outroId}\"", semEscolha);
        Assert.DoesNotContain("data-peso-comparacao-tabela", semEscolha);
        // A «Calote Tampo» (antes «Supomos») saiu deste cartão (Diogo, 23/10/2026); o cálculo
        // fica (PesoSuposicaoCalculationTests) até o Diogo dizer onde vai.
        Assert.DoesNotContain("<h4 style=\"margin:0 0 6px\">Supomos</h4>", semEscolha);
        Assert.DoesNotContain("Calote do tampão suposta", semEscolha);

        var html = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoId}&comparar={outroId}&supor_calote=5,2");
        Assert.Contains($"<option value=\"{outroId}\" selected=\"selected\">", html);
        Assert.Contains("data-peso-comparacao-tabela", html);
        Assert.DoesNotContain("id=\"pesoSupomos\"", html);
        Assert.DoesNotContain("3.000 → 5.200 mm", html);

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(3m, (await finalDb.JobOns.SingleAsync(j => j.Id == jobOnId)).CaloteMm);
        Assert.Equal(new[] { 199.64m, 99.82m }, await finalDb.PesoMeasurements
            .Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition).Select(m => m.WaterWeightGrams).ToArrayAsync());
        Assert.Equal(3, await finalDb.Pesos.CountAsync());
        Assert.Equal(0, await finalDb.PesoComparacoes.CountAsync());
    }

    /// <summary>
    /// Os formulários da folha levam de volta a comparação escolhida e o
    /// «Supomos» aberto: as leituras e a produção anterior (E4-04), o
    /// «Calcular» do apoio TP (E4-03) e, no Peso aberto pelo Histórico, as
    /// leituras (E4-05). Uma calote suposta ilegível não segue, e a recusa
    /// dela vai para a pilha do canto, sem empurrar a página (T10).
    /// </summary>
    [Fact]
    public async Task Folha_OsFormulariosLevamAComparacaoEOSupomos_ERecusaDoSupomosNoCanto()
    {
        var (controloId, jobOnId, pesoId) = await SeedProducaoAsync();
        var outroId = await SeedOutraComGeometriaAsync(jobOnId);
        // A produção anterior é uma que começou antes desta (deep sweep 2,
        // F-11): a 202602 da comparação é posterior e não serve.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOns.Single(j => j.Id == jobOnId).InicioEm = new DateOnly(2026, 1, 12);
            db.JobOns.Add(new JobOn { Reference = "1075C142", ProductionNumber = "202552", MachineId = db.Machines.Single().Id, InicioEm = new DateOnly(2025, 12, 15) });
            await db.SaveChangesAsync();
        }
        SeedUser("4325", "Peso Operador", "provider-cmp-25", "PesoCmp25!");
        using var client = await LoginClientAsync("4325", "PesoCmp25!");
        var comparar = $"<input type=\"hidden\" name=\"comparar\" value=\"{outroId}\" />";
        const string supor = "<input type=\"hidden\" name=\"supor_calote\" value=\"5,2\" />";

        var folha = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparar={outroId}&supor_calote=5,2");
        Assert.DoesNotContain("id=\"pesoSupomos\"", folha);
        foreach (var zona in new[] { "pesoLeituraContexto", "pesoAnteriorContexto", "pesoCapContexto" })
        {
            var campos = PesoZonas.Zona(folha, zona);
            Assert.Contains(comparar, campos);
            Assert.Contains(supor, campos);
        }
        // O «Calcular» é um GET da folha: os campos dele vão na zona dentro do apoio, que fica.
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica>\\s*<details class=\"tp-support\"[\\s\\S]*id=\"pesoCapContexto\"", folha);
        Assert.Contains($"<input type=\"hidden\" name=\"controlo_id\" value=\"{controloId}\" />", PesoZonas.Zona(folha, "pesoCapContexto"));

        var historico = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoId}&comparar={outroId}&supor_calote=5,2");
        var leituras = PesoZonas.Zona(historico, "pesoLeituraContexto");
        Assert.Contains(comparar, leituras);
        Assert.Contains(supor, leituras);
        Assert.DoesNotContain("controlo_id", leituras);

        var ilegivel = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparar={outroId}&supor_calote=abc");
        Assert.DoesNotContain("id=\"pesoSupomos\"", ilegivel);
        foreach (var zona in new[] { "pesoLeituraContexto", "pesoAnteriorContexto", "pesoCapContexto", "pesoComparacao" })
        {
            Assert.DoesNotContain("name=\"supor_calote\" value", PesoZonas.Zona(ilegivel, zona));
        }
    }

    [Fact]
    public async Task Folha_ComComparacao_PrevisualizarNaoAlteraPesosGuardadosNemPdf()
    {
        var (controloId, _, pesoId) = await SeedProducaoAsync();
        int anteriorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var job = new JobOn { Reference = "1075C142", ProductionNumber = "202512", MachineId = db.Machines.Single().Id };
            db.JobOns.Add(job);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = job.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var anterior = new Peso
            {
                ControloId = controlo.Id, CmToolId = "T3", BqToolId = "BQ-TOOL-9",
                Process = "NNPB", WaterTemperatureC = 20, VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m
            };
            db.Pesos.Add(anterior);
            await db.SaveChangesAsync();
            anteriorId = anterior.Id;
            db.PesoMeasurements.Add(new PesoMeasurement { PesoId = anteriorId, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 198.64m });
            await db.SaveChangesAsync();
        }

        SeedUser("4308", "Peso Operador", "provider-cmp-8", "PesoCmp8!");
        using var client = await LoginClientAsync("4308", "PesoCmp8!");
        var url = $"{PagePath}?controlo_id={controloId}&comparar={anteriorId}";
        var before = await GetPageHtmlAsync(client, url);
        Assert.Contains("data-peso-comparison><span>Comparação anterior</span><strong>202512</strong>", before);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(before)!;
        using var preview = await client.PostAsync($"{PagePath}?handler=Preview", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("lines", "[{\"cm_subject\":\"31\",\"water_weight\":\"150.25\"}]"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var after = await GetPageHtmlAsync(client, url);
        Assert.Contains("data-peso-comparison><span>Comparação anterior</span><strong>202512</strong>", after);
        Assert.Contains("450.52", after);
        Assert.Contains("200.26", after);

        using var pdf = await client.PostAsync($"{PagePath}?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("comparar", anteriorId.ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", pdf.Content.Headers.ContentDisposition!.DispositionType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        if (Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { } root)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(Path.Combine(root, "peso-comparacao-preview.pdf"), bytes);
        }

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(new[] { 199.64m, 99.82m }, await finalDb.PesoMeasurements
            .Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition).Select(m => m.WaterWeightGrams).ToArrayAsync());
        Assert.Equal(198.64m, (await finalDb.PesoMeasurements.SingleAsync(m => m.PesoId == anteriorId)).WaterWeightGrams);
        Assert.Equal(2, await finalDb.Pesos.CountAsync());
        Assert.Equal(0, await finalDb.PesoComparacoes.CountAsync());
    }

    [Fact]
    public async Task ComparacaoTab_ATerraPreselecionadoPelaFolha()
    {
        // O atalho da folha (?controlo_id=N&comparacao=1): a produção e o
        // peso inicial pré-selecionados (o padrão do html ao sair do
        // Resumo), o contra-molde com o CM do peso inicial.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4301", "Peso Operador", "provider-cmp-1", "PesoCmp1!");
        using var client = await LoginClientAsync("4301", "PesoCmp1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        Assert.Contains("Comparação de Peso", html);
        Assert.DoesNotContain("Pese os contra-moldes necessários e veja a diferença face ao peso inicial da produção", html);
        Assert.Contains("Produção / Job On", html);
        Assert.Contains("Peso inicial desta produção", html);
        Assert.Contains("selected=\"selected\">1075C142 · 202601 · B1</option>", html);
        Assert.Contains("1075C142 · 202601 · B1 · Lote 3 · 2 CM</option>", html);
        Assert.Contains("Contra-molde a pesar", html);
        Assert.Contains("selected=\"selected\">CM 5447 · Lote 3</option>", html);
        Assert.Contains("Temperatura da água (°C)", html);
        Assert.Contains("N.º CM", html);
        Assert.Contains("Massa de água (g)", html);
        // «Adicionar leitura» e «Remover leitura» no canto do cartão, com
        // «Comparar pesos» e «Guardar comparação», e não por baixo das
        // leituras (regra 74 do deep sweep de 18/10/2026).
        Assert.Matches("<div class=\"dmo-card-topo-acoes\">\\s*"
            + "<button type=\"button\" class=\"dmo-button\" form=\"cmpForm\" data-cmp-add-row>Adicionar leitura</button>\\s*"
            + "<button type=\"button\" class=\"dmo-button\" form=\"cmpForm\" data-cmp-remove-row>Remover leitura</button>\\s*"
            + "<button type=\"submit\" class=\"dmo-button\" form=\"cmpForm\">Comparar pesos</button>", html);
        var cmpForm = System.Text.RegularExpressions.Regex.Match(html, "<form [^>]*id=\"cmpForm\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.NotEmpty(cmpForm);
        Assert.DoesNotContain("<button", cmpForm);
        Assert.Contains("Comparar pesos", html);
        Assert.Contains("Guardar comparação", html);

        // A folha do Peso fica de fora (o padrão do tab do histórico).
        Assert.DoesNotContain("Leituras por CM", html);

        // Os ids nunca são factos da folha (o tool_id só viaja no POST).
        Assert.DoesNotContain(">T3<", html);
    }

    /// <summary>
    /// A base aparece CM a CM e as CM pesadas dizem quanto mexem na média
    /// da base, sem gravar nada.
    /// </summary>
    [Fact]
    public async Task ComparacaoTab_MostraBaseEEfeitoNaMedia()
    {
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();

        SeedUser("4319", "Peso Operador", "provider-cmp-19", "PesoCmp19!");
        using var client = await LoginClientAsync("4319", "PesoCmp19!");

        var comBase = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}");
        Assert.Contains("data-cmp-base", comBase);
        Assert.Contains("Base · média 325.39 g", comBase);
        Assert.Contains(">&#x2B;125.13<", comBase);
        Assert.Contains("a que mais puxa", comBase);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(comBase)!;
        var response = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("cm_subject", "31"), ("water_weight", "150"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-cmp-influencia", html);
        Assert.Contains("Efeito na média da base (325.39 g)", html);
        Assert.Contains("31 · a que mais mexe", html);
        Assert.Contains(">325.61<", html);
        Assert.Contains(">&#x2B;0.23<", html);

        using var finalScope = _factory.Services.CreateScope();
        Assert.Equal(0, await finalScope.ServiceProvider.GetRequiredService<DmoDbContext>().PesoComparacoes.CountAsync());
    }

    [Fact]
    public async Task CompararPesos_CalculaAoVivoSemGravar()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4302", "Peso Operador", "provider-cmp-2", "PesoCmp2!");
        using var client = await LoginClientAsync("4302", "PesoCmp2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var response = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("cm_subject", "21"), ("water_weight", "99.82"),
            ("cm_subject", "31"), ("water_weight", "150"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // O resultado do BACKEND: o peso do vidro pela fórmula do Peso, os
        // pares pelo nº de CM e o «Sem contraparte». A média dos três novos
        // CM: (450,52 + 200,26 + 326,06) / 3 = 325,61 g.
        Assert.Contains("Peso do vidro · controlo inicial / novas leituras", html);
        Assert.Contains("Peso dos novos CM", html);
        Assert.Contains("325.61 g", html);
        Assert.Contains("Pares válidos", html);
        Assert.Contains("Δ peso médio dos pares", html);
        Assert.Contains("Sem contraparte", html);
        Assert.Contains("Sem par", html);
        Assert.Contains(">450.52<", html);   // a linha do CM «12» na tabela dos pares
        Assert.Contains(">326.06<", html);   // o CM «31» novo, sem contraparte
        Assert.Contains("3 CM medido(s)", html);
        Assert.Contains("Pré-visualização não guardada", html);

        // ZERO escrita: a comparação ao vivo não cria registo nenhum.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task GuardarComparacao_CriaORegistoDoHistorico()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4303", "Peso Operador", "provider-cmp-3", "PesoCmp3!");
        using var client = await LoginClientAsync("4303", "PesoCmp3!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var response = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "198.64"),
            ("comparacao_note", "Verificação do desgaste"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("comparacao_id=", location, StringComparison.Ordinal);

        var comparacaoId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(location, @"comparacao_id=(\d+)").Groups[1].Value);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains("Comparação guardada · 1075C142 · 202601", html);
        Assert.Contains("Verificação do desgaste", html);
        Assert.Contains("Pares válidos", html);
        Assert.Contains("CM 5447 · Lote 3", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var registo = await db.PesoComparacoes.AsNoTracking().SingleAsync();
        Assert.Equal(controloId, registo.ControloId);
        Assert.Equal(pesoInicialId, registo.PesoInicialId);
        Assert.Equal(20, registo.WaterTemperatureC);
        Assert.Equal(2.50m, registo.GlassDensityGramsPerCm3);
        Assert.Equal(comparacaoId, registo.Id);

        // O registo aparece no histórico com o tipo Comparação.
        var historico = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&tipo=Comparacao");
        Assert.Contains("Comparação", historico);
        Assert.Contains("1 contra-moldes", historico);
    }

    [Fact]
    public async Task RecusasTipadas_PreservamOFormulario()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4304", "Peso Operador", "provider-cmp-4", "PesoCmp4!");
        using var client = await LoginClientAsync("4304", "PesoCmp4!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        // O peso inicial não selecionado.
        var semInicial = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, semInicial.StatusCode);
        var htmlSemInicial = await semInicial.Content.ReadAsStringAsync();
        Assert.Contains("Selecione o Peso inicial desta produção", htmlSemInicial);
        Assert.Contains("value=\"199.64\"", htmlSemInicial); // o form preserva o que foi submetido

        // A única leitura sem massa (o nº de CM sem massa): fica de fora com
        // aviso e, sem nenhuma leitura utilizável, nada há a comparar.
        var semMassa = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, semMassa.StatusCode);
        var htmlSemMassa = System.Net.WebUtility.HtmlDecode(await semMassa.Content.ReadAsStringAsync());
        Assert.Contains("Nenhuma leitura tem massa de água legível", htmlSemMassa);
        Assert.Contains("Leitura 1 (CM 12): falta a massa de água.", htmlSemMassa);

        // Sem leituras (só linhas em branco).
        var semLeituras = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("__RequestVerificationToken", token)));
        Assert.Contains("Registe pelo menos uma leitura de contra-molde", await semLeituras.Content.ReadAsStringAsync());

        // A temperatura fora da tabela.
        var temperaturaTorta = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "40"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", token)));
        Assert.Contains("Temperatura da água fora do intervalo suportado", await temperaturaTorta.Content.ReadAsStringAsync());

        // A temperatura escrita mas ilegível diz «ilegível» (não «fora do intervalo»).
        var temperaturaIlegivel = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "vinte"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", token)));
        var htmlTemperaturaIlegivel = System.Net.WebUtility.HtmlDecode(await temperaturaIlegivel.Content.ReadAsStringAsync());
        Assert.Contains("A temperatura da água está vazia ou ilegível", htmlTemperaturaIlegivel);
        Assert.Contains("20,4 ou 20.4", htmlTemperaturaIlegivel);
        Assert.DoesNotContain("fora do intervalo", htmlTemperaturaIlegivel);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task GuardarComparacao_ExisteOModuloControloCriar()
    {
        // O template sem o Controlo Criar não entra na página: o pedido vai
        // para o primeiro módulo dele e nada é gravado.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4305", "Chefe Aprove", "provider-cmp-5", "PesoCmp5!", comCapability: false);
        using var client = await LoginClientAsync("4305", "PesoCmp5!");
        var page = await client.GetAsync($"{PagePath}?controlo_id={controloId}&comparacao=1");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/JobOn", page.Headers.Location!.ToString());
        var token = await GetVerificationTokenAsync(client, "/JobOn?novo=1");

        var response = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199.64"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/JobOn", response.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Historico_ProcuraUnificadaPelosQuatroTipos()
    {
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        int comparacaoId;

        // Um registo de Pegamentos e um de Comparação no mesmo controlo.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pegamentos.Add(new Pegamentos
            {
                ControloId = controloId,
                CmToolId = "T3",
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3",
                ToleranceMm = 0.20m
            });
            var comparacao = new PesoComparacao
            {
                ControloId = controloId,
                PesoInicialId = pesoInicialId,
                CmToolId = "T3",
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3",
                WaterTemperatureC = 20,
                WaterDensityGramsPerCm3 = 0.99717m,
                GlassDensityGramsPerCm3 = 2.50m,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                CreatedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc)
            };
            db.PesoComparacoes.Add(comparacao);
            await db.SaveChangesAsync();
            db.PesoComparacaoMeasurements.Add(new PesoComparacaoMeasurement { ComparacaoId = comparacao.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 199.64m });
            await db.SaveChangesAsync();
            comparacaoId = comparacao.Id;
        }

        SeedUser("4306", "Peso Operador", "provider-cmp-6", "PesoCmp6!");
        using var client = await LoginClientAsync("4306", "PesoCmp6!");

        // O histórico abre vazio (decisão do proprietário, 16/10/2026): sem
        // filtros não lista nada; a procura pela referência traz os três tipos.
        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.Contains("Histórico do Controlo", vazio);
        Assert.Contains("Pesquise por referência (e depois a produção), ferramenta, tipo ou data.", vazio);
        Assert.DoesNotContain("Escreva primeiro a referência", vazio);
        Assert.Contains("<option value=\"\">Produção</option>", vazio);
        Assert.DoesNotContain(">Peso</td>", vazio);
        Assert.DoesNotContain(">Pegamentos</td>", vazio);
        Assert.DoesNotContain(">Comparação</td>", vazio);
        // O «Comparar Pesos guardados» saiu do Histórico (Diogo, 21/10/2026):
        // compara-se na folha do Peso de cada produção.
        Assert.DoesNotContain("Comparar Pesos guardados", vazio);

        var tudo = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=1075C142");
        Assert.Contains("Histórico do Controlo", tudo);
        Assert.DoesNotContain("Pesquise por referência (e depois a produção)", tudo);
        Assert.Contains(">Peso</td>", tudo);
        Assert.Contains(">Pegamentos</td>", tudo);
        Assert.Contains(">Comparação</td>", tudo);
        Assert.Contains(">Resumo</td>", tudo);
        Assert.Contains($"data-dmo-open=\"/Controlo/Resumo?jobon_id={jobOnId}\"", tudo);
        // O segundo passo: o nº de produção num dropdown com as produções da referência.
        Assert.Contains("<option value=\"202601|1075C142\">202601</option>", tudo);

        var daProducao = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=1075C142&producao=202601");
        Assert.Contains("<option value=\"202601|1075C142\" selected=\"selected\">202601</option>", daProducao);
        Assert.Contains(">Resumo</td>", daProducao);
        Assert.Contains("Ferramenta", tudo);
        Assert.DoesNotContain("Comparar Pesos guardados", tudo);

        var soPegamentos = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&tipo=Pegamentos");
        Assert.Contains(">Pegamentos</td>", soPegamentos);
        Assert.DoesNotContain(">Comparação</td>", soPegamentos);
        Assert.DoesNotContain(">Peso</td>", soPegamentos);

        var porData = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&data=2026-10-13");
        Assert.Contains(">Comparação</td>", porData);
        Assert.DoesNotContain(">Peso</td>", porData);

        // O detalhe do registo de comparação (?comparacao_id — o open).
        var detalhe = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&comparacao_id={comparacaoId}");
        Assert.Contains("Comparação · 1075C142 · 202601", detalhe);
        Assert.Contains("Pares válidos", detalhe);
        Assert.Contains("Pares válidos", detalhe);
    }

    [Fact]
    public async Task Historico_Filtros_TrocamSoOsResultadosEAsProducoes()
    {
        // Deep sweep, 18/10/2026 (n.º 69 / B-peso-5): cada filtro do
        // histórico recarregava a página toda, e um filtro escolhido com a
        // página anterior a carregar perdia-se. Os filtros ficam
        // (data-dmo-fica) e o DmoTroca troca só os resultados, o dropdown das
        // produções e o detalhe; para isso, fora das zonas, a página é igual
        // em todas as escolhas.
        var (controloId, _, pesoInicialId) = await SeedProducaoAsync();
        int comparacaoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var comparacao = new PesoComparacao
            {
                ControloId = controloId,
                PesoInicialId = pesoInicialId,
                CmToolId = "T3",
                CmToolReferenceFrozen = "5447",
                CmToolLotFrozen = "3",
                WaterTemperatureC = 20,
                WaterDensityGramsPerCm3 = 0.99717m,
                GlassDensityGramsPerCm3 = 2.50m,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                CreatedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc)
            };
            db.PesoComparacoes.Add(comparacao);
            await db.SaveChangesAsync();
            comparacaoId = comparacao.Id;
        }

        SeedUser("4381", "Peso Operador", "provider-cmp-16", "PesoCmp16!");
        using var client = await LoginClientAsync("4381", "PesoCmp16!");

        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.Contains("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>", vazio);
        Assert.Contains("id=\"pesoHistoricoFiltros\"", vazio);
        Assert.Contains("<div id=\"pesoHistoricoProducaoZona\" class=\"dmo-field\" data-dmo-regiao>", vazio);
        Assert.Contains("<div id=\"pesoHistoricoDetalhe\" class=\"dmo-zona\" data-dmo-regiao>", vazio);
        // A produção aplica-se logo com o rato e no Enter com o teclado (a
        // regra do beta.214), como os outros dropdowns.
        Assert.Contains("<select id=\"historicoProducao\" name=\"producao\" disabled>", vazio);
        var comReferencia = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&referencia=1075C142");
        Assert.Contains("<select id=\"historicoProducao\" name=\"producao\" data-dmo-autosubmit data-dmo-depende=\"historicoReferencia\">", comReferencia);

        var fora = PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[]
        {
            "referencia=1075C142",
            "referencia=1075C142&producao=202601",
            "referencia=1075C142&tipo=Comparacao",
            "ferramenta=5447",
            "tipo=Pegamentos",
            "sem_jobon=1",
            "data=2026-10-13",
            "referencia=NAOEXISTE",
            $"comparacao_id={comparacaoId}",
        })
        {
            var html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&{filtros}");
            Assert.Equal(fora, PesoZonas.ForaDasZonas(html));
        }

        Assert.NotEqual(PesoZonas.Zona(vazio, "pesoHistoricoProducaoZona"), PesoZonas.Zona(comReferencia, "pesoHistoricoProducaoZona"));
        Assert.Contains("Comparação · 1075C142 · 202601",
            PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?historico=1&comparacao_id={comparacaoId}"), "pesoHistoricoDetalhe"));

        // A referência e a ferramenta aplicam-se ao escrever (deep sweep 2,
        // 18/10/2026, F-10).
        Assert.Matches("<form [^>]*id=\"pesoHistoricoFiltros\" data-dmo-ao-escrever=\"pesoHistoricoResultados\"", vazio);
    }

    [Fact]
    public async Task ComparacaoTab_EscolherAReferenciaEAProducao_TrocaSoAsZonas()
    {
        // Deep sweep 2 (18/10/2026, F-8): a referência, a produção e o Peso
        // inicial recarregavam a página inteira (o data-auto-submit fazia
        // form.submit() fora de data-dmo-parcial). A referência fica
        // (data-dmo-fica) e o formulário vai pelo DmoTroca; os dropdowns são
        // zonas dentro dele e aplicam ao escolher com a proteção das setas
        // (data-dmo-autosubmit). Fora das zonas, a página é igual em todas as
        // escolhas: senão o componente abria a página inteira.
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4795", "Peso Operador", "provider-cmp-escolha", "PesoCmpEsc1!");
        using var client = await LoginClientAsync("4795", "PesoCmpEsc1!");

        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1");
        var escolha = System.Text.RegularExpressions.Regex.Match(vazio, "<form [^>]*id=\"cmpEscolha\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>\\s*<form [^>]*id=\"cmpEscolha\"", vazio);
        Assert.Contains("<div id=\"pesoComparacaoProducaoZona\" class=\"dmo-field\" data-dmo-regiao>", escolha);
        Assert.Contains("<div id=\"pesoComparacaoInicialZona\" class=\"dmo-field\" data-dmo-regiao>", escolha);
        Assert.Contains("<select id=\"cmpProducao\" name=\"jobon_id\" data-dmo-autosubmit data-dmo-filter", escolha);
        Assert.Contains("<select id=\"cmpInicial\" name=\"peso_inicial\" data-dmo-autosubmit>", escolha);
        Assert.DoesNotContain("data-auto-submit", escolha);
        Assert.Contains("<div id=\"pesoComparacaoAvisos\" class=\"dmo-zona\" data-dmo-regiao>", vazio);

        var fora = PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[]
        {
            "referencia=1075C142",
            "referencia=1075c142",
            $"referencia=1075C142&jobon_id={jobOnId}",
            $"referencia=1075C142&jobon_id={jobOnId}&peso_inicial={pesoInicialId}",
            $"jobon_id={jobOnId}&peso_inicial={pesoInicialId}",
            $"referencia=9999X001&jobon_id={jobOnId}",
            "referencia=NAOEXISTE",
            // A procura pelo início da referência ou da parte BQ (beta.237):
            // o início também troca só as zonas; um pedaço do meio não encontra.
            "referencia=1075",
            $"referencia=1075&jobon_id={jobOnId}",
            "referencia=c142",
            "referencia=075C",
        })
        {
            var html = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&{filtros}");
            Assert.Equal(fora, PesoZonas.ForaDasZonas(html));
        }

        var comProducao = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075C142&jobon_id={jobOnId}");
        Assert.Contains($"<option value=\"{jobOnId}\" selected=\"selected\">", PesoZonas.Zona(comProducao, "pesoComparacaoProducaoZona"));
        Assert.Contains($"<option value=\"{pesoInicialId}\" selected=\"selected\">", PesoZonas.Zona(comProducao, "pesoComparacaoInicialZona"));
        Assert.Contains("<span>Peso inicial</span>", PesoZonas.Zona(comProducao, "pesoComparacaoContexto"));
        Assert.Contains("Sem produções para «NAOEXISTE».",
            System.Net.WebUtility.HtmlDecode(PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=NAOEXISTE"), "pesoComparacaoContexto")));

        // O dropdown da produção segue a regra da procura: o início da
        // referência («1075») e o da parte BQ («c142») trazem a produção, e a
        // produção pedida fica escolhida; «075C» (a meio) não traz nada.
        var peloInicio = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075&jobon_id={jobOnId}");
        Assert.Contains($"<option value=\"{jobOnId}\" selected=\"selected\">", PesoZonas.Zona(peloInicio, "pesoComparacaoProducaoZona"));
        Assert.Contains($"<option value=\"{jobOnId}\"", PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=c142"), "pesoComparacaoProducaoZona"));
        var aMeio = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=075C&jobon_id={jobOnId}");
        Assert.DoesNotContain($"<option value=\"{jobOnId}\"", PesoZonas.Zona(aMeio, "pesoComparacaoProducaoZona"));
        Assert.Contains("Sem produções para «075C».", System.Net.WebUtility.HtmlDecode(PesoZonas.Zona(aMeio, "pesoComparacaoContexto")));
    }

    [Fact]
    public async Task Historico_SemJobOnAssociado_EncontraOPesoSemProducao()
    {
        var (_, _, pesoInicialId) = await SeedProducaoAsync();
        int segundoPesoId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var segundo = new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-TOOL-9",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m
            };
            db.Pesos.Add(segundo);
            await db.SaveChangesAsync();
            db.PesoMeasurements.Add(new PesoMeasurement { PesoId = segundo.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 198.64m });
            await db.SaveChangesAsync();
            segundoPesoId = segundo.Id;
        }

        SeedUser("4307", "Peso Operador", "provider-cmp-7", "PesoCmp7!");
        using var client = await LoginClientAsync("4307", "PesoCmp7!");

        // O segundo Peso não tem Job On: o filtro «Sem Job On associado» encontra-o.
        var porAssociar = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&sem_jobon=1");
        Assert.Contains($"data-dmo-open=\"/Controlo/Peso?peso_id={segundoPesoId}\"", porAssociar);
        Assert.Contains("Sem Job On · 5447", porAssociar);
        Assert.Contains("Por associar", porAssociar);
        Assert.DoesNotContain($"peso_id={pesoInicialId}\"", porAssociar);
    }

    [Fact]
    public async Task CompararPesos_LeiturasMasFicamDeForaComAviso_AsOutrasComparam()
    {
        // A3 (beta.65): uma massa ilegível, um nº de CM sem massa e uma massa
        // ≤ 0 já não anulam a comparação — essas linhas ficam de fora e o
        // aviso diz quais; a leitura boa compara-se.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4320", "Peso Operador", "provider-cmp-20", "PesoCmp8!");
        using var client = await LoginClientAsync("4320", "PesoCmp8!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var response = await client.PostAsync($"{PagePath}?handler=Comparar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "199,64"),
            ("cm_subject", "21"), ("water_weight", "abc"),
            ("cm_subject", "31"), ("water_weight", "0"),
            ("cm_subject", "41"), ("water_weight", ""),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // O resultado existe, só com a leitura utilizável.
        Assert.Contains("Peso do vidro · controlo inicial / novas leituras", html);
        Assert.Contains("1 CM medido(s)", html);
        Assert.Contains(">450.52<", html);

        // O aviso (estilo de aviso, não de recusa) diz quais ficaram de fora.
        Assert.Contains("peso-aviso warn", html);
        Assert.Contains("3 leituras ficaram de fora; as outras foram comparadas.", html);
        Assert.Contains("Leitura 2 (CM 21): a massa de água «abc» é ilegível (escreva por exemplo 199,64 ou 199.64).", html);
        Assert.Contains("Leitura 3 (CM 31): a massa de água 0 g não é maior que 0.", html);
        Assert.Contains("Leitura 4 (CM 41): falta a massa de água.", html);
        Assert.DoesNotContain("Nenhuma comparação foi feita", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PesoComparacoes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task GuardarComparacao_GravaAsLeiturasUtilizaveisEAvisaAsDeFora()
    {
        // A4 (beta.65): o Guardar já não é anulado por uma leitura má — grava
        // as utilizáveis e o aviso (depois do PRG) diz quais ficaram de fora.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4321", "Peso Operador", "provider-cmp-21", "PesoCmp9!");
        using var client = await LoginClientAsync("4321", "PesoCmp9!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var response = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", "12"), ("water_weight", "198.64"),
            ("cm_subject", "21"), ("water_weight", "-3"),
            ("cm_subject", "31"), ("water_weight", "1o0"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("comparacao_id=", location, StringComparison.Ordinal);
        Assert.Contains("ignoradas=", location, StringComparison.Ordinal);

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("Comparação guardada · 1075C142 · 202601", html);
        Assert.Contains("2 leituras ficaram de fora; a comparação foi guardada sem elas.", html);
        Assert.Contains("Leitura 2 (CM 21): a massa de água -3 g não é maior que 0.", html);
        Assert.Contains("Leitura 3 (CM 31): a massa de água «1o0» é ilegível", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var registo = await db.PesoComparacoes.AsNoTracking().SingleAsync();
        var leituras = await db.PesoComparacaoMeasurements.AsNoTracking()
            .Where(m => m.ComparacaoId == registo.Id).ToListAsync();
        var gravada = Assert.Single(leituras);
        Assert.Equal("12", gravada.CmSubject);
        Assert.Equal(198.64m, gravada.WaterWeightGrams);
        Assert.Equal(1, gravada.RowPosition);
    }

    [Fact]
    public async Task GuardarComparacao_CliquesRepetidos_UmSoRegistoESemMensagem()
    {
        // B-peso-2 (deep sweep 18/10/2026): três cliques em «Guardar
        // comparação» criavam três comparações iguais, e um clique que já
        // chegava à página da comparação guardada mostrava «Registe pelo
        // menos uma leitura… Nenhuma comparação foi feita».
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        SeedUser("4322", "Peso Operador", "provider-cmp-22", "PesoCmp22!");
        using var client = await LoginClientAsync("4322", "PesoCmp22!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}&comparacao=1");

        var locations = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()),
                ("peso_inicial", pesoInicialId.ToString()),
                ("cm_tool_id", "T3"),
                ("water_temperature", "22"),
                ("cm_subject", "76"), ("water_weight", "132,2"),
                ("comparacao_note", ""),
                ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            locations.Add(response.Headers.Location!.ToString());
        }

        var location = Assert.Single(locations.Distinct());
        var comparacaoId = System.Text.RegularExpressions.Regex.Match(location, @"comparacao_id=(\d+)").Groups[1].Value;

        // A página da comparação guardada leva-a no formulário: o clique que
        // chega a ela, com o formulário sem leituras, volta à mesma.
        var guardada = await GetPageHtmlAsync(client, location);
        Assert.Contains($"name=\"comparacao_guardada\" value=\"{comparacaoId}\"", guardada);
        var repetido = await client.PostAsync($"{PagePath}?handler=GuardarComparacao", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()),
            ("peso_inicial", pesoInicialId.ToString()),
            ("comparacao_guardada", comparacaoId),
            ("ignoradas", ""),
            ("cm_tool_id", "T3"),
            ("water_temperature", "20"),
            ("cm_subject", ""), ("water_weight", ""),
            ("cm_subject", ""), ("water_weight", ""),
            ("comparacao_note", ""),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(guardada)!)));
        Assert.Equal(HttpStatusCode.Redirect, repetido.StatusCode);
        Assert.Equal(location, repetido.Headers.Location!.ToString());

        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("Comparação guardada · 1075C142 · 202601", html);
        Assert.DoesNotContain("Registe pelo menos uma leitura", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.PesoComparacoes.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.PesoComparacaoMeasurements.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(false, true, "boquilha (BQ)")]
    [InlineData(true, false, "CM")]
    [InlineData(false, false, "CM nem boquilha (BQ)")]
    public async Task Folha_ProducaoSemCmOuBq_ExplicaPorQueNaoHaNovoPeso(bool comBq, bool comCm, string emFalta)
    {
        // Bug (beta.65): sem CM ou sem BQ no Job On o «Novo Peso da produção»
        // desaparecia sem explicação; agora um aviso diz porquê.
        await SeedProducaoAsync();
        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = new JobOn
            {
                Reference = "1075C142",
                ProductionNumber = "202699",
                MachineId = db.Machines.Single().Id
            };
            db.JobOns.Add(jobOn);
            if (comCm)
            {
                db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
            }
            if (comBq)
            {
                db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
            }
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            controloId = controlo.Id;
        }

        SeedUser("4322", "Peso Operador", "provider-cmp-22", "PesoCmp10!");
        using var client = await LoginClientAsync("4322", "PesoCmp10!");
        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));

        Assert.DoesNotContain("Novo Peso da produção</h3>", html);
        Assert.Contains("data-peso-sem-ferramentas", html);
        Assert.Contains($"não aparece porque o Job On desta produção não tem {emFalta}.", html);
    }

    [Fact]
    public async Task Folha_ProducaoComCmEBq_NaoMostraOAvisoDeFerramentas()
    {
        var (controloId, _, _) = await SeedProducaoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
            db.Pesos.RemoveRange(db.Pesos);
            await db.SaveChangesAsync();
        }

        SeedUser("4323", "Peso Operador", "provider-cmp-23", "PesoCmp11!");
        using var client = await LoginClientAsync("4323", "PesoCmp11!");
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        Assert.Contains("Novo Peso da produção</h3>", html);
        Assert.DoesNotContain("data-peso-sem-ferramentas", html);
    }

    [Fact]
    public async Task CamposDecimais_SaoTextoComTecladoDecimal_ParaAceitarVirgula()
    {
        // beta.65: os campos de temperatura, volumes, nominal, massa e
        // comparação deixam de ser type="number" (o browser bloqueava a
        // vírgula); passam a texto com inputmode="decimal" e os placeholders
        // dão os dois exemplos.
        var (controloId, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
            await db.SaveChangesAsync();
        }

        SeedUser("4324", "Peso Operador", "provider-cmp-24", "PesoCmp24!");
        using var client = await LoginClientAsync("4324", "PesoCmp24!");

        var comparacao = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&jobon_id={jobOnId}&peso_inicial={pesoInicialId}");
        Assert.DoesNotContain("type=\"number\"", comparacao);
        Assert.Contains("id=\"cmpTemperature\" name=\"water_temperature\" type=\"text\" inputmode=\"decimal\"", comparacao);
        Assert.Contains("placeholder=\"Ex.: 20,4 ou 20.4\"", comparacao);
        Assert.Contains("name=\"water_weight\" type=\"text\" inputmode=\"decimal\"", comparacao);
        Assert.Contains("placeholder=\"Ex.: 199,64 ou 199.64\"", comparacao);

        // O Peso da produção apagado: o formulário «Novo Peso da produção»
        // pede a temperatura e os técnicos em falta.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
            db.Pesos.RemoveRange(db.Pesos);
            await db.SaveChangesAsync();
        }

        var folha = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("Novo Peso da produção</h3>", folha);
        Assert.DoesNotContain("type=\"number\"", folha);
        Assert.Contains("name=\"water_temperature\" type=\"text\" inputmode=\"decimal\"", folha);
        Assert.Contains("name=\"cm_volume_puncao\" type=\"text\" inputmode=\"decimal\"", folha);
        Assert.Contains("placeholder=\"Ex.: 70,00 ou 70.00\"", folha);
        Assert.Contains("name=\"cm_nominal_weight\" type=\"text\" inputmode=\"decimal\"", folha);
        Assert.Contains("placeholder=\"Ex.: 200,00 ou 200.00\"", folha);
        Assert.Contains("name=\"bq_volume_marisa\" type=\"text\" inputmode=\"decimal\"", folha);
        Assert.Contains("placeholder=\"Ex.: 10,00 ou 10.00\"", folha);
    }

    [Fact]
    public async Task DoisPassos_SemReferenciaNadaCarrega_ComReferenciaSoAsDela()
    {
        // A procura em dois passos (16/10/2026): a Comparação de Peso abre
        // vazia; a referência traz só as produções dela; uma referência sem
        // nada é aviso.
        var (_, jobOnId, pesoInicialId) = await SeedProducaoAsync();
        int outraJobOnId;
        int outroPesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = await db.Machines.SingleAsync();
            var outra = new JobOn { Reference = "9999X001", ProductionNumber = "202699", MachineId = machine.Id };
            db.JobOns.Add(outra);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = outra.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var peso = new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-TOOL-9",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m,
                ControloId = controlo.Id
            };
            db.Pesos.Add(peso);
            // A CM T3 trabalha na B1 e na C3 (as ligações da ferramenta).
            var c3 = new Machine { Code = "C3" };
            db.Machines.Add(c3);
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", Machine = machine });
            db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "T3", Machine = c3 });
            await db.SaveChangesAsync();
            outraJobOnId = outra.Id;
            outroPesoId = peso.Id;
        }

        SeedUser("4320", "Peso Operador", "provider-cmp-20", "PesoCmp20!");
        using var client = await LoginClientAsync("4320", "PesoCmp20!");

        // Comparação de Peso sem referência: o campo e o select vazio.
        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1");
        Assert.Contains("name=\"referencia\"", vazio);
        Assert.Contains("data-dmo-suggest=\"referencias\" data-dmo-suggest-submit", vazio);
        Assert.Contains(">Indique a referência</option>", System.Net.WebUtility.HtmlDecode(vazio));
        Assert.DoesNotContain($"<option value=\"{jobOnId}\"", vazio);
        Assert.DoesNotContain($"<option value=\"{outraJobOnId}\"", vazio);

        // Com a referência (sem distinguir maiúsculas): só as produções dela.
        var comReferencia = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075c142");
        Assert.Contains($"<option value=\"{jobOnId}\">1075C142 · 202601 · B1</option>", comReferencia);
        Assert.DoesNotContain($"<option value=\"{outraJobOnId}\"", comReferencia);

        // O início da referência ou da parte BQ chega; um pedaço do meio não
        // (deep sweep 2, F-3: «35» trazia as produções de quatro referências
        // com 35 a meio).
        var inicio = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075");
        Assert.Contains($"<option value=\"{jobOnId}\">1075C142 · 202601 · B1</option>", inicio);
        Assert.DoesNotContain($"<option value=\"{outraJobOnId}\"", inicio);
        Assert.Contains($"<option value=\"{jobOnId}\">1075C142 · 202601 · B1</option>",
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=c14"));
        var meio = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=075"));
        Assert.DoesNotContain($"<option value=\"{jobOnId}\"", meio);
        Assert.Contains("Sem produções para «075».", meio);

        // A produção pedida por link entra com a mesma regra: pelo início, sim;
        // por um pedaço do meio, não.
        Assert.Contains($"<option value=\"{jobOnId}\" selected=\"selected\">1075C142 · 202601 · B1</option>",
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075&jobon_id={jobOnId}"));
        Assert.DoesNotContain($"<option value=\"{jobOnId}\"",
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=075&jobon_id={jobOnId}"));

        // A produção de outra referência no link fica de fora do select.
        var trocada = await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=1075C142&jobon_id={outraJobOnId}");
        Assert.DoesNotContain("selected=\"selected\">9999X001", trocada);
        Assert.DoesNotContain($"<option value=\"{outraJobOnId}\"", trocada);

        // Referência sem produções: aviso âmbar, nada bloqueado.
        var semProducoes = System.Net.WebUtility.HtmlDecode(
            await GetPageHtmlAsync(client, $"{PagePath}?comparacao=1&referencia=NAOEXISTE"));
        Assert.Contains("Sem produções para «NAOEXISTE».", semProducoes);
        Assert.Contains("peso-aviso warn", semProducoes);

        // O «Comparar Pesos guardados» saiu do Histórico (Diogo, 21/10/2026).
        var historico = await GetPageHtmlAsync(client, $"{PagePath}?historico=1");
        Assert.DoesNotContain("Comparar Pesos guardados", historico);
        Assert.DoesNotContain("peso1Procura", historico);
    }
}
