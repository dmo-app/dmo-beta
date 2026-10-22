using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Prova o slice vertical nativo da página Peso (Controlo Create) no corte
/// tool-scoped: o surface Razor resolve as ferramentas por tool_id canónico,
/// cria o peso_id sem produção/Job On, liga os POSTs aos handlers canónicos
/// (criação com completion dos valores técnicos em falta e registo de
/// leituras com o cálculo produzido pelo backend) e apresenta o Peso
/// consultável a partir da sua ferramenta no histórico. A mutação nativa
/// exige sessão autenticada; a memória do browser não é fonte de verdade.
/// </summary>
public partial class PesoToolScopedPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoToolScopedPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Tools.RemoveRange(db.Tools);
        // A base em memória é partilhada pelas classes de teste: as CM que
        // outra classe deixou fariam falhar a contagem de contextos CM.
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
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
        // sem Template = sem operacional»): o template operacional com as
        // capabilities Beta (jobon.create + controlo.create) — os POSTs do
        // Controlo exigem a capability (o modo chefe: o approve não cria).
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-operacional");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-teste-operacional",
                Name = "Operacional de teste",
                Capabilities = new[]
                {
                    DmoCapabilities.JobOnCreate,
                    DmoCapabilities.ControloCreate
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

    private async Task SeedWorkedExampleAsync()
    {
        // As ferramentas reais da pesagem e a configuração do processo: a CM
        // T3 (5447, lote 3) tem o processo registado; os restantes valores
        // técnicos são completados no fluxo da página.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb
        });
        db.GlassDensities.Add(new GlassDensity
        {
            Process = CmProcess.Nnpb,
            DensityGramsPerCm3 = 2.50m
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string tokenPath,
        string cmToolId,
        string bqToolId,
        string waterTemperature,
        string? volumePuncao = null,
        string? nominalWeight = null,
        string? volumeMarisa = null)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        var fields = new List<(string, string)>
        {
            ("cm_tool_id", cmToolId),
            ("bq_tool_id", bqToolId),
            ("water_temperature", waterTemperature)
        };

        if (volumePuncao is not null)
        {
            fields.Add(("cm_volume_puncao", volumePuncao));
        }

        if (nominalWeight is not null)
        {
            fields.Add(("cm_nominal_weight", nominalWeight));
        }

        if (volumeMarisa is not null)
        {
            fields.Add(("bq_volume_marisa", volumeMarisa));
        }

        fields.Add(("__RequestVerificationToken", token));

        return await client.PostAsync($"{PagePath}?handler=Create", HtmlFormHelpers.BuildFormPost(fields.ToArray()));
    }

    private async Task<HttpResponseMessage> PostMeasurementAsync(
        HttpClient client,
        string tokenPath,
        int pesoId,
        string cmSubject,
        string waterWeight)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        return await client.PostAsync(
            $"{PagePath}?handler=RegisterMeasurement",
            HtmlFormHelpers.BuildFormPost(
                ("peso_id", pesoId.ToString()),
                ("cm_subject", cmSubject),
                ("water_weight", waterWeight),
                ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task ToolScopedFlow_ResolvesByTheHumanReference_WhatTheSheetsShow()
    {
        // P2-5: o operador escreve o que vê nas folhas — A REFERÊNCIA
        // (5447, T194), nunca o id opaco. A folha resolve pela
        // referência (a Tool mais recente com ela) e mostra os factos
        // humanos (referência + lote); uma referência desconhecida é o
        // aviso limpo do slot.
        await SeedWorkedExampleAsync();
        SeedUser("4221", "Peso Operador", "provider-peso-11", "PesoPass11!");
        using var client = await LoginClientAsync("4221", "PesoPass11!");

        var byReference = await GetPageHtmlAsync(client, $"{PagePath}?cm_tool_id=5447&bq_tool_id=T194");
        Assert.Contains("id=\"peso-tools-title\">Ferramentas<", byReference);
        Assert.Contains("Ex.: ST100", byReference);          // o placeholder pelo que as folhas mostram
        Assert.Contains("Ex.: C142", byReference);
        Assert.Contains("5447", byReference);
        Assert.Contains("<span>Lote</span><strong>3</strong>", byReference);
        Assert.Contains("T194", byReference);
        Assert.Contains("<span>Lote</span><strong>12</strong>", byReference);
        Assert.Contains("Volume punção (cm³)", byReference);   // em falta → input de completion
        Assert.Contains("Volume marisa (cm³)", byReference);  // em falta → input de completion
        Assert.Contains("NNPB", byReference);

        // Os ids opacos NUNCA como factos da folha (P2-4): o hex vive no
        // POST/URL, nunca no card.
        Assert.DoesNotContain(">T3<", byReference);
        Assert.DoesNotContain(">BQ-TOOL-9<", byReference);

        var unknown = await GetPageHtmlAsync(client, $"{PagePath}?cm_tool_id=DESCONHECIDA");
        Assert.Contains("não está registada no Ferramentas", unknown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolScopedFlow_CreateRecordConsult_ThroughThePage(bool decimalComma)
    {
        string Input(string value) => decimalComma ? value.Replace('.', ',') : value;
        await SeedWorkedExampleAsync();
        SeedUser("4200", "Peso Operador", "provider-peso-1", "PesoPass1!");
        using var client = await LoginClientAsync("4200", "PesoPass1!");

        // Escolher ferramentas: a página resolve os tool_ids canónicos e
        // mostra os valores técnicos atuais, com a completion dos em falta.
        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var toolsHtml = await GetPageHtmlAsync(client, toolsUrl);
        Assert.Contains("T3", toolsHtml);
        Assert.Contains("5447", toolsHtml);
        Assert.Contains("Volume punção (cm³)", toolsHtml);   // em falta → input de completion
        Assert.Contains("Volume marisa (cm³)", toolsHtml);    // em falta → input de completion
        Assert.Contains("NNPB", toolsHtml);

        // Criar o Peso (P30 do exemplo): temperatura medida 20.4 e os valores
        // técnicos em falta completados no fluxo (gravados nas Tools).
        var created = await PostCreateAsync(
            client,
            toolsUrl,
            "T3",
            "BQ-TOOL-9",
            Input("20.4"),
            volumePuncao: Input("30.00"),
            nominalWeight: Input("200.00"),
            volumeMarisa: Input("10.00"));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var createdLocation = created.Headers.Location!.ToString();
        Assert.Contains("peso_id=", createdLocation, StringComparison.Ordinal);

        var pesoId = int.Parse(
            System.Text.RegularExpressions.Regex.Match(createdLocation, @"peso_id=(\d+)").Groups[1].Value);

        // O Peso aberto: o título e o cartão mostram a REFERÊNCIA da CM
        // (5447 · Lote 3 — o fato humano), nunca o tool_id (o Owner
        // 2026-10-12: «o CM MF e boquilha estao a mostrar os uuid da BD em
        // vez da referencia da tool_id»); a folha mostra APENAS os
        // resultados (as densidades/volumes/nominal vivem na ferramenta e
        // no PDF — o Owner: «deveria apenas ter informaçao relacionada aos
        // resultados do peso»).
        var pesoUrl = $"{PagePath}?peso_id={pesoId}";
        var pesoHtml = await GetPageHtmlAsync(client, pesoUrl);
        Assert.Contains("Peso — CM 5447", pesoHtml);
        // Prototype presentation on the native real-data Peso page.
        Assert.Contains("css/dmo-peso-prototype.css", pesoHtml);
        Assert.Contains("peso-card-heading", pesoHtml);
        Assert.Contains("peso-section-heading", pesoHtml);
        Assert.Contains("Leituras por CM", pesoHtml);
        Assert.DoesNotContain("Peso — CM T3", pesoHtml);
        Assert.Contains("Lote 3", pesoHtml);
        Assert.Contains("sem associação a produção", pesoHtml);
        Assert.Contains("Ainda sem leituras", pesoHtml);

        var previewToken = await GetVerificationTokenAsync(client, pesoUrl);
        using var preview = await client.PostAsync($"{PagePath}?handler=Preview", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("lines", System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { cm_subject = "12", water_weight = Input("199.64") },
                new { cm_subject = "13", water_weight = "199,64.0" },
                new { cm_subject = "14", water_weight = "199,6,4" }
            })), ("__RequestVerificationToken", previewToken)));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = System.Text.Json.JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        var previewLine = previewJson.RootElement.GetProperty("lines")[0];
        Assert.Equal(199.64m, previewLine.GetProperty("waterWeightGrams").GetDecimal());
        Assert.Equal(450.516461586289m,
            decimal.Round(previewLine.GetProperty("glassWeightGrams").GetDecimal(), 12));
        Assert.Equal(0, previewJson.RootElement.GetProperty("persisted").GetProperty("sampleCount").GetInt32());
        Assert.Equal(1, previewJson.RootElement.GetProperty("totals").GetProperty("sampleCount").GetInt32());
        Assert.Equal("invalid_water_weight", previewJson.RootElement.GetProperty("lines")[1].GetProperty("status").GetString());
        Assert.Equal("invalid_water_weight", previewJson.RootElement.GetProperty("lines")[2].GetProperty("status").GetString());

        // Um JSON válido com itens que não são linhas (só num pedido forjado:
        // a página envia sempre objetos) não dá erro 500: esses itens ficam de
        // fora e a linha verdadeira conta (sweep do Codex, 19/10/2026, ponto 2).
        using var forjado = await client.PostAsync($"{PagePath}?handler=Preview", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("lines", "[1, \"x\", null, [], {\"cm_subject\": \"12\", \"water_weight\": \"" + Input("199.64") + "\"}]"),
            ("__RequestVerificationToken", previewToken)));
        Assert.Equal(HttpStatusCode.OK, forjado.StatusCode);
        using var forjadoJson = System.Text.Json.JsonDocument.Parse(await forjado.Content.ReadAsStringAsync());
        Assert.Equal(1, forjadoJson.RootElement.GetProperty("lines").GetArrayLength());
        Assert.Equal(1, forjadoJson.RootElement.GetProperty("totals").GetProperty("sampleCount").GetInt32());

        // Registar as leituras reais: o peso de cada CM (a fórmula total),
        // com os valores derivados calculados pelo backend.
        var firstReading = await PostMeasurementAsync(client, pesoUrl, pesoId, "12", Input("199.64"));
        Assert.Equal(HttpStatusCode.Redirect, firstReading.StatusCode);
        var secondReading = await PostMeasurementAsync(client, pesoUrl, pesoId, "21", Input("99.82"));
        Assert.Equal(HttpStatusCode.Redirect, secondReading.StatusCode);

        pesoHtml = await GetPageHtmlAsync(client, pesoUrl);
        // A folha com leituras vai para o Chromium (tests/visual/ecra_pequeno_browser.py).
        ExportarParaOBrowser.Gravar("peso-folha.html", pesoHtml);

        // Cálculo por leitura com a tabela real da fábrica (0,99717 @ 20°C):
        // 199,64 → capacidade 200,2066 → (200,2066 + 10 − 30) × 2,5 = 450,52 g
        // 99,82 → 100,1033 → 200,26 g — as colunas da tabela são O PESO
        // (o Owner 2026-10-12: «na tabela massa actual deveria ser o peso
        // actual, peso é a formula total para cada CM»); a massa de água é
        // o input da medição, não o resultado.
        Assert.Contains(">450.52<", pesoHtml);
        Assert.Contains(">200.26<", pesoHtml);
        Assert.DoesNotContain(">199.64<", pesoHtml);

        // Médias (as faixas de resultados, 2 casas): 149,73 em água (o
        // vivo), 325,39 de peso do vidro — o peso médio é O PESO que a
        // fabricação usa. A DIFERENÇA NOMINAL (+125,39 = 325,39 − 200)
        // aparece no resumo estável (a auditoria do Codex: o servidor
        // renderiza os MESMOS blocos que o peso-live.js escreve ao vivo —
        // 4 factos + 6 conclusões; o sinal «+» como a entidade &#x2B;).
        Assert.Contains("149.73", pesoHtml);
        Assert.Contains("325.39", pesoHtml);
        Assert.Contains("Diferença nominal", pesoHtml);
        Assert.Contains("&#x2B;125.39 g", pesoHtml);

        // P30 permanece diretamente consultável pela sua folha (?peso_id):
        // o resumo com os números — os históricos vivem SÓ na tab (o Owner
        // 2026-10-13: «na pagina de peso deve apenas apresentar a referencia
        // atual»), nunca duplicados na folha.
        var pesoIdValue = createdLocation.Substring(
            createdLocation.IndexOf("peso_id=", StringComparison.Ordinal) + "peso_id=".Length).Split('&')[0];
        var directHtml = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoIdValue}");
        Assert.Contains("325", directHtml);

        // Nenhuma identidade de produção foi criada pela pesagem: o Peso
        // tool-scoped existe sem Job On, controlo ou contexto CM.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.JobOns.AsNoTracking().CountAsync());
            Assert.Equal(0, await db.CmContexts.AsNoTracking().CountAsync());
            Assert.Equal(1, await db.Pesos.AsNoTracking().CountAsync());
            Assert.Equal(2, await db.PesoMeasurements.AsNoTracking().CountAsync());
            Assert.Equal(new[] { 199.64m, 99.82m }, await db.PesoMeasurements.AsNoTracking()
                .OrderBy(x => x.RowPosition).Select(x => x.WaterWeightGrams).ToArrayAsync());

            // A completion gravou nos tool_ids owners (regra Ferramentas).
            var cmTechnical = await db.ToolTechnicalContexts.AsNoTracking()
                .SingleAsync(t => t.ToolId == "T3");
            Assert.Equal(30m, cmTechnical.VolumePuncaoCm3);
            Assert.Equal(200m, cmTechnical.NominalWeightGrams);
            var bqTechnical = await db.ToolTechnicalContexts.AsNoTracking()
                .SingleAsync(t => t.ToolId == "BQ-TOOL-9");
            Assert.Equal(10m, bqTechnical.VolumeMarisaCm3);
        }
    }

    [Fact]
    public async Task SavedPeso_KeepsRecordedIdentificationAfterCatalogEdit_AndDownloadsPdf()
    {
        await SeedWorkedExampleAsync();
        SeedUser("4290", "Peso Histórico", "provider-peso-frozen", "PesoFrozen1!");
        using var client = await LoginClientAsync("4290", "PesoFrozen1!");
        using var created = await PostCreateAsync(client,
            PagePath + "?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9", "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", nominalWeight: "200.00", volumeMarisa: "10.00");
        var url = created.Headers.Location!.ToString();
        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            pesoId = db.Pesos.Single().Id;
            var cm = db.Tools.Single(t => t.ToolId == "T3");
            cm.Reference = "CM-EDITADA"; cm.Lot = "33";
            var bq = db.Tools.Single(t => t.ToolId == "BQ-TOOL-9");
            bq.Reference = "BQ-EDITADA"; bq.Lot = "120";
            await db.SaveChangesAsync();
        }
        using var reading = await PostMeasurementAsync(client, url, pesoId, "61", "100.00");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, url));
        Assert.Contains("Peso — CM 5447", html);
        Assert.Contains("CM 5447 · Lote 3", html);
        Assert.DoesNotContain("CM-EDITADA", html);
        var token = await GetVerificationTokenAsync(client, url);
        using var pdf = await client.PostAsync(PagePath + "?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        if (Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, "peso-identificacao-historica.pdf"),
                await pdf.Content.ReadAsByteArrayAsync());
        }
        using var check = _factory.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("CM-EDITADA", saved.Tools.Single(t => t.ToolId == "T3").Reference);
        Assert.Single(saved.Pesos);
        Assert.Equal(100m, saved.PesoMeasurements.Single().WaterWeightGrams);
    }

    [Fact]
    public async Task UnauthenticatedRequests_ReceiveNoPageAndNoMutation()
    {
        await SeedWorkedExampleAsync();

        // Sem sessão autenticada a página nativa não é servida: o
        // [Authorize] desafia para o login antes de qualquer leitura.
        var anonymous = CreateClient();
        var response = await anonymous.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        // E o POST de criação também não: desafiado (ou recusado pelo
        // antiforgery da sessão anónima) antes de escrever qualquer coisa —
        // o par token/cookie vem da página de login, a única superfície
        // anónima que o emite.
        var token = await GetVerificationTokenAsync(anonymous, "/login");
        var refused = await anonymous.PostAsync(
            $"{PagePath}?handler=Create",
            HtmlFormHelpers.BuildFormPost(
                ("cm_tool_id", "T3"),
                ("bq_tool_id", "BQ-TOOL-9"),
                ("water_temperature", "20.0"),
                ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateRefusal_IsTypedAndRedirectsWithToolContext()
    {
        // A recusa tipada volta por query (PRG) com o contexto das
        // ferramentas escolhidas: o volume punção em falta é explicado sem
        // nenhum Peso criado.
        await SeedWorkedExampleAsync();
        SeedUser("4201", "Peso Operador", "provider-peso-2", "PesoPass2!");
        using var client = await LoginClientAsync("4201", "PesoPass2!");

        // Remove o registo técnico da CM: a completion do fluxo agora
        // fornecerá volume_puncao, mas falta o processo → recusa tipada.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
            db.SaveChanges();
        }

        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var refused = await PostCreateAsync(
            client,
            toolsUrl,
            "T3",
            "BQ-TOOL-9",
            "20.0",
            volumePuncao: "30.00",
            volumeMarisa: "10.00");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains("create_refusal=MissingCmProcess", location, StringComparison.Ordinal);
        Assert.Contains("cm_tool_id=T3", location, StringComparison.Ordinal);

        // A mensagem tipada é apresentada no contexto das ferramentas.
        var refusalHtml = await GetPageHtmlAsync(client, location);
        Assert.Contains("processo de fabrico da CM não está registado", refusalHtml);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Pesos.AsNoTracking().CountAsync());
        }
    }

    [Theory]
    [InlineData("vinte", "30.00", "10.00", "UnreadableWaterTemperature", "A temperatura da água está vazia ou ilegível", "20,4 ou 20.4")]
    [InlineData("20", "abc", "10.00", "UnreadableCmVolumePuncao", "O volume punção da CM escrito é ilegível", "70,10 ou 70.10")]
    [InlineData("20", "30.00", "1,2,3", "UnreadableBqVolumeMarisa", "O volume marisa da BQ escrito é ilegível", "10,50 ou 10.50")]
    public async Task CreateRefusal_ValorEscritoMasIlegivel_DizIlegivel(
        string temperature, string puncao, string marisa, string refusal, string message, string examples)
    {
        // Bug (beta.65): um valor escrito mas ilegível dizia «fora do
        // intervalo» ou «não está registado». Continua a ser recusa (o
        // valor entra no cálculo), mas a mensagem diz «ilegível» e dá os
        // dois exemplos, com vírgula e com ponto.
        await SeedWorkedExampleAsync();
        SeedUser("4202", "Peso Operador", "provider-peso-ilegivel", "PesoPass3!");
        using var client = await LoginClientAsync("4202", "PesoPass3!");

        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var refused = await PostCreateAsync(client, toolsUrl, "T3", "BQ-TOOL-9", temperature,
            volumePuncao: puncao, volumeMarisa: marisa);

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = refused.Headers.Location!.ToString();
        Assert.Contains($"create_refusal={refusal}", location, StringComparison.Ordinal);
        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains(message, html);
        Assert.Contains(examples, html);
        Assert.DoesNotContain("fora do intervalo suportado", html);
        Assert.DoesNotContain("não está registado nem foi indicado", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task CreateRefusal_TemperaturaForaDoIntervalo_ContinuaRecusa()
    {
        // B (fica): a tabela da água só vai de 5 a 35 °C.
        await SeedWorkedExampleAsync();
        SeedUser("4203", "Peso Operador", "provider-peso-temp", "PesoPass4!");
        using var client = await LoginClientAsync("4203", "PesoPass4!");

        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var refused = await PostCreateAsync(client, toolsUrl, "T3", "BQ-TOOL-9", "40",
            volumePuncao: "30.00", volumeMarisa: "10.00");

        var location = refused.Headers.Location!.ToString();
        Assert.Contains("create_refusal=InvalidWaterTemperature", location, StringComparison.Ordinal);
        var html = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
        Assert.Contains("Temperatura da água fora do intervalo suportado (5–35 °C)", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pesos.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task VolumeMarisaZero_ERecusado_OOperadorLeARecusaNoNovoPeso_NadaFicaGravado()
    {
        // Um volume marisa 0 escrito é legível: a página leva-o até à regra
        // dos volumes positivos e a recusa aparece no cartão «Novo Peso»,
        // sem passar por «em falta» e sem completar a ficha das ferramentas.
        await SeedWorkedExampleAsync();
        SeedUser("4205", "Peso Operador", "provider-peso-marisa-zero", "PesoPass6!");
        using var client = await LoginClientAsync("4205", "PesoPass6!");

        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var refused = await PostCreateAsync(client, toolsUrl, "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", volumeMarisa: "0");

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var aviso = AvisoDoNovoPeso(await GetPageHtmlAsync(client, refused.Headers.Location!.ToString()));
        Assert.Contains("Um valor técnico fornecido é fisicamente inválido", aviso);
        Assert.Contains("Nenhum Peso foi criado", aviso);
        Assert.DoesNotContain("não está registado nem foi indicado", aviso);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.Pesos.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.ToolTechnicalContexts.AsNoTracking().CountAsync(t => t.ToolId == "BQ-TOOL-9"));
        Assert.Null((await db.ToolTechnicalContexts.AsNoTracking().SingleAsync(t => t.ToolId == "T3")).VolumePuncaoCm3);
    }

    // O texto do aviso de erro dentro do cartão «Novo Peso»; falha se a
    // página não tiver o cartão ou o aviso.
    private static string AvisoDoNovoPeso(string html)
    {
        var cartao = System.Text.RegularExpressions.Regex.Match(html,
            "<section\\b[^>]*\\baria-labelledby=\"peso-create-title\"[^>]*>(.*?)</section>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(cartao.Success, "a página não tem o cartão «Novo Peso»");
        var aviso = System.Text.RegularExpressions.Regex.Match(cartao.Groups[1].Value,
            "<div\\b[^>]*\\bdata-dmo-toast=\"erro\"[^>]*>(.*?)</div>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(aviso.Success, "o cartão «Novo Peso» não tem aviso de erro");
        return System.Net.WebUtility.HtmlDecode(aviso.Groups[1].Value);
    }

    [Fact]
    public async Task RegisterMeasurement_MassaIlegivelDizIlegivel_MassaZeroContinuaRecusa()
    {
        await SeedWorkedExampleAsync();
        SeedUser("4204", "Peso Operador", "provider-peso-leitura", "PesoPass5!");
        using var client = await LoginClientAsync("4204", "PesoPass5!");

        var toolsUrl = $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9";
        var created = await PostCreateAsync(client, toolsUrl, "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", volumeMarisa: "10.00");
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var pesoUrl = created.Headers.Location!.ToString();
        var pesoId = int.Parse(System.Text.RegularExpressions.Regex.Match(pesoUrl, @"peso_id=(\d+)").Groups[1].Value);

        // Escrita mas ilegível: diz «ilegível» e dá os dois exemplos.
        var ilegivel = await PostMeasurementAsync(client, pesoUrl, pesoId, "12", "abc");
        var ilegivelLocation = ilegivel.Headers.Location!.ToString();
        Assert.Contains("leitura_refusal=ilegivel", ilegivelLocation, StringComparison.Ordinal);
        var ilegivelHtml = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, ilegivelLocation));
        Assert.Contains("A massa de água escrita é ilegível", ilegivelHtml);
        Assert.Contains("199,64 ou 199.64", ilegivelHtml);

        // B (fica): massa ≤ 0 numa leitura continua recusa.
        var zero = await PostMeasurementAsync(client, pesoUrl, pesoId, "12", "0");
        var zeroLocation = zero.Headers.Location!.ToString();
        Assert.Contains("leitura_refusal=massa", zeroLocation, StringComparison.Ordinal);
        var zeroHtml = System.Net.WebUtility.HtmlDecode(await GetPageHtmlAsync(client, zeroLocation));
        Assert.Contains("A massa de água tem de ser maior que 0", zeroHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(0, await db.PesoMeasurements.AsNoTracking().CountAsync());
    }
}
