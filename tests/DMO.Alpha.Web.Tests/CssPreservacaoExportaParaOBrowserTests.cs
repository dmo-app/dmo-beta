using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Exportador do HTML real do Sumário para a guarda de preservação do CSS
/// (PLANO-CSS passo 1, A-0452). Os dados são sintéticos e vivem só na base
/// InMemory de uma fábrica nova por cenário; nenhuma base oficial é tocada.
/// </summary>
public sealed class CssPreservacaoExportaParaOBrowserTests
{
    private const string PagePath = "/Planeamento/Sumario";

    private static DateOnly Hoje => DateOnly.FromDateTime(DmoBusinessTime.Now);

    private static async Task<HttpClient> LoginAsync(CustomWebApplicationFactory factory, string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"CssPreservacao{operatorId}!";
        factory.AuthProvider.AddAccount(email, $"provider-csspreservacao-{operatorId}", password, email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-csspreservacao-{operatorId}", Name = $"Preservação CSS {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-csspreservacao-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<int> JobOnAsync(CustomWebApplicationFactory factory, string reference, DateOnly? inicio, DateTime? criadoEmUtc = null, string maquina = "B1")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Code == maquina);
        if (machine is null)
        {
            machine = new Machine { Code = maquina };
            db.Machines.Add(machine);
        }

        var jobOn = new JobOn { Reference = reference, ProductionNumber = "7", Machine = machine, InicioEm = inicio, CriadoEmUtc = criadoEmUtc };
        db.JobOns.Add(jobOn);
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    private static async Task<string> PaginaAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"{PagePath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task ExportaOSumarioComDiaPopulado()
    {
        using var factory = new CustomWebApplicationFactory();
        await JobOnAsync(factory, "S1A001", Hoje);
        var comAlteracao = await JobOnAsync(factory, "S1A002", Hoje.AddDays(-1));
        await JobOnAsync(factory, "S1A003", Hoje.AddDays(-2));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = comAlteracao, Field = JobOnChangeFields.Machine, OldValueFrozen = "B1", NewValueFrozen = "B2",
                ChangedByNameFrozen = "Operador Teste", ChangedAtUtc = DateTime.UtcNow.AddHours(-2)
            });
            db.BqMovementDeletions.Add(new BqMovementDeletion
            {
                BqMovementId = 77, BqRepairTraceId = 7777, Type = BqMovementType.Saida, Quantity = 3,
                OccurredAtUtc = DateTime.UtcNow, MachineCodeFrozen = "C2",
                DeletedByNameFrozen = "Operador Teste", DeletedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = await LoginAsync(factory, "9601",
            DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove, DmoCapabilities.Boquilhas);
        ExportarParaOBrowser.Gravar("css-preservacao-sumario-populado.html", await PaginaAsync(client));
    }

    [Fact]
    public async Task ExportaOSumarioComListaLonga()
    {
        using var factory = new CustomWebApplicationFactory();
        for (var i = 0; i < 28; i++)
        {
            await JobOnAsync(factory, $"L{i:000}A", Hoje.AddDays(-(i % 7)));
        }

        using var client = await LoginAsync(factory, "9602", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);
        ExportarParaOBrowser.Gravar("css-preservacao-sumario-longo.html", await PaginaAsync(client, "?dias=7"));
    }

    [Fact]
    public async Task ExportaOSumarioVazio()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = await LoginAsync(factory, "9603", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove);
        ExportarParaOBrowser.Gravar("css-preservacao-sumario-vazio.html", await PaginaAsync(client));
    }
}
