using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O REGISTO DE VISÃO de um controlo (o approve-mínimo): quem abriu a
/// folha (o Resumo) e quando — a última visita de cada operador. O
/// get-or-mark é idempotente (uma linha por controlo×operador; reabrir
/// atualiza o momento). NUNCA é gate — a folha regista, o resto segue.
/// </summary>
public sealed class ControloViewingHandler
{
    private readonly DmoDbContext _db;

    public ControloViewingHandler(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Regista a visita do operador à folha do controlo (o upsert da linha
    /// única) e devolve as visitas, do mais recente para o mais antigo.
    /// O operatorId vem da claim da sessão (extraído pela página — o nº de
    /// operador é o fato humano); sem operador ou controlo inexistente,
    /// nenhuma linha é inventada.
    /// </summary>
    public async Task<IReadOnlyList<ControloViewingReadModel>> MarkAndGetViewingsAsync(
        int controloId,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        var controloExists = await _db.Controlos
            .AsNoTracking()
            .AnyAsync(c => c.Id == controloId, cancellationToken);
        if (!controloExists)
        {
            return Array.Empty<ControloViewingReadModel>();
        }

        return await MarkAndGetViewingsOfExistingAsync(controloId, operatorId, cancellationToken);
    }

    /// <summary>
    /// Igual a <see cref="MarkAndGetViewingsAsync"/>, para um controlo que o
    /// chamador acabou de ler: não volta a confirmar que o controlo existe.
    /// </summary>
    public async Task<IReadOnlyList<ControloViewingReadModel>> MarkAndGetViewingsOfExistingAsync(
        int controloId,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(operatorId))
        {
            // Duas aberturas ao mesmo tempo (duas abas): a segunda colide no
            // índice único e volta a correr como atualização.
            await UniqueViolationRetry.RunAsync(_db, () => MarkAsync(controloId, operatorId, cancellationToken));
        }

        return await _db.ControloViewings
            .AsNoTracking()
            .Where(v => v.ControloId == controloId)
            .OrderByDescending(v => v.ViewedAtUtc)
            .Select(v => new ControloViewingReadModel(
                v.OperatorId,
                v.ViewedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> MarkAsync(int controloId, string operatorId, CancellationToken cancellationToken)
    {
        // O upsert: uma linha por (controlo, operador) — o índice único
        // garante; a reabertura da folha atualiza o momento.
        var existing = await _db.ControloViewings
            .FirstOrDefaultAsync(
                v => v.ControloId == controloId && v.OperatorId == operatorId,
                cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            _db.ControloViewings.Add(new ControloViewing
            {
                ControloId = controloId,
                OperatorId = operatorId,
                ViewedAtUtc = now
            });
        }
        else if (now - existing.ViewedAtUtc > TimeSpan.FromSeconds(30))
        {
            // A visita de AGORA só re-escreve o momento se passou tempo
            // (evita um update a cada refresh da mesma visita).
            existing.ViewedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
