using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Os dados da ferramenta para o cartão da folha: a % de uso é a dela (a
/// última leitura, 0 sem nenhuma) e as notas e a informação técnica são as do
/// último Job On em que foi usada (o mais recente pelo início). A utilização
/// nunca vem desse Job On: é a da ferramenta.
/// </summary>
public sealed class FolhaDaFerramentaQuery(DmoDbContext db) : IFolhaDaFerramenta
{
    public async Task<FolhaDaFerramenta> ParaAsync(string tipo, string toolId, int? excetoJobOnId, CancellationToken cancellationToken = default)
    {
        var utilizacao = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.ToolId == toolId)
            .OrderByDescending(u => u.Id)
            .Select(u => (decimal?)u.Percentagem)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        // Os Job On da ferramenta, os de agora e os em que foi trocada.
        IQueryable<int> jobOns = tipo switch
        {
            JobOnFolhaCampos.Cm => db.CmContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId ?? 0),
            JobOnFolhaCampos.Mf => db.MfContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId ?? 0),
            JobOnFolhaCampos.Bq => db.BqContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId ?? 0),
            _ => Enumerable.Empty<int>().AsQueryable()
        };
        var ultimo = await db.JobOns.AsNoTracking()
            .Where(j => jobOns.Contains(j.Id) && j.Id != excetoJobOnId)
            .OrderByDescending(j => j.InicioEm)
            .ThenByDescending(j => j.Id)
            .Select(j => (int?)j.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (ultimo is not int jobOnId)
        {
            return new FolhaDaFerramenta(utilizacao, null, new Dictionary<string, string?>());
        }

        var familias = tipo == JobOnFolhaCampos.Bq ? new[] { JobOnFolhaCampos.Bq, JobOnFolhaCampos.An } : new[] { tipo };
        var folha = await JobOnFolhaQuery.LerAsync(db, jobOnId, cancellationToken);
        var campos = familias
            .SelectMany(JobOnFolhaCampos.DaFamilia)
            // As quantidades (stock e em máquina) são da produção, não da ferramenta.
            .Where(c => !c.Chave.EndsWith("_utilizacao", StringComparison.Ordinal)
                && c.Rotulo is not JobOnFolhaCampos.QtdStock and not JobOnFolhaCampos.QtdMaq)
            .ToDictionary(c => c.Chave, c => folha.Valor(c.Chave), StringComparer.Ordinal);
        return new FolhaDaFerramenta(utilizacao, jobOnId, campos);
    }
}
