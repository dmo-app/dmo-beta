using System.Globalization;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMO.Alpha.Web.Pages.Boquilhas;

/// <summary>
/// A correção dos movimentos do registo: o modo de edição (?editar_mov=1),
/// o movimento aberto por duplo clique (?mov_id) e os POSTs de corrigir e apagar.
/// </summary>
public sealed partial class IndexModel
{
    [BindProperty(SupportsGet = true, Name = "editar_mov")] public int? EditMovementsFlag { get; set; }
    [BindProperty(SupportsGet = true, Name = "mov_id")] public int? EditMovementId { get; set; }
    [BindProperty(SupportsGet = true, Name = "edit_refusal")] public string? EditRefusal { get; set; }

    public bool EditMovementsMode => EditMovementsFlag == 1;

    /// <summary>O movimento aberto para correção; null quando não pertence ao registo.</summary>
    public BoquilhasRegistoMovement? EditingMovement { get; private set; }

    /// <summary>True quando o movimento aberto está entre os mais recentes do registo.</summary>
    public bool EditingMovementDeletable { get; private set; }

    /// <summary>«Corrigido por X em dd/mm hh:mm» do movimento aberto; vazio quando nunca foi corrigido.</summary>
    public string EditingMovementCorrectionNote => EditingMovement?.CorrectedAtUtc is DateTime correctedAt
        ? string.IsNullOrWhiteSpace(EditingMovement.CorrectedByNameFrozen)
            ? $"Corrigido em {FormatCorrectionTime(correctedAt)}"
            : $"Corrigido por {EditingMovement.CorrectedByNameFrozen} em {FormatCorrectionTime(correctedAt)}"
        : string.Empty;

    private static string FormatCorrectionTime(DateTime utc) =>
        DmoBusinessTime.ToLocal(utc).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// O nome de quem corrige ou apaga (congelado no movimento ou no rasto):
    /// o nome do utilizador, senão o nº de operador, senão o email.
    /// </summary>
    private string? CurrentActorName() =>
        User.FindFirst(DmoClaimTypes.DisplayName)?.Value is { Length: > 0 } name
            ? name
            : User.FindFirst(DmoClaimTypes.OperatorId)?.Value
                ?? User.FindFirst(DmoClaimTypes.Email)?.Value;

    public string EditingMovementOccurredAt => EditingMovement is { } movement
        ? DmoBusinessTime.ToLocal(movement.OccurredAtUtc).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    public string EditRefusalMessage => EditRefusal switch
    {
        nameof(EditBqMovementResult.MovementNotFound) =>
            "O movimento indicado já não existe neste registo. Nada foi alterado.",
        nameof(EditBqMovementResult.UnknownMovementType) =>
            "Tipo de movimento desconhecido. Use saída, entrada ou entrada sem reparação. Nada foi alterado.",
        nameof(EditBqMovementResult.InvalidQuantity) =>
            "Indique uma quantidade igual ou superior a 1. Nada foi alterado.",
        nameof(EditBqMovementResult.InvalidOccurrenceTime) =>
            "A data e hora indicadas não são legíveis. Indique a data e a hora reais do movimento. Nada foi alterado.",
        nameof(DeleteBqMovementResult.NotAmongMostRecent) =>
            $"Só é possível apagar um dos {DeleteBqMovement.MostRecentLimit} movimentos mais recentes deste registo. Nada foi apagado.",
        _ => string.Empty
    };

    /// <summary>A janela de pôr a discrepância a zero aberta (?acerto=1).</summary>
    [BindProperty(SupportsGet = true, Name = "acerto")] public int? DiscrepancyResetFlag { get; set; }

    public bool DiscrepancyResetOpen => DiscrepancyResetFlag == 1;

    /// <summary>O registo aberto com a janela de pôr a zero aberta ou fechada.</summary>
    public string? DiscrepancyResetHref(int traceId, bool open) =>
        Url.Page("/Boquilhas/Index", new
        {
            trace_id = traceId,
            acerto = open ? 1 : (int?)null,
            mov_pagina = TraceMovementsPage
        });

    /// <summary>«Posta a zero por X em dd/mm hh:mm»; vazio quando nunca foi posta a zero.</summary>
    public string DiscrepancyResetNote => RegistoTrace?.DiscrepancyResetAtUtc is DateTime resetAt
        ? string.IsNullOrWhiteSpace(RegistoTrace.DiscrepancyResetBy)
            ? $"Posta a zero em {FormatCorrectionTime(resetAt)}. Conta só os movimentos depois disso."
            : $"Posta a zero por {RegistoTrace.DiscrepancyResetBy} em {FormatCorrectionTime(resetAt)}. Conta só os movimentos depois disso."
        : string.Empty;

    /// <summary>
    /// POST: põe a zero a discrepância acumulada do registo (Diogo, 17/10/2026:
    /// começar a registar a meio das produções). Os movimentos ficam como estão.
    /// </summary>
    public async Task<IActionResult> OnPostResetDiscrepancyAsync(
        [FromServices] IResetBqDiscrepancyHandler handler,
        [FromForm(Name = "trace_id")] int traceId)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        await handler.HandleAsync(new ResetBqDiscrepancy(traceId, CurrentActorName()), HttpContext.RequestAborted);
        return Redirect($"{DiscrepancyResetHref(traceId, open: false)}#bqMetrics");
    }

    public string? MovementCorrectionHref(int traceId, bool editMode, int? movementId = null) =>
        Url.Page("/Boquilhas/Index", new
        {
            trace_id = traceId,
            editar_mov = editMode ? 1 : (int?)null,
            mov_id = movementId,
            mov_pagina = TraceMovementsPage
        });

    private void ComposeMovementCorrection()
    {
        if (!EditMovementsMode || RegistoTrace is not { } trace || EditMovementId is not int movementId)
        {
            return;
        }

        EditingMovement = trace.Movements.FirstOrDefault(m => m.MovementId == movementId);
        EditingMovementDeletable = EditingMovement is not null && trace.Movements
            .OrderByDescending(m => m.OccurredAtUtc)
            .ThenByDescending(m => m.MovementId)
            .Take(DeleteBqMovement.MostRecentLimit)
            .Any(m => m.MovementId == movementId);
    }

    /// <summary>POST: corrige o tipo, a quantidade e a data/hora reais de um movimento.</summary>
    public async Task<IActionResult> OnPostEditMovementAsync(
        [FromServices] IEditBqMovementHandler handler,
        [FromForm(Name = "trace_id")] int traceId,
        [FromForm(Name = "movement_id")] int movementId,
        [FromForm(Name = "movement_type")] string? movementType,
        [FromForm(Name = "quantity")] int? quantity,
        [FromForm(Name = "occurred_at")] string? occurredAt)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        // Sem data mantém a data gravada; uma data ilegível não é aplicada em silêncio.
        DateTime? occurredLocal = null;
        if (!string.IsNullOrWhiteSpace(occurredAt))
        {
            if (!DateTime.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return CorrectionRedirect(traceId, movementId, nameof(EditBqMovementResult.InvalidOccurrenceTime));
            }

            occurredLocal = parsed;
        }

        var outcome = await handler.HandleAsync(
            new EditBqMovement(
                traceId, movementId, movementType ?? string.Empty, quantity ?? 0, occurredLocal, CurrentActorName()),
            HttpContext.RequestAborted);

        // A hora que caía na mudança de hora foi gravada na hora seguinte: a folha avisa.
        if (outcome.Result == EditBqMovementResult.Success
            && outcome.OccurredAtShiftedLocal is DateTime shiftedLocal
            && occurredLocal is DateTime requestedLocal)
        {
            return RedirectToPage("/Boquilhas/Index", new
            {
                trace_id = traceId,
                editar_mov = 1,
                hora_indicada = LocalFieldValue(requestedLocal),
                hora_gravada = LocalFieldValue(shiftedLocal)
            });
        }

        return outcome.Result switch
        {
            EditBqMovementResult.Success => CorrectionRedirect(traceId),
            EditBqMovementResult.MovementNotFound => CorrectionRedirect(traceId, null, outcome.Result.ToString()),
            _ => CorrectionRedirect(traceId, movementId, outcome.Result.ToString())
        };
    }

    /// <summary>POST: apaga um dos movimentos mais recentes do registo.</summary>
    public async Task<IActionResult> OnPostDeleteMovementAsync(
        [FromServices] IDeleteBqMovementHandler handler,
        [FromForm(Name = "trace_id")] int traceId,
        [FromForm(Name = "movement_id")] int movementId)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await handler.HandleAsync(
            new DeleteBqMovement(traceId, movementId, CurrentActorName()),
            HttpContext.RequestAborted);

        return result switch
        {
            DeleteBqMovementResult.Success => CorrectionRedirect(traceId),
            DeleteBqMovementResult.MovementNotFound => CorrectionRedirect(traceId, null, result.ToString()),
            _ => CorrectionRedirect(traceId, movementId, result.ToString())
        };
    }

    private RedirectToPageResult CorrectionRedirect(int traceId, int? movementId = null, string? refusal = null) =>
        RedirectToPage("/Boquilhas/Index", new
        {
            trace_id = traceId,
            editar_mov = 1,
            mov_id = movementId,
            edit_refusal = refusal
        });
}
