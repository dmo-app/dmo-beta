using System.Net;
using System.Text.RegularExpressions;
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
/// Um Peso por CM+BQ da produção (18/10/2026; regra do Diogo: «o cm_id é
/// snapshot de um tool_id naquele momento»; respostas R15 e R16 do
/// especialista). Antes: depois de trocar a CM a meio da produção, as
/// leituras novas iam para o Peso da CM antiga e eram calculadas com ela.
/// Agora a folha propõe criar o Peso das ferramentas novas e diz porquê; o
/// Peso anterior abre com as ferramentas dele, sem a leitura nova; voltar à
/// CM anterior retoma o Peso dela; o PDF do segundo leva «-2»; o Resumo, a
/// Revisão e o detalhe mostram os dois. Com um só Peso, tudo como sempre.
/// </summary>
public class PesoPorCombinacaoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoPorCombinacaoPageTests(CustomWebApplicationFactory factory)
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
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
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

    private void SeedUser(string operatorId, string providerUserId, string password)
    {
        var generatedEmail = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(generatedEmail, providerUserId, password, generatedEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-peso-combinacao");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-peso-combinacao",
                Name = "Peso e revisão",
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove }
            };
            db.Templates.Add(template);
        }

        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Peso Chefe", ProviderUserId = providerUserId };
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginClientAsync(string identifier, string password)
    {
        var client = CreateClient();
        var login = await client.GetAsync("/login");
        login.EnsureSuccessStatusCode();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await login.Content.ReadAsStringAsync())!;
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, string handler, params (string, string)[] fields)
    {
        // O token vem da folha da produção, como no posto.
        var controloId = fields.First(f => f.Item1 == "controlo_id").Item2;
        var page = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(page)!;
        return await client.PostAsync($"{PagePath}?handler={handler}",
            HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    private sealed record Producao(int ControloId, int JobOnId, int MachineId, int ControloAnteriorId);

    /// <summary>
    /// A referência 5447T173 na B1: a produção anterior 202604 (com controlo)
    /// e a atual 202605 com a CM T8 (5447 lote 8) e a BQ BQ4 (T173 lote 4).
    /// A CM T9 (5448 lote 9) e as BQ BQ5 (T174 lote 5) e BQ6 (T175 lote 6)
    /// existem para as trocas.
    /// </summary>
    private async Task<Producao> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = "T8", Type = ToolType.Cm, Reference = "5447", Lot = "8" },
            new Tool { ToolId = "T9", Type = ToolType.Cm, Reference = "5448", Lot = "9" },
            new Tool { ToolId = "BQ4", Type = ToolType.Bq, Reference = "T173", Lot = "4" },
            new Tool { ToolId = "BQ5", Type = ToolType.Bq, Reference = "T174", Lot = "5" },
            new Tool { ToolId = "BQ6", Type = ToolType.Bq, Reference = "T175", Lot = "6" },
            new Tool { ToolId = "MF3", Type = ToolType.Mf, Reference = "M88", Lot = "3" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = "T8", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 8.9m, NominalWeightGrams = 176m },
            new ToolTechnicalContext { ToolId = "T9", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 9.5m, NominalWeightGrams = 178m },
            new ToolTechnicalContext { ToolId = "BQ4", VolumeMarisaCm3 = 12m },
            new ToolTechnicalContext { ToolId = "BQ5", VolumeMarisaCm3 = 12.5m },
            new ToolTechnicalContext { ToolId = "BQ6", VolumeMarisaCm3 = 13m });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4027m });
        var anterior = new JobOn { Reference = "5447T173", ProductionNumber = "202604", Machine = machine };
        var atual = new JobOn { Reference = "5447T173", ProductionNumber = "202605", Machine = machine };
        db.JobOns.AddRange(anterior, atual);
        db.CmContexts.Add(new CmContext { JobOn = atual, ToolId = "T8" });
        db.MfContexts.Add(new MfContext { JobOn = atual, ToolId = "MF3" });
        db.BqContexts.Add(new BqContext { JobOn = atual, ToolId = "BQ4" });
        await db.SaveChangesAsync();
        var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = atual.Id };
        db.Controlos.AddRange(controloAnterior, controlo);
        await db.SaveChangesAsync();
        return new Producao(controlo.Id, atual.Id, machine.Id, controloAnterior.Id);
    }

    /// <summary>«Criar Peso» na folha da produção; devolve o peso_id que ficou.</summary>
    private async Task<int> CriarPesoAsync(HttpClient client, Producao producao)
    {
        using var criado = await PostAsync(client, "CreateForControlo",
            ("controlo_id", producao.ControloId.ToString()), ("water_temperature", "24"));
        Assert.Equal(HttpStatusCode.Redirect, criado.StatusCode);
        Assert.Equal($"{PagePath}?controlo_id={producao.ControloId}", criado.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var contexto = await db.CmContexts.AsNoTracking().SingleAsync(c => c.JobOnId == producao.JobOnId);
        var bq = await db.BqContexts.AsNoTracking().SingleAsync(c => c.JobOnId == producao.JobOnId);
        return await db.Pesos.AsNoTracking()
            .Where(p => p.ControloId == producao.ControloId && p.CmToolId == contexto.ToolId && p.BqToolId == bq.ToolId)
            .Select(p => p.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> RegistarLeituraAsync(HttpClient client, Producao producao, int pesoId, string cm, string massa) =>
        await PostAsync(client, "RegisterMeasurement",
            ("peso_id", pesoId.ToString()), ("controlo_id", producao.ControloId.ToString()),
            ("cm_subject", cm), ("water_weight", massa));

    /// <summary>Troca a CM e/ou a BQ da produção no Editar do Job On.</summary>
    private async Task TrocarAsync(Producao producao, string cm, string bq)
    {
        using var scope = _factory.Services.CreateScope();
        var trocado = await scope.ServiceProvider.GetRequiredService<IUpdateJobOnHandler>().HandleAsync(
            new UpdateJobOn(producao.JobOnId, producao.MachineId, cm, "MF3", bq));
        Assert.Equal(UpdateJobOnResult.Success, trocado.Result);
    }

    private async Task<List<Peso>> PesosAsync(Producao producao)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.Pesos.AsNoTracking().Include(p => p.Measurements)
            .Where(p => p.ControloId == producao.ControloId).OrderBy(p => p.Id).ToListAsync();
    }

    private static string CartaoDosPesos(string html)
    {
        var lista = Regex.Match(html, "data-dmo-list=\"Pesos desta produção\"[\\s\\S]*?</table>");
        Assert.True(lista.Success, "a página não tem a lista «Pesos desta produção»");
        return lista.Value;
    }

    [Fact]
    public async Task UmSoPeso_AFolhaEOsEcrasDeRevisaoFicamComoSempre()
    {
        var producao = await SeedAsync();
        SeedUser("4901", "provider-peso-comb-1", "PesoComb1!");
        using var client = await LoginClientAsync("4901", "PesoComb1!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}");
        Assert.Contains("<div id=\"pesoPesos\" class=\"dmo-zona\" data-dmo-regiao>", html);
        Assert.DoesNotContain("Pesos desta produção", html);
        Assert.DoesNotContain("Peso 1 de", html);
        Assert.DoesNotContain("Abrir o Peso atual", html);
        Assert.Contains("data-peso-rows-form", html);
        Assert.DoesNotContain("Novo Peso da produção", html);

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        Assert.DoesNotContain("aria-label=\"Pesos da produção\"", resumo);
        Assert.DoesNotContain("falta o Peso das ferramentas atuais", resumo);
        Assert.Contains($"<a class=\"resumo-card-link\" href=\"/Controlo/Peso?controlo_id={producao.ControloId}\">", resumo);

        var approve = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={producao.JobOnId}");
        Assert.DoesNotContain("Peso 1 de 1", approve);
        // A CM e o lote ficam só na Consulta do Peso (Diogo, 19/10/2026).
        Assert.DoesNotContain("<span>Temperatura da água</span>", approve);
        Assert.DoesNotContain("<span>Boquilha</span>", approve);
        Assert.Contains("<span>Peso médio atual</span>", approve);
        var lista = await GetPageHtmlAsync(client, $"/Controlo/Approve?handler=Resultados&jobon_id={producao.JobOnId}");
        Assert.DoesNotContain(" Pesos</span>", lista);
        var detalhe = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={producao.ControloId}&tipo=peso");
        Assert.DoesNotContain("Peso 1 de", detalhe);
        Assert.Contains("<h3 class=\"revisao-section\">Dados do Peso</h3>", detalhe);
        Assert.Contains("<div><span>CM</span><strong>5447</strong><small>Lote 8</small></div>", detalhe);
    }

    [Fact]
    public async Task DepoisDeTrocarACm_AFolhaPropoeCriarOPesoNovo_EDizPorque_SemGravarNada()
    {
        var producao = await SeedAsync();
        SeedUser("4902", "provider-peso-comb-2", "PesoComb2!");
        using var client = await LoginClientAsync("4902", "PesoComb2!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }
        await TrocarAsync(producao, "T9", "BQ4");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}");

        Assert.Contains("Novo Peso da produção", html);
        Assert.Contains("A CM mudou nesta produção; o Peso anterior ficou como estava.", html);
        var cartao = CartaoDosPesos(html);
        Assert.Contains($"/Controlo/Peso?controlo_id={producao.ControloId}&amp;peso_id={pesoA}", cartao);
        Assert.Contains("5447 · Lote 8", cartao);
        Assert.Contains("Ferramentas anteriores", cartao);
        Assert.Contains("5448 · Lote 9", cartao);
        Assert.Contains("Ferramentas atuais · por criar · aberto", cartao);
        Assert.DoesNotContain("data-peso-rows-form", html);

        // Abrir a folha não grava nada: o Peso novo só nasce no «Criar Peso».
        Assert.Single(await PesosAsync(producao));

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        Assert.Contains("A CM ou a BQ mudou nesta produção: falta o Peso das ferramentas atuais.", resumo);
        Assert.Contains($"<a class=\"dmo-button\" href=\"/Controlo/Peso?controlo_id={producao.ControloId}\" aria-label=\"Registar o Peso\">Registar</a>", resumo);
        Assert.Contains($"<a class=\"resumo-card-link\" href=\"/Controlo/Peso?controlo_id={producao.ControloId}&amp;peso_id={pesoA}\">", resumo);
    }

    [Fact]
    public async Task CriarDepoisDaTroca_AbreOPesoNovo_CopiaAProducaoAnterior_EOAnteriorFicaSemLeituraNova()
    {
        var producao = await SeedAsync();
        SeedUser("4903", "provider-peso-comb-3", "PesoComb3!");
        using var client = await LoginClientAsync("4903", "PesoComb3!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var anterior = await db.Pesos.SingleAsync(p => p.Id == pesoA);
            anterior.ProducaoAnteriorControloId = producao.ControloAnteriorId;
            anterior.ProducaoAnteriorPesoGramas = 205.5m;
            await db.SaveChangesAsync();
        }

        await TrocarAsync(producao, "T9", "BQ4");
        var pesoB = await CriarPesoAsync(client, producao);
        // Um segundo clique em «Criar Peso» abre o mesmo Peso, sem mensagem.
        Assert.Equal(pesoB, await CriarPesoAsync(client, producao));

        var pesos = await PesosAsync(producao);
        Assert.Equal(new[] { pesoA, pesoB }, pesos.Select(p => p.Id));
        Assert.Equal("T9", pesos[1].CmToolId);
        Assert.Equal(9.5m, pesos[1].VolumePuncaoCm3);
        // A produção anterior e o peso do SAP vêm do Peso que a produção já
        // tinha (resposta R16-7 do especialista).
        Assert.Equal(producao.ControloAnteriorId, pesos[1].ProducaoAnteriorControloId);
        Assert.Equal(205.5m, pesos[1].ProducaoAnteriorPesoGramas);
        Assert.Single(pesos[0].Measurements);

        var atual = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}");
        Assert.Contains("Peso 2 de 2 · ferramentas atuais.", atual);
        Assert.Contains("data-peso-rows-form", atual);
        Assert.DoesNotContain("Novo Peso da produção", atual);
        Assert.Contains("Ferramentas atuais · aberto", CartaoDosPesos(atual));

        // O Peso anterior: as ferramentas e os valores dele, sem a leitura
        // nova, com «Abrir o Peso atual» no canto do cartão.
        var antigo = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}&peso_id={pesoA}");
        Assert.Contains("Peso 1 de 2 · de antes da troca da CM ou da BQ: fica como estava e já não recebe leituras.", antigo);
        Assert.Contains($"<a class=\"dmo-button\" href=\"/Controlo/Peso?controlo_id={producao.ControloId}\">Abrir o Peso atual</a>", antigo);
        Assert.DoesNotContain("data-peso-rows-form", antigo);
        Assert.Contains("<div class=\"peso-fact\"><span>CM</span><strong>5447</strong></div>", antigo);
        Assert.Contains("<div class=\"peso-fact\"><span>Lote da CM</span><strong>8</strong></div>", antigo);
        Assert.Contains("Ferramentas anteriores · aberto", CartaoDosPesos(antigo));
        Assert.Contains($"name=\"peso_id\" value=\"{pesoA}\"", antigo);

        // Pelo histórico (só o peso_id), o mesmo: sem a leitura nova.
        var historico = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoA}");
        Assert.DoesNotContain("data-peso-rows-form", historico);
        Assert.Contains("Abrir o Peso atual", historico);
        Assert.Contains("Pesos desta produção", historico);
    }

    [Fact]
    public async Task LeituraNoPesoAnterior_ERecusada_ENadaEGravado()
    {
        var producao = await SeedAsync();
        SeedUser("4904", "provider-peso-comb-4", "PesoComb4!");
        using var client = await LoginClientAsync("4904", "PesoComb4!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }
        await TrocarAsync(producao, "T9", "BQ4");

        // A folha aberta antes da troca (outro separador): a leitura não
        // entra no Peso da CM antiga, e a recusa aparece nesse Peso.
        using var recusada = await RegistarLeituraAsync(client, producao, pesoA, "54", "70.5");
        Assert.Equal(HttpStatusCode.Redirect, recusada.StatusCode);
        var destino = recusada.Headers.Location!.ToString();
        Assert.Contains("measurement_refusal=PesoNotCurrent", destino);
        Assert.Contains($"peso_id={pesoA}", destino);
        var html = await GetPageHtmlAsync(client, destino);
        Assert.Contains("Este Peso é de uma CM ou de uma boquilha (BQ) que já não está na produção. A leitura não foi gravada. "
            + "As leituras novas vão para o Peso das ferramentas atuais.", html);

        // Sem o Peso, a recusa aparece nos avisos da produção.
        var semPeso = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}&measurement_refusal=PesoNotCurrent");
        Assert.Contains("A leitura não foi gravada.", Regex.Match(semPeso, "id=\"pesoProducaoAvisos\"[\\s\\S]*?</div>\\s*</div>").Value);

        var pesos = await PesosAsync(producao);
        Assert.Single(pesos);
        Assert.Single(pesos[0].Measurements);
    }

    [Fact]
    public async Task VoltarACmAnterior_ReabreOPesoDela()
    {
        var producao = await SeedAsync();
        SeedUser("4905", "provider-peso-comb-5", "PesoComb5!");
        using var client = await LoginClientAsync("4905", "PesoComb5!");
        var pesoA = await CriarPesoAsync(client, producao);
        await TrocarAsync(producao, "T9", "BQ4");
        var pesoB = await CriarPesoAsync(client, producao);

        // A→B→A: a combinação já usada retoma o Peso dela (R16-2).
        await TrocarAsync(producao, "T8", "BQ4");
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}");
        Assert.DoesNotContain("Novo Peso da produção", html);
        Assert.Contains("Peso 1 de 2 · ferramentas atuais.", html);
        Assert.Contains("data-peso-rows-form", html);
        Assert.Contains($"peso_id={pesoB}", CartaoDosPesos(html));
        Assert.Equal(pesoA, await CriarPesoAsync(client, producao));
        using (var lida = await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1"))
        {
            Assert.DoesNotContain("measurement_refusal", lida.Headers.Location!.ToString());
        }

        Assert.Equal(2, (await PesosAsync(producao)).Count);
    }

    [Fact]
    public async Task TrocarABq_PedePesoNovo_EAssociarUmPesoDeOutraBqERecusadoComARazao()
    {
        var producao = await SeedAsync();
        SeedUser("4906", "provider-peso-comb-6", "PesoComb6!");
        using var client = await LoginClientAsync("4906", "PesoComb6!");
        await CriarPesoAsync(client, producao);
        await TrocarAsync(producao, "T8", "BQ5");

        int deOutraBq, destaBq;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Peso Solto(string bq) => new()
            {
                CmToolId = "T8", BqToolId = bq, Process = "NNPB", WaterTemperatureC = 24,
                VolumeMarisaCm3 = 12m, VolumePuncaoCm3 = 8.9m, GlassDensityGramsPerCm3 = 2.4027m
            };
            var outra = Solto("BQ6");
            var esta = Solto("BQ5");
            db.Pesos.AddRange(outra, esta);
            await db.SaveChangesAsync();
            (deOutraBq, destaBq) = (outra.Id, esta.Id);
        }

        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={producao.ControloId}");
        Assert.Contains("A BQ mudou nesta produção; o Peso anterior ficou como estava.", html);
        var associar = Regex.Match(html, "<table class=\"dmo-table\" id=\"pesoAssociateList\"[\\s\\S]*?</table>").Value;
        Assert.Contains($"data-id=\"{destaBq}\"", associar);
        Assert.DoesNotContain($"data-id=\"{deOutraBq}\"", associar);
        Assert.Contains("Pesos sem produção destas ferramentas", html);

        // A BQ de outro Peso entra no cálculo: a associação recusa e diz porquê.
        using (var recusada = await PostAsync(client, "Associate",
                   ("peso_id", deOutraBq.ToString()), ("controlo_id", producao.ControloId.ToString())))
        {
            var destino = recusada.Headers.Location!.ToString();
            Assert.Contains("associacao_refusal=BqMismatch", destino);
            var recusa = await GetPageHtmlAsync(client, destino);
            Assert.Contains("A boquilha (BQ) deste Peso não é a do Job On, nem uma que a produção teve antes. Nenhum Peso foi associado.", recusa);
        }

        using (var aceite = await PostAsync(client, "Associate",
                   ("peso_id", destaBq.ToString()), ("controlo_id", producao.ControloId.ToString())))
        {
            Assert.Equal($"{PagePath}?controlo_id={producao.ControloId}&associado=1", aceite.Headers.Location!.ToString());
        }

        var pesos = await PesosAsync(producao);
        Assert.Equal(new[] { "BQ4", "BQ5" }, pesos.Select(p => p.BqToolId));
    }

    [Fact]
    public async Task PdfDoSegundoPeso_LevaMenos2_OPrimeiroFicaComONomeDeSempre()
    {
        var producao = await SeedAsync();
        SeedUser("4907", "provider-peso-comb-7", "PesoComb7!");
        using var client = await LoginClientAsync("4907", "PesoComb7!");
        var pesoA = await CriarPesoAsync(client, producao);
        await TrocarAsync(producao, "T9", "BQ4");
        var pesoB = await CriarPesoAsync(client, producao);

        async Task<string?> NomeAsync(int pesoId)
        {
            using var pdf = await PostAsync(client, "GeneratePdf",
                ("controlo_id", producao.ControloId.ToString()), ("peso_id", pesoId.ToString()));
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            return pdf.Content.Headers.ContentDisposition?.FileNameStar ?? pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        }

        Assert.Equal("PESO-5447T173-202605.pdf", await NomeAsync(pesoA));
        Assert.Equal("PESO-5447T173-202605-2.pdf", await NomeAsync(pesoB));
    }

    [Fact]
    public async Task ComDoisPesos_OResumoARevisaoEODetalheMostramOsDois()
    {
        var producao = await SeedAsync();
        SeedUser("4908", "provider-peso-comb-8", "PesoComb8!");
        using var client = await LoginClientAsync("4908", "PesoComb8!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }
        await TrocarAsync(producao, "T9", "BQ4");
        var pesoB = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoB, "45", "71.2")) { }

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var lista = Regex.Match(resumo, "<ul class=\"resumo-peg-folhas\" aria-label=\"Pesos da produção\">[\\s\\S]*?</ul>").Value;
        Assert.Contains($"<a href=\"/Controlo/Peso?controlo_id={producao.ControloId}&amp;peso_id={pesoA}\">Peso 1</a>", lista);
        Assert.Contains($"<a href=\"/Controlo/Peso?controlo_id={producao.ControloId}\">Peso 2</a>", lista);
        Assert.Contains("CM 5447 · Lote 8 · BQ T173 · Lote 4", lista);
        Assert.Contains("CM 5448 · Lote 9 · BQ T173 · Lote 4", lista);
        Assert.Contains("de antes da troca", lista);
        Assert.DoesNotContain("falta o Peso das ferramentas atuais", resumo);
        Assert.DoesNotContain("aria-label=\"Registar o Peso\"", resumo);

        var pdfToken = HtmlFormHelpers.ExtractRequestVerificationToken(resumo)!;
        using (var pdf = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
                   ("jobon_id", producao.JobOnId.ToString()), ("__RequestVerificationToken", pdfToken))))
        {
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            Assert.True((await pdf.Content.ReadAsByteArrayAsync()).Length > 0);
        }

        var approve = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={producao.JobOnId}");
        Assert.Contains("Peso 1 de 2 · CM 5447 · Lote 8 · BQ T173 · Lote 4 · 1 leitura · ferramentas anteriores", approve);
        Assert.Contains("Peso 2 de 2 · CM 5448 · Lote 9 · BQ T173 · Lote 4 · 1 leitura · ferramentas atuais", approve);
        Assert.Equal(2, Regex.Matches(approve, "<span>Peso médio atual</span>").Count);

        var resultados = await GetPageHtmlAsync(client, $"/Controlo/Approve?handler=Resultados&jobon_id={producao.JobOnId}");
        // Uma linha por produção, não uma por Peso (a anterior 202604 também está no dia).
        Assert.Single(Regex.Matches(resultados, "<strong>5447T173 · 202605</strong>"));
        Assert.Contains("<span> · 2 Pesos</span>", resultados);

        var detalhe = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={producao.ControloId}&tipo=peso");
        Assert.Contains("<h3 class=\"revisao-section\">Peso 1 de 2 · CM 5447 · Lote 8 · BQ T173 · Lote 4 · 1 leitura · ferramentas anteriores</h3>", detalhe);
        Assert.Contains("<h3 class=\"revisao-section\">Peso 2 de 2 · CM 5448 · Lote 9 · BQ T173 · Lote 4 · 1 leitura</h3>", detalhe);
        Assert.Contains("<div><span>CM</span><strong>5447</strong><small>Lote 8</small></div>", detalhe);
        Assert.Contains("<div><span>CM</span><strong>5448</strong><small>Lote 9</small></div>", detalhe);
    }
    [Fact]
    public async Task PdfCompleto_ComUmPesoSemLeituras_DeixaOVazioDeFora()
    {
        // O PDF completo (Diogo, 19/10/2026): só entram os Pesos com
        // leituras; o Peso novo ainda vazio não parte o PDF.
        var producao = await SeedAsync();
        SeedUser("4909", "provider-peso-comb-9", "PesoComb9!");
        using var client = await LoginClientAsync("4909", "PesoComb9!");
        var pesoA = await CriarPesoAsync(client, producao);
        using (await RegistarLeituraAsync(client, producao, pesoA, "45", "70.1")) { }
        await TrocarAsync(producao, "T9", "BQ4");
        await CriarPesoAsync(client, producao);

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(resumo)!;
        async Task<byte[]> PdfAsync(bool apenasResumo)
        {
            using var pdf = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", producao.JobOnId.ToString()), ("apenas_resumo", apenasResumo.ToString()), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            return await pdf.Content.ReadAsByteArrayAsync();
        }
        Assert.True((await PdfAsync(false)).Length > (await PdfAsync(true)).Length);
    }
}
