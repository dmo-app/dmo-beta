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
/// Os Pegamentos sem Job On (DMO-P01): criar com as ferramentas escolhidas
/// à mão, medir pelo pegamentos_id e associar mais tarde a um Job On, com a
/// escolha em dois passos (referência e máquina, depois a produção).
/// </summary>
public sealed class PegamentosSemJobOnPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Pegamentos";

    private readonly CustomWebApplicationFactory _factory;

    public PegamentosSemJobOnPageTests(CustomWebApplicationFactory factory)
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
        db.PegamentosMeasurements.RemoveRange(db.PegamentosMeasurements);
        db.Pegamentos.RemoveRange(db.Pegamentos);
        db.PegamentosTolerances.RemoveRange(db.PegamentosTolerances);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
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

    private async Task<HttpClient> LoginAsync(string operatorId)
    {
        var password = $"PegSem{operatorId}!";
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, $"provider-pegsem-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
            if (template is null)
            {
                template = new Template
                {
                    TemplateKey = "tpl-teste-operacional",
                    Name = "Operacional de teste",
                    Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
                };
                db.Templates.Add(template);
                db.SaveChanges();
            }

            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Pegamentos sem Job On", ProviderUserId = $"provider-pegsem-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = CreateClient();
        var token = await TokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return HtmlFormHelpers.ExtractRequestVerificationToken(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private static async Task<string> HtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string tokenPath, string handler, params (string, string)[] fields)
    {
        var token = await TokenAsync(client, tokenPath);
        return await client.PostAsync($"{PagePath}?handler={handler}",
            HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    /// <summary>O J10 real (B1, CM T3 5447, BQ-1 T194, MF-TOOL-9 5810) e uma CM avulsa (T9 6000).</summary>
    private async Task<int> SeedProductionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "T9", Type = ToolType.Cm, Reference = "6000", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await db.SaveChangesAsync();

        var created = await scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>()
            .HandleAsync(new CreateJobOn("J10", "5810", machine.Id, "T3", "MF-TOOL-9", "BQ-1"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        return created.JobOn!.JobOnId;
    }

    private async Task<int> CreateToolScopedAsync(HttpClient client, string cmRef, string cmToolId)
    {
        // Sem o MF não se cria: os Pegamentos medem sempre a CM, a BQ e o MF.
        var semMf = await HtmlAsync(client, $"{PagePath}?cm_ref={cmRef}&bq_ref=T194");
        Assert.DoesNotContain("Criar Pegamentos", semMf);

        var tokenPath = $"{PagePath}?cm_ref={cmRef}&bq_ref=T194&mf_ref=5810";
        var escolha = await HtmlAsync(client, tokenPath);
        Assert.Contains("Criar Pegamentos", escolha);

        var response = await PostAsync(client, tokenPath, "CreateToolScoped",
            ("cm_tool_id", cmToolId), ("bq_tool_id", "BQ-1"), ("mf_tool_id", "MF-TOOL-9"),
            ("cm_ref", cmRef), ("bq_ref", "T194"), ("mf_ref", "5810"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"{PagePath}?pegamentos_id=", location);
        return int.Parse(location[(location.IndexOf('=') + 1)..]);
    }

    private async Task<DMO.Alpha.Core.Controlo.Pegamentos> RecordAsync(int pegamentosId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.Pegamentos.AsNoTracking().Include(p => p.Measurements).SingleAsync(p => p.Id == pegamentosId);
    }

    [Fact]
    public async Task SemJobOn_CriaMedeEAssociaMaisTarde()
    {
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4501");

        // Criar sem Job On: as ferramentas à mão, nenhum controlo.
        var pegamentosId = await CreateToolScopedAsync(client, "5447", "T3");
        var criado = await RecordAsync(pegamentosId);
        Assert.Null(criado.ControloId);
        Assert.Equal("T3", criado.CmToolId);
        Assert.Equal("BQ-1", criado.BqToolId);
        Assert.Equal("MF-TOOL-9", criado.MfToolId);
        Assert.Equal("5447", criado.CmToolReferenceFrozen);
        Assert.Equal(PegamentosCalculation.DefaultToleranceMm, criado.ToleranceMm);

        var folhaUrl = $"{PagePath}?pegamentos_id={pegamentosId}";
        var folha = await HtmlAsync(client, folhaUrl);
        Assert.Contains("Pegamentos sem Job On", folha);
        Assert.Contains("Associar Job On", folha);
        Assert.Contains("data-peg-component=\"CM\"", folha);
        Assert.Contains("data-peg-component=\"BQ\"", folha);
        Assert.Contains("data-peg-component=\"MF\"", folha);
        // O «Associar Job On» fica como o operador o deixou (data-dmo-fica):
        // fechado ou aberto, o que está fora das zonas é igual e gravar uma
        // leitura com ele aberto não abre a página inteira.
        Assert.Matches("<div id=\"pegAssociar\" class=\"dmo-zona\" data-dmo-fica>\\s*</div>", folha);

        // Medir pelo pegamentos_id: a folha volta pelo mesmo registo.
        var medida = await PostAsync(client, folhaUrl, "RegisterMeasurement",
            ("pegamentos_id", pegamentosId.ToString()), ("component", "CM"),
            ("costura", "70.10"), ("contra_costura", "69.90"), ("gargalo", "70.00"));
        Assert.Equal(HttpStatusCode.Redirect, medida.StatusCode);
        Assert.Equal(folhaUrl, medida.Headers.Location!.OriginalString);

        Assert.Single((await RecordAsync(pegamentosId)).Measurements);
        // O operador que gravou ocupa o lugar da máquina (Diogo, 17/10/2026).
        var folhaMedida = await HtmlAsync(client, folhaUrl);
        Assert.Contains("<span>Operador</span><strong>", folhaMedida);
        Assert.DoesNotContain("<span>Operador</span><strong>—</strong>", folhaMedida);

        // O PDF sai também sem Job On, gerado em memória.
        var pdf = await PostAsync(client, folhaUrl, "GeneratePdf", ("pegamentos_id", pegamentosId.ToString()));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);

        // O histórico do Controlo encontra-os em «Sem Job On associado» e abre-os pelo pegamentos_id.
        var porAssociar = await HtmlAsync(client, "/Controlo/Peso?historico=1&sem_jobon=1");
        Assert.Contains($"data-dmo-open=\"/Controlo/Pegamentos?pegamentos_id={pegamentosId}\"", porAssociar);
        var abrir = await client.GetAsync($"/Controlo/Peso?historico=1&abrir=Pegamentos:{pegamentosId}:");
        Assert.Equal(folhaUrl, abrir.Headers.Location!.OriginalString);

        // Associar: referência e máquina, depois a produção.
        var associarUrl = $"{folhaUrl}&associar=1&referencia=5810&maquina=B1";
        var escolha = await HtmlAsync(client, associarUrl);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", escolha);
        Assert.Matches("<div id=\"pegAssociar\" class=\"dmo-zona\" data-dmo-fica>\\s*<section class=\"dmo-card peg-card\" aria-labelledby=\"peg-associar-title\">[\\s\\S]*?</section>\\s*</div>\\s*<div id=\"pegAvisos\"", escolha);
        // Os campos de procura aplicam ao escolher (Diogo, 17/10/2026):
        // Sem «Procurar», nem sem JavaScript; associar mantém o botão.
        Assert.Contains("data-dmo-suggest=\"referencias\" data-dmo-suggest-submit", escolha);
        Assert.Contains("name=\"maquina\" data-dmo-autosubmit", escolha);
        Assert.DoesNotContain(">Procurar</button>", escolha);
        Assert.Contains(">Associar</button>", escolha);

        var associado = await PostAsync(client, associarUrl, "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, associado.StatusCode);

        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            controloId = await db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        }

        Assert.Equal($"{PagePath}?controlo_id={controloId}&associado=1", associado.Headers.Location!.OriginalString);
        var registo = await RecordAsync(pegamentosId);
        Assert.Equal(controloId, registo.ControloId);
        Assert.Single(registo.Measurements);

        var producao = await HtmlAsync(client, associado.Headers.Location!.OriginalString);
        Assert.Contains("Contexto da produção", producao);
        Assert.Contains("Pegamentos associados a esta produção", producao);
        Assert.Contains("70.10", producao);

        // Depois de associados, deixam de estar por associar.
        Assert.DoesNotContain($"pegamentos_id={pegamentosId}\"", await HtmlAsync(client, "/Controlo/Peso?historico=1&sem_jobon=1"));
    }

    [Fact]
    public async Task Associar_ComOutraCm_Recusa_ENadaMuda()
    {
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4502");
        var pegamentosId = await CreateToolScopedAsync(client, "6000", "T9");

        var folhaUrl = $"{PagePath}?pegamentos_id={pegamentosId}&associar=1&referencia=5810";
        var response = await PostAsync(client, folhaUrl, "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.Equal($"{PagePath}?pegamentos_id={pegamentosId}&associar=1&recusa=ToolMismatch-Cm", response.Headers.Location!.OriginalString);
        Assert.Null((await RecordAsync(pegamentosId)).ControloId);

        var html = await HtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("A CM destes Pegamentos não é a CM do Job On. Nada foi associado.", html);
    }

    [Fact]
    public async Task Associar_PegamentosJaAssociadosAOutraProducao_RecusaComMensagemNoCanto()
    {
        // Deep sweep, 18/10/2026, B-pegamentos-4: dois separadores com o
        // «Associar Job On»; o segundo associa a outra produção depois de o
        // primeiro ter associado. A recusa abria a folha da primeira produção
        // sem mensagem nenhuma; agora a recusa aparece no canto.
        var jobOnId = await SeedProductionAsync();
        int outroJobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var machine = new Machine { Code = "B2" };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();
            var created = await scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>()
                .HandleAsync(new CreateJobOn("J11", "5810", machine.Id, "T3", "MF-TOOL-9", "BQ-1"));
            Assert.Equal(CreateJobOnResult.Success, created.Result);
            outroJobOnId = created.JobOn!.JobOnId;
        }

        using var client = await LoginAsync("4505");
        var pegamentosId = await CreateToolScopedAsync(client, "5447", "T3");
        var folhaUrl = $"{PagePath}?pegamentos_id={pegamentosId}&associar=1&referencia=5810";
        var primeiro = await PostAsync(client, folhaUrl, "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.Contains("associado=1", primeiro.Headers.Location!.OriginalString);
        var controloId = (await RecordAsync(pegamentosId)).ControloId;
        Assert.NotNull(controloId);

        var segundo = await PostAsync(client, folhaUrl, "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", outroJobOnId.ToString()));
        Assert.EndsWith("recusa=PegamentosAlreadyAssociated", segundo.Headers.Location!.OriginalString);
        Assert.Equal(controloId, (await RecordAsync(pegamentosId)).ControloId);

        var html = await HtmlAsync(client, segundo.Headers.Location!.OriginalString);
        Assert.Contains("Contexto da produção", html);
        Assert.Contains("<div data-dmo-toast=\"erro\" class=\"peg-aviso\" role=\"alert\">Estes Pegamentos já pertencem a outra produção. Nada foi alterado.</div>", html);
        // Uma só vez.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Estes Pegamentos já pertencem a outra produção"));
    }

    [Fact]
    public async Task Associar_ProducaoQueJaTemPegamentos_Recusa()
    {
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4503");

        // A produção já mediu os seus Pegamentos pelo controlo_id.
        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ensured = await scope.ServiceProvider.GetRequiredService<IEnsureControloForJobOnHandler>()
                .HandleAsync(new EnsureControloForJobOn(jobOnId));
            controloId = ensured.ControloId!.Value;
            var outcome = await scope.ServiceProvider.GetRequiredService<IRecordPegamentosMeasurementHandler>()
                .HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.1m, GargaloMm: 70.00m));
            Assert.Equal(RecordPegamentosMeasurementResult.Success, outcome.Result);
        }

        var pegamentosId = await CreateToolScopedAsync(client, "5447", "T3");
        var folhaUrl = $"{PagePath}?pegamentos_id={pegamentosId}&associar=1&referencia=5810";
        var response = await PostAsync(client, folhaUrl, "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.EndsWith("recusa=ControloAlreadyHasPegamentos", response.Headers.Location!.OriginalString);
        Assert.Null((await RecordAsync(pegamentosId)).ControloId);
    }

    [Fact]
    public async Task Novos_SemFerramentas_Recusa_EAssociarAbrePeloControloId()
    {
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4504");

        var semFerramentas = await PostAsync(client, $"{PagePath}?cm_ref=5447&bq_ref=T194&mf_ref=5810", "CreateToolScoped",
            ("cm_tool_id", ""), ("bq_tool_id", ""), ("mf_tool_id", ""));
        Assert.EndsWith("recusa=sem-ferramentas", semFerramentas.Headers.Location!.OriginalString);

        // Só com a CM e a BQ também recusa: falta o MF.
        var semMf = await PostAsync(client, $"{PagePath}?cm_ref=5447&bq_ref=T194&mf_ref=5810", "CreateToolScoped",
            ("cm_tool_id", "T3"), ("bq_tool_id", "BQ-1"), ("mf_tool_id", ""));
        Assert.EndsWith("recusa=sem-ferramentas", semMf.Headers.Location!.OriginalString);
        var recusa = await HtmlAsync(client, semMf.Headers.Location!.OriginalString);
        Assert.Contains("Escolha a CM, a BQ e o MF", recusa);

        var desconhecida = await HtmlAsync(client, $"{PagePath}?cm_ref=NAOEXISTE");
        Assert.Contains("não está registada no Ferramentas", desconhecida);
        Assert.DoesNotContain("Criar Pegamentos", desconhecida);

        var escolhaUrl = $"{PagePath}?modo=jobon&referencia=5810";
        var escolha = await HtmlAsync(client, escolhaUrl);
        Assert.Contains("id=\"peg-jobon-title\">Job On<", escolha);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", escolha);

        var aberto = await PostAsync(client, escolhaUrl, "AbrirJobOn", ("jobon_id", jobOnId.ToString()));
        Assert.StartsWith($"{PagePath}?controlo_id=", aberto.Headers.Location!.OriginalString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pegamentos.CountAsync());
    }

    [Fact]
    public async Task EscolherJobOn_ReferenciaEmMinusculas_EncontraAsProducoes()
    {
        // Deep sweep 2 (18/10/2026, F-7): «3540y079» em minúsculas dava «Não há
        // Job Ons da referência». O servidor passa a referência a maiúsculas e
        // o campo escreve-a logo em maiúsculas, como o do Peso.
        var jobOnId = await SeedProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOns.Add(new JobOn { Reference = "5810Y079", ProductionNumber = "J11", MachineId = (await db.Machines.SingleAsync(m => m.Code == "B1")).Id });
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync("4505");

        var inteira = await HtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810");
        Assert.Contains("data-dmo-maiusculas autocapitalize=\"characters\" name=\"referencia\"", inteira);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", inteira);
        // A referência inteira: a 5810Y079 começa por 5810 mas não é a 5810.
        Assert.DoesNotContain("J11", inteira);

        var y079 = await HtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810y079");
        Assert.Contains(">J11 · B1</option>", y079);
        Assert.DoesNotContain("Não há Job Ons da referência", y079);
    }

    /// <summary>O formulário com este id (a abertura até ao fecho).</summary>
    private static string Formulario(string html, string id)
    {
        var form = System.Text.RegularExpressions.Regex.Match(html, $"<form [^>]*id=\"{id}\"[^>]*>[\\s\\S]*?</form>");
        Assert.True(form.Success, $"a página não tem o formulário #{id}");
        return form.Value;
    }

    [Fact]
    public async Task EscolherJobOn_TrocaSoAsZonas_NumaLinha()
    {
        // Deep sweep 2 (18/10/2026, F-8 e F-9): cada referência ou máquina
        // escolhida recarregava a página inteira, e a produção ia para uma
        // segunda linha, à largura toda. Agora a procura fica (data-dmo-fica)
        // e vai pelo DmoTroca (data-dmo-parcial); a produção é uma zona na
        // mesma linha, com o dropdown do formulário POST (form=), que abre a
        // folha como sempre (data-dmo-nativo). Fora das zonas, a página é
        // igual em todas as escolhas: senão o componente abria a página inteira.
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4791");

        var vazio = await HtmlAsync(client, $"{PagePath}?modo=jobon");
        var procura = Formulario(vazio, "pegJobOnProcura");
        Assert.Contains("class=\"dmo-filter-bar dmo-filter-bar-conteudo\"", procura);
        Assert.Contains("<div id=\"pegJobOnProducaoZona\" class=\"dmo-field dmo-field-producao\" data-dmo-regiao hidden=\"hidden\">", procura);
        Assert.Contains("class=\"dmo-field dmo-field-maquina\"", procura);
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>\\s*<form [^>]*id=\"pegJobOnProcura\"", vazio);
        Assert.Contains("<div id=\"pegEscolhaAvisos\" class=\"dmo-zona\" data-dmo-regiao>", vazio);
        Assert.Contains("<div id=\"pegJobOnVazia\" data-dmo-regiao>", vazio);
        var abrir = Formulario(vazio, "pegJobOnEscolha");
        Assert.Contains("handler=AbrirJobOn", abrir);
        Assert.Contains("data-dmo-nativo", abrir);

        var escolha = await HtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810");
        var procuraComProducoes = Formulario(escolha, "pegJobOnProcura");
        // A produção na mesma linha (dentro do formulário da procura), do
        // formulário POST; desativada enquanto a referência escrita for outra.
        Assert.Contains("<div id=\"pegJobOnProducaoZona\" class=\"dmo-field dmo-field-producao\" data-dmo-regiao>", procuraComProducoes);
        Assert.Contains("<select id=\"pegJobOn\" name=\"jobon_id\" form=\"pegJobOnEscolha\" required data-dmo-autosubmit=\"valor\" data-dmo-depende=\"pegJobOnReferencia\" data-dmo-depende-valor=\"5810\">", procuraComProducoes);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", procuraComProducoes);

        var fora = Controlo.PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[] { "referencia=5810", "referencia=5810&maquina=B1", "referencia=5810&maquina=B9", "referencia=9999", "recusa=sem-escolha", "referencia=5810&recusa=inexistente" })
        {
            var html = await HtmlAsync(client, $"{PagePath}?modo=jobon&{filtros}");
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
        }
        Assert.Contains("Escolha um Job On. Nada foi associado.", Controlo.PesoZonas.Zona(await HtmlAsync(client, $"{PagePath}?modo=jobon&recusa=sem-escolha"), "pegEscolhaAvisos"));
        Assert.Contains("Não há Job Ons da referência 9999.", Controlo.PesoZonas.Zona(await HtmlAsync(client, $"{PagePath}?modo=jobon&referencia=9999"), "pegJobOnVazia"));

        // O dropdown envia o formulário POST: abre a folha da produção.
        var aberto = await PostAsync(client, $"{PagePath}?modo=jobon&referencia=5810", "AbrirJobOn", ("jobon_id", jobOnId.ToString()));
        Assert.StartsWith($"{PagePath}?controlo_id=", aberto.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AssociarJobOn_TrocaSoAsZonasDaEscolha_ComOBotaoNaMesmaLinha()
    {
        // Deep sweep 2 (18/10/2026, F-8 e F-9): no «Associar Job On» dos
        // Pegamentos sem Job On (dentro de #pegAssociar, que fica), a
        // produção e o «Associar» são zonas da procura; o botão e o dropdown
        // enviam o formulário POST do Associate (form=).
        var jobOnId = await SeedProductionAsync();
        using var client = await LoginAsync("4792");
        var pegamentosId = await CreateToolScopedAsync(client, "5447", "T3");
        var folhaUrl = $"{PagePath}?pegamentos_id={pegamentosId}&associar=1";

        var vazio = await HtmlAsync(client, folhaUrl);
        Assert.Contains("<div id=\"pegJobOnAssociarZona\" class=\"dmo-field\" data-dmo-regiao hidden=\"hidden\">", Formulario(vazio, "pegJobOnProcura"));

        var escolha = await HtmlAsync(client, $"{folhaUrl}&referencia=5810&maquina=B1");
        var procura = Formulario(escolha, "pegJobOnProcura");
        Assert.Contains($"<input type=\"hidden\" name=\"pegamentos_id\" value=\"{pegamentosId}\" />", procura);
        Assert.Contains("<select id=\"pegJobOn\" name=\"jobon_id\" form=\"pegJobOnEscolha\" required data-dmo-depende=\"pegJobOnReferencia\" data-dmo-depende-valor=\"5810\">", procura);
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" form=\"pegJobOnEscolha\" data-dmo-depende=\"pegJobOnReferencia\" data-dmo-depende-valor=\"5810\">Associar</button>", procura);
        var associar = Formulario(escolha, "pegJobOnEscolha");
        Assert.Contains("handler=Associate", associar);
        Assert.Contains("data-dmo-nativo", associar);
        Assert.Contains($"<input type=\"hidden\" name=\"pegamentos_id\" value=\"{pegamentosId}\" />", associar);

        // As zonas existem em todas as escolhas (o DmoTroca troca-as dentro do
        // «Associar Job On», que fica), e fora delas a página é a mesma.
        var fora = Controlo.PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[] { "&referencia=5810", "&referencia=5810&maquina=B1", "&referencia=9999" })
        {
            var html = await HtmlAsync(client, folhaUrl + filtros);
            Assert.Equal(fora, Controlo.PesoZonas.ForaDasZonas(html));
            foreach (var id in new[] { "pegJobOnProducaoZona", "pegJobOnAssociarZona", "pegJobOnVazia" })
            {
                Assert.Contains($"<div id=\"{id}\"", html);
            }
        }

        var associado = await PostAsync(client, $"{folhaUrl}&referencia=5810", "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.Contains("associado=1", associado.Headers.Location!.OriginalString);
    }

    /// <summary>Mais lotes da BQ T194 do J10 (BQ-1, lote 12) e uma BQ T173 com dois lotes.</summary>
    private async Task SeedLotesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
        db.Tools.Add(new Tool { ToolId = "BQ-SL", Type = ToolType.Bq, Reference = "T194", Lot = ToolRegistryConstants.NoLot });
        db.Tools.Add(new Tool { ToolId = "BQ-T173-1", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-T173-2", Type = ToolType.Bq, Reference = "T173", Lot = "2" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SemJobOn_ReferenciaComVariosLotes_EscolheOLoteEAssocia()
    {
        // Sweep de 19/10/2026, C-4: a T194 com vários lotes escolhia sozinha
        // o mais alto (e «s/lote» ganhava aos numerados); as leituras ficavam
        // nesse lote e associar ao Job On, que tem outro, era recusado. Agora
        // a página mostra os lotes e o operador escolhe o que está na máquina.
        var jobOnId = await SeedProductionAsync();
        await SeedLotesAsync();
        using var client = await LoginAsync("4506");

        var escolha = await HtmlAsync(client, $"{PagePath}?cm_ref=5447&bq_ref=T194&mf_ref=5810");
        Assert.Contains("<label for=\"pegBqRefLote\">Boquilha (BQ) — lote</label>", escolha);
        Assert.Contains("<select id=\"pegBqRefLote\" name=\"bq_tool\" data-dmo-autosubmit=\"valor\" data-dmo-depende=\"pegBqRef\" data-dmo-depende-valor=\"T194\">", escolha);
        // Do mais alto ao «s/lote», nenhum escolhido: nada para criar.
        Assert.Matches("<option value=\"\">Escolha o lote</option>\\s*<option value=\"BQ-2\">13</option>\\s*<option value=\"BQ-1\">12</option>\\s*<option value=\"BQ-SL\">s/lote</option>\\s*</select>", escolha);
        Assert.Contains("<span>BQ</span><strong>T194 · escolha o lote</strong>", escolha);
        Assert.DoesNotContain("Criar Pegamentos", escolha);
        Assert.DoesNotContain("name=\"bq_tool_id\"", escolha);
        // A CM e o MF têm um só lote: seguem direto, sem escolha.
        Assert.DoesNotContain("pegCmRefLote", escolha);
        Assert.DoesNotContain("pegMfRefLote", escolha);
        Assert.Contains("<span>CM</span><strong>5447 · Lote 3</strong>", escolha);
        Assert.Contains("<span>MF</span><strong>5810 · Lote 1</strong>", escolha);

        // Escolher o lote 12, o da máquina (o do Job On).
        var urlLote = $"{PagePath}?cm_ref=5447&bq_ref=T194&bq_tool=BQ-1&mf_ref=5810";
        var escolhido = await HtmlAsync(client, urlLote);
        Assert.Contains("<option value=\"BQ-1\" selected=\"selected\">12</option>", escolhido);
        Assert.Contains("<span>BQ</span><strong>T194 · Lote 12</strong>", escolhido);
        Assert.Contains("<input type=\"hidden\" name=\"bq_tool_id\" value=\"BQ-1\" />", escolhido);
        Assert.Contains("Criar Pegamentos", escolhido);

        var criado = await PostAsync(client, urlLote, "CreateToolScoped",
            ("cm_tool_id", "T3"), ("bq_tool_id", "BQ-1"), ("mf_tool_id", "MF-TOOL-9"),
            ("cm_ref", "5447"), ("bq_ref", "T194"), ("mf_ref", "5810"));
        var location = criado.Headers.Location!.OriginalString;
        Assert.StartsWith($"{PagePath}?pegamentos_id=", location);
        var pegamentosId = int.Parse(location[(location.IndexOf('=') + 1)..]);
        Assert.Equal("BQ-1", (await RecordAsync(pegamentosId)).BqToolId);

        // Associa ao Job On sem refazer nada.
        var associado = await PostAsync(client, $"{PagePath}?pegamentos_id={pegamentosId}&associar=1&referencia=5810", "Associate",
            ("pegamentos_id", pegamentosId.ToString()), ("jobon_id", jobOnId.ToString()));
        Assert.Contains("associado=1", associado.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task SemJobOn_LoteQueNaoEDaReferencia_NaoConta()
    {
        // O lote que ficou de outra referência (escrever outra e escolher a
        // sugestão) não é da nova: com vários lotes a escolha volta, com um
        // só segue direto.
        await SeedProductionAsync();
        await SeedLotesAsync();
        using var client = await LoginAsync("4507");

        var outra = await HtmlAsync(client, $"{PagePath}?cm_ref=5447&bq_ref=T173&bq_tool=BQ-1&mf_ref=5810");
        Assert.Contains("<span>BQ</span><strong>T173 · escolha o lote</strong>", outra);
        Assert.Matches("<option value=\"\">Escolha o lote</option>\\s*<option value=\"BQ-T173-2\">2</option>\\s*<option value=\"BQ-T173-1\">1</option>", outra);
        Assert.DoesNotContain("Criar Pegamentos", outra);

        var umLote = await HtmlAsync(client, $"{PagePath}?cm_ref=5447&cm_tool=T9&bq_ref=T173&bq_tool=BQ-T173-1&mf_ref=5810");
        Assert.Contains("<span>CM</span><strong>5447 · Lote 3</strong>", umLote);
        Assert.Contains("<input type=\"hidden\" name=\"cm_tool_id\" value=\"T3\" />", umLote);
        Assert.Contains("<option value=\"BQ-T173-1\" selected=\"selected\">1</option>", umLote);
        Assert.Contains("<input type=\"hidden\" name=\"bq_tool_id\" value=\"BQ-T173-1\" />", umLote);
    }

    [Fact]
    public async Task SemJobOn_CriarRecusado_VoltaComOLoteEscolhido()
    {
        // A recusa volta à escolha com as referências e os lotes escolhidos.
        await SeedProductionAsync();
        await SeedLotesAsync();
        using var client = await LoginAsync("4508");

        var urlLote = $"{PagePath}?cm_ref=5447&bq_ref=T194&bq_tool=BQ-2&mf_ref=5810";
        var recusado = await PostAsync(client, urlLote, "CreateToolScoped",
            ("cm_tool_id", "T3"), ("bq_tool_id", "BQ-2"), ("mf_tool_id", "MF-APAGADO"),
            ("cm_ref", "5447"), ("bq_ref", "T194"), ("mf_ref", "5810"));
        var location = recusado.Headers.Location!.OriginalString;
        Assert.Equal($"{PagePath}?cm_ref=5447&bq_ref=T194&mf_ref=5810&cm_tool=T3&bq_tool=BQ-2&mf_tool=MF-APAGADO&recusa=MfToolNotFound", location);

        var html = await HtmlAsync(client, location);
        Assert.Contains("O molde final (MF) indicado não está registado no Ferramentas. Nada foi criado.", html);
        Assert.Contains("<option value=\"BQ-2\" selected=\"selected\">13</option>", html);
        Assert.Contains("<input type=\"hidden\" name=\"bq_tool_id\" value=\"BQ-2\" />", html);

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DmoDbContext>().Pegamentos.CountAsync());
    }
}
