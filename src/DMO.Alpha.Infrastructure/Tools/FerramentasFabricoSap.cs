using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// O fabrico pelo SAP sobre o <see cref="DmoDbContext"/>: cada linha do
/// relatório fica uma vez (material, linha, início e fim); lida outra vez,
/// atualiza as produções. As produções de uma ferramenta são os Job On em que
/// ela foi a CM, o MF ou a BQ (também os em que foi trocada), com a soma das
/// linhas do SAP da mesma referência, máquina e datas.
/// </summary>
public sealed class FerramentasFabricoSap(DmoDbContext db, ICurrentAccountContext account) : IFerramentasFabricoSap
{
    public async Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc, CancellationToken cancellationToken = default)
    {
        var agora = DateTime.UtcNow;
        var actor = account.Current;
        var autor = actor.IsAuthenticated ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName) : null;
        var nome = ficheiro is { Length: > 0 } f ? f[..Math.Min(f.Length, FerramentaUtilizacao.FicheiroMaxLength)] : null;
        var unicas = linhas
            .GroupBy(l => (l.Material, l.Linha, l.InicioEm, l.FimEm))
            .Select(g => g.Last())
            .ToList();
        var materiais = unicas.Select(l => l.Material).Distinct().ToList();
        var existentes = await db.SapFabricos
            .Where(s => materiais.Contains(s.Material))
            .ToListAsync(cancellationToken);
        var novas = 0;
        foreach (var linha in unicas)
        {
            var existente = existentes.FirstOrDefault(s => s.Material == linha.Material && s.Linha == linha.Linha && s.InicioEm == linha.InicioEm && s.FimEm == linha.FimEm);
            if (existente is null)
            {
                existente = new SapFabrico { Material = linha.Material, Linha = linha.Linha, InicioEm = linha.InicioEm, FimEm = linha.FimEm };
                db.SapFabricos.Add(existente);
                novas++;
            }

            existente.ProducaoBruta = linha.ProducaoBruta;
            existente.ProducaoLiquida = linha.ProducaoLiquida;
            existente.LidoEmUtc = agora;
            existente.LidoPor = autor;
            existente.Ficheiro = nome;
            existente.FicheiroModificadoEmUtc = ficheiroModificadoEmUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportarFabricoSapOutcome(unicas.Count, novas);
    }

    public Task<DateTime?> UltimoFicheiroAsync(CancellationToken cancellationToken = default) =>
        db.SapFabricos.AsNoTracking().MaxAsync(s => s.FicheiroModificadoEmUtc, cancellationToken);

    public async Task<PagedList<FerramentaProducao>> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default)
    {
        var cm = db.CmContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var mf = db.MfContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var bq = db.BqContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var ids = (await cm.Concat(mf).Concat(bq).ToListAsync(cancellationToken))
            .OfType<int>()
            .Distinct()
            .ToList();
        var jobOns = (await db.JobOns.AsNoTracking()
                .Where(j => ids.Contains(j.Id))
                .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
                .ToListAsync(cancellationToken))
            .OrderByDescending(j => j.InicioEm.HasValue)
            .ThenByDescending(j => j.InicioEm)
            .ThenByDescending(j => j.Id)
            .ToList();
        var pagedJobOns = PagedList<int>.FromLoaded(jobOns.Select(j => j.Id).ToList(), pagina, porPagina);
        var daPagina = jobOns.Where(j => pagedJobOns.Items.Contains(j.Id)).ToList();

        var comDatas = daPagina.Where(j => j.InicioEm.HasValue).ToList();
        var sap = new List<SapFabrico>();
        if (comDatas.Count > 0)
        {
            var maquinas = comDatas.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
            var de = comDatas.Min(j => j.InicioEm!.Value);
            // Sem fim, o Job On ainda está na máquina: conta tudo desde o início.
            var ate = comDatas.Max(j => j.FimEm is { } f ? (f >= j.InicioEm!.Value ? f : j.InicioEm!.Value) : DateOnly.MaxValue);
            sap = await db.SapFabricos.AsNoTracking()
                .Where(s => maquinas.Contains(s.Linha) && s.InicioEm <= ate && s.FimEm >= de)
                .ToListAsync(cancellationToken);
        }

        var itens = daPagina.Select(j =>
        {
            var linhas = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, j.Reference, j.Maquina, j.InicioEm, j.FimEm)).ToList();
            return new FerramentaProducao(
                j.Id,
                j.Reference,
                j.ProductionNumber,
                j.Maquina,
                j.InicioEm,
                j.FimEm,
                linhas.Any(s => s.ProducaoBruta.HasValue) ? linhas.Sum(s => s.ProducaoBruta ?? 0) : null,
                linhas.Any(s => s.ProducaoLiquida.HasValue) ? linhas.Sum(s => s.ProducaoLiquida ?? 0) : null,
                linhas.Count);
        }).ToList();
        return new PagedList<FerramentaProducao>(itens, pagedJobOns.TotalCount, pagedJobOns.PageNumber, pagedJobOns.PageSize);
    }
}
