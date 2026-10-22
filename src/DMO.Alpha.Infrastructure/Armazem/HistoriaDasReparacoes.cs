using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// A história das reparações sobre o <see cref="DmoDbContext"/>. Do Job On:
/// as RI dele (quem registou é quem reparou) e as idas à Reparação do
/// Armazém das CM e dos MF dele, pelo Job On guardado no movimento; os
/// movimentos de antes (sem Job On) ligam-se pela ferramenta e pelas datas do
/// Job On. Da CM: as RI e os Pesos, cada um com o Job On e a data.
/// </summary>
public sealed class HistoriaDasReparacoes(DmoDbContext db) : IHistoriaDasReparacoes
{
    public async Task<IReadOnlyList<QuemReparou>> DoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var job = await db.JobOns.AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new { j.Id, j.InicioEm, j.FimEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return [];
        }

        // As CM e os MF da produção (também os trocados no Editar do Job On).
        var cms = await db.CmContexts.AsNoTracking()
            .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId).Select(c => c.ToolId).ToListAsync(cancellationToken);
        var mfs = await db.MfContexts.AsNoTracking()
            .Where(c => c.JobOnId == jobOnId || c.ReplacedJobOnId == jobOnId).Select(c => c.ToolId).ToListAsync(cancellationToken);
        var ferramentas = cms.Concat(mfs).Distinct(StringComparer.Ordinal).ToList();

        // As RI: o reparador é quem registou.
        var ri = await (
                from r in db.ReparacoesInternas.AsNoTracking()
                where r.JobOnId == jobOnId && r.AnuladaEmUtc == null
                join c in db.CmContexts.AsNoTracking() on r.CmContextId equals (int?)c.Id into cs
                from c in cs.DefaultIfEmpty()
                join m in db.MfContexts.AsNoTracking() on r.MfContextId equals (int?)m.Id into ms
                from m in ms.DefaultIfEmpty()
                join u in db.Users.AsNoTracking() on r.RegistadoPorUserId equals (Guid?)u.Id into us
                from u in us.DefaultIfEmpty()
                select new
                {
                    r.Id, r.Tipo, r.Numero, r.RegistadoEmUtc, r.ReferenciaEscrita, r.LoteEscrito,
                    ToolId = r.AssociadaToolId ?? (c != null ? c.ToolId : m != null ? m.ToolId : null),
                    Quem = u != null ? u.Name : null
                })
            .ToListAsync(cancellationToken);

        // As idas à Reparação do Armazém: pelo Job On guardado; as de antes,
        // pela ferramenta e pelas datas do Job On.
        var saidas = db.ArmazemMovimentos.AsNoTracking()
            .Where(m => m.Tipo == ArmazemMovimentoTipo.Saida && m.Destino == ArmazemDestino.Reparacao);
        var guardadas = saidas.Where(m => m.JobOnId == jobOnId);
        IQueryable<DMO.Alpha.Core.Armazem.ArmazemMovimento> pelaData = saidas.Where(_ => false);
        if (job.InicioEm is { } inicio && ferramentas.Count > 0)
        {
            var de = DmoBusinessTime.ToUtc(inicio.ToDateTime(TimeOnly.MinValue));
            pelaData = saidas.Where(m => m.JobOnId == null && ferramentas.Contains(m.ToolId) && m.OcorridoEmUtc >= de);
            if (job.FimEm is { } fim)
            {
                var ate = DmoBusinessTime.ToUtc(fim.AddDays(1).ToDateTime(TimeOnly.MinValue));
                pelaData = pelaData.Where(m => m.OcorridoEmUtc < ate);
            }
        }

        var idsDasSaidas = await guardadas.Select(m => m.Id).Union(pelaData.Select(m => m.Id)).ToListAsync(cancellationToken);
        var armazem = await db.ArmazemMovimentos.AsNoTracking()
            .Where(m => idsDasSaidas.Contains(m.Id))
            .Select(m => new
            {
                m.Id, m.ToolId, m.ReparadorNome, m.OcorridoEmUtc, m.JobOnId,
                Regresso = db.ArmazemMovimentos
                    .Where(e => e.ToolId == m.ToolId && e.Id > m.Id && e.Tipo == ArmazemMovimentoTipo.Entrada)
                    .OrderBy(e => e.Id)
                    .Select(e => new { e.OcorridoEmUtc, e.EntradaReparadorNome })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var ids = ri.Select(r => r.ToolId).Concat(armazem.Select(a => a.ToolId)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => ids.Contains(t.ToolId))
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .ToDictionaryAsync(t => t.ToolId, StringComparer.Ordinal, cancellationToken);

        var linhas = new List<QuemReparou>();
        foreach (var r in ri)
        {
            var t = r.ToolId is null ? null : tools.GetValueOrDefault(r.ToolId);
            linhas.Add(new QuemReparou(
                QuemReparouOrigem.ReparacaoInterna,
                r.ToolId ?? string.Empty,
                t?.Type ?? (r.Tipo == ReparacaoTipo.Cm ? ToolType.Cm : ToolType.Mf),
                t?.Reference ?? r.ReferenciaEscrita ?? string.Empty,
                t?.Lot ?? r.LoteEscrito ?? string.Empty,
                r.Quem,
                r.RegistadoEmUtc,
                Numero: r.Numero));
        }

        foreach (var a in armazem)
        {
            var t = tools.GetValueOrDefault(a.ToolId);
            linhas.Add(new QuemReparou(
                QuemReparouOrigem.Armazem,
                a.ToolId,
                t?.Type ?? ToolType.Cm,
                t?.Reference ?? string.Empty,
                t?.Lot ?? string.Empty,
                // O reparador corrigido no regresso manda; senão, o da Saída.
                a.Regresso?.EntradaReparadorNome ?? a.ReparadorNome,
                a.OcorridoEmUtc,
                a.Regresso?.OcorridoEmUtc,
                PelaData: a.JobOnId is null));
        }

        return linhas.OrderBy(l => l.EmUtc).ThenBy(l => l.Origem).ToList();
    }
}
