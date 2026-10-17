using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O SELO DE APROVAÇÃO de um controlo (o corte D — o verbatim do Owner:
/// «o nao estar aprovado nao deve criar blocks ou negar continuidade.
/// deve apenas deixar registado»): o chefe marca o controlo da produção
/// como APROVADO — o operador e o momento ficam no próprio registo (ao
/// contrário do visto, que é uma linha por visita; a aprovação é UMA,
/// a última vence, idempotente). NUNCA é gate: nada muda de comportamento
/// por estar aprovado ou não — é a informação do histórico.
/// </summary>
public sealed class ControloApprovalHandler
{
    private readonly DmoDbContext _db;

    public ControloApprovalHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Aprova (ou reaprova) o controlo da produção: grava o operador e o
    /// momento. Sem operador identificado ou controlo inexistente: nada
    /// acontece (nenhum facto inventado). A leitura devolve o estado
    /// corrente para a folha reabrir com ele.
    /// </summary>
    public async Task<ControloApprovalReadModel?> ApproveAndGetAsync(
        int controloId,
        string? operatorId,
        CancellationToken cancellationToken = default) =>
        await ApproveAndGetAsync(controloId, operatorId, note: null, cancellationToken);

    /// <summary>A aprovação com a nota opcional da Revisão; limpa um «Não aprovar» anterior.</summary>
    public async Task<ControloApprovalReadModel?> ApproveAndGetAsync(
        int controloId,
        string? operatorId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var controlo = await _db.Controlos
            .FirstOrDefaultAsync(c => c.Id == controloId, cancellationToken);
        if (controlo is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(operatorId))
        {
            controlo.ApprovedByOperatorId = operatorId;
            controlo.ApprovedAtUtc = DateTime.UtcNow;
            controlo.NotApprovedByOperatorId = null;
            controlo.NotApprovedAtUtc = null;
            controlo.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            WriteAllApprovalColumns(controlo);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ReadModel(controlo);
    }

    /// <summary>
    /// O «Não aprovar» (17/10/2026): a nota é obrigatória e diz o que tem de
    /// mudar. Limpa uma aprovação anterior. Nunca bloqueia nada: é registo.
    /// Sem operador, sem nota ou controlo inexistente: nada é gravado.
    /// </summary>
    public async Task<ControloApprovalReadModel?> NotApproveAndGetAsync(
        int controloId,
        string? operatorId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var controlo = await _db.Controlos
            .FirstOrDefaultAsync(c => c.Id == controloId, cancellationToken);
        if (controlo is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(operatorId) && !string.IsNullOrWhiteSpace(note))
        {
            controlo.NotApprovedByOperatorId = operatorId;
            controlo.NotApprovedAtUtc = DateTime.UtcNow;
            controlo.ApprovedByOperatorId = null;
            controlo.ApprovedAtUtc = null;
            controlo.ReviewNote = note.Trim();
            WriteAllApprovalColumns(controlo);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ReadModel(controlo);
    }

    /// <summary>
    /// O EF só grava as colunas que mudaram. Um «Aprovar» e um «Não aprovar»
    /// ao mesmo tempo no mesmo controlo pendente gravariam colunas
    /// diferentes e ficavam os dois; gravando sempre as cinco, a última
    /// decisão substitui a outra por inteiro (e a regra da base,
    /// CK_Controlos_ApprovedOrNotApproved, nunca é tocada).
    /// </summary>
    private void WriteAllApprovalColumns(DMO.Alpha.Core.Controlo.Controlo controlo)
    {
        var entry = _db.Entry(controlo);
        entry.Property(c => c.ApprovedByOperatorId).IsModified = true;
        entry.Property(c => c.ApprovedAtUtc).IsModified = true;
        entry.Property(c => c.NotApprovedByOperatorId).IsModified = true;
        entry.Property(c => c.NotApprovedAtUtc).IsModified = true;
        entry.Property(c => c.ReviewNote).IsModified = true;
    }

    private static ControloApprovalReadModel ReadModel(DMO.Alpha.Core.Controlo.Controlo controlo) =>
        new(controlo.ApprovedByOperatorId, controlo.ApprovedAtUtc,
            controlo.NotApprovedByOperatorId, controlo.NotApprovedAtUtc, controlo.ReviewNote);

    /// <summary>O estado de aprovação corrente (sem marcar), null quando o controlo não existe.</summary>
    public async Task<ControloApprovalReadModel?> GetAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.Id == controloId)
            .Select(c => new ControloApprovalReadModel(
                c.ApprovedByOperatorId, c.ApprovedAtUtc,
                c.NotApprovedByOperatorId, c.NotApprovedAtUtc, c.ReviewNote))
            .FirstOrDefaultAsync(cancellationToken);

        return state;
    }

    /// <summary>
    /// O controlo_id da produção e o estado de aprovação numa só consulta;
    /// null quando a produção ainda não tem controlo. Só leitura.
    /// </summary>
    public async Task<(int ControloId, ControloApprovalReadModel Approval)?> FindByJobOnAsync(
        int jobOnId,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.Controlos
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => new { c.Id, c.ApprovedByOperatorId, c.ApprovedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        return state is null
            ? null
            : (state.Id, new ControloApprovalReadModel(state.ApprovedByOperatorId, state.ApprovedAtUtc));
    }
}

/// <summary>
/// O estado de aprovação de um controlo: o operador e o momento (null = não
/// aprovado), o «Não aprovar» e a nota da última decisão.
/// </summary>
public sealed record ControloApprovalReadModel(
    string? ApprovedByOperatorId,
    DateTime? ApprovedAtUtc,
    string? NotApprovedByOperatorId = null,
    DateTime? NotApprovedAtUtc = null,
    string? ReviewNote = null);
