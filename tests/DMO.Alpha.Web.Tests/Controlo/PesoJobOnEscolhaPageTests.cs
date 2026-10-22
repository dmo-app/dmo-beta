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
/// O Novo Peso tem duas formas: sem Job On (as ferramentas escritas à mão)
/// ou associado a um Job On, em que a produção escolhida abre pelo seu
/// controlo_id e as ferramentas vêm do Job On.
/// </summary>
public sealed class PesoJobOnEscolhaPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/Controlo/Peso";

    private readonly CustomWebApplicationFactory _factory;

    public PesoJobOnEscolhaPageTests(CustomWebApplicationFactory factory)
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
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
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

    private void SeedUser(string operatorId, string providerUserId, string password)
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
            db.SaveChanges();
        }

        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Peso Job On", ProviderUserId = providerUserId };
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

    private static async Task<string> GetVerificationTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private static async Task<string> GetPageHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<int> SeedJobOnAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        db.Tools.Add(new Tool { ToolId = "MF-TOOL-9", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        await db.SaveChangesAsync();

        var created = await scope.ServiceProvider.GetRequiredService<ICreateJobOnHandler>()
            .HandleAsync(new CreateJobOn("J10", "5810", machine.Id, "T3", "MF-TOOL-9", "BQ-1"));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        return created.JobOn!.JobOnId;
    }

    private static async Task<HttpResponseMessage> PostAbrirJobOnAsync(HttpClient client, string? jobOnId)
    {
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?modo=jobon&referencia=5810");
        var fields = new List<(string, string)> { ("__RequestVerificationToken", token) };
        if (jobOnId is not null)
        {
            fields.Add(("jobon_id", jobOnId));
        }

        return await client.PostAsync($"{PagePath}?handler=AbrirJobOn", HtmlFormHelpers.BuildFormPost(fields.ToArray()));
    }

    [Fact]
    public async Task NovoPeso_MostraAsDuasFormas_SemJobOnPorOmissao()
    {
        SeedUser("4311", "provider-peso-jobon-1", "PesoJobOn1!");
        using var client = await LoginClientAsync("4311", "PesoJobOn1!");

        var semJobOn = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("Sem Job On", semJobOn);
        Assert.Contains("Associar Job On", semJobOn);
        Assert.Contains("id=\"peso-tools-title\">Ferramentas<", semJobOn);
        Assert.DoesNotContain("id=\"peso-jobon-title\">Job On<", semJobOn);
        AssertModoNoCartao(semJobOn, "peso-tools-title", jobOn: false);

        var comJobOn = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon");
        Assert.Contains("id=\"peso-jobon-title\">Job On<", comJobOn);
        Assert.Contains("name=\"referencia\"", comJobOn);
        Assert.DoesNotContain("name=\"jobon_id\"", comJobOn);
        Assert.DoesNotContain("id=\"peso-tools-title\">Ferramentas<", comJobOn);
        AssertModoNoCartao(comJobOn, "peso-jobon-title", jobOn: true);

        var semProducoes = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=9999");
        Assert.Contains("Não há Job Ons da referência 9999.", semProducoes);
        // O aviso leva a criar o Job On, já com a referência (Diogo, 19/10/2026).
        Assert.Contains("<a class=\"dmo-aviso-link\" href=\"/JobOn?novo=1&amp;reference=9999#lightSheet\">Criar o Job On</a>", semProducoes);
    }

    [Fact]
    public async Task AssociarJobOn_AbrePeloControloId_ComAsFerramentasDoJobOn()
    {
        var jobOnId = await SeedJobOnAsync();
        SeedUser("4312", "provider-peso-jobon-2", "PesoJobOn2!");
        using var client = await LoginClientAsync("4312", "PesoJobOn2!");

        // Primeiro passo: a referência e a máquina; segundo: a produção.
        var escolha = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810&maquina=B1");
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", escolha);

        var outraMaquina = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810&maquina=B9");
        Assert.DoesNotContain("name=\"jobon_id\"", outraMaquina);

        var response = await PostAbrirJobOnAsync(client, jobOnId.ToString());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            controloId = await db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
            Assert.Equal(0, await db.Pesos.CountAsync());
        }

        Assert.Equal($"{PagePath}?controlo_id={controloId}", response.Headers.Location!.OriginalString);

        var producao = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("Produção J10", producao);
        Assert.Contains("Novo Peso da produção", producao);
        Assert.DoesNotContain("id=\"peso-tools-title\">Ferramentas<", producao);
    }

    [Theory]
    [InlineData(null, "sem-escolha")]
    [InlineData("999999", "inexistente")]
    public async Task AssociarJobOn_SemEscolhaOuInexistente_VoltaComRecusa(string? jobOnId, string recusa)
    {
        var existente = await SeedJobOnAsync();
        SeedUser("4313", "provider-peso-jobon-3", "PesoJobOn3!");
        using var client = await LoginClientAsync("4313", "PesoJobOn3!");

        var response = await PostAbrirJobOnAsync(client, jobOnId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{PagePath}?modo=jobon&jobon_refusal={recusa}", response.Headers.Location!.OriginalString);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("role=\"alert\"", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.Controlos.CountAsync(c => c.JobOnId == existente));
        Assert.Equal(1, await db.Controlos.CountAsync());
    }

    [Fact]
    public async Task JobOnIdSo_ComControlo_AbreOPesoDaProducao()
    {
        // Só o jobon_id (o «Verificar o Peso» do Job On, 18/10/2026): a folha
        // da produção abre pelo controlo_id dela, não o Novo Peso sem Job On.
        var jobOnId = await SeedJobOnAsync();
        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            controloId = await db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        }
        SeedUser("4314", "provider-peso-jobon-4", "PesoJobOn4!");
        using var client = await LoginClientAsync("4314", "PesoJobOn4!");

        using var response = await client.GetAsync($"{PagePath}?jobon_id={jobOnId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{PagePath}?controlo_id={controloId}", response.Headers.Location!.OriginalString);

        var producao = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("Produção J10", producao);
        Assert.DoesNotContain("id=\"peso-tools-title\">Ferramentas<", producao);
    }

    [Fact]
    public async Task JobOnIdSo_SemControlo_AbreOResumoDaProducao_SemCriarNadaNoPeso()
    {
        // Sem controlo ainda, o Resumo da produção (que o assegura e propõe
        // o Peso); a folha do Peso não escreve nada num GET.
        var jobOnId = await SeedJobOnAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Controlos.RemoveRange(db.Controlos);
            await db.SaveChangesAsync();
        }
        SeedUser("4315", "provider-peso-jobon-5", "PesoJobOn5!");
        using var client = await LoginClientAsync("4315", "PesoJobOn5!");

        using var response = await client.GetAsync($"{PagePath}?jobon_id={jobOnId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Controlo/Resumo?jobon_id={jobOnId}", response.Headers.Location!.OriginalString);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(0, await db.Controlos.CountAsync());
            Assert.Equal(0, await db.Pesos.CountAsync());
        }

        var resumo = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        int controloId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            controloId = await db.Controlos.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        }
        Assert.Contains($"<a class=\"dmo-button\" href=\"/Controlo/Peso?controlo_id={controloId}\" aria-label=\"Registar o Peso\">Registar</a>", resumo);
    }

    [Fact]
    public async Task AssociarJobOn_EscolherTrocaSoAsZonas_NumaLinha()
    {
        // Deep sweep 2 (18/10/2026, F-8 e F-9): cada referência ou máquina
        // recarregava a página inteira e a produção ia para uma segunda linha.
        // A procura fica (data-dmo-fica) e vai pelo DmoTroca; a produção é uma
        // zona na mesma linha, com o dropdown do formulário POST (form=), que
        // abre a folha como sempre (data-dmo-nativo).
        var jobOnId = await SeedJobOnAsync();
        SeedUser("4793", "provider-peso-jobon-6", "PesoJobOn6!");
        using var client = await LoginClientAsync("4793", "PesoJobOn6!");

        var vazio = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon");
        var procura = System.Text.RegularExpressions.Regex.Match(vazio, "<form [^>]*id=\"pesoJobOnProcura\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.Contains("class=\"dmo-filter-bar dmo-filter-bar-conteudo\"", procura);
        Assert.Contains("<div id=\"pesoJobOnProducaoZona\" class=\"dmo-field dmo-field-producao\" data-dmo-regiao hidden=\"hidden\">", procura);
        Assert.Matches("<div class=\"dmo-zona\" data-dmo-fica data-dmo-parcial>\\s*<form [^>]*id=\"pesoJobOnProcura\"", vazio);
        Assert.Matches("<form [^>]*id=\"pesoJobOnEscolha\" data-dmo-nativo[^>]*>", vazio);
        Assert.Contains("<div id=\"pesoJobOnAvisos\" class=\"dmo-zona\" data-dmo-regiao>", vazio);

        var escolha = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810");
        var comProducoes = System.Text.RegularExpressions.Regex.Match(escolha, "<form [^>]*id=\"pesoJobOnProcura\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.Contains("<select id=\"pesoJobOn\" name=\"jobon_id\" form=\"pesoJobOnEscolha\" required data-dmo-autosubmit=\"valor\" data-dmo-depende=\"pesoJobOnReferencia\" data-dmo-depende-valor=\"5810\">", comProducoes);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", comProducoes);

        var fora = PesoZonas.ForaDasZonas(vazio);
        foreach (var filtros in new[] { "referencia=5810", "referencia=5810&maquina=B1", "referencia=5810&maquina=B9", "referencia=9999", "jobon_refusal=sem-escolha", "referencia=5810&jobon_refusal=inexistente" })
        {
            var html = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&{filtros}");
            Assert.Equal(fora, PesoZonas.ForaDasZonas(html));
        }
        Assert.Contains("Escolha um Job On para associar o Peso.", PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&jobon_refusal=sem-escolha"), "pesoJobOnAvisos"));
        Assert.Contains("Não há Job Ons da referência 9999.", PesoZonas.Zona(await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=9999"), "pesoJobOnVazia"));
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
