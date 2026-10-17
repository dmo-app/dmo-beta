using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Leitura da configuração do Controlo: a tolerância do corredor de
/// Pegamentos, persistida na tabela canónica singleton da configuração
/// (uma linha — discriminador único filtrado, padrão singleton).
/// Só de leitura — a edição pertence à superfície de administração; a
/// criação de um registo de Pegamentos consome o valor efetivo (esta
/// configuração quando existir, default do Owner na sua falta) e
/// preserva-o congelado no registo como evidência histórica.
/// </summary>
public sealed class PegamentosToleranceConfiguration : IPegamentosToleranceConfiguration
{
    private readonly DmoDbContext _db;

    public PegamentosToleranceConfiguration(DmoDbContext db)
    {
        _db = db;
    }

    public Task<decimal?> FindConfiguredAsync(CancellationToken cancellationToken = default)
    {
        // Uma linha no máximo (índice único do singleton); a ordem por Id
        // é apenas determinismo defensivo de leitura.
        return _db.PegamentosTolerances
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => (decimal?)t.ToleranceMm)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
