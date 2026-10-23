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

public sealed class PesoFolhaCarregamentoPageTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();
    private const string Password = "Local-Peso-Test-Only!";

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task AbrirProducao_MostraCriacaoEAssociacaoSemGravar()
    {
        var data = await SeedAsync(DmoCapabilities.ControloCreate);
        using var client = await LoginAsync(_factory);
        using var response = await client.GetAsync($"/Controlo/Peso?controlo_id={data.ControloId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var candidates = Region(html, "table", "pesoAssociateList");
        Assert.Contains($"Peso {data.CandidateId}", WebUtility.HtmlDecode(candidates));
        Assert.Contains("5448 · Lote 2", WebUtility.HtmlDecode(candidates));
        var form = Region(html, "form", "pesoCreateForControlo");
        var submitted = EnvioDoFormulario.Preencher(html, ("waterTemperature", "20,4"));
        Assert.Contains(("controlo_id", data.ControloId.ToString()), submitted.Campos);
        Assert.Contains("2.4", WebUtility.HtmlDecode(form));
        await AssertUnchangedAsync(data);
    }

    [Fact]
    public async Task RecusaDeCriacao_PreservaATemperaturaEscritaEAMensagem()
    {
        var data = await SeedAsync(DmoCapabilities.ControloCreate);
        using var client = await LoginAsync(_factory);
        using var response = await client.GetAsync($"/Controlo/Peso?controlo_id={data.ControloId}"
            + "&production_refusal=UnreadableWaterTemperature&echo_temperature=abc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal("abc", InputValue(Region(html, "form", "pesoCreateForControlo"), "waterTemperature"));
        Assert.Contains("A temperatura da água está vazia ou ilegível", WebUtility.HtmlDecode(Region(html, "div", "pesoProducaoAvisos")));
        await AssertUnchangedAsync(data);
    }

    [Fact]
    public async Task SemSessaoOuSemModulo_NaoExecutaOCarregamentoDaFolha()
    {
        var data = await SeedAsync(DmoCapabilities.Ferramentas);
        var sentinel = new UnavailableSheet();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IPesoFolhaQuery>(sentinel)));
        using var anonymous = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var challenge = await anonymous.GetAsync($"/Controlo/Peso?controlo_id={data.ControloId}");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Equal("/login", new Uri(anonymous.BaseAddress!, challenge.Headers.Location!).AbsolutePath);
        using var client = await LoginAsync(app);
        using var denied = await client.GetAsync($"/Controlo/Peso?controlo_id={data.ControloId}");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal(DmoCapabilities.HomeFor(new[] { DmoCapabilities.Ferramentas }), denied.Headers.Location!.ToString());
        Assert.Equal(0, sentinel.Calls);
        await AssertUnchangedAsync(data);
    }

    private sealed record Seed(int ControloId, int JobOnId, int CandidateId);

    private async Task<Seed> SeedAsync(string capability)
    {
        _factory.AuthProvider.AddAccount("7801@dmo.local", "peso-pr1-user", Password, "7801@dmo.local");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = new Template { TemplateKey = "peso-pr1", Name = "Peso PR1", Capabilities = new[] { capability } };
        var user = new User { Id = Guid.NewGuid(), OperatorId = "7801", Name = "Peso PR1", ProviderUserId = "peso-pr1-user" };
        db.Templates.Add(template);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        var production = new JobOn
        {
            Machine = new Machine { Code = "B1" }, Reference = "5447T173", ProductionNumber = "202605", CaloteMm = 9m
        };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = production };
        db.Controlos.Add(controlo);
        db.Tools.AddRange(
            new Tool { ToolId = "CM", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" });
        db.CmContexts.Add(new CmContext { JobOn = production, ToolId = "CM" });
        db.BqContexts.Add(new BqContext { JobOn = production, ToolId = "BQ" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = "CM", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 7m, TpDiametroMm = 50m, TpSagittaMm = 8m },
            new ToolTechnicalContext { ToolId = "BQ", VolumeMarisaCm3 = 12m });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4m });
        var candidate = new Peso
        {
            CmToolId = "CM", BqToolId = "BQ", Process = "NNPB", WaterTemperatureC = 20,
            WaterDensityGramsPerCm3 = 0.9982m, GlassDensityGramsPerCm3 = 2.4m,
            VolumePuncaoCm3 = 7m, VolumeMarisaCm3 = 12m
        };
        db.Pesos.Add(candidate);
        await db.SaveChangesAsync();
        return new Seed(controlo.Id, production.Id, candidate.Id);
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var login = await client.GetAsync("/login");
        login.EnsureSuccessStatusCode();
        var html = await login.Content.ReadAsStringAsync();
        var form = Regex.Match(html, "<form\\b(?<attrs>[^>]*\\bdata-native-login[^>]*)>");
        Assert.True(form.Success, "Expected the rendered native login form.");
        // A form without action submits to its document URL. Make that browser
        // default explicit for the shared helper; keep every rendered field.
        if (!Regex.IsMatch(form.Groups["attrs"].Value, "\\baction="))
        {
            html = html.Insert(form.Index + "<form".Length,
                $" action=\"{WebUtility.HtmlEncode(login.RequestMessage!.RequestUri!.PathAndQuery)}\"");
        }
        var submitted = EnvioDoFormulario.Preencher(html, ("identifier", "7801"), ("password", Password));
        using var response = await client.PostAsync(submitted.Acao, EnvioDoFormulario.Corpo(submitted.Campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task AssertUnchangedAsync(Seed data)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var peso = await db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(data.CandidateId, peso.Id);
        Assert.Null(peso.ControloId);
        Assert.Equal(7m, peso.VolumePuncaoCm3);
        Assert.Empty(await db.PesoMeasurements.AsNoTracking().ToListAsync());
        Assert.Equal(9m, await db.JobOns.AsNoTracking().Where(j => j.Id == data.JobOnId).Select(j => j.CaloteMm).SingleAsync());
        Assert.Equal(8m, await db.ToolTechnicalContexts.AsNoTracking().Where(t => t.ToolId == "CM").Select(t => t.TpSagittaMm).SingleAsync());
    }

    private static string Region(string html, string tag, string id)
    {
        var match = Regex.Match(html, $"<{tag}\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>(?<body>[\\s\\S]*?)</{tag}>");
        Assert.True(match.Success, $"Expected region #{id}.");
        return match.Value;
    }

    private static string InputValue(string html, string id)
    {
        var input = Regex.Match(html, $"<input\\b(?=[^>]*\\bid=\"{Regex.Escape(id)}\")(?<attrs>[^>]*)>");
        Assert.True(input.Success, $"Expected input #{id}.");
        var value = Regex.Match(input.Groups["attrs"].Value, "\\bvalue=\"(?<value>[^\"]*)\"");
        Assert.True(value.Success, $"Expected value for #{id}.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private sealed class UnavailableSheet : IPesoFolhaQuery
    {
        public int Calls { get; private set; }
        public Task<PesoFolhaInicial> CarregarAsync(int controloId, int? pesoPedidoId, decimal? caloteSimulada,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Unauthorized requests must not load the sheet.");
        }
    }
}
