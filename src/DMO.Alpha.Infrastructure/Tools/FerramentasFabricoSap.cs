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
    public async Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc,
        IReadOnlyList<FabricoSapDetalhe>? detalhes = null, CancellationToken cancellationToken = default)
    {
        var detalheDe = (detalhes ?? [])
            .GroupBy(d => (d.Material, d.Linha, d.InicioEm, d.FimEm))
            .ToDictionary(g => g.Key, g => g.Last());
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
            existente.PesoMedioGramas = linha.PesoMedioGramas;
            existente.DiasProducao = linha.DiasProducao;
            if (detalheDe.TryGetValue((linha.Material, linha.Linha, linha.InicioEm, linha.FimEm), out var detalhe))
            {
                existente.Centro = detalhe.Centro;
                existente.Cadencia = detalhe.Cadencia;
                existente.Gob = detalhe.Gob;
                existente.Sectores = detalhe.Sectores;
                existente.Kcr = detalhe.Kcr;
                existente.DeltaPesoGramas = detalhe.DeltaPesoGramas;
                existente.PackToPull = detalhe.PackToPull;
                existente.NetQuant = detalhe.NetQuant;
                existente.Paletes = detalhe.Paletes;
                existente.RejeicaoTotal = detalhe.RejeicaoTotal;
                existente.RejeicaoMil = detalhe.RejeicaoMil;
                existente.HotEnd = detalhe.HotEnd;
            }

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

    public async Task<IReadOnlyDictionary<int, PesoSapResumo>> PesoDosJobOnsAsync(IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        var jobOns = await db.JobOns.AsNoTracking()
            .Where(j => jobOnIds.Contains(j.Id) && j.InicioEm != null)
            .Select(j => new { j.Id, j.Reference, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        var resumos = new Dictionary<int, PesoSapResumo>();
        if (jobOns.Count == 0)
        {
            return resumos;
        }

        var maquinas = jobOns.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
        var de = jobOns.Min(j => j.InicioEm!.Value);
        var sap = await db.SapFabricos.AsNoTracking()
            .Where(s => maquinas.Contains(s.Linha) && s.FimEm >= de && s.PesoMedioGramas != null)
            .ToListAsync(cancellationToken);
        foreach (var j in jobOns)
        {
            var ordens = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, j.Reference, j.Maquina, j.InicioEm, j.FimEm));
            if (FabricoSapPeso.Resumo(ordens, j.InicioEm!.Value, j.FimEm) is { } resumo)
            {
                resumos[j.Id] = resumo;
            }
        }

        return resumos;
    }

    public async Task<IReadOnlyList<SapFabrico>> DoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var jobOn = await db.JobOns.AsNoTracking()
            .Where(j => j.Id == jobOnId && j.InicioEm != null)
            .Select(j => new { j.Reference, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (jobOn is null)
        {
            return [];
        }

        var maquina = jobOn.Maquina.Trim().ToUpperInvariant();
        var de = jobOn.InicioEm!.Value;
        return (await db.SapFabricos.AsNoTracking()
                .Where(s => s.Linha == maquina && s.FimEm >= de)
                .ToListAsync(cancellationToken))
            .Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, jobOn.Reference, jobOn.Maquina, jobOn.InicioEm, jobOn.FimEm))
            .OrderBy(s => s.InicioEm)
            .ThenBy(s => s.Id)
            .ToList();
    }

    public async Task<FerramentaProducoes> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default)
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
        var comDatas = jobOns.Where(j => j.InicioEm.HasValue).ToList();
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

        // As leituras da utilização, pela data em Portugal.
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => u.ToolId == toolId)
                .OrderBy(u => u.RegistadaEmUtc)
                .ThenBy(u => u.Id)
                .Select(u => new { u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .Select(u => (Dia: DateOnly.FromDateTime(DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem))
            .ToList();

        var itens = jobOns.Select(j => Montar(j.Id, j.Reference, j.ProductionNumber, j.Maquina, j.InicioEm, j.FimEm, sap, leituras)).ToList();
        return new FerramentaProducoes(
            PagedList<FerramentaProducao>.FromLoaded(itens, pagina, porPagina),
            itens.Any(p => p.ProducaoBruta.HasValue) ? itens.Sum(p => p.ProducaoBruta ?? 0) : null,
            itens.Any(p => p.ProducaoLiquida.HasValue) ? itens.Sum(p => p.ProducaoLiquida ?? 0) : null);
    }

    /// <summary>
    /// As produções de várias ferramentas de uma vez (o Histórico): as mesmas
    /// de <see cref="ProducoesAsync"/> para cada uma, na mesma ordem, mas com um
    /// número fixo de leituras e só os Job On que cabem na linha e nos dias do
    /// filtro (filtrados na base). A ferramenta sem produções fica de fora.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>>> ProducoesDeFerramentasAsync(
        IReadOnlyCollection<string> toolIds, HistoricoFiltro? filtro, CancellationToken cancellationToken = default)
    {
        var resultado = new Dictionary<string, IReadOnlyList<FerramentaProducao>>(StringComparer.Ordinal);
        if (toolIds.Count == 0)
        {
            return resultado;
        }

        var relacoes = (await db.CmContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Concat(await db.MfContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Concat(await db.BqContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Where(c => c.JobOnId.HasValue)
            .Select(c => (c.ToolId, JobOnId: c.JobOnId!.Value))
            .Distinct()
            .ToList();
        if (relacoes.Count == 0)
        {
            return resultado;
        }

        var ids = relacoes.Select(r => r.JobOnId).Distinct().ToList();
        var filtrados = db.JobOns.AsNoTracking().Where(j => ids.Contains(j.Id));
        if (filtro is not null)
        {
            if (!string.IsNullOrWhiteSpace(filtro.Linha))
            {
                var linha = filtro.Linha.Trim().ToUpperInvariant();
                filtrados = filtrados.Where(j => j.Machine.Code.ToUpper() == linha);
            }

            if (filtro.De is { } de)
            {
                filtrados = filtrados.Where(j => j.FimEm == null || j.FimEm >= de);
            }

            if (filtro.Ate is { } ate)
            {
                filtrados = filtrados.Where(j => j.InicioEm != null && j.InicioEm <= ate);
            }
        }

        var jobOns = (await filtrados
                .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
                .ToListAsync(cancellationToken))
            .OrderByDescending(j => j.InicioEm.HasValue)
            .ThenByDescending(j => j.InicioEm)
            .ThenByDescending(j => j.Id)
            .ToList();
        if (jobOns.Count == 0)
        {
            return resultado;
        }

        var sap = new List<SapFabrico>();
        var comDatas = jobOns.Where(j => j.InicioEm.HasValue).ToList();
        if (comDatas.Count > 0)
        {
            var maquinas = comDatas.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
            var de = comDatas.Min(j => j.InicioEm!.Value);
            var ate = comDatas.Max(j => j.FimEm is { } f ? (f >= j.InicioEm!.Value ? f : j.InicioEm!.Value) : DateOnly.MaxValue);
            sap = await db.SapFabricos.AsNoTracking()
                .Where(s => maquinas.Contains(s.Linha) && s.InicioEm <= ate && s.FimEm >= de)
                .ToListAsync(cancellationToken);
        }

        var comProducao = relacoes.Select(r => r.ToolId).Distinct().ToList();
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => comProducao.Contains(u.ToolId))
                .OrderBy(u => u.RegistadaEmUtc).ThenBy(u => u.Id)
                .Select(u => new { u.ToolId, u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => g.Select(u => (Dia: DateOnly.FromDateTime(DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem)).ToList(), StringComparer.Ordinal);

        var porFerramenta = relacoes.ToLookup(r => r.ToolId, r => r.JobOnId);
        foreach (var toolId in comProducao)
        {
            var seus = porFerramenta[toolId].ToHashSet();
            var l = leituras.TryGetValue(toolId, out var dela) ? dela : [];
            var itens = jobOns.Where(j => seus.Contains(j.Id))
                .Select(j => Montar(j.Id, j.Reference, j.ProductionNumber, j.Maquina, j.InicioEm, j.FimEm, sap, l))
                .ToList();
            if (itens.Count > 0)
            {
                resultado[toolId] = itens;
            }
        }

        return resultado;
    }

    private static FerramentaProducao Montar(int id, string reference, string producao, string maquina, DateOnly? inicio, DateOnly? fim,
        IReadOnlyList<SapFabrico> sap, IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras)
    {
        var linhas = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, reference, maquina, inicio, fim)).ToList();
        return new FerramentaProducao(
            id,
            reference,
            producao,
            maquina,
            inicio,
            fim,
            linhas.Any(s => s.ProducaoBruta.HasValue) ? linhas.Sum(s => s.ProducaoBruta ?? 0) : null,
            linhas.Any(s => s.ProducaoLiquida.HasValue) ? linhas.Sum(s => s.ProducaoLiquida ?? 0) : null,
            linhas.Count,
            UsoAntes(leituras, inicio),
            UsoDepois(leituras, inicio, fim));
    }

    // Antes: a última leitura até ao dia do início (a desse dia é o ponto de
    // partida). Depois: a primeira a partir do dia do fim; sem fim (ainda na
    // máquina), a última desde o início. As leituras pela ordem, com o dia em Portugal.
    public static decimal? UsoAntes(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio) => inicio is { } de
        ? leituras.Where(u => u.Dia <= de).Select(u => (decimal?)u.Percentagem).LastOrDefault()
        : null;

    public static decimal? UsoDepois(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio, DateOnly? fim) => (inicio, fim) switch
    {
        (null, _) => null,
        (_, { } ate) => leituras.Where(u => u.Dia >= ate).Select(u => (decimal?)u.Percentagem).FirstOrDefault(),
        ({ } de, null) => leituras.Where(u => u.Dia >= de).Select(u => (decimal?)u.Percentagem).LastOrDefault()
    };

    /// <summary>A % de uso numa produção: depois − antes (a mesma conta da ficha).</summary>
    public static decimal? UsoNaProducao(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio, DateOnly? fim) =>
        UsoAntes(leituras, inicio) is { } antes && UsoDepois(leituras, inicio, fim) is { } depois ? depois - antes : null;
}
