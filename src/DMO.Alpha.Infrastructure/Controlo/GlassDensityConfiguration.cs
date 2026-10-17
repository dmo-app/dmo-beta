using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Leitura da configuração do Controlo: a densidade do vidro configurada por
/// processo de fabrico, persistida na tabela canónica da configuração
/// (uma linha por processo). Só de leitura — a edição das definições
/// pertence à superfície de administração; o Peso consome o valor aplicável
/// e preserva-o como evidência histórica no Peso criado.
/// </summary>
public sealed class GlassDensityConfiguration : IGlassDensityConfiguration
{
    private readonly DmoDbContext _db;

    public GlassDensityConfiguration(DmoDbContext db)
    {
        _db = db;
    }

    public Task<decimal?> FindByProcessAsync(
        CmProcess process,
        CancellationToken cancellationToken = default)
    {
        return _db.GlassDensities
            .AsNoTracking()
            .Where(g => g.Process == process)
            .Select(g => (decimal?)g.DensityGramsPerCm3)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
