using System.Globalization;
using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Boquilhas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A data e a hora reais no futuro (Diogo, 19/10/2026: «as datas têm de ser
/// livres»; «só coisas mesmo complicadas é que se dá block, o resto
/// warnings»). Na beta.241 (BD-8 do segundo deep sweep) era recusado; agora
/// registar ou corrigir assim grava e a página avisa no canto, para se
/// corrigir se a data estiver errada. A folga de
/// <see cref="BqMovementOccurrence.FutureToleranceMinutes"/> minutos cobre o
/// relógio do posto adiantado e não avisa; as datas passadas gravam sem aviso.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    /// <summary>O aviso no canto depois de gravar um movimento com a data no futuro.</summary>
    private static string FutureWarningToast(DateTime local) =>
        "<div data-dmo-toast=\"aviso\" role=\"status\">A data e a hora do movimento (" +
        local.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-PT")) + ") estão no futuro. " +
        "O movimento foi gravado. Corrija o movimento se a data real for outra.";

    /// <summary>A hora local da fábrica no formato do campo datetime-local.</summary>
    private static string LocalField(DateTime local) =>
        local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    [Fact]
    public async Task PostMovement_RealDateInTheFuture_IsSaved_AndWarnsInTheCorner()
    {
        await SeedToolAsync("BQ-FUTURO-1", "RGT941", "1");
        var trace = await SeedTraceAsync("BQ-FUTURO-1");
        SeedUser("4741", "Registo Futuro", "provider-futuro-1", "FuturoPass1!");
        using var client = await LoginClientAsync("4741", "FuturoPass1!");

        // Amanhã à mesma hora, em hora de Lisboa (o que o campo envia).
        var typedLocal = DmoBusinessTime.Now.AddDays(1);
        var typed = LocalField(typedLocal);
        var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 4, typed);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.DoesNotContain("movement_refusal", location);
        Assert.Contains("data_no_futuro=", location);

        // Gravado com a data que se escreveu.
        var saved = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(BqMovementType.Entrada, saved.Type);
        Assert.Equal(4, saved.Quantity);
        Assert.Equal(typed, LocalField(DmoBusinessTime.ToLocal(saved.OccurredAtUtc)));

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains(FutureWarningToast(DateTime.ParseExact(typed, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)), html);
        Assert.DoesNotContain("data-dmo-toast=\"erro\"", html);
        // O aviso leva à correção dos movimentos (Diogo, 19/10/2026).
        Assert.Contains($"outra. <a class=\"dmo-aviso-link\" href=\"/Boquilhas?trace_id={trace.Id}&amp;editar_mov=1", html);
        Assert.Contains("#bqLatest\">Corrigir movimentos</a></div>", html);
    }

    [Fact]
    public async Task PostMovement_RealDateWithinTheTolerance_OrInThePast_IsSaved_WithoutWarning()
    {
        await SeedToolAsync("BQ-FUTURO-2", "RGT942", "1");
        var trace = await SeedTraceAsync("BQ-FUTURO-2");
        SeedUser("4742", "Registo Futuro", "provider-futuro-2", "FuturoPass2!");
        using var client = await LoginClientAsync("4742", "FuturoPass2!");

        // O relógio do posto 5 minutos adiantado: dentro da folga, grava sem aviso.
        var nearlyNow = DmoBusinessTime.Now.AddMinutes(5);
        var withinTolerance = await PostMovementAsync(
            client, TraceUrl(trace.Id), trace.Id, "saida", 2, LocalField(nearlyNow));
        Assert.Equal(HttpStatusCode.Redirect, withinTolerance.StatusCode);
        Assert.DoesNotContain("movement_refusal", withinTolerance.Headers.Location?.OriginalString ?? string.Empty);
        Assert.DoesNotContain("data_no_futuro", withinTolerance.Headers.Location?.OriginalString ?? string.Empty);

        // Uma guia lançada com meses de atraso: grava com a data real, sem aviso.
        var monthsAgo = new DateTime(2026, 7, 15, 9, 15, 0);
        var past = await PostMovementAsync(
            client, TraceUrl(trace.Id), trace.Id, "entrada", 2, LocalField(monthsAgo));
        Assert.Equal(HttpStatusCode.Redirect, past.StatusCode);
        Assert.DoesNotContain("movement_refusal", past.Headers.Location?.OriginalString ?? string.Empty);
        Assert.DoesNotContain("data_no_futuro", past.Headers.Location?.OriginalString ?? string.Empty);

        var movements = await ReadMovementsAsync(trace.Id);
        Assert.Equal(2, movements.Count);
        Assert.Equal(LocalField(nearlyNow), LocalField(DmoBusinessTime.ToLocal(movements[0].OccurredAtUtc)));
        Assert.Equal(LocalField(monthsAgo), LocalField(DmoBusinessTime.ToLocal(movements[1].OccurredAtUtc)));
    }

    [Fact]
    public async Task EditMovement_RealDateInTheFuture_IsSaved_AndWarnsInTheCorner()
    {
        await SeedToolAsync("BQ-FUTURO-3", "RGT943", "1");
        var trace = await SeedTraceAsync("BQ-FUTURO-3");
        var recordedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var movementId = await SeedDatedMovementAsync(trace.Id, BqMovementType.Saida, 3, recordedAt);
        SeedUser("4743", "Correção Futuro", "provider-futuro-3", "FuturoPass3!");
        using var client = await LoginClientAsync("4743", "FuturoPass3!");

        var typed = LocalField(DmoBusinessTime.Now.AddDays(2));
        var response = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "entrada"),
            ("quantity", "5"),
            ("occurred_at", typed));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.DoesNotContain("edit_refusal", location);
        Assert.Contains("data_no_futuro=", location);

        // A correção gravou, com a data no futuro.
        var saved = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(BqMovementType.Entrada, saved.Type);
        Assert.Equal(5, saved.Quantity);
        Assert.Equal(typed, LocalField(DmoBusinessTime.ToLocal(saved.OccurredAtUtc)));
        Assert.NotNull(saved.CorrectedAtUtc);

        var html = await GetPageHtmlAsync(client, location);
        Assert.Contains(FutureWarningToast(DateTime.ParseExact(typed, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)), html);
        Assert.DoesNotContain("data-dmo-toast=\"erro\"", html);

        // Corrigir outra vez para uma data passada grava sem aviso.
        var pastLocal = new DateTime(2026, 9, 2, 10, 30, 0);
        var corrected = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "entrada"),
            ("quantity", "5"),
            ("occurred_at", LocalField(pastLocal)));
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}&editar_mov=1", corrected.Headers.Location?.ToString());
        Assert.Equal(DmoBusinessTime.ToUtc(pastLocal), Assert.Single(await ReadMovementsAsync(trace.Id)).OccurredAtUtc);
    }

    /// <summary>
    /// O fuso dos dois lados (revisão do coordenador, 18/10/2026): o campo
    /// envia a hora de Lisboa e o servidor compara em UTC. No verão (UTC+1)
    /// comparar a hora local com o UTC avisava em todos os movimentos do
    /// momento; no inverno (UTC+0) não se notava. Com o relógio do servidor
    /// fixo, a hora de agora em Lisboa e 9 minutos à frente gravam sem aviso e
    /// 11 minutos à frente grava com aviso, ao registar e ao corrigir, nas
    /// duas épocas.
    /// </summary>
    [Theory]
    [InlineData("4744", "2026-10-19T07:30:00Z")] // segunda 19/10, verão: 08:30 em Lisboa (UTC+1)
    [InlineData("4745", "2026-07-15T22:55:00Z")] // verão, perto da meia-noite: 23:55 em Lisboa
    [InlineData("4746", "2026-11-02T07:30:00Z")] // inverno: 07:30 em Lisboa (UTC+0)
    public async Task RealDate_TheFieldIsLisbonTime_TheServerComparesInUtc_SummerAndWinter(string operatorId, string serverNowUtc)
    {
        var now = DateTime.Parse(serverNowUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var lisbonNow = DmoBusinessTime.ToLocal(now);
        var toolId = "BQ-FUSO-" + operatorId;
        await SeedToolAsync(toolId, "RGT" + operatorId[^3..], "1");
        var trace = await SeedTraceAsync(toolId);
        SeedUser(operatorId, "Registo Fuso", "provider-fuso-" + operatorId, "FusoPass1!");
        using var factory = FactoryWithServerClock(now);
        using var client = await LoginClientAsync(factory, operatorId, "FusoPass1!");

        // Registar: o agora de Lisboa (o que o campo traz preenchido) e o
        // relógio do posto 9 minutos adiantado gravam sem aviso; 11 minutos
        // grava com aviso.
        foreach (var minutes in new[] { 0, 9 })
        {
            var saved = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 1,
                LocalField(lisbonNow.AddMinutes(minutes)));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.DoesNotContain("movement_refusal", saved.Headers.Location?.OriginalString ?? string.Empty);
            Assert.DoesNotContain("data_no_futuro", saved.Headers.Location?.OriginalString ?? string.Empty);
        }

        var warned = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 1,
            LocalField(lisbonNow.AddMinutes(11)));
        Assert.Contains("data_no_futuro=", warned.Headers.Location?.OriginalString ?? string.Empty);

        // Gravados no instante UTC certo: a hora de Lisboa menos o fuso da época.
        var movements = await ReadMovementsAsync(trace.Id);
        Assert.Equal(new[] { now, now.AddMinutes(9), now.AddMinutes(11) }, movements.Select(m => m.OccurredAtUtc));

        // Corrigir: 11 minutos à frente grava com aviso; 9 minutos grava sem aviso.
        var movementId = movements[0].Id;
        var warnedCorrection = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "saida"),
            ("quantity", "1"),
            ("occurred_at", LocalField(lisbonNow.AddMinutes(11))));
        Assert.Contains("data_no_futuro=", warnedCorrection.Headers.Location?.OriginalString ?? string.Empty);
        Assert.Equal(now.AddMinutes(11), (await ReadMovementsAsync(trace.Id)).Single(m => m.Id == movementId).OccurredAtUtc);

        var correction = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "saida"),
            ("quantity", "1"),
            ("occurred_at", LocalField(lisbonNow.AddMinutes(9))));
        Assert.DoesNotContain("edit_refusal", correction.Headers.Location?.OriginalString ?? string.Empty);
        Assert.DoesNotContain("data_no_futuro", correction.Headers.Location?.OriginalString ?? string.Empty);
        Assert.Equal(now.AddMinutes(9), (await ReadMovementsAsync(trace.Id)).Single(m => m.Id == movementId).OccurredAtUtc);
    }

    /// <summary>A app com o relógio do servidor fixo no registo e na correção dos movimentos.</summary>
    private WebApplicationFactory<Program> FactoryWithServerClock(DateTime utcNow)
    {
        var clock = new FixedClock(utcNow);
        return _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IRegisterBqMovementHandler>(provider =>
                ActivatorUtilities.CreateInstance<RegisterBqMovementHandler>(provider, clock));
            services.AddScoped<IEditBqMovementHandler>(provider =>
                ActivatorUtilities.CreateInstance<EditBqMovementHandler>(provider, clock));
        }));
    }

    private async Task<HttpClient> LoginClientAsync(WebApplicationFactory<Program> factory, string identifier, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = await GetVerificationTokenAsync(client, "/login");
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identifier), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
