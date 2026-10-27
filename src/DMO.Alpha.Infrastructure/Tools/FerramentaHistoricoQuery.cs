using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// O Histórico das ferramentas sobre o <see cref="DmoDbContext"/>: os Job On
/// de cada ferramenta e a % de uso vêm das produções da ficha
/// (<see cref="IFerramentasFabricoSap.ProducoesAsync"/>: CM, MF e BQ pelos
/// contextos, também as trocadas a meio); as outras ferramentas de cada Job
/// On vêm dos contextos dele (a BQ pelo BqContext, nunca pelo MF).
/// </summary>
public sealed class FerramentaHistoricoQuery(DmoDbContext db, IFerramentasFabricoSap fabrico) : IFerramentaHistorico
{
    /// <summary>Quantas ferramentas a procura lê de cada vez (mais avisa «cortada»).</summary>
    public const int MaxFerramentas = 40;

    /// <summary>Quantas linhas a procura mostra.</summary>
    public const int MaxLinhas = 300;

    public async Task<HistoricoFerramenta?> FerramentaAsync(string? toolId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId) || toolId == ToolRegistryConstants.RemovedToolId)
        {
            return null;
        }

        var ferramenta = await db.Tools.AsNoTracking()
            .Where(t => t.ToolId == toolId)
            .Select(t => new HistoricoFerramenta(t.ToolId, t.Type, t.Reference, t.Lot))
            .SingleOrDefaultAsync(cancellationToken);
        return ferramenta is null ? null : ParteDaBq(ferramenta);
    }

    public async Task<IReadOnlyList<HistoricoLinha>> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default) =>
        await FerramentaAsync(toolId, cancellationToken) is { } ferramenta
            ? await LinhasAsync([ferramenta], null, cancellationToken)
            : [];

    public async Task<HistoricoProcura> ProcurarAsync(HistoricoFiltro filtro, CancellationToken cancellationToken = default)
    {
        if (!filtro.ComFiltro)
        {
            return new HistoricoProcura([], false);
        }

        var ferramentas = db.Tools.AsNoTracking().Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId);
        if (filtro.Tipo is { } tipo)
        {
            ferramentas = ferramentas.Where(t => t.Type == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Lote))
        {
            var lote = filtro.Lote.Trim().ToUpperInvariant();
            ferramentas = ferramentas.Where(t => t.Lot.ToUpper() == lote);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Referencia))
        {
            var referencia = filtro.Referencia.Trim().ToUpperInvariant();
            ferramentas = ferramentas.Where(t => t.Reference.ToUpper().Contains(referencia));
        }
        else
        {
            // Sem referência: as ferramentas dos Job On da linha e dos dias.
            var jobOns = JobOnsDoFiltro(filtro).Select(j => (int?)j.Id);
            var ids = db.CmContexts.Where(c => jobOns.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId)
                .Concat(db.MfContexts.Where(c => jobOns.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId))
                .Concat(db.BqContexts.Where(c => jobOns.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId));
            ferramentas = ferramentas.Where(t => ids.Contains(t.ToolId));
        }

        var encontradas = await ferramentas
            .OrderBy(t => t.Type).ThenBy(t => t.Reference).ThenBy(t => t.Lot)
            .Select(t => new HistoricoFerramenta(t.ToolId, t.Type, t.Reference, t.Lot))
            .Take(MaxFerramentas + 1)
            .ToListAsync(cancellationToken);
        encontradas = encontradas.Select(ParteDaBq).ToList();
        var cortada = encontradas.Count > MaxFerramentas;
        var linhas = await LinhasAsync(encontradas.Take(MaxFerramentas).ToList(), filtro, cancellationToken);
        return new HistoricoProcura(linhas.Take(MaxLinhas).ToList(), cortada || linhas.Count > MaxLinhas);
    }

    /// <summary>
    /// A BQ mostra só a sua parte da referência (Diogo, 22/10/2026): a total é
    /// MF + BQ, e 5447T173 fica T173 (a mesma divisão do ToolCandidateQuery).
    /// </summary>
    public static HistoricoFerramenta ParteDaBq(HistoricoFerramenta f) =>
        f.Tipo == ToolType.Bq && ToolCandidateQuery.SplitReference(f.Referencia.Trim().ToUpperInvariant()).BqPart is { } parte
            ? f with { Referencia = parte }
            : f;

    private IQueryable<DMO.Alpha.Core.Production.JobOn> JobOnsDoFiltro(HistoricoFiltro filtro)
    {
        var jobOns = db.JobOns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Linha))
        {
            var linha = filtro.Linha.Trim().ToUpperInvariant();
            jobOns = jobOns.Where(j => j.Machine.Code.ToUpper() == linha);
        }

        if (filtro.De is { } de)
        {
            jobOns = jobOns.Where(j => j.FimEm == null || j.FimEm >= de);
        }

        if (filtro.Ate is { } ate)
        {
            jobOns = jobOns.Where(j => j.InicioEm != null && j.InicioEm <= ate);
        }

        return jobOns;
    }

    /// <summary>A produção cabe na linha e nos dias (o Job On cruza o intervalo).</summary>
    internal static bool Cabe(FerramentaProducao p, HistoricoFiltro? filtro) =>
        filtro is null
        || ((string.IsNullOrWhiteSpace(filtro.Linha) || string.Equals(p.Maquina, filtro.Linha.Trim(), StringComparison.OrdinalIgnoreCase))
            && (filtro.De is not { } de || p.FimEm is null || p.FimEm >= de)
            && (filtro.Ate is not { } ate || (p.InicioEm is { } inicio && inicio <= ate)));

    private async Task<List<HistoricoLinha>> LinhasAsync(IReadOnlyList<HistoricoFerramenta> ferramentas, HistoricoFiltro? filtro, CancellationToken cancellationToken)
    {
        var porFerramenta = await fabrico.ProducoesDeFerramentasAsync(ferramentas.Select(f => f.ToolId).ToList(), filtro, cancellationToken);
        var producoes = new List<(HistoricoFerramenta Ferramenta, FerramentaProducao Producao)>();
        foreach (var ferramenta in ferramentas)
        {
            if (porFerramenta.TryGetValue(ferramenta.ToolId, out var dela))
            {
                producoes.AddRange(dela.Where(p => Cabe(p, filtro)).Select(p => (ferramenta, p)));
            }
        }

        if (producoes.Count == 0)
        {
            return [];
        }

        var ids = producoes.Select(p => (int?)p.Producao.JobOnId).Distinct().ToList();
        var contextos = (await db.CmContexts.AsNoTracking()
                .Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                .Select(c => new { JobOnId = (c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, Atual = c.JobOnId != null, c.Id })
                .ToListAsync(cancellationToken))
            .Select(c => (c.JobOnId, c.ToolId, c.Atual, c.Id, Tipo: ToolType.Cm))
            .Concat((await db.MfContexts.AsNoTracking()
                    .Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                    .Select(c => new { JobOnId = (c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, Atual = c.JobOnId != null, c.Id })
                    .ToListAsync(cancellationToken))
                .Select(c => (c.JobOnId, c.ToolId, c.Atual, c.Id, Tipo: ToolType.Mf)))
            .Concat((await db.BqContexts.AsNoTracking()
                    .Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                    .Select(c => new { JobOnId = (c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, Atual = c.JobOnId != null, c.Id })
                    .ToListAsync(cancellationToken))
                .Select(c => (c.JobOnId, c.ToolId, c.Atual, c.Id, Tipo: ToolType.Bq)))
            .ToList();
        var toolIds = contextos.Select(c => c.ToolId).Distinct().ToList();
        var nomes = await db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new HistoricoFerramenta(t.ToolId, t.Type, t.Reference, t.Lot))
            .ToDictionaryAsync(t => t.ToolId, t => ParteDaBq(t), StringComparer.Ordinal, cancellationToken);

        // A % de uso de cada ferramenta em cada produção, pela mesma conta da ficha.
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => toolIds.Contains(u.ToolId))
                .OrderBy(u => u.RegistadaEmUtc).ThenBy(u => u.Id)
                .Select(u => new { u.ToolId, u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(DateOnly Dia, decimal Percentagem)>)g
                .Select(u => (DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem)).ToList(), StringComparer.Ordinal);
        IReadOnlyDictionary<string, decimal?> Usos(FerramentaProducao p) => contextos
            .Where(c => c.JobOnId == p.JobOnId)
            .Select(c => c.ToolId).Distinct()
            .ToDictionary(id => id, id => leituras.TryGetValue(id, out var l) ? FerramentasFabricoSap.UsoNaProducao(l, p.InicioEm, p.FimEm) : null, StringComparer.Ordinal);

        // A atual primeiro; as trocadas a meio a seguir, pela ordem em que entraram.
        IReadOnlyList<HistoricoFerramenta> Do(int jobOnId, ToolType tipo) => contextos
            .Where(c => c.JobOnId == jobOnId && c.Tipo == tipo)
            .OrderByDescending(c => c.Atual).ThenBy(c => c.Id)
            .Select(c => nomes.TryGetValue(c.ToolId, out var t) ? t : null)
            .OfType<HistoricoFerramenta>()
            .DistinctBy(t => t.ToolId)
            .ToList();

        return producoes
            .Select(x => new HistoricoLinha(
                x.Producao.JobOnId,
                x.Producao.Referencia,
                x.Producao.Producao,
                x.Producao.Maquina,
                x.Producao.InicioEm,
                x.Producao.FimEm,
                x.Producao.DeltaUtilizacao,
                x.Ferramenta,
                Do(x.Producao.JobOnId, ToolType.Cm),
                Do(x.Producao.JobOnId, ToolType.Mf),
                Do(x.Producao.JobOnId, ToolType.Bq),
                Usos(x.Producao)))
            .OrderByDescending(l => l.InicioEm.HasValue)
            .ThenByDescending(l => l.InicioEm)
            .ThenByDescending(l => l.JobOnId)
            .ThenBy(l => l.Ferramenta.Tipo)
            .ThenBy(l => l.Ferramenta.Referencia, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Ferramenta.Lote, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

