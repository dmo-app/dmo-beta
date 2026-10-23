using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// Uma linha de «O que mudou» na ficha do Job On (o proprietário, 17/10/2026:
/// o Job On apresenta o problema e leva à solução): uma alteração da produção
/// ou uma mudança de lote, quando, quem, e a ação no módulo que ela afeta
/// («Mudou a CM … → Verificar o Peso»).
/// </summary>
public sealed record JobOnSummaryEntry(
    string When,
    string Text,
    string? Who,
    string ActionLabel,
    string? ActionHref,
    string ActionCapability)
{
    /// <summary>A alteração gravada (o «Remover por engano»); null numa mudança de lote.</summary>
    public int? ChangeId { get; init; }

    /// <summary>«Removida por X a dd/MM/yyyy HH:mm» quando foi removida por engano; null = ativa.</summary>
    public string? Removida { get; init; }

    /// <summary>Removida por engano: fica visível, riscada, sem ação.</summary>
    public bool IsRemoved => Removida is not null;

    /// <summary>«Removida por Diogo a 23/10/2026 14:05».</summary>
    public static string RemovidaTexto(string? por, DateTime removedAtUtc) =>
        $"Removida{(string.IsNullOrWhiteSpace(por) ? "" : " por " + por)} a {DmoBusinessTime.ToLocal(removedAtUtc):dd/MM/yyyy HH:mm}";

    /// <summary>
    /// Sem o módulo da ação, a linha fica só como informação (o Diogo,
    /// 17/10/2026: o botão «deve aparecer apenas a quem tem» o módulo).
    /// </summary>
    public static IReadOnlyList<JobOnSummaryEntry> ForCapabilities(
        IReadOnlyList<JobOnSummaryEntry> entries, Func<string, bool> has) =>
        entries.Select(e => has(e.ActionCapability) ? e : e with { ActionHref = null }).ToList();

    /// <summary>
    /// Junta as alterações (com hora) e as mudanças de lote (com o dia), da
    /// mais recente para a mais antiga; no mesmo dia, as alterações primeiro.
    /// Só entra o que foi feito depois de a produção começar (o Diogo,
    /// 17/10/2026): o que se acertou antes do início não é histórico.
    /// </summary>
    public static IReadOnlyList<JobOnSummaryEntry> Compose(
        int jobOnId,
        DateOnly? inicioEm,
        IReadOnlyList<JobOnChangeView> changes,
        IReadOnlyList<LotNoteView> lotNotes,
        bool incluirRemovidas = false)
    {
        // As removidas por engano só entram em «O que mudou» da folha (riscadas).
        var fromChanges = changes
            .Where(c => incluirRemovidas || c.RemovedAtUtc is null)
            .Where(c => JobOnChange.AfterStart(c.ChangedAtUtc, inicioEm)).Select(c =>
        {
            var local = DmoBusinessTime.ToLocal(c.ChangedAtUtc);
            var entry = Build(jobOnId, local.ToString("dd/MM/yyyy HH:mm"), c.Text, c.ChangedBy, c.Field) with
            {
                ChangeId = c.Id == 0 ? null : c.Id,
                Removida = c.RemovedAtUtc is { } removida ? RemovidaTexto(c.RemovedBy, removida) : null
            };
            if (entry.IsRemoved)
            {
                entry = entry with { ActionHref = null };
            }
            return (Day: DateOnly.FromDateTime(local), Order: 1, Time: local, Entry: entry);
        });
        var fromLotNotes = lotNotes.Where(n => inicioEm is { } inicio && n.Dia >= inicio).Select(n =>
            (Day: n.Dia, Order: 0, Time: DateTime.MinValue,
                Entry: Build(jobOnId, n.Dia.ToString("dd/MM/yyyy"), n.Text, n.CreatedBy, n.Slot)));

        return fromChanges
            .Concat(fromLotNotes)
            .OrderByDescending(e => e.Day)
            .ThenByDescending(e => e.Order)
            .ThenByDescending(e => e.Time)
            .Select(e => e.Entry)
            .ToList();
    }

    private static JobOnSummaryEntry Build(int jobOnId, string when, string text, string? who, string field)
    {
        var followUp = JobOnChangeFields.FollowUp(field);
        var href = followUp switch
        {
            // A folha não leva a nenhum módulo: a linha fica sem botão.
            JobOnFollowUp.Nenhum => null,
            JobOnFollowUp.Peso => $"/Controlo/Peso?jobon_id={jobOnId}",
            // Os Pegamentos abrem-se pelo Controlo da produção.
            JobOnFollowUp.Pegamentos or JobOnFollowUp.Controlo => $"/Controlo/Resumo?jobon_id={jobOnId}",
            _ => "/Boquilhas"
        };
        var capability = followUp == JobOnFollowUp.Boquilhas ? DmoCapabilities.Boquilhas : DmoCapabilities.ControloCreate;
        return new JobOnSummaryEntry(when, text, who, JobOnChangeFields.FollowUpLabel(followUp), href, capability);
    }
}
