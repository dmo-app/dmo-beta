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
/// O Peso só para comparação (o Diogo, 22/10/2026: «ao registar um peso
/// agora nunca tenho comparação porque não existem pesos anteriores»): o
/// «Criar» da comparação abre o Novo Peso sem Job On, o Peso criado fica
/// ligado ao Peso atual como o anterior dele, a comparação aparece ao voltar,
/// e a app nunca pede para o associar a um Job On. Pelos formulários que a
/// página desenha.
/// </summary>
public class PesoSoParaComparacaoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoSoParaComparacaoPageTests(CustomWebApplicationFactory factory)
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
        // Primeiro os só para comparação: apontam para outro Peso.
        db.Pesos.RemoveRange(db.Pesos.Where(p => p.ComparacaoDoPesoId != null));
        db.SaveChanges();
        db.Pesos.RemoveRange(db.Pesos);
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

    private void SeedUser(string operatorId, string name, string providerUserId, string password)
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
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
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

        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
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
    /// A referência 5447T173 na B1: a produção atual 202605 com um Peso de
    /// duas leituras e nenhuma anterior com Peso; e a 202606 com a mesma CM e
    /// BQ e ainda sem Peso (a que a associação proporia a um Peso solto).
    /// </summary>
    private async Task<(int ControloId, int PesoId)> SeedAsync()
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
        var atual = new JobOn { Reference = "5447T173", ProductionNumber = "202605", Machine = machine, InicioEm = new DateOnly(2026, 5, 4) };
        var seguinte = new JobOn { Reference = "5447T173", ProductionNumber = "202606", Machine = machine, InicioEm = new DateOnly(2026, 6, 1) };
        db.JobOns.AddRange(atual, seguinte);
        foreach (var job in new[] { atual, seguinte })
        {
            db.CmContexts.Add(new CmContext { JobOn = job, ToolId = "T8" });
            db.BqContexts.Add(new BqContext { JobOn = job, ToolId = "BQ4" });
        }
        await db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = atual.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();

        var peso = NovoPeso();
        peso.ControloId = controlo.Id;
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "45", WaterWeightGrams = 70.1m });
        db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 2, CmSubject = "54", WaterWeightGrams = 70.5m });
        await db.SaveChangesAsync();
        return (controlo.Id, peso.Id);
    }

    private static Peso NovoPeso() => new()
    {
        CmToolId = "T8", BqToolId = "BQ4", Process = "NNPB", WaterTemperatureC = 24,
        VolumeMarisaCm3 = 12m, VolumePuncaoCm3 = 8.9m, GlassDensityGramsPerCm3 = 2.4027m
    };

    private static string Href(string html, string marca)
    {
        var match = Regex.Match(html, $@"<a[^>]*href=""(?<href>[^""]+)""[^>]*{marca}");
        Assert.True(match.Success, $"A página não tem o link {marca}.");
        return WebUtility.HtmlDecode(match.Groups["href"].Value);
    }

    // O envio de um formulário GET: os campos no endereço.
    private static string ComoEndereco(string acao, IReadOnlyList<(string Nome, string Valor)> campos) =>
        acao + "?" + string.Join("&", campos
            .Where(c => c.Nome != "__RequestVerificationToken")
            .Select(c => $"{Uri.EscapeDataString(c.Nome)}={Uri.EscapeDataString(c.Valor)}"));

    // A tabela da comparação (o table data-peso-comparacao-tabela inteiro).
    private static string Tabela(string html)
    {
        var inicio = html.IndexOf("<table class=\"dmo-table\" data-peso-comparacao-tabela>", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A página não tem a tabela da comparação.");
        var fim = html.IndexOf("</table>", inicio, StringComparison.Ordinal);
        return html[inicio..(fim + "</table>".Length)];
    }

    // As células (td/th) de cada linha da tabela, sem espaços à volta.
    private static List<List<string>> Linhas(string tabela) =>
        Regex.Matches(tabela, @"<tr>(?<linha>.*?)</tr>", RegexOptions.Singleline)
            .Select(l => Regex.Matches(l.Groups["linha"].Value, @"<t[dh][^>]*>(?<c>.*?)</t[dh]>", RegexOptions.Singleline)
                .Select(c => WebUtility.HtmlDecode(Regex.Replace(c.Groups["c"].Value, "<[^>]+>", "")).Trim())
                .ToList())
            .ToList();

    [Fact]
    public async Task CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn()
    {
        var (controloId, pesoId) = await SeedAsync();
        SeedUser("4501", "Peso Comparação", "provider-so-comparacao", "SoComp501!");
        using var client = await LoginClientAsync("4501", "SoComp501!");

        // A folha da produção: sem Pesos anteriores, a comparação oferece «Criar».
        var html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("Sem Pesos anteriores para esta referência.", html);
        var criar = Href(html, "data-peso-comparacao-criar");

        // O Novo Peso sem Job On: as ferramentas pelo formulário, depois a temperatura.
        html = await GetPageHtmlAsync(client, criar);
        Assert.Contains("data-peso-so-comparacao", html);
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("cmToolId", "5447"), ("bqToolId", "T173"));
        html = await GetPageHtmlAsync(client, ComoEndereco(acao, campos));
        (acao, campos) = EnvioDoFormulario.Preencher(html, ("waterTemperature", "24"));
        int comparacaoId;
        using (var criado = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, criado.StatusCode);
            var destino = criado.Headers.Location?.ToString() ?? string.Empty;
            var match = Regex.Match(destino, @"peso_id=(\d+)");
            Assert.True(match.Success, $"O Peso não foi criado: voltou a {destino}.");
            comparacaoId = int.Parse(match.Groups[1].Value);
        }

        // Ligado ao Peso atual, sem produção.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var criadoNaBase = await db.Pesos.AsNoTracking().SingleAsync(p => p.Id == comparacaoId);
            Assert.Equal(pesoId, criadoNaBase.ComparacaoDoPesoId);
            Assert.Null(criadoNaBase.ControloId);
        }

        // A folha do Peso criado não pergunta por um Job On (a 202606 tem a
        // mesma CM e BQ e ainda não tem Peso) e regista leituras.
        html = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={comparacaoId}");
        Assert.DoesNotContain("Quer associar este Peso?", html);
        Assert.Contains("data-peso-so-comparacao", html);
        var leiturasZona = PesoZonas.Zona(html, "pesoLeituras");
        Assert.Contains("Peso só para comparação: não é associado a nenhum Job On.", leiturasZona);
        Assert.DoesNotContain("handler=Associate", leiturasZona);
        Assert.DoesNotContain("jobon_id", leiturasZona);
        (acao, campos) = EnvioDoFormulario.Preencher(html, ("cmSubject", "45"), ("waterWeight", "70.0"));
        using (var leitura = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, leitura.StatusCode);
        }

        // Voltar ao Peso original: a comparação com o Peso criado aparece.
        html = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={comparacaoId}");
        var voltar = Href(html, "data-peso-comparacao-voltar");
        Assert.Contains($"controlo_id={controloId}", voltar);
        html = await GetPageHtmlAsync(client, voltar);
        Assert.Contains("Peso para comparação", html);
        // Na tabela: o CM 45 atual (70.1 g de água → 70.36 cm³, 176.51 g de
        // vidro) ao lado do 45 do Peso criado (70.0 g → 70.261 cm³, 176.27 g)
        // e a diferença; o 54 não tem par. As médias comparam o mesmo.
        var linhas = Linhas(Tabela(html));
        Assert.Equal(4, linhas.Count);
        Assert.Equal(new[] { "45", "70.36", "176.51", "45", "70.26", "176.27", "+0.10", "+0.24" }, linhas[1]);
        Assert.Equal(new[] { "54", "70.76", "177.47", "—", "—", "—", "—", "—" }, linhas[2]);
        Assert.Equal(new[] { "Média", "70.56", "176.99", "", "70.26", "176.27", "+0.30", "+0.72 (+0.41 %)" }, linhas[3]);

        // Sem escolha no endereço, a folha da produção também o compara.
        html = await GetPageHtmlAsync(client, $"{PagePath}?controlo_id={controloId}");
        Assert.Contains("data-peso-comparacao-tabela", html);

        // A lista dos Pesos por associar a um Job On não o mostra.
        html = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&sem_jobon=1");
        Assert.Contains("Não há Pesos nem Pegamentos por associar a um Job On.", html);
    }

    [Fact]
    public async Task PesoSoParaComparacao_AssociarAUmJobOn_ERecusado()
    {
        var (_, pesoId) = await SeedAsync();
        int comparacaoId;
        int seguinteJobOnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var peso = NovoPeso();
            peso.ComparacaoDoPesoId = pesoId;
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            comparacaoId = peso.Id;
            seguinteJobOnId = await db.JobOns.Where(j => j.ProductionNumber == "202606").Select(j => j.Id).SingleAsync();
        }

        SeedUser("4502", "Peso Comparação", "provider-so-comparacao-2", "SoComp502!");
        using var client = await LoginClientAsync("4502", "SoComp502!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?peso_id={comparacaoId}");
        using var resposta = await client.PostAsync($"{PagePath}?handler=Associate", HtmlFormHelpers.BuildFormPost(
            ("peso_id", comparacaoId.ToString()),
            ("jobon_id", seguinteJobOnId.ToString()),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        var recusa = PesoZonas.Zona(await GetPageHtmlAsync(client, resposta.Headers.Location!.ToString()), "pesoLeituras");
        Assert.Contains("Este Peso já pertence a outra produção. Nada foi alterado.", recusa);
        Assert.Contains("data-peso-so-comparacao", recusa);
        Assert.DoesNotContain("handler=Associate", recusa);
        Assert.DoesNotContain("jobon_id", recusa);

        using var verificar = _factory.Services.CreateScope();
        var dbVerificar = verificar.ServiceProvider.GetRequiredService<DmoDbContext>();
        var depois = await dbVerificar.Pesos.AsNoTracking().SingleAsync(p => p.Id == comparacaoId);
        Assert.Null(depois.ControloId);
        Assert.Equal(pesoId, depois.ComparacaoDoPesoId);
    }
}
