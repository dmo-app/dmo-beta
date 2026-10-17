using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O ESTADO DO DOCUMENTO de um controlo: OK/NOK e comentário por área
/// (CM/BQ/MF/PU/CS). Gravar substitui o estado da área (a última vence);
/// área sem estado nem comentário apaga a linha. Nunca é gate.
/// </summary>
public sealed class ControloAreaStateHandler
{
    private readonly DmoDbContext _db;

    public ControloAreaStateHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Grava o estado de uma área. Área ou estado fora do vocabulário, ou
    /// controlo inexistente: nada é gravado e devolve false.
    /// </summary>
    public async Task<bool> SetAsync(
        int controloId,
        string? area,
        string? status,
        string? comment,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var canonicalArea = area?.Trim().ToUpperInvariant();
        if (canonicalArea is null || !ControloAreaState.Areas.Contains(canonicalArea))
        {
            return false;
        }

        var canonicalStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (canonicalStatus is not (null or "OK" or "NOK"))
        {
            return false;
        }

        var canonicalComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (canonicalComment is { Length: > ControloAreaState.CommentMaxLength })
        {
            canonicalComment = canonicalComment[..ControloAreaState.CommentMaxLength];
        }

        if (!await _db.Controlos.AnyAsync(c => c.Id == controloId, cancellationToken))
        {
            return false;
        }

        // Duas gravações ao mesmo tempo da mesma área ainda sem linha: a
        // segunda colide no índice único e volta a correr como atualização.
        return await UniqueViolationRetry.RunAsync(
            _db,
            () => WriteAsync(controloId, canonicalArea, canonicalStatus, canonicalComment, updatedBy, cancellationToken));
    }

    private async Task<bool> WriteAsync(
        int controloId,
        string canonicalArea,
        string? canonicalStatus,
        string? canonicalComment,
        string? updatedBy,
        CancellationToken cancellationToken)
    {
        var existing = await _db.ControloAreaStates
            .FirstOrDefaultAsync(s => s.ControloId == controloId && s.Area == canonicalArea, cancellationToken);

        if (canonicalStatus is null && canonicalComment is null)
        {
            if (existing is not null)
            {
                _db.ControloAreaStates.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        if (existing is null)
        {
            existing = new ControloAreaState { ControloId = controloId, Area = canonicalArea };
            _db.ControloAreaStates.Add(existing);
        }

        existing.Status = canonicalStatus;
        existing.Comment = canonicalComment;
        existing.UpdatedBy = updatedBy;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>O estado das cinco áreas, pela ordem do documento (as não registadas com Status e Comment null).</summary>
    public async Task<IReadOnlyList<ControloAreaStateReadModel>> GetAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ControloAreaStates
            .AsNoTracking()
            .Where(s => s.ControloId == controloId)
            .ToListAsync(cancellationToken);

        return ControloAreaState.Areas
            .Select(area => rows.FirstOrDefault(r => r.Area == area) is { } row
                ? new ControloAreaStateReadModel(area, row.Status, row.Comment, row.UpdatedBy, row.UpdatedAtUtc)
                : new ControloAreaStateReadModel(area, null, null, null, null))
            .ToArray();
    }
}

/// <summary>O estado de uma área do documento: OK/NOK (null = não registado) e o comentário.</summary>
public sealed record ControloAreaStateReadModel(
    string Area,
    string? Status,
    string? Comment,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc);
