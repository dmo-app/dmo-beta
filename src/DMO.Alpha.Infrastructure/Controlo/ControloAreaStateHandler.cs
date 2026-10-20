using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O ESTADO DO DOCUMENTO de um controlo: OK/NOK e comentário por área
/// (CM/BQ/MF/PU/CS). Gravar substitui o estado da área (a última vence);
/// área sem estado nem comentário apaga a linha. Nunca é gate. O OK/NOK e
/// o comentário gravam-se em separado: gravar um mantém o outro como está
/// na base, para uma folha aberta noutro posto não apagar o que lá se gravou.
/// Cada mudança do OK/NOK fica também no histórico da área (Diogo,
/// 19/10/2026, D13): quem, quando e o valor; a última é a que vale.
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
    /// controlo inexistente: nada é gravado e devolve false. Com
    /// <paramref name="setStatus"/> ou <paramref name="setComment"/> a false,
    /// esse campo fica como está na base. A identificação (referência e
    /// lote) só se grava com <paramref name="setIdentificacao"/> e só no PU e
    /// no CS, que não têm ferramenta na produção.
    /// </summary>
    public async Task<bool> SetAsync(
        int controloId,
        string? area,
        string? status,
        string? comment,
        string? updatedBy,
        bool setStatus = true,
        bool setComment = true,
        CancellationToken cancellationToken = default,
        string? identificacao = null,
        bool setIdentificacao = false)
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

        if (setIdentificacao && !ControloAreaState.AreasSemFerramenta.Contains(canonicalArea))
        {
            return false;
        }

        var canonicalIdentificacao = string.IsNullOrWhiteSpace(identificacao) ? null : identificacao.Trim();
        if (canonicalIdentificacao is { Length: > ControloAreaState.IdentificacaoMaxLength })
        {
            canonicalIdentificacao = canonicalIdentificacao[..ControloAreaState.IdentificacaoMaxLength];
        }

        if (!await _db.Controlos.AnyAsync(c => c.Id == controloId, cancellationToken))
        {
            return false;
        }

        // Duas gravações ao mesmo tempo da mesma área ainda sem linha: a
        // segunda colide no índice único e volta a correr como atualização.
        return await UniqueViolationRetry.RunAsync(
            _db,
            () => WriteAsync(controloId, canonicalArea, canonicalStatus, canonicalComment, updatedBy, setStatus, setComment, cancellationToken,
                canonicalIdentificacao, setIdentificacao));
    }

    private async Task<bool> WriteAsync(
        int controloId,
        string canonicalArea,
        string? canonicalStatus,
        string? canonicalComment,
        string? updatedBy,
        bool setStatus,
        bool setComment,
        CancellationToken cancellationToken,
        string? canonicalIdentificacao = null,
        bool setIdentificacao = false)
    {
        var existing = await _db.ControloAreaStates
            .FirstOrDefaultAsync(s => s.ControloId == controloId && s.Area == canonicalArea, cancellationToken);
        if (!setStatus)
        {
            canonicalStatus = existing?.Status;
        }
        else if (canonicalStatus != existing?.Status)
        {
            // O histórico do OK/NOK (D13): só as mudanças do valor; a mesma
            // escolha repetida não acrescenta nada. Um OK/NOK gravado antes
            // do histórico entra primeiro, com quem e quando o gravou.
            if (existing?.Status is { } anterior
                && !await _db.ControloAreaStateHistorico.AnyAsync(
                    h => h.ControloId == controloId && h.Area == canonicalArea, cancellationToken))
            {
                _db.ControloAreaStateHistorico.Add(new ControloAreaStateHistorico
                {
                    ControloId = controloId,
                    Area = canonicalArea,
                    Status = anterior,
                    Por = existing.UpdatedBy,
                    EmUtc = existing.UpdatedAtUtc
                });
            }

            _db.ControloAreaStateHistorico.Add(new ControloAreaStateHistorico
            {
                ControloId = controloId,
                Area = canonicalArea,
                Status = canonicalStatus,
                Por = updatedBy,
                EmUtc = DateTime.UtcNow
            });
        }

        if (!setComment)
        {
            canonicalComment = existing?.Comment;
        }

        if (!setIdentificacao)
        {
            canonicalIdentificacao = existing?.Identificacao;
        }

        if (canonicalStatus is null && canonicalComment is null && canonicalIdentificacao is null)
        {
            if (existing is not null)
            {
                _db.ControloAreaStates.Remove(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (existing is null)
        {
            existing = new ControloAreaState { ControloId = controloId, Area = canonicalArea };
            _db.ControloAreaStates.Add(existing);
        }

        existing.Status = canonicalStatus;
        existing.Comment = canonicalComment;
        existing.Identificacao = canonicalIdentificacao;
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
        var historico = await _db.ControloAreaStateHistorico
            .AsNoTracking()
            .Where(h => h.ControloId == controloId)
            .OrderByDescending(h => h.EmUtc)
            .ThenByDescending(h => h.Id)
            .Select(h => new ControloAreaStateMudanca(h.Area, h.Status, h.Por, h.EmUtc))
            .ToListAsync(cancellationToken);

        // Quem gravou, pelo nome do utilizador (Diogo, 20/10/2026: «Registado
        // por 0001» devia mostrar o nome). Grava-se o número de operador ou o
        // email; sem utilizador com esse número ou email, fica como está.
        var quem = rows.Select(r => r.UpdatedBy).Concat(historico.Select(h => h.Por))
            .Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => q!).Distinct().ToArray();
        var nomes = quem.Length == 0 ? new Dictionary<string, string>()
            : (await _db.Users.AsNoTracking()
                .Where(u => quem.Contains(u.OperatorId) || (u.Email != null && quem.Contains(u.Email)))
                .Select(u => new { u.OperatorId, u.Email, u.Name })
                .ToListAsync(cancellationToken))
              .SelectMany(u => new[] { (Chave: u.OperatorId, u.Name), (Chave: u.Email, u.Name) })
              .Where(x => x.Chave is not null && quem.Contains(x.Chave))
              .GroupBy(x => x.Chave!).ToDictionary(g => g.Key, g => g.First().Name);
        string? Nome(string? q) => q is not null && nomes.TryGetValue(q, out var nome) ? nome : q;
        historico = historico.Select(h => h with { Por = Nome(h.Por) }).ToList();

        return ControloAreaState.Areas
            .Select(area => (rows.FirstOrDefault(r => r.Area == area) is { } row
                ? new ControloAreaStateReadModel(area, row.Status, row.Comment, Nome(row.UpdatedBy), row.UpdatedAtUtc) { Identificacao = row.Identificacao }
                : new ControloAreaStateReadModel(area, null, null, null, null))
                with { Historico = historico.Where(h => h.Area == area).ToArray() })
            .ToArray();
    }
}

/// <summary>
/// O estado de uma área do documento: OK/NOK (null = não registado) e o
/// comentário. UpdatedBy (e Por no histórico) é o nome de quem gravou, ou o
/// que ficou gravado quando não há utilizador com esse número ou email.
/// </summary>
public sealed record ControloAreaStateReadModel(
    string Area,
    string? Status,
    string? Comment,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc)
{
    /// <summary>
    /// As gravações do OK/NOK desta área neste controlo, da mais recente para
    /// a mais antiga (D13); a primeira é a que vale. Vazio antes de 19/10/2026.
    /// </summary>
    public IReadOnlyList<ControloAreaStateMudanca> Historico { get; init; } = [];

    /// <summary>A referência e o lote escritos à mão (só no PU e no CS).</summary>
    public string? Identificacao { get; init; }

    /// <summary>As gravações anteriores à que vale (o rasto das mudanças).</summary>
    public IReadOnlyList<ControloAreaStateMudanca> Anteriores => Historico.Skip(1).ToArray();
}

/// <summary>Uma gravação do OK/NOK de uma área: o valor («—» = null), quem e quando.</summary>
public sealed record ControloAreaStateMudanca(
    string Area,
    string? Status,
    string? Por,
    DateTime EmUtc);
