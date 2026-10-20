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
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// A folha do Peso juntada (o Owner 2026-10-17): a primeira tabela com o
/// volume e o peso de cada CM, as leituras em várias linhas («+» e «−»), a
/// comparação que aplica ao escolher no dropdown e a tabela por produção mais abaixo, a
/// produção anterior com o peso do SAP (guardado, com o delta e no PDF) e o
/// botão «Exportar PDF».
/// </summary>
public class PesoFolhaProducaoAnteriorPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoFolhaProducaoAnteriorPageTests(CustomWebApplicationFactory factory)
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
    /// A referência 5447T173 com três produções na B1: 202603 (sem Peso),
    /// 202604 (Peso com duas leituras) e a atual 202605 (Peso com quatro
    /// leituras a 24 °C: 70.1, 70.5, 70.63 e 70.5 g).
    /// </summary>
    private async Task<(int ControloId, int PesoId, int AnteriorJobOnId, int MaisAntigaJobOnId)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T8", Type = ToolType.Cm, Reference = "5447", Lot = "8" });
        db.Tools.Add(new Tool { ToolId = "BQ4", Type = ToolType.Bq, Reference = "T173", Lot = "4" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T8", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 8.9m });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ4", VolumeMarisaCm3 = 12m });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4027m });
        var antiga = new JobOn { Reference = "5447T173", ProductionNumber = "202603", Machine = machine, InicioEm = new DateOnly(2026, 3, 2), FimEm = new DateOnly(2026, 3, 20) };
        var anterior = new JobOn { Reference = "5447T173", ProductionNumber = "202604", Machine = machine, InicioEm = new DateOnly(2026, 4, 6), FimEm = new DateOnly(2026, 4, 24) };
        var atual = new JobOn { Reference = "5447T173", ProductionNumber = "202605", Machine = machine, InicioEm = new DateOnly(2026, 5, 4) };
        db.JobOns.AddRange(antiga, anterior, atual);
        foreach (var job in new[] { anterior, atual })
        {
            db.CmContexts.Add(new CmContext { JobOn = job, ToolId = "T8" });
            db.BqContexts.Add(new BqContext { JobOn = job, ToolId = "BQ4" });
        }
        await db.SaveChangesAsync();

        var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = atual.Id };
        db.Controlos.AddRange(controloAnterior, controlo);
        await db.SaveChangesAsync();

        int pesoAtualId = 0;
        foreach (var (ctl, rows) in new[]
                 {
                     (controloAnterior, new[] { ("45", 70.0m), ("54", 70.4m) }),
                     (controlo, new[] { ("45", 70.1m), ("54", 70.5m), ("21", 70.63m), ("41", 70.5m) })
                 })
        {
            var peso = new Peso
            {
                CmToolId = "T8", BqToolId = "BQ4", Process = "NNPB", WaterTemperatureC = 24,
                VolumeMarisaCm3 = 12m, VolumePuncaoCm3 = 8.9m, GlassDensityGramsPerCm3 = 2.4027m,
                ControloId = ctl.Id
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            var position = 1;
            foreach (var (cm, water) in rows)
            {
                db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = position++, CmSubject = cm, WaterWeightGrams = water });
            }
            await db.SaveChangesAsync();
            pesoAtualId = peso.Id;
        }

        return (controlo.Id, pesoAtualId, anterior.Id, antiga.Id);
    }

    [Fact]
    public async Task Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4401", "Peso Operador", "provider-folha-1", "Folha401!");
        using var client = await LoginClientAsync("4401", "Folha401!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        // A primeira tabela: N.º CM, volume (3 casas) e peso de cada CM.
        var leituras = html[html.IndexOf("data-peso-leituras", StringComparison.Ordinal)..];
        leituras = leituras[..leituras.IndexOf("</table>", StringComparison.Ordinal)];
        Assert.Contains("<th>Volume (cm³)</th>", leituras);
        Assert.Contains("<td>70.362</td>", leituras);
        Assert.Contains("<strong>176.51</strong>", leituras);
        Assert.Contains("<td>70.894</td>", leituras);
        Assert.Contains("<strong>177.78</strong>", leituras);
        Assert.DoesNotContain("202604", leituras);

        // Sem escolha, a procura mostra as médias de cada produção e a tabela
        // ainda não aparece (o Owner 2026-10-17: «ao escolher aí sim a tabela
        // com detalhes»).
        Assert.Contains("Procurar produção…", html);
        Assert.Contains("202604 · Lote 8 · B1 · Peso 176.75 g (&#x394; &#x2B;0.56) · Volume 70.462 cm³ (&#x394; &#x2B;0.23)", html);
        Assert.DoesNotContain("data-peso-comparacao-tabela", html);
        var escolhida = System.Text.RegularExpressions.Regex.Match(html, "<option value=\"(\\d+)\"[^>]*>\\s*202604 ").Groups[1].Value;
        html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparar={escolhida}");

        // A comparação mais abaixo: escolher no dropdown aplica logo, e cada
        // opção mostra a produção, o lote da CM e as máquinas da ficha.
        Assert.True(Posicao(html, "data-peso-comparacao-tabela") > Posicao(html, "data-peso-leituras"));
        Assert.Contains("name=\"comparar\" data-auto-submit", html);
        Assert.DoesNotContain("Aplicar comparação", html);
        Assert.Contains("202604 · Lote 8 · B1", html);
        // A produção é só a procura; a tabela compara CM com CM pela ordem
        // das leituras (o Owner 2026-10-17), sem «Sem par», e as médias no fim.
        Assert.Contains("<th>CM anterior · 202604</th>", html);
        Assert.Contains("<td>70.261</td>", html);
        Assert.Contains("<td>70.462</td>", html);
        Assert.Contains("data-peso-comparacao-medias", html);
        Assert.DoesNotContain("Sem par", html);
        Assert.DoesNotContain("a mesma máquina", html);

        // As leituras novas: «+», «−» e «Registar leitura»; o PDF é «Exportar PDF».
        Assert.Contains("data-peso-add-row", html);
        Assert.Contains("data-peso-remove-row", html);
        // Sem a barra das leituras recusadas por cima do formulário: empurrava
        // as leituras ao escrever; o cartão da leitura já diz o motivo (B-peso-6).
        Assert.DoesNotContain("data-peso-refusals", html);
        Assert.Contains("Exportar PDF", html);
        Assert.DoesNotContain("Descarregar PDF de Peso separado", html);
        Assert.DoesNotContain("Escreva a massa", html);
    }

    [Fact]
    public async Task Folha_GravaSemRecarregar_ZonasTrocadasELeituraNovaQueFica()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4406", "Peso Operador", "provider-folha-6", "Folha406!");
        using var client = await LoginClientAsync("4406", "Folha406!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");

        // Gravar sem recarregar (Diogo, 17/10/2026): a folha é a raiz da
        // troca; cada parte que muda é uma zona, sempre presente.
        Assert.Contains("<main class=\"peso-page dmo-workspace\" aria-labelledby=\"peso-title\" data-dmo-troca>", html);
        foreach (var zona in new[]
                 {
                     "pesoProducaoAvisos", "pesoLeituras", "pesoLeituraResumo", "pesoLeituraContexto",
                     "pesoLeiturasGravadas", "pesoComparacao", "pesoAnteriorAvisos", "pesoAnteriorContexto",
                     "pesoAnteriorDelta", "pesoPdf"
                 })
        {
            Assert.Contains($"<div id=\"{zona}\" class=\"dmo-zona\" data-dmo-regiao>", html);
        }
        Assert.Contains("<div id=\"pesoSupomos\" class=\"peso-supomos\" data-peso-supomos data-dmo-regiao", html);
        // A produção anterior fica como o operador a deixou (E4-01): o peso
        // escrito e por guardar não se perde quando outra parte da folha troca.
        Assert.Contains("<div id=\"pesoAnterior\" class=\"dmo-zona\" data-dmo-fica>", html);

        // Registar leituras e escolher a comparação, a produção anterior e o
        // «Supomos» trocam só as zonas; o PDF fica fora (envio do browser).
        Assert.Contains("data-preview-url=\"/Controlo/Peso?handler=Preview\" data-dmo-parcial>", html);
        Assert.Contains("<section class=\"dmo-card peso-card peso-compare\" aria-labelledby=\"peso-comparacao-title\" data-dmo-parcial>", html);
        var folha = html[..html.IndexOf("</main>", StringComparison.Ordinal)];
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(folha, "data-dmo-parcial").Count);
        Assert.True(html.IndexOf("id=\"pesoPdf\"", StringComparison.Ordinal) > html.LastIndexOf("</section>", StringComparison.Ordinal));

        // A leitura nova que o operador está a escrever fica como está; os
        // cartões gravados trocam à parte, dentro da mesma grelha.
        Assert.Contains("<div class=\"peso-leitura-nova\" data-dmo-fica>", html);
        Assert.True(html.IndexOf("id=\"pesoLeiturasGravadas\"", StringComparison.Ordinal)
            < html.IndexOf("<div class=\"peso-leitura-nova\" data-dmo-fica>", StringComparison.Ordinal));

        // O «Guardar» da produção anterior tem id: o foco fica nele depois de
        // gravar. (A CM do exemplo não tem geometria: sem «Supomos».)
        Assert.Contains("id=\"producaoAnteriorGuardar\">Guardar</button>", html);

        // A comparação escolhida volta depois de gravar: o campo vai na zona
        // do formulário das leituras.
        var escolhida = System.Text.RegularExpressions.Regex.Match(html, "<option value=\"(\\d+)\"[^>]*>\\s*202604 ").Groups[1].Value;
        var comparada = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&comparar={escolhida}");
        var contexto = comparada[comparada.IndexOf("id=\"pesoLeituraContexto\"", StringComparison.Ordinal)..];
        contexto = contexto[..contexto.IndexOf("</div>", StringComparison.Ordinal)];
        Assert.Contains($"<input type=\"hidden\" name=\"controlo_id\" value=\"{controloId}\" />", contexto);
        Assert.Contains($"<input type=\"hidden\" name=\"comparar\" value=\"{escolhida}\" />", contexto);
    }

    /// <summary>
    /// A troca real do lado do servidor: o que o DmoTroca compara entre a
    /// página aberta e a resposta. Fora das zonas e dos blocos que ficam, as
    /// páginas são iguais, por isso escolher a comparação, o «Supomos» ou
    /// gravar não abrem a página inteira, também com a folha aberta pelo
    /// «Calcular» do apoio TP (cap_h, E4-03) ou com a recusa do SAP no
    /// endereço (E4-01). A comparação escolhida vai nos campos escondidos de
    /// cada formulário, numa zona (o do SAP fica dentro do bloco que fica).
    /// </summary>
    [Fact]
    public async Task Folha_TrocaParcial_ForaDasZonasNadaMuda_EOsFormulariosLevamAComparacao()
    {
        var (controloId, _, anteriorJobOnId, _) = await SeedAsync();
        SeedUser("4407", "Peso Operador", "provider-folha-7", "Folha407!");
        using var client = await LoginClientAsync("4407", "Folha407!");
        var url = $"{PagePath}?controlo_id={controloId}";

        var aberta = await GetPageHtmlAsync(client, url);
        var escolhida = System.Text.RegularExpressions.Regex.Match(aberta, "<option value=\"(\\d+)\"[^>]*>\\s*202604 ").Groups[1].Value;
        var comparada = await GetPageHtmlAsync(client, $"{url}&comparar={escolhida}");
        var calculada = await GetPageHtmlAsync(client, $"{url}&cap_h=4.5");
        var recusada = await GetPageHtmlAsync(client, $"{url}&anterior={anteriorJobOnId}&anterior_recusa=peso&anterior_peso=3o5");
        var suposta = await GetPageHtmlAsync(client, $"{url}&comparar={escolhida}&supor_calote=5,2");

        var fora = PesoZonas.ForaDasZonas(aberta);
        Assert.Equal(fora, PesoZonas.ForaDasZonas(comparada));
        Assert.Equal(fora, PesoZonas.ForaDasZonas(calculada));
        Assert.Equal(fora, PesoZonas.ForaDasZonas(recusada));
        Assert.Equal(fora, PesoZonas.ForaDasZonas(suposta));

        // O apoio TP aberto pelo «Calcular» fica: a resposta seguinte não traz cap_h.
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica>\\s*<details class=\"tp-support\"[^>]*open=\"open\"", calculada);

        // A recusa do SAP volta com o echo no campo, no bloco que fica.
        Assert.Contains("value=\"3o5\"", PesoZonas.Zona(recusada, "pesoAnterior"));

        // A comparação vai na zona dos campos escondidos do SAP, dentro do bloco que fica.
        var anterior = PesoZonas.Zona(comparada, "pesoAnterior");
        var contexto = PesoZonas.Zona(comparada, "pesoAnteriorContexto");
        Assert.Contains(contexto, anterior);
        Assert.Contains($"<input type=\"hidden\" name=\"comparar\" value=\"{escolhida}\" />", contexto);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(anterior, "name=\"comparar\""));
        Assert.DoesNotContain("name=\"comparar\"", PesoZonas.Zona(aberta, "pesoAnterior"));
        // E o «Supomos» também, no SAP e nas leituras (E4-04).
        Assert.Contains("<input type=\"hidden\" name=\"supor_calote\" value=\"5,2\" />", PesoZonas.Zona(suposta, "pesoAnteriorContexto"));
        Assert.Contains("<input type=\"hidden\" name=\"supor_calote\" value=\"5,2\" />", PesoZonas.Zona(suposta, "pesoLeituraContexto"));
    }

    /// <summary>
    /// Registar uma leitura e guardar a produção anterior voltam à folha com a
    /// comparação escolhida e o «Supomos» aberto (E4-04), também na recusa, e
    /// o Peso aberto pelo Histórico (peso_id) mantém a comparação (E4-05).
    /// </summary>
    [Fact]
    public async Task Gravar_VoltaComAComparacaoEOSupomos_TambemNoPesoAbertoPeloHistorico()
    {
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        SeedUser("4408", "Peso Operador", "provider-folha-8", "Folha408!");
        using var client = await LoginClientAsync("4408", "Folha408!");
        var aberta = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(aberta)!;
        var escolhida = System.Text.RegularExpressions.Regex.Match(aberta, "<option value=\"(\\d+)\"[^>]*>\\s*202604 ").Groups[1].Value;

        async Task<string> Registar(params (string, string)[] campos)
        {
            using var response = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement",
                HtmlFormHelpers.BuildFormPost(campos.Append(("__RequestVerificationToken", token)).ToArray()));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return Uri.UnescapeDataString(response.Headers.Location!.ToString());
        }

        async Task<string> GuardarSap(params (string, string)[] campos)
        {
            using var response = await client.PostAsync($"{PagePath}?handler=GuardarProducaoAnterior",
                HtmlFormHelpers.BuildFormPost(campos.Append(("__RequestVerificationToken", token)).ToArray()));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return Uri.UnescapeDataString(response.Headers.Location!.ToString());
        }

        // A folha da produção: com sucesso e numa recusa.
        var gravou = await Registar(("peso_id", pesoId.ToString()), ("controlo_id", controloId.ToString()),
            ("comparar", escolhida), ("supor_calote", "5,2"), ("cm_subject", "12"), ("water_weight", "70,2"));
        Assert.Contains($"controlo_id={controloId}", gravou);
        Assert.Contains($"comparar={escolhida}", gravou);
        Assert.Contains("supor_calote=5,2", gravou);
        var recusou = await Registar(("peso_id", pesoId.ToString()), ("controlo_id", controloId.ToString()),
            ("comparar", escolhida), ("supor_calote", "5,2"), ("cm_subject", "12"), ("water_weight", "7o"));
        Assert.Contains("leitura_refusal=ilegivel", recusou);
        Assert.Contains($"comparar={escolhida}", recusou);
        Assert.Contains("supor_calote=5,2", recusou);

        // O Peso aberto pelo Histórico: a comparação não se perde (nem no PDF).
        var historico = await Registar(("peso_id", pesoId.ToString()), ("comparar", escolhida),
            ("cm_subject", "13"), ("water_weight", "70,3"));
        Assert.Contains($"peso_id={pesoId}", historico);
        Assert.Contains($"comparar={escolhida}", historico);
        Assert.DoesNotContain("controlo_id", historico);
        var folhaHistorico = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoId}&comparar={escolhida}");
        Assert.Contains($"<input type=\"hidden\" name=\"comparar\" value=\"{escolhida}\" />", PesoZonas.Zona(folhaHistorico, "pesoLeituraContexto"));
        Assert.Contains($"<input type=\"hidden\" name=\"comparar\" value=\"{escolhida}\" />", PesoZonas.Zona(folhaHistorico, "pesoPdf"));

        // Guardar a produção anterior: com sucesso e numa recusa.
        var guardou = await GuardarSap(("peso_id", pesoId.ToString()), ("controlo_id", controloId.ToString()),
            ("comparar", escolhida), ("supor_calote", "5,2"), ("anterior", anteriorJobOnId.ToString()), ("anterior_peso", "181"));
        Assert.Contains("anterior_ok=1", guardou);
        Assert.Contains($"comparar={escolhida}", guardou);
        Assert.Contains("supor_calote=5,2", guardou);
        var sapRecusado = await GuardarSap(("peso_id", pesoId.ToString()), ("controlo_id", controloId.ToString()),
            ("comparar", escolhida), ("supor_calote", "5,2"), ("anterior", anteriorJobOnId.ToString()), ("anterior_peso", "18l"));
        Assert.Contains("anterior_recusa=peso", sapRecusado);
        Assert.Contains("supor_calote=5,2", sapRecusado);
    }

    /// <summary>
    /// A recusa de uma leitura deixa uma marca na zona das leituras: o
    /// peso-live.js não limpa a leitura a corrigir, mesmo que os cartões
    /// mudem por uma leitura gravada entretanto noutro separador (E4-07).
    /// </summary>
    [Fact]
    public async Task RecusaDaLeitura_DeixaAMarcaNaZonaDasLeituras()
    {
        var (controloId, _, _, _) = await SeedAsync();
        SeedUser("4409", "Peso Operador", "provider-folha-9", "Folha409!");
        using var client = await LoginClientAsync("4409", "Folha409!");
        const string marca = "<span hidden data-peso-registo-recusado></span>";

        var semRecusa = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.DoesNotContain(marca, semRecusa);
        var recusada = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&leitura_refusal=ilegivel&leitura_cm=12&leitura_water=7o");
        Assert.Contains(marca, PesoZonas.Zona(recusada, "pesoLeituras"));
        var outraRecusa = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}&measurement_refusal=PesoNotFound");
        Assert.Contains(marca, PesoZonas.Zona(outraRecusa, "pesoLeituras"));
    }

    [Fact]
    public async Task RegistarLeitura_VariasLinhas_GravaTodasEVoltaAFolhaDaProducao()
    {
        var (controloId, pesoId, _, _) = await SeedAsync();
        SeedUser("4402", "Peso Operador", "provider-folha-2", "Folha402!");
        using var client = await LoginClientAsync("4402", "Folha402!");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(
            await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"))!;

        using var response = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("controlo_id", controloId.ToString()),
            ("cm_subject", "12"), ("water_weight", "70,2"),
            ("cm_subject", "13"), ("water_weight", ""),
            ("cm_subject", "14"), ("water_weight", "70.3"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"controlo_id={controloId}", response.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var leituras = db.PesoMeasurements.Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition).ToList();
        Assert.Equal(6, leituras.Count);
        Assert.Equal(("12", 70.2m), (leituras[4].CmSubject, leituras[4].WaterWeightGrams));
        Assert.Equal(("14", 70.3m), (leituras[5].CmSubject, leituras[5].WaterWeightGrams));
    }

    [Fact]
    public async Task RegistarLeitura_UmaLinhaIlegivel_NaoGravaNenhuma()
    {
        var (controloId, pesoId, _, _) = await SeedAsync();
        SeedUser("4403", "Peso Operador", "provider-folha-3", "Folha403!");
        using var client = await LoginClientAsync("4403", "Folha403!");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(
            await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"))!;

        using var response = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("controlo_id", controloId.ToString()),
            ("cm_subject", "12"), ("water_weight", "70,2"),
            ("cm_subject", "14"), ("water_weight", "7o"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("leitura_refusal=ilegivel", location);
        Assert.Contains($"controlo_id={controloId}", location);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(4, db.PesoMeasurements.Count(m => m.PesoId == pesoId));
    }

    [Fact]
    public async Task ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta()
    {
        var (controloId, pesoId, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        SeedUser("4404", "Peso Operador", "provider-folha-4", "Folha404!");
        using var client = await LoginClientAsync("4404", "Folha404!");
        var url = $"{PagePath}?controlo_id={controloId}";

        // Sem nada guardado: a produção anterior proposta é a mais recente
        // da mesma referência, com o início e o fim do Job On.
        var before = await GetPageHtmlAsync(client, url);
        Assert.Contains("Produção anterior (SAP)", before);
        Assert.Contains($"value=\"{anteriorJobOnId}\" data-inicio=\"06/04/2026\" data-fim=\"24/04/2026\" selected=\"selected\">202604 · B1</option>", before);
        Assert.Contains($"value=\"{maisAntigaJobOnId}\"", before);
        Assert.Contains("Início <span data-inicio>06/04/2026</span> · Fim <span data-fim>24/04/2026</span>", before);
        Assert.DoesNotContain("data-producao-anterior-delta", before);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(before)!;
        using var save = await client.PostAsync($"{PagePath}?handler=GuardarProducaoAnterior", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("controlo_id", controloId.ToString()),
            ("anterior", anteriorJobOnId.ToString()),
            ("anterior_peso", "181"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = db.Pesos.Single(p => p.Id == pesoId);
            Assert.Equal(anteriorJobOnId, db.Controlos.Single(c => c.Id == peso.ProducaoAnteriorControloId).JobOnId);
            Assert.Equal(181m, peso.ProducaoAnteriorPesoGramas);
        }

        // O delta: peso médio atual 177.31 g − 181 g = −3.69 g (−2.04 %), com o sinal «−».
        var after = await GetPageHtmlAsync(client, save.Headers.Location!.ToString());
        Assert.Contains("Produção anterior guardada.", after);
        Assert.Contains("data-producao-anterior-delta", after);
        Assert.Contains("Peso usado — 202604 · B1</span><strong>181.00 g</strong>", after);
        Assert.Contains("&#x2212;3.69 g", after);
        Assert.Contains("&#x2212;2.04 %", after);
        Assert.Contains("value=\"181\"", after);

        // O PDF sai com a produção anterior (gerado em memória, descarregado).
        using var pdf = await client.PostAsync($"{PagePath}?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(after)!)));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
        if (Environment.GetEnvironmentVariable("DMO_PDF_EVIDENCE_DIR") is { } root)
        {
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(Path.Combine(root, "peso-producao-anterior.pdf"), bytes);
        }
    }

    /// <summary>O select da produção anterior (sem os outros selects da folha, que têm ids de Peso).</summary>
    private static string SelectAnterior(string html)
    {
        var inicio = html.IndexOf("id=\"producaoAnterior\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a folha não tem o select da produção anterior");
        return html[inicio..html.IndexOf("</select>", inicio, StringComparison.Ordinal)];
    }

    private async Task<int> SeedJobOnAsync(string number, DateOnly? inicio, DateOnly? fim = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var job = new JobOn { Reference = "5447T173", ProductionNumber = number, MachineId = db.Machines.Single().Id, InicioEm = inicio, FimEm = fim };
        db.JobOns.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    /// <summary>Guarda no Peso a produção anterior (o controlo do Job On, criado se falta) e o peso do SAP.</summary>
    private async Task GuardarAnteriorAsync(int pesoId, int jobOnId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var controlo = db.Controlos.SingleOrDefault(c => c.JobOnId == jobOnId);
        if (controlo is null)
        {
            controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
        }

        var peso = db.Pesos.Single(p => p.Id == pesoId);
        peso.ProducaoAnteriorControloId = controlo.Id;
        peso.ProducaoAnteriorPesoGramas = 181m;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ProducaoAnterior_SoAsAnteriores_SemAPosteriorNemASemDatas()
    {
        // Deep sweep 2 (18/10/2026, F-11): numa produção de 2021 vinha
        // escolhida uma de 2026 (posterior); noutra, uma sem datas. Só as
        // que começaram antes da atual, a mais recente escolhida.
        var (controloId, _, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        var posterior = await SeedJobOnAsync("202606", new DateOnly(2026, 6, 1));
        var semDatas = await SeedJobOnAsync("202650", null);
        SeedUser("4407", "Peso Operador", "provider-folha-7", "Folha407!");
        using var client = await LoginClientAsync("4407", "Folha407!");

        var select = SelectAnterior(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains($"value=\"{anteriorJobOnId}\" data-inicio=\"06/04/2026\" data-fim=\"24/04/2026\" selected=\"selected\">202604 · B1</option>", select);
        Assert.Contains($"value=\"{maisAntigaJobOnId}\"", select);
        Assert.DoesNotContain($"value=\"{posterior}\"", select);
        Assert.DoesNotContain($"value=\"{semDatas}\"", select);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(select, "<option ").Count);
    }

    [Fact]
    public async Task ProducaoAnterior_AsUltimas5_EAGuardadaForaDelasFicaEscolhida()
    {
        // As últimas 5 anteriores; a guardada (a 202603, já fora delas) fica
        // no select e escolhida: o «Guardar» só envia o que o select tem, e
        // trocava-a sem aviso pela primeira.
        var (controloId, pesoId, anteriorJobOnId, maisAntigaJobOnId) = await SeedAsync();
        var ultimas = new List<int>();
        foreach (var dia in Enumerable.Range(25, 5))
        {
            ultimas.Add(await SeedJobOnAsync($"2026{dia}", new DateOnly(2026, 4, dia)));
        }
        await GuardarAnteriorAsync(pesoId, maisAntigaJobOnId);
        SeedUser("4408", "Peso Operador", "provider-folha-8", "Folha408!");
        using var client = await LoginClientAsync("4408", "Folha408!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        var select = SelectAnterior(html);
        Assert.Matches($"value=\"{ultimas[4]}\"[^>]*>202629 · B1</option>[\\s\\S]*value=\"{ultimas[0]}\"[^>]*>202625 · B1</option>", select);
        Assert.DoesNotContain($"value=\"{anteriorJobOnId}\"", select);
        Assert.Contains($"value=\"{maisAntigaJobOnId}\" data-inicio=\"02/03/2026\" data-fim=\"20/03/2026\" selected=\"selected\">202603 · B1</option>", select);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(select, "<option ").Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(select, "selected=\"selected\""));
        Assert.Contains("Início <span data-inicio>02/03/2026</span> · Fim <span data-fim>20/03/2026</span>", html);
        Assert.Contains("value=\"181\"", html);
    }

    [Fact]
    public async Task ProducaoAnterior_SemAnteriores_AGuardadaContinuaNoSelect()
    {
        // Sem produções anteriores, a guardada antes (uma posterior, do tempo
        // em que vinha proposta) continua escolhida: com o select vazio, o
        // «Guardar» apagava-a.
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var atual = db.Controlos.Include(c => c.JobOn).Single(c => c.Id == controloId).JobOn;
            atual.InicioEm = new DateOnly(2026, 1, 5);
            await db.SaveChangesAsync();
        }
        SeedUser("4409", "Peso Operador", "provider-folha-9", "Folha409!");
        using var client = await LoginClientAsync("4409", "Folha409!");

        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("Sem produções anteriores desta referência.", vazio);
        Assert.DoesNotContain("id=\"producaoAnterior\"", vazio);

        await GuardarAnteriorAsync(pesoId, anteriorJobOnId);
        var select = SelectAnterior(await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"));
        Assert.Contains($"value=\"{anteriorJobOnId}\" data-inicio=\"06/04/2026\" data-fim=\"24/04/2026\" selected=\"selected\">202604 · B1</option>", select);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(select, "<option "));
    }

    [Fact]
    public async Task ProducaoAnterior_PesoIlegivel_RecusaComEcho_ENaoGuarda()
    {
        var (controloId, pesoId, anteriorJobOnId, _) = await SeedAsync();
        SeedUser("4405", "Peso Operador", "provider-folha-5", "Folha405!");
        using var client = await LoginClientAsync("4405", "Folha405!");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(
            await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}"))!;

        using var save = await client.PostAsync($"{PagePath}?handler=GuardarProducaoAnterior", HtmlFormHelpers.BuildFormPost(
            ("peso_id", pesoId.ToString()),
            ("controlo_id", controloId.ToString()),
            ("anterior", anteriorJobOnId.ToString()),
            ("anterior_peso", "18l"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var html = await GetPageHtmlAsync(client, save.Headers.Location!.ToString());
        Assert.Contains("O peso da produção anterior tem de ser um número maior que 0. Nada foi guardado.", html);
        Assert.Contains("value=\"18l\"", html);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Null(db.Pesos.Single(p => p.Id == pesoId).ProducaoAnteriorPesoGramas);
    }
}
