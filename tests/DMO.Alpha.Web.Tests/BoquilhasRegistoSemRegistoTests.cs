using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Decisão do proprietário de 16/10/2026: abrir uma máquina nas Boquilhas
/// (?bq_id) já não cria o registo de reparação. A folha mostra o mesmo
/// estado (a produção, sem movimentos, o form do movimento) sem escrever
/// nada; o primeiro movimento cria o registo e o segundo reutiliza-o.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private async Task<int> CountTracesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.BqRepairTraces.AsNoTracking().CountAsync();
    }

    private async Task<HttpResponseMessage> PostMovementForMachineAsync(
        HttpClient client, int bqContextId, string movementType, int quantity)
    {
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?bq_id={bqContextId}");
        return await client.PostAsync(
            $"{PagePath}?handler=RegisterMovement",
            HtmlFormHelpers.BuildFormPost(
                ("bq_id", bqContextId.ToString()),
                ("movement_type", movementType),
                ("quantity", quantity.ToString()),
                ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task OpeningAMachine_DoesNotCreateTheTrace()
    {
        await SeedToolAsync("BQ-OPEN-1", "RGT951", "5");
        var bq = await SeedProductionAsync("BQ-OPEN-1", "ZQREF-51", "PN-551001", "B2");
        SeedUser("4501", "Abrir máquina", "provider-open-1", "OpenPass1!");
        using var client = await LoginClientAsync("4501", "OpenPass1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={bq.Id}");
        // Abrir duas vezes continua sem escrever.
        await GetPageHtmlAsync(client, $"{PagePath}?bq_id={bq.Id}");

        Assert.Equal(0, await CountTracesAsync());
        // O mesmo estado de antes: a produção, sem movimentos, o form do movimento.
        Assert.Contains("Registo — BQ RGT951 · Lote 5", html);
        // Os cartões do registo (Diogo, 17/10/2026): referência, BQ e lote,
        // produção e datas; sem «Máquinas compatíveis» no cabeçalho.
        Assert.Contains("<div class=\"dmo-fact\"><span>Referência</span><strong>ZQREF-51</strong></div>", html);
        Assert.Contains("<div class=\"dmo-fact\"><span>BQ · lote</span><strong>RGT951 · Lote 5</strong></div>", html);
        Assert.Contains("<div class=\"dmo-fact\"><span>Produção</span><strong>PN-551001</strong></div>", html);
        Assert.Contains("<span>Início</span>", html);
        Assert.Contains("<span>Fim</span>", html);
        var facts = html[html.IndexOf("id=\"bqRegisterFacts\"", StringComparison.Ordinal)..];
        Assert.DoesNotContain("compatíveis", facts[..facts.IndexOf("<span>Fim</span>", StringComparison.Ordinal)]);
        // Sem a pill «Registo em produção» (Diogo, 17/10/2026).
        Assert.DoesNotContain("Registo em produção", html);
        Assert.Contains("Ainda sem movimentos neste registo.", html);
        Assert.Contains("class=\"bq-movement-form\"", html);
        Assert.Contains($"<input type=\"hidden\" name=\"bq_id\" value=\"{bq.Id}\" />", html);
        Assert.DoesNotContain("name=\"trace_id\" value=\"0\"", html);
        Assert.DoesNotContain("Corrigir movimentos", html);
    }

    [Fact]
    public async Task Cartoes_InicioEFimSaoOsDoJobOn_SemInicioAvisoAmarelo()
    {
        // Diogo, 17/10/2026: o início e o fim do registo são os do Job On; sem
        // data de início no Job On, um aviso amarelo discreto e o fim fica «—».
        await SeedToolAsync("BQ-DATA-1", "RGT961", "1");
        var semData = await SeedProductionAsync("BQ-DATA-1", "ZQREF-61", "PN-561001", "B1");
        await SeedToolAsync("BQ-DATA-2", "RGT962", "1");
        var comData = await SeedProductionAsync("BQ-DATA-2", "ZQREF-62", "PN-561002", "B2");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = await db.JobOns.SingleAsync(j => j.ProductionNumber == "PN-561002");
            jobOn.InicioEm = new DateOnly(2026, 10, 12);
            await db.SaveChangesAsync();
        }
        SeedUser("4503", "Datas do registo", "provider-open-3", "OpenPass3!");
        using var client = await LoginClientAsync("4503", "OpenPass3!");

        var sem = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={semData.Id}");
        Assert.Contains("<div class=\"dmo-fact dmo-fact-falta\"><span>Início</span><strong>Sem data no Job On</strong></div>", sem);
        Assert.Contains("<div class=\"dmo-fact\"><span>Fim</span><strong>—</strong></div>", sem);
        Assert.DoesNotContain("Até aviso", sem);

        var com = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={comData.Id}");
        Assert.Contains("<div class=\"dmo-fact\"><span>Início</span><strong>12/10/2026</strong></div>", com);
        Assert.Contains("<div class=\"dmo-fact\"><span>Fim</span><strong>Até aviso</strong></div>", com);
        Assert.DoesNotContain("dmo-fact-falta", com);
    }

    [Fact]
    public async Task FirstMovement_CreatesTheTrace_AndTheSecondReusesIt()
    {
        await SeedToolAsync("BQ-OPEN-2", "RGT952", "6");
        var bq = await SeedProductionAsync("BQ-OPEN-2", "ZQREF-52", "PN-551002", "B3");
        SeedUser("4502", "Primeiro movimento", "provider-open-2", "OpenPass2!");
        using var client = await LoginClientAsync("4502", "OpenPass2!");

        var first = await PostMovementForMachineAsync(client, bq.Id, "saida", 4);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        BqRepairTrace trace;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            trace = await db.BqRepairTraces.AsNoTracking().SingleAsync();
        }
        Assert.Equal(bq.Id, trace.BqContextId);
        Assert.Null(trace.ToolId);
        Assert.Equal("RGT952", trace.ToolReferenceFrozen);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}", first.Headers.Location?.ToString());
        var movements = await ReadMovementsAsync(trace.Id);
        Assert.Single(movements);
        Assert.Equal(4, movements[0].Quantity);

        // Reaberta a máquina, a folha usa o registo criado.
        var reopened = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={bq.Id}");
        Assert.Contains($"<input type=\"hidden\" name=\"trace_id\" value=\"{trace.Id}\" />", reopened);
        Assert.DoesNotContain("name=\"bq_id\"", reopened);

        // O segundo movimento (pelo form do registo) reutiliza-o.
        var second = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 4);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        // Uma página antiga ainda com o bq_id também não cria um segundo.
        var third = await PostMovementForMachineAsync(client, bq.Id, "saida", 1);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}", third.Headers.Location?.ToString());

        Assert.Equal(1, await CountTracesAsync());
        Assert.Equal(3, (await ReadMovementsAsync(trace.Id)).Count);
    }

    [Fact]
    public async Task RefusedFirstMovement_ReturnsToTheMachine_WithoutCreatingTheTrace()
    {
        await SeedToolAsync("BQ-OPEN-3", "RGT953", "7");
        var bq = await SeedProductionAsync("BQ-OPEN-3", "ZQREF-53", "PN-551003", "B4");
        SeedUser("4503", "Recusa", "provider-open-3", "OpenPass3!");
        using var client = await LoginClientAsync("4503", "OpenPass3!");

        var response = await PostMovementForMachineAsync(client, bq.Id, "saida", 0);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Boquilhas?bq_id={bq.Id}&movement_refusal=InvalidQuantity", response.Headers.Location?.ToString());
        Assert.Equal(0, await CountTracesAsync());
    }

    [Fact]
    public async Task OpeningAMachine_WarnsAboutTheUnresolvedTraceOfTheSameBq()
    {
        await SeedToolAsync("BQ-OPEN-4", "RGT954", "8");
        var bq = await SeedProductionAsync("BQ-OPEN-4", "ZQREF-54", "PN-551004", "B5");
        var unresolved = await SeedTraceAsync("BQ-OPEN-4");
        await SeedMovementAsync(unresolved.Id, "saida", 2);
        SeedUser("4504", "Aviso", "provider-open-4", "OpenPass4!");
        using var client = await LoginClientAsync("4504", "OpenPass4!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={bq.Id}");

        Assert.Contains("Esta boquilha tem um registo sem Job On com 1 movimento(s).", html);
        Assert.Contains($"href=\"/Boquilhas?trace_id={unresolved.Id}\">Abrir o registo sem Job On</a>", html);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var trace = await db.BqRepairTraces.AsNoTracking().SingleAsync();
            Assert.Null(trace.BqContextId);
        }

        // O primeiro movimento desta produção associa o registo sem Job On.
        await PostMovementForMachineAsync(client, bq.Id, "entrada", 2);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var trace = await db.BqRepairTraces.AsNoTracking().SingleAsync();
            Assert.Equal(unresolved.Id, trace.Id);
            Assert.Equal(bq.Id, trace.BqContextId);
        }
        Assert.Equal(2, (await ReadMovementsAsync(unresolved.Id)).Count);
    }
}
