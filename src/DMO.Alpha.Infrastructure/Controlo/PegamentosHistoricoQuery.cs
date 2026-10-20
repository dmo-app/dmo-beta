using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O rasto das leituras dos Pegamentos corrigidas ou apagadas de um controlo
/// (sweep de 19/10/2026, 4.6), para a consulta da Revisão: o valor antigo, o
/// novo, quem e quando. Uma consulta, só quando se abre a consulta.
/// </summary>
public sealed class PegamentosHistoricoQuery
{
    private readonly DmoDbContext _db;

    public PegamentosHistoricoQuery(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>As correções das folhas do controlo, da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<PegamentosMeasurementHistorico>> ListByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default) =>
        await _db.PegamentosMeasurementHistorico
            .AsNoTracking()
            .Where(h => _db.Pegamentos.Any(g => g.Id == h.PegamentosId && g.ControloId == controloId))
            .OrderByDescending(h => h.EmUtc)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);
}
