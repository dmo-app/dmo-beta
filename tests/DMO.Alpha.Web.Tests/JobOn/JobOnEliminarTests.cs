using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Duas regras do Diogo de 19/10/2026 na ficha do Job On:
/// «Eliminar Job On» com confirmação (D2 / AV-JO-04: a partir do dia de
/// início avisa que pode já ter produzido e o que se perde; nada bloqueia) e o
/// Controlo no Job On só para quem tem um módulo do Controlo («aparece a quem
/// tem modulos do controlo»).
/// </summary>
public class JobOnEliminarTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;

    public JobOnEliminarTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose() => ResetDomainState();

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = "EliminarPass1!";
        _factory.AuthProvider.AddAccount(email, "provider-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-eliminar-" + operatorId,
                Name = "Eliminar " + operatorId,
                Capabilities = capabilities
            };
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeador " + operatorId, ProviderUserId = "provider-" + operatorId };
            db.Templates.Add(template);
            db.Users.Add(user);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = await TokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token found on {path}.");
    }

    private static async Task<string> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private async Task<(int JobOnId, int MachineId)> SeedAsync(DateOnly? inicio, bool comPeso = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        db.Tools.Add(new Tool { ToolId = "CM-E", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "MF-E", Type = ToolType.Mf, Reference = "5447", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-E", Type = ToolType.Bq, Reference = "T173", Lot = "2" });
        var jobOn = new JobOn { Reference = "5447T173", ProductionNumber = "202610", Machine = b1, InicioEm = inicio };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-E" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-E" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-E" });
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        if (comPeso)
        {
            db.Pesos.Add(new DMO.Alpha.Core.Controlo.Peso
            {
                ControloId = controlo.Id, CmToolId = "CM-E", BqToolId = "BQ-E", Process = "NNPB",
                WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.5m
            });
            await db.SaveChangesAsync();
        }

        return (jobOn.Id, b1.Id);
    }

    private static string Dialog(string html)
    {
        var start = html.IndexOf("<dialog id=\"jobon-delete-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "a janela do Eliminar não está na ficha");
        return html[start..(html.IndexOf("</dialog>", start, StringComparison.Ordinal) + 9)];
    }

    [Fact]
    public async Task Eliminar_DepoisDoInicio_AvisaQuePodeTerProduzido_EDizOQueSePerde_EElimina()
    {
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var (jobOnId, _) = await SeedAsync(hoje.AddDays(-2), comPeso: true);
        using var client = await LoginAsync("7101", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);

        var ficha = await PageAsync(client, $"/JobOn?jobon_id={jobOnId}");
        Assert.Contains("data-dmo-dialog=\"jobon-delete-dialog\">Eliminar Job On</a>", ficha);
        var dialog = Dialog(ficha);
        Assert.Contains("Quer mesmo eliminar o Job On 5447T173 · nº 202610?", dialog);
        Assert.Contains($"O dia de início ({hoje.AddDays(-2):dd/MM/yyyy}) já chegou: esta produção pode já ter produzido.", dialog);
        Assert.Contains("Perde-se também: 1 Peso.", dialog);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(ficha)!;
        var response = await client.PostAsync("/JobOn/Eliminar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var destino = response.Headers.Location!.OriginalString;
        Assert.Contains("eliminado=1", destino);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.False(await db.JobOns.AsNoTracking().AnyAsync(j => j.Id == jobOnId));
            Assert.Empty(await db.Pesos.AsNoTracking().ToListAsync());
            Assert.Equal(3, await db.Tools.AsNoTracking().CountAsync());
        }

        Assert.Contains("Job On eliminado.", await PageAsync(client, destino));

        // O mesmo envio outra vez (duplo clique): a produção já não existe e
        // volta ao calendário da mesma maneira, sem erro.
        var repetido = await client.PostAsync("/JobOn/Eliminar", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, repetido.StatusCode);
        Assert.Equal(destino, repetido.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Eliminar_AntesDoInicio_SemRegistos_SoPergunta()
    {
        var (jobOnId, _) = await SeedAsync(DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(1));
        using var client = await LoginAsync("7102", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);

        var dialog = Dialog(await PageAsync(client, $"/JobOn?jobon_id={jobOnId}"));

        Assert.Contains("Quer mesmo eliminar o Job On 5447T173 · nº 202610?", dialog);
        Assert.DoesNotContain("data-jobon-eliminar=\"ja-produziu\"", dialog);
        Assert.DoesNotContain("data-jobon-eliminar=\"perde\"", dialog);
    }

    [Fact]
    public async Task SemModuloDoControlo_AFichaNaoMostraOControlo_NemContaOsRegistosDele()
    {
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var (jobOnId, _) = await SeedAsync(hoje, comPeso: true);
        using var client = await LoginAsync("7103", DmoCapabilities.JobOnCreate);

        var ficha = await PageAsync(client, $"/JobOn?jobon_id={jobOnId}");

        Assert.Contains("data-dmo-dialog=\"jobon-delete-dialog\">Eliminar Job On</a>", ficha);
        Assert.DoesNotContain($"/Controlo/Resumo?jobon_id={jobOnId}", ficha);
        var dialog = Dialog(ficha);
        // O próprio dia de início já conta (a partir do dia de início).
        Assert.Contains("já chegou: esta produção pode já ter produzido.", dialog);
        Assert.Contains("Perde-se também: os registos do Controlo desta produção.", dialog);
        Assert.DoesNotContain("1 Peso", dialog);
    }

    [Theory]
    [InlineData(DmoCapabilities.ControloCreate)]
    [InlineData(DmoCapabilities.ControloApprove)]
    public async Task ComUmModuloDoControlo_AFichaMostraOControlo(string modulo)
    {
        var (jobOnId, _) = await SeedAsync(null);
        using var client = await LoginAsync(modulo == DmoCapabilities.ControloCreate ? "7104" : "7105", DmoCapabilities.JobOnCreate, modulo);

        var ficha = await PageAsync(client, $"/JobOn?jobon_id={jobOnId}");

        Assert.Contains($"href=\"/Controlo/Resumo?jobon_id={jobOnId}\">Controlo</a>", ficha);
    }
}
