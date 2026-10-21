using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação de <see cref="IJobOnFolhaQuery"/>: duas consultas, a linha da
/// folha e as linhas CAL do Job On. Os valores saem por chave do catálogo; um
/// Job On sem linha dá todas as chaves a null. As linhas CAL vêm pela ordem do
/// ecrã e, com a mesma ordem, pelo identificador.
/// </summary>
public sealed class JobOnFolhaQuery : IJobOnFolhaQuery
{
    private readonly DmoDbContext _db;

    public JobOnFolhaQuery(DmoDbContext db)
    {
        _db = db;
    }

    public Task<JobOnFolhaLida> ForJobOnAsync(int jobOnId, CancellationToken cancellationToken = default) =>
        LerAsync(_db, jobOnId, cancellationToken);

    /// <summary>A leitura, também para os handlers que gravam (na mesma transação).</summary>
    internal static async Task<JobOnFolhaLida> LerAsync(DmoDbContext db, int jobOnId, CancellationToken cancellationToken)
    {
        var folha = await db.JobOnFolhas
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.JobOnId == jobOnId, cancellationToken);

        var cal = await db.JobOnFolhaCalLinhas
            .AsNoTracking()
            .Where(l => l.JobOnId == jobOnId)
            .OrderBy(l => l.Ordem)
            .ThenBy(l => l.Id)
            .Select(l => new JobOnFolhaCalLida(l.Ordem, l.Designacao, l.Valor, l.QtdMaq))
            .ToListAsync(cancellationToken);

        if (folha is null && cal.Count == 0)
        {
            return JobOnFolhaLida.VaziaDe(jobOnId);
        }

        var valores = JobOnFolhaCampos.Todos.ToDictionary(
            c => c.Chave,
            c => folha?.Valor(c.Chave),
            StringComparer.Ordinal);
        return new JobOnFolhaLida(jobOnId, valores, cal);
    }
}
