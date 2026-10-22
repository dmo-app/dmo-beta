using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A correção dos movimentos no registo: o modo de edição só aparece a pedido,
/// o duplo clique abre o movimento, corrigir muda o tipo e apagar qualquer movimento.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private string EditModeUrl(int traceId, int? movementId = null) =>
        $"{PagePath}?trace_id={traceId}&editar_mov=1" + (movementId is int id ? $"&mov_id={id}" : string.Empty);

    private async Task<int> SeedDatedMovementAsync(int traceId, BqMovementType type, int quantity, DateTime occurredAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var movement = new BqMovement
        {
            BqRepairTraceId = traceId,
            Type = type,
            Quantity = quantity,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = occurredAtUtc
        };
        db.BqMovements.Add(movement);
        await db.SaveChangesAsync();
        return movement.Id;
    }

    private async Task<HttpResponseMessage> PostCorrectionAsync(
        HttpClient client, string handler, string tokenPath, params (string name, string value)[] fields)
    {
        var token = await GetVerificationTokenAsync(client, tokenPath);
        return await client.PostAsync(
            $"{PagePath}?handler={handler}",
            HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    [Fact]
    public async Task MovementCorrection_IsHiddenUntilTheEditModeIsOpened()
    {
        await SeedToolAsync("BQ-EDIT-1", "RGT931", "1");
        var trace = await SeedTraceAsync("BQ-EDIT-1");
        await SeedMovementAsync(trace.Id, "saida", 3);
        var movementId = (await ReadMovementsAsync(trace.Id)).Single().Id;
        SeedUser("4301", "Correção", "provider-edit-1", "EditPass1!");
        using var client = await LoginClientAsync("4301", "EditPass1!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains($"href=\"/Boquilhas?trace_id={trace.Id}&amp;editar_mov=1#bqLatest\">Corrigir movimentos</a>", html);
        // A tabela mostra a BQ e o lote em cada movimento (Diogo, 17/10/2026).
        Assert.Contains("<th>BQ · lote</th>", html);
        Assert.Contains("<td>RGT931 · Lote 1</td>", html);
        Assert.DoesNotContain("id=\"bqCorrectMovements\"", html);
        Assert.DoesNotContain("data-dmo-list=\"Movimentos\"", html);
        var table = html[html.IndexOf("bqTraceMovements", StringComparison.Ordinal)..];
        Assert.DoesNotContain("data-dmo-row", table[..table.IndexOf("</table>", StringComparison.Ordinal)]);
        Assert.DoesNotContain("Guardar correção", html);
        Assert.DoesNotContain("Apagar movimento", html);

        var editMode = await GetPageHtmlAsync(client, EditModeUrl(trace.Id));
        Assert.Contains("data-dmo-list=\"Movimentos\"", editMode);
        Assert.Contains($"data-dmo-open=\"/Boquilhas?trace_id={trace.Id}&amp;editar_mov=1&amp;mov_id={movementId}#bqLatest\"", editMode);
        // A correção abre numa janela por cima da página; «Fechar» volta aos
        // últimos movimentos (Diogo, 17/10/2026).
        Assert.Contains("<div class=\"dmo-modal-backdrop open\" id=\"bqCorrectMovements\"", editMode);
        Assert.Contains($"href=\"/Boquilhas?trace_id={trace.Id}#bqLatest\">Fechar</a>", editMode);
        // Sem «Abrir» nas linhas: o formulário só aparece depois do duplo clique.
        Assert.DoesNotContain("Guardar correção", editMode);

        var opened = await GetPageHtmlAsync(client, EditModeUrl(trace.Id, movementId));
        Assert.Contains("Guardar correção", opened);
        Assert.Contains("Apagar movimento", opened);
        // Apagar pede confirmação na janela da app, não na do browser.
        Assert.Contains("data-dmo-dialog=\"bq-apagar-movimento\">Apagar movimento</button>", opened);
        Assert.Contains("<dialog id=\"bq-apagar-movimento\" class=\"dmo-dialog\"", opened);
        Assert.Contains("Apagar este movimento? A contagem do registo é recalculada.", opened);
        Assert.DoesNotContain("confirm(", opened);
    }

    [Fact]
    public async Task EditMovement_ChangesTheTypeAndRecomputesTheBalance()
    {
        await SeedToolAsync("BQ-EDIT-2", "RGT932", "1");
        var trace = await SeedTraceAsync("BQ-EDIT-2");
        await SeedMovementAsync(trace.Id, "saida", 3);
        var movementId = (await ReadMovementsAsync(trace.Id)).Single().Id;
        SeedUser("4302", "Correção", "provider-edit-2", "EditPass2!");
        using var client = await LoginClientAsync("4302", "EditPass2!");

        var response = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "entrada"),
            ("quantity", "3"),
            ("occurred_at", "2026-10-05T10:30"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}&editar_mov=1", response.Headers.Location?.ToString());
        var edited = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(BqMovementType.Entrada, edited.Type);
        Assert.Equal(-3, edited.Discrepancy);
        Assert.Equal(new DateTime(2026, 10, 5, 9, 30, 0, DateTimeKind.Utc), edited.OccurredAtUtc);
        // Quem corrigiu e quando ficam no movimento e aparecem no formulário de correção.
        Assert.Equal("Correção", edited.CorrectedByNameFrozen);
        Assert.NotNull(edited.CorrectedAtUtc);
        var reopened = await GetPageHtmlAsync(client, EditModeUrl(trace.Id, movementId));
        Assert.Contains("id=\"bqMovementCorrectedBy\" style=\"margin:0 0 8px\">Corrigido por Correção em ", reopened);

        // Uma quantidade inválida é recusada com a causa e nada muda.
        var refused = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "saida"),
            ("quantity", "0"));
        Assert.Contains("edit_refusal=InvalidQuantity", refused.Headers.Location?.ToString());
        var page = await GetPageHtmlAsync(client, refused.Headers.Location!.ToString());
        Assert.Contains("<div data-dmo-toast=\"erro\" class=\"result\" role=\"alert\">Indique uma quantidade igual ou superior a 1. Nada foi alterado.</div>", page);
        Assert.Equal(BqMovementType.Entrada, Assert.Single(await ReadMovementsAsync(trace.Id)).Type);
    }

    [Fact]
    public async Task DeleteMovement_RecentMovementIsDeleted()
    {
        await SeedToolAsync("BQ-EDIT-3", "RGT933", "1");
        var trace = await SeedTraceAsync("BQ-EDIT-3");
        await SeedMovementAsync(trace.Id, "saida", 3);
        await SeedMovementAsync(trace.Id, "entrada", 5);
        var saidaId = (await ReadMovementsAsync(trace.Id)).First(m => m.Type == BqMovementType.Saida).Id;
        SeedUser("4303", "Correção", "provider-edit-3", "EditPass3!");
        using var client = await LoginClientAsync("4303", "EditPass3!");

        var response = await PostCorrectionAsync(client, "DeleteMovement", EditModeUrl(trace.Id, saidaId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", saidaId.ToString()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}&editar_mov=1", response.Headers.Location?.ToString());
        var remaining = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(BqMovementType.Entrada, remaining.Type);
        Assert.Equal(-5, remaining.Discrepancy);

        // O rasto da eliminação: os factos do movimento e quem apagou.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        // A base em memória é partilhada por todas as classes de testes (o
        // Sumário do Planeamento semeia um apagado com movimento 99): o rasto
        // é o deste rasto de BQ.
        var trail = await db.BqMovementDeletions.AsNoTracking()
            .SingleAsync(d => d.BqMovementId == saidaId && d.BqRepairTraceId == trace.Id);
        Assert.Equal(trace.Id, trail.BqRepairTraceId);
        Assert.Equal(BqMovementType.Saida, trail.Type);
        Assert.Equal(3, trail.Quantity);
        Assert.Equal("Correção", trail.DeletedByNameFrozen);
    }

    [Fact]
    public async Task DeleteMovement_TheOldestOfSixCanBeDeleted()
    {
        // Diogo, 19/10/2026: apaga-se qualquer movimento, também o mais
        // antigo; a contagem recalcula pela data real.
        await SeedToolAsync("BQ-EDIT-4", "RGT934", "1");
        var trace = await SeedTraceAsync("BQ-EDIT-4");
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var oldestId = await SeedDatedMovementAsync(trace.Id, BqMovementType.Saida, 1, start);
        for (var i = 1; i <= 5; i++)
        {
            await SeedDatedMovementAsync(trace.Id, BqMovementType.Saida, 1, start.AddDays(i));
        }
        SeedUser("4304", "Correção", "provider-edit-4", "EditPass4!");
        using var client = await LoginClientAsync("4304", "EditPass4!");

        var opened = await GetPageHtmlAsync(client, EditModeUrl(trace.Id, oldestId));
        Assert.Contains("Guardar correção", opened);
        Assert.Contains("Apagar movimento", opened);
        // O movimento aberto sem correções não mostra «Corrigido por».
        Assert.DoesNotContain("bqMovementCorrectedBy", opened);

        var response = await PostCorrectionAsync(client, "DeleteMovement", EditModeUrl(trace.Id, oldestId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", oldestId.ToString()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("edit_refusal", response.Headers.Location?.ToString());
        var remaining = await ReadMovementsAsync(trace.Id);
        Assert.Equal(5, remaining.Count);
        Assert.DoesNotContain(remaining, m => m.Id == oldestId);
    }

    [Fact]
    public async Task Registo_ShowsOnlyThePageAsked_AndAMovementFromAnotherPageOpensToCorrect()
    {
        // 30 movimentos: a tabela mostra 25 de cada vez, mas a contagem e o
        // saldo usam todos, e o mais antigo (página 2) abre para corrigir
        // estando na página 1.
        await SeedToolAsync("BQ-EDIT-5", "RGT935", "1");
        var trace = await SeedTraceAsync("BQ-EDIT-5");
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var oldestId = await SeedDatedMovementAsync(trace.Id, BqMovementType.Saida, 1, start);
        for (var i = 1; i < 30; i++)
        {
            await SeedDatedMovementAsync(trace.Id, BqMovementType.Saida, 1, start.AddHours(i));
        }
        SeedUser("4305", "Correção", "provider-edit-5", "EditPass5!");
        using var client = await LoginClientAsync("4305", "EditPass5!");

        static int Rows(string html, string tableId)
        {
            var table = html[html.IndexOf($"id=\"{tableId}\"", StringComparison.Ordinal)..];
            table = table[..table.IndexOf("</table>", StringComparison.Ordinal)];
            return table.Split("<tr").Length - 2;
        }

        var first = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Equal(25, Rows(first, "bqTraceMovements"));
        var second = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&mov_pagina=2");
        Assert.Equal(5, Rows(second, "bqTraceMovements"));

        var opened = await GetPageHtmlAsync(client, EditModeUrl(trace.Id, oldestId));
        Assert.Contains("Guardar correção", opened);
        Assert.Contains($"name=\"movement_id\" value=\"{oldestId}\"", opened);
        Assert.Contains("Apagar movimento", opened);
    }
}
