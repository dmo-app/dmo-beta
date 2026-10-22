using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A densidade do vidro NNPB e PS nas Definições do Controlo (o Diogo,
/// 19/10/2026, D15: «nas Definições deve haver densidade de vidro NNPB e PS,
/// e campos para alterar»). Fora do habitual grava com aviso; só o que não é
/// um número maior que zero fica por gravar, e então nenhum dos dois grava.
/// </summary>
public sealed class ControloDefinicoesDensidadeTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PagePath = "/Controlo/Definicoes";
    private readonly CustomWebApplicationFactory _factory;

    public ControloDefinicoesDensidadeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.SaveChanges();
        db.GlassDensities.Add(new DMO.Alpha.Core.Controlo.GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4027m });
        db.GlassDensities.Add(new DMO.Alpha.Core.Controlo.GlassDensity { Process = CmProcess.Ps, DensityGramsPerCm3 = 2.4231m });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Densidade{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-densidade-{operatorId}", Name = $"Densidade {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email), ("password", password), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await DefinicoesPin.DesbloquearAsync(_factory.Services, client);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string nnpb, string ps)
    {
        var html = await GetAsync(client, "/Definicoes");
        return await client.PostAsync($"{PagePath}?handler=SaveGlassDensity", HtmlFormHelpers.BuildFormPost(
            ("densidade_nnpb", nnpb), ("densidade_ps", ps),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
    }

    private Dictionary<CmProcess, decimal> Guardadas()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DmoDbContext>().GlassDensities.AsNoTracking()
            .ToDictionary(g => g.Process, g => g.DensityGramsPerCm3);
    }

    private static string Cartao(string html)
    {
        var inicio = html.IndexOf("id=\"ctlDefDensidade\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "O cartão da densidade não está na página.");
        return html[inicio..];
    }

    [Fact]
    public async Task ControloCriar_VeAsDuasDensidades_EGuardarMudaAsDuas()
    {
        using var client = await LoginOperatorAsync("4441", DmoCapabilities.ControloCreate);

        var cartao = Cartao(await GetAsync(client, "/Definicoes"));
        Assert.Contains("Densidade do vidro", cartao);
        Assert.Contains("name=\"densidade_nnpb\" inputmode=\"decimal\" size=\"8\" value=\"2.4027\"", cartao);
        Assert.Contains("name=\"densidade_ps\" inputmode=\"decimal\" size=\"8\" value=\"2.4231\"", cartao);

        var response = await PostAsync(client, "2,4100", "2.4300");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Definicoes?densidade=ok&secao=controlo", response.Headers.Location!.ToString());
        Assert.Equal(2.41m, Guardadas()[CmProcess.Nnpb]);
        Assert.Equal(2.43m, Guardadas()[CmProcess.Ps]);
        var depois = Cartao(await GetAsync(client, response.Headers.Location!.ToString()));
        Assert.Contains("Densidade do vidro guardada. Vale para os próximos Pesos; os Pesos já gravados ficam com a que usaram.", depois);
        Assert.Contains("value=\"2.4100\"", depois);
    }

    [Theory]
    [InlineData("4442", "abc", "2.4231")]
    [InlineData("4443", "2.4027", "0")]
    [InlineData("4444", "", "2.4231")]
    public async Task NaoENumeroMaiorQueZero_NenhumaGrava(string operatorId, string nnpb, string ps)
    {
        using var client = await LoginOperatorAsync(operatorId, DmoCapabilities.ControloCreate);

        var response = await PostAsync(client, nnpb, ps);

        Assert.Equal("/Definicoes?densidade=invalida&secao=controlo", response.Headers.Location!.ToString());
        Assert.Equal(2.4027m, Guardadas()[CmProcess.Nnpb]);
        Assert.Equal(2.4231m, Guardadas()[CmProcess.Ps]);
        var cartao = Cartao(await GetAsync(client, response.Headers.Location!.ToString()));
        Assert.Contains("data-dmo-toast=\"erro\"", cartao);
        Assert.Contains("Não foi guardada.", cartao);
    }

    [Fact]
    public async Task ForaDoHabitual_GravaComAviso()
    {
        using var client = await LoginOperatorAsync("4445", DmoCapabilities.ControloCreate);

        var response = await PostAsync(client, "3.5", "2.42314");

        Assert.Equal("/Definicoes?densidade=aviso-arredondada-ps-fora-nnpb&secao=controlo", response.Headers.Location!.ToString());
        Assert.Equal(3.5m, Guardadas()[CmProcess.Nnpb]);
        Assert.Equal(2.4231m, Guardadas()[CmProcess.Ps]);
        var cartao = Cartao(await GetAsync(client, response.Headers.Location!.ToString()));
        Assert.Contains("data-dmo-toast=\"aviso\"", cartao);
        Assert.Contains("NNPB 3.5000 g/cm³, fora do habitual; PS 2.4231 g/cm³, arredondada a quatro casas decimais (o habitual é de 2.0000 a 3.0000 g/cm³).", cartao);
    }

    [Fact]
    public async Task SemControloCriar_NaoGrava()
    {
        using var client = await LoginOperatorAsync("4446", DmoCapabilities.Boquilhas);

        var response = await client.PostAsync($"{PagePath}?handler=SaveGlassDensity", HtmlFormHelpers.BuildFormPost(
            ("densidade_nnpb", "2.9"), ("densidade_ps", "2.9")));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2.4027m, Guardadas()[CmProcess.Nnpb]);
    }
}
