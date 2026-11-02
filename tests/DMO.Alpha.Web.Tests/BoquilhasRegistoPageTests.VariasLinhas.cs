using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

// VÁRIAS LINHAS DE UMA VEZ (Diogo, 02/11/2026, DMO-721): escolher vários
// cartões das linhas e registar tudo de uma vez, cada linha no registo da
// produção que está nela, com a mesma data e hora.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task RegisterMany_RecordsEachLineOnItsOwnProduction_WithTheSameTime()
    {
        await SeedToolAsync("BQ-VAR-1", "RVA001", "1");
        await SeedToolAsync("BQ-VAR-2", "RVA002", "2");
        var b1 = await SeedProductionAsync("BQ-VAR-1", "8001T001", "202691", "B1");
        var c2 = await SeedProductionAsync("BQ-VAR-2", "8002T002", "202692", "C2");
        SeedUser("4191", "Registo Varias", "provider-registo-91", "RegistoPass91!");
        using var client = await LoginClientAsync("4191", "RegistoPass91!");

        var html = await GetPageHtmlAsync(client, PagePath);
        Assert.Contains("id=\"bqVariasLigar\"", html);
        Assert.Contains("id=\"bqVarias\"", html);

        var token = await GetVerificationTokenAsync(client, PagePath);
        var response = await client.PostAsync($"{PagePath}?handler=RegisterMany", HtmlFormHelpers.BuildFormPost(
            ("linha_bq_id", b1.Id.ToString()), ("linha_tipo", "saida"), ("linha_qtd", "4"), ("linha_qtd_an", ""),
            ("linha_bq_id", c2.Id.ToString()), ("linha_tipo", "entrada"), ("linha_qtd", "2"), ("linha_qtd_an", "1"),
            ("occurred_at", "2026-10-20T14:30"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("varias_ok=B1", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("varias_falhou", response.Headers.Location!.OriginalString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var traces = await db.BqRepairTraces.AsNoTracking()
            .Where(t => t.BqContextId == b1.Id || t.BqContextId == c2.Id).ToListAsync();
        Assert.Equal(2, traces.Count);
        var movimentos = await db.BqMovements.AsNoTracking()
            .Where(m => traces.Select(t => t.Id).Contains(m.BqRepairTraceId)).ToListAsync();
        Assert.Equal(2, movimentos.Count);
        Assert.Single(movimentos.Select(m => m.OccurredAtUtc).Distinct());
        Assert.Contains(movimentos, m => m.Quantity == 4 && m.AnQuantity == null);
        Assert.Contains(movimentos, m => m.Quantity == 2 && m.AnQuantity == 1);
    }

    [Fact]
    public async Task RegisterMany_ALineWithoutQuantity_DoesNotStopTheOthers()
    {
        await SeedToolAsync("BQ-VAR-3", "RVA003", "3");
        await SeedToolAsync("BQ-VAR-4", "RVA004", "4");
        var b2 = await SeedProductionAsync("BQ-VAR-3", "8003T003", "202693", "B2");
        var c3 = await SeedProductionAsync("BQ-VAR-4", "8004T004", "202694", "C3");
        SeedUser("4192", "Registo Varias 2", "provider-registo-92", "RegistoPass92!");
        using var client = await LoginClientAsync("4192", "RegistoPass92!");

        var token = await GetVerificationTokenAsync(client, PagePath);
        var response = await client.PostAsync($"{PagePath}?handler=RegisterMany", HtmlFormHelpers.BuildFormPost(
            ("linha_bq_id", b2.Id.ToString()), ("linha_tipo", "saida"), ("linha_qtd", ""), ("linha_qtd_an", ""),
            ("linha_bq_id", c3.Id.ToString()), ("linha_tipo", "saida"), ("linha_qtd", "3"), ("linha_qtd_an", ""),
            ("__RequestVerificationToken", token)));

        var destino = Uri.UnescapeDataString(response.Headers.Location!.OriginalString);
        Assert.Contains("varias_ok=C3", destino);
        Assert.Contains("varias_falhou=B2", destino);
        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("Não ficou registado em B2.", html);
    }
}
