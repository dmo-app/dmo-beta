using System.Linq.Expressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A procura do HISTÓRICO DO CONTROLO ao uso do dmo-html (a secção history):
/// os registos guardados dos tipos Peso, Pegamentos, Comparação e Resumo com
/// os filtros da oficina — a referência do conjunto (pelo início dela ou da
/// parte BQ, a regra do <see cref="ReferenceMatch"/>), o nº de produção (o do
/// dropdown, igual, com a referência dessa produção), a ferramenta (a
/// referência da CM congelada ou corrente, pela mesma regra), o tipo e a
/// data. A data de cada registo é a que ficou gravada: os Pesos e os
/// Pegamentos pelo carimbo do registo (<c>RegistadoEmUtc</c>, desde
/// 17/10/2026), as comparações pela gravação, o Resumo pela aprovação. A
/// data escolhida é o dia de Lisboa. A ordenação é do mais recente para o
/// mais antigo; os registos sem data (os Pesos e os Pegamentos anteriores ao
/// carimbo, os Resumos por aprovar) ficam no fim — nenhuma data é inventada,
/// e um filtro de data não os encontra.
/// </summary>
public sealed class ControloHistorySearchQuery : IControloHistorySearchQuery
{
    private readonly DmoDbContext _db;

    public ControloHistorySearchQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<PagedList<ControloHistorySearchItem>> SearchPageAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        int? pageNumber,
        int pageSize = ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default,
        bool semJobOn = false,
        string? productionReference = null)
    {
        // A PAGINAÇÃO NA QUERY sobre várias tabelas: cada tipo devolve a sua
        // contagem e, no máximo, as linhas até ao fim da página pedida (já
        // na ordem final de cada tipo); a junção ordena e corta a página.
        // Lê a página pedida com a contagem; só volta a ler quando a página
        // pedida está fora do intervalo e é ajustada.
        var filtro = new Filtro(
            ReferenceMatch.Normalize(reference),
            Texto(productionNumber),
            Texto(productionReference)?.ToUpperInvariant(),
            ReferenceMatch.Normalize(tool),
            data is { } dia ? DiaDeLisboa(dia) : null);
        var requested = Math.Clamp(pageNumber ?? 1, 1, int.MaxValue / pageSize);
        var (items, total) = await SearchCoreAsync(filtro, tipo, semJobOn, requested * pageSize, cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        if (page != requested)
        {
            (items, _) = await SearchCoreAsync(filtro, tipo, semJobOn, page * pageSize, cancellationToken);
        }
        return new PagedList<ControloHistorySearchItem>(
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList(), total, page, pageSize);
    }

    public async Task<JobOnProductionOption?> ProducaoDaReferenciaAsync(
        string reference,
        string productionNumber,
        string? productionReference,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceMatch.Normalize(reference) is not { } referencia || Texto(productionNumber) is not { } numero)
        {
            return null;
        }

        // A mesma regra da tabela; o nº sozinho (um link antigo) é o da
        // produção mais recente com esse nº, nunca o de duas juntas.
        return await JobOnsDaProcura(referencia, numero, Texto(productionReference)?.ToUpperInvariant())
            .OrderByDescending(j => j.Id)
            .Select(j => new JobOnProductionOption(j.ProductionNumber, j.Reference))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Os filtros já normalizados: a referência e a ferramenta em maiúsculas
    /// (a regra do <see cref="ReferenceMatch"/>), o nº e a referência da
    /// produção escolhida (iguais) e o dia escolhido em UTC (início incluído,
    /// fim excluído).
    /// </summary>
    private sealed record Filtro(
        string? Referencia,
        string? Producao,
        string? ReferenciaDaProducao,
        string? Ferramenta,
        (DateTime DesdeUtc, DateTime AteUtc)? Dia);

    /// <param name="limit">null = sem limite; N = no máximo N linhas de cada tipo (0 = só a contagem).</param>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> SearchCoreAsync(
        Filtro filtro,
        ControloHistoryKind? tipo,
        bool semJobOn,
        int? limit,
        CancellationToken cancellationToken)
    {
        var items = new List<ControloHistorySearchItem>();
        var total = 0;

        // «Sem Job On associado»: só o que ficou por associar (Pesos e
        // Pegamentos sem controlo_id). Não têm produção: a referência
        // procura-se nas ferramentas do registo, a data é a do registo.
        if (semJobOn)
        {
            if (tipo is null or ControloHistoryKind.Peso)
            {
                var (pesos, count) = await PesosSemJobOnAsync(filtro, limit, cancellationToken);
                items.AddRange(pesos);
                total += count;
            }

            if (tipo is null or ControloHistoryKind.Pegamentos)
            {
                var (pegamentos, count) = await PegamentosSemJobOnAsync(filtro, limit, cancellationToken);
                items.AddRange(pegamentos);
                total += count;
            }

            return (Ordenar(items), total);
        }

        var jobs = JobOnsDaProcura(filtro.Referencia, filtro.Producao, filtro.ReferenciaDaProducao);

        if (tipo is null or ControloHistoryKind.Peso)
        {
            var (pesos, count) = await PesosAsync(jobs, filtro, limit, cancellationToken);
            items.AddRange(pesos);
            total += count;
        }

        if (tipo is null or ControloHistoryKind.Pegamentos)
        {
            var (pegamentos, count) = await PegamentosAsync(jobs, filtro, limit, cancellationToken);
            items.AddRange(pegamentos);
            total += count;
        }

        if (tipo is null or ControloHistoryKind.Comparacao)
        {
            var (comparacoes, count) = await ComparacoesAsync(jobs, filtro, limit, cancellationToken);
            items.AddRange(comparacoes);
            total += count;
        }

        if (tipo is null or ControloHistoryKind.Resumo)
        {
            var (resumos, count) = await ResumosAsync(jobs, filtro, limit, cancellationToken);
            items.AddRange(resumos);
            total += count;
        }

        return (Ordenar(items), total);
    }

    /// <summary>
    /// A ordem final da lista: do mais recente para o mais antigo pela data
    /// de cada registo; os sem data no fim, por tipo e pela ordem de gravação
    /// (Id descendente). Cada tipo lê as suas linhas já por esta ordem (as
    /// datas nulas no fim, depois o Id), para a junção das páginas não
    /// perder nem repetir linhas.
    /// </summary>
    private static List<ControloHistorySearchItem> Ordenar(IEnumerable<ControloHistorySearchItem> items) => items
        .OrderBy(i => i.DataUtc is null)
        .ThenByDescending(i => i.DataUtc)
        .ThenBy(i => i.Kind)
        .ThenByDescending(i => i.Id)
        .ToList();

    /// <summary>
    /// As produções da procura: a referência pelo início dela ou da parte BQ
    /// (<see cref="ReferenceMatch"/>), o nº de produção igual e, com a
    /// produção escolhida no dropdown, a referência dela igual (o nº só é
    /// único dentro da referência).
    /// </summary>
    private IQueryable<JobOn> JobOnsDaProcura(string? referencia, string? producao, string? referenciaDaProducao)
    {
        var jobs = _db.JobOns.AsNoTracking();
        if (referencia is not null)
        {
            jobs = jobs.Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, referencia));
        }
        if (producao is not null)
        {
            jobs = jobs.Where(j => j.ProductionNumber == producao);
        }
        if (referenciaDaProducao is not null)
        {
            jobs = jobs.Where(j => j.Reference == referenciaDaProducao);
        }
        return jobs;
    }

    /// <summary>
    /// Os registos com uma das referências que corresponde ao texto (a regra
    /// do <see cref="ReferenceMatch"/>, traduzida no PostgreSQL); sem texto,
    /// todos.
    /// </summary>
    private static IQueryable<T> ComReferencia<T>(
        IQueryable<T> query, string? texto, params Expression<Func<T, string?>>[] referencias)
    {
        if (texto is null)
        {
            return query;
        }

        var condicao = referencias
            .Select(referencia => ReferenceMatch.Predicate(referencia, texto))
            .Aggregate((uma, outra) => uma.Or(outra));
        return query.Where(condicao);
    }

    /// <summary>O dia de Lisboa em UTC: o início do dia e o início do dia seguinte (excluído).</summary>
    private static (DateTime DesdeUtc, DateTime AteUtc) DiaDeLisboa(DateOnly dia) =>
        (DmoBusinessTime.ToUtc(dia.ToDateTime(TimeOnly.MinValue)),
         DmoBusinessTime.ToUtc(dia.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    private static string? Texto(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// As linhas de um tipo: a contagem do filtro (só quando há limite) e,
    /// no máximo, <paramref name="limit"/> linhas (null = todas).
    /// </summary>
    private static async Task<(List<TRow> Rows, int Total)> ReadAsync<TRow>(
        IQueryable<TRow> query, int? limit, CancellationToken cancellationToken)
    {
        if (limit is not int max)
        {
            var all = await query.ToListAsync(cancellationToken);
            return (all, all.Count);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = max > 0 && total > 0 ? await query.Take(max).ToListAsync(cancellationToken) : new List<TRow>();
        return (rows, total);
    }

    /// <summary>Os Pesos com a data do registo no dia escolhido (os sem carimbo não entram).</summary>
    private static IQueryable<Peso> PesosDoDia(IQueryable<Peso> pesos, Filtro filtro)
    {
        if (filtro.Dia is not { } dia)
        {
            return pesos;
        }
        var (desde, ate) = dia;
        return pesos.Where(p => p.RegistadoEmUtc >= desde && p.RegistadoEmUtc < ate);
    }

    /// <summary>Os Pegamentos com a data do registo no dia escolhido (os sem carimbo não entram).</summary>
    private static IQueryable<Pegamentos> PegamentosDoDia(IQueryable<Pegamentos> pegamentos, Filtro filtro)
    {
        if (filtro.Dia is not { } dia)
        {
            return pegamentos;
        }
        var (desde, ate) = dia;
        return pegamentos.Where(g => g.RegistadoEmUtc >= desde && g.RegistadoEmUtc < ate);
    }

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PesosAsync(
        IQueryable<JobOn> jobs,
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        // A ferramenta: a CM congelada no Peso ou a corrente.
        var pesos = ComReferencia(
            _db.Pesos.AsNoTracking().Where(p => p.ControloId != null),
            filtro.Ferramenta,
            p => p.CmToolReferenceFrozen,
            p => p.CmTool.Reference);
        pesos = PesosDoDia(pesos, filtro);

        var (rows, total) = await ReadAsync(
            from p in pesos
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id
            join j in jobs on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            // A ordem final da lista: os com data primeiro, pela data; depois pelo Id.
            orderby p.RegistadoEmUtc == null, p.RegistadoEmUtc descending, p.Id descending
            select new
            {
                RegistroId = p.Id,
                ControloId = c.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                p.CmToolReferenceFrozen,
                p.CmToolLotFrozen,
                ToolReference = t.Reference,
                ToolLot = t.Lot,
                // Os Pesos da produção (18/10/2026): com mais do que um (a
                // CM ou a BQ trocou a meio), a linha diz também a BQ do Peso.
                Pesos = _db.Pesos.Count(x => x.ControloId == p.ControloId),
                BqReference = p.BqToolReferenceFrozen ?? _db.Tools.Where(x => x.ToolId == p.BqToolId).Select(x => x.Reference).FirstOrDefault(),
                BqLot = p.BqToolLotFrozen ?? _db.Tools.Where(x => x.ToolId == p.BqToolId).Select(x => x.Lot).FirstOrDefault(),
                p.WaterTemperatureC,
                p.WaterDensityGramsPerCm3,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.GlassDensityGramsPerCm3,
                p.RegistadoEmUtc
            }, limit, cancellationToken);

        if (rows.Count == 0)
        {
            return (Array.Empty<ControloHistorySearchItem>(), total);
        }

        var pesoIds = rows.Select(r => r.RegistroId).ToList();
        var readings = await _db.PesoMeasurements
            .AsNoTracking()
            .Where(x => pesoIds.Contains(x.PesoId))
            .Select(x => new { x.PesoId, x.WaterWeightGrams })
            .ToListAsync(cancellationToken);

        var porPeso = readings
            .GroupBy(r => r.PesoId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.WaterWeightGrams).ToList());

        return (rows.Select(row =>
        {
            decimal? averageGlass = null;
            var leituras = porPeso.TryGetValue(row.RegistroId, out var weights) ? weights.Count : 0;
            if (weights is { Count: > 0 }
                && WaterDensityTable.ForPeso(row.WaterDensityGramsPerCm3, row.WaterTemperatureC) is { } density)
            {
                var total = 0m;
                foreach (var weight in weights)
                {
                    var capacity = PesoCalculation.CapacityCm3(weight, density);
                    total += PesoCalculation.GlassWeightGrams(
                        capacity, row.VolumeMarisaCm3, row.VolumePuncaoCm3, row.GlassDensityGramsPerCm3);
                }

                averageGlass = Math.Round(total / weights.Count, 2, MidpointRounding.AwayFromZero);
            }

            var toolLabel = $"{row.CmToolReferenceFrozen ?? row.ToolReference} · Lote {row.CmToolLotFrozen ?? row.ToolLot}"
                + (row.Pesos < 2 ? "" : $" · BQ {row.BqReference ?? "—"} · Lote {row.BqLot ?? "—"}");
            var resumo = averageGlass is { } media
                ? string.Create(DmoNumberFormat.Culture, $"{leituras} leituras · média {media:0.00} g")
                : "sem leituras";

            return new ControloHistorySearchItem(
                ControloHistoryKind.Peso,
                row.RegistroId,
                row.ControloId,
                row.Reference,
                row.ProductionNumber,
                row.Machine,
                toolLabel,
                Utc(row.RegistadoEmUtc),
                resumo);
        }).ToList(), total);
    }

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PegamentosAsync(
        IQueryable<JobOn> jobs,
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        // A ferramenta: a CM, a BQ ou o MF congelados nos Pegamentos.
        var pegamentos = ComReferencia(
            _db.Pegamentos.AsNoTracking(),
            filtro.Ferramenta,
            g => g.CmToolReferenceFrozen,
            g => g.BqToolReferenceFrozen,
            g => g.MfToolReferenceFrozen);
        pegamentos = PegamentosDoDia(pegamentos, filtro);

        var (rows, total) = await ReadAsync(
            from g in pegamentos
            join c in _db.Controlos.AsNoTracking() on g.ControloId equals c.Id
            join j in jobs on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            // A ordem final da lista: os com data primeiro, pela data; depois pelo Id.
            orderby g.RegistadoEmUtc == null, g.RegistadoEmUtc descending, g.Id descending
            select new
            {
                RegistroId = g.Id,
                ControloId = c.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                g.CmToolReferenceFrozen,
                g.CmToolLotFrozen,
                g.BqToolReferenceFrozen,
                g.BqToolLotFrozen,
                g.MfToolReferenceFrozen,
                g.MfToolLotFrozen,
                g.ToleranceMm,
                g.RegistadoEmUtc,
                // As folhas da produção (18/10/2026): com mais do que
                // uma, a linha diz também a BQ e o MF da folha, com o lote:
                // duas folhas que só diferem no lote da BQ não ficam com o
                // mesmo texto.
                Folhas = _db.Pegamentos.Count(x => x.ControloId == g.ControloId)
            }, limit, cancellationToken);

        if (rows.Count == 0)
        {
            return (Array.Empty<ControloHistorySearchItem>(), total);
        }

        var pegamentosIds = rows.Select(r => r.RegistroId).ToList();
        var counts = await _db.PegamentosMeasurements
            .AsNoTracking()
            .Where(x => pegamentosIds.Contains(x.PegamentosId))
            .GroupBy(x => x.PegamentosId)
            .Select(g => new { PegamentosId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var contagem = counts.ToDictionary(x => x.PegamentosId, x => x.Count);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Pegamentos,
            row.RegistroId,
            row.ControloId,
            row.Reference,
            row.ProductionNumber,
            row.Machine,
            $"CM {row.CmToolReferenceFrozen ?? "—"} · Lote {row.CmToolLotFrozen ?? "—"}"
                + (row.Folhas < 2 ? "" : (row.BqToolReferenceFrozen is { } bq ? $" · BQ {bq}{ComLote(row.BqToolLotFrozen)}" : "")
                    + (row.MfToolReferenceFrozen is { } mf ? $" · MF {mf}{ComLote(row.MfToolLotFrozen)}" : "")),
            Utc(row.RegistadoEmUtc),
            string.Create(DmoNumberFormat.Culture,
                $"{(contagem.TryGetValue(row.RegistroId, out var n) ? n : 0)} medições · corredor ±{row.ToleranceMm:0.00} mm"))).ToList(), total);
    }

    private static string ComLote(string? lote) => lote is null ? "" : $" · Lote {lote}";

    /// <summary>
    /// O Resumo de cada produção (um por controlo_id): a CM do Job On e o
    /// estado da aprovação, nos três estados (sweep de 19/10/2026, C-3):
    /// «Aprovado por X», «Não aprovado por X · nota» e «Por aprovar», com o
    /// nome de quem decidiu, como na Revisão. O Id da linha é o jobon_id (o
    /// Resumo abre por ele); o ControloId é o do controlo. A data é a da
    /// decisão (por aprovar, um filtro de data não o encontra).
    /// </summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> ResumosAsync(
        IQueryable<JobOn> jobs,
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        // A ferramenta: a referência da CM do Job On.
        var ferramentas = ComReferencia(
            _db.Tools.AsNoTracking().Where(x => x.ToolId != ToolRegistryConstants.RemovedToolId),
            filtro.Ferramenta,
            t => t.Reference);
        var porFerramenta = filtro.Ferramenta is not null;
        // O estado de cada controlo pela regra única (ControloAprovacao).
        var controlos = ControloAprovacao.Estados(_db);
        if (filtro.Dia is { } dia)
        {
            var (desde, ate) = dia;
            controlos = controlos.Where(c => (c.ApprovedAtUtc ?? c.NotApprovedAtUtc) >= desde
                && (c.ApprovedAtUtc ?? c.NotApprovedAtUtc) < ate);
        }

        var (rows, total) = await ReadAsync(
            from c in controlos
            join j in jobs on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join cm in _db.CmContexts.AsNoTracking() on j.Id equals cm.JobOnId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join t in ferramentas on cm.ToolId equals t.ToolId into toolGroup
            from t in toolGroup.DefaultIfEmpty()
            where !porFerramenta || t != null
            // A ordem final da lista: os decididos (com data) primeiro, pela data; depois pelo Id da linha.
            orderby (c.ApprovedAtUtc ?? c.NotApprovedAtUtc) == null, (c.ApprovedAtUtc ?? c.NotApprovedAtUtc) descending, j.Id descending
            select new
            {
                Id = c.ControloId,
                JobOnId = j.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                ToolReference = t != null ? t.Reference : null,
                ToolLot = t != null ? t.Lot : null,
                c.ApprovedAtUtc,
                c.ApprovedByOperatorId,
                c.NotApprovedAtUtc,
                c.NotApprovedByOperatorId,
                c.ReviewNote,
                Nome = _db.Users
                    .Where(u => u.OperatorId == (c.ApprovedByOperatorId ?? c.NotApprovedByOperatorId))
                    .Select(u => u.Name)
                    .FirstOrDefault()
            }, limit, cancellationToken);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Resumo,
            row.JobOnId,
            row.Id,
            row.Reference,
            row.ProductionNumber,
            row.Machine,
            row.ToolReference is null ? null : $"CM {row.ToolReference} · Lote {row.ToolLot ?? "—"}",
            row.ApprovedAtUtc ?? row.NotApprovedAtUtc,
            row.ApprovedAtUtc is not null ? $"Aprovado por {row.Nome ?? row.ApprovedByOperatorId ?? "—"}"
            : row.NotApprovedAtUtc is not null ? $"Não aprovado por {row.Nome ?? row.NotApprovedByOperatorId ?? "—"}{NotaCurta(row.ReviewNote)}"
            : "Por aprovar")).ToList(), total);
    }

    /// <summary>A nota do «Não aprovar» cabe na linha da lista; inteira, no Resumo.</summary>
    private static string NotaCurta(string? nota) =>
        nota is null ? "" : " · " + (nota.Length <= 80 ? nota : nota[..79].TrimEnd() + "…");

    /// <summary>
    /// Os Pesos sem Job On (ControloId null). A ferramenta procura-se na CM
    /// (congelada ou corrente); a referência, que o registo não tem, nas
    /// ferramentas dele (a CM e a BQ, congeladas ou correntes); a data é a
    /// do registo.
    /// </summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PesosSemJobOnAsync(
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        var pesos = _db.Pesos.AsNoTracking().Where(p => p.ControloId == null);
        pesos = ComReferencia(
            pesos,
            filtro.Referencia,
            p => p.CmToolReferenceFrozen,
            p => p.CmTool.Reference,
            p => p.BqToolReferenceFrozen,
            p => p.BqTool.Reference);
        pesos = ComReferencia(pesos, filtro.Ferramenta, p => p.CmToolReferenceFrozen, p => p.CmTool.Reference);
        pesos = PesosDoDia(pesos, filtro);

        var (rows, total) = await ReadAsync(
            from p in pesos
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId into toolGroup
            from t in toolGroup.DefaultIfEmpty()
            // A ordem final da lista: os com data primeiro, pela data; depois pelo Id.
            orderby p.RegistadoEmUtc == null, p.RegistadoEmUtc descending, p.Id descending
            select new
            {
                p.Id,
                CmReference = p.CmToolReferenceFrozen ?? (t != null ? t.Reference : null),
                CmLot = p.CmToolLotFrozen ?? (t != null ? t.Lot : null),
                Leituras = _db.PesoMeasurements.Count(x => x.PesoId == p.Id),
                p.RegistadoEmUtc
            }, limit, cancellationToken);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Peso,
            row.Id,
            null,
            row.CmReference ?? "—",
            SemJobOnProductionNumber,
            "—",
            $"CM {row.CmReference ?? "—"} · Lote {row.CmLot ?? "—"}",
            Utc(row.RegistadoEmUtc),
            row.Leituras == 0 ? "sem leituras" : $"{row.Leituras} leituras")).ToList(), total);
    }

    /// <summary>
    /// Os Pegamentos sem Job On (ControloId null). A ferramenta e a
    /// referência procuram-se nas ferramentas congeladas (CM, BQ ou MF); a
    /// data é a do registo.
    /// </summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PegamentosSemJobOnAsync(
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        var pegamentos = _db.Pegamentos.AsNoTracking().Where(g => g.ControloId == null);
        foreach (var texto in new[] { filtro.Referencia, filtro.Ferramenta })
        {
            pegamentos = ComReferencia(
                pegamentos,
                texto,
                g => g.CmToolReferenceFrozen,
                g => g.BqToolReferenceFrozen,
                g => g.MfToolReferenceFrozen);
        }
        pegamentos = PegamentosDoDia(pegamentos, filtro);

        var (rows, total) = await ReadAsync(
            from g in pegamentos
            // A ordem final da lista: os com data primeiro, pela data; depois pelo Id.
            orderby g.RegistadoEmUtc == null, g.RegistadoEmUtc descending, g.Id descending
            select new
            {
                g.Id,
                g.CmToolReferenceFrozen,
                g.CmToolLotFrozen,
                g.BqToolReferenceFrozen,
                g.MfToolReferenceFrozen,
                g.ToleranceMm,
                Medicoes = _db.PegamentosMeasurements.Count(x => x.PegamentosId == g.Id),
                g.RegistadoEmUtc
            }, limit, cancellationToken);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Pegamentos,
            row.Id,
            null,
            row.CmToolReferenceFrozen ?? row.BqToolReferenceFrozen ?? row.MfToolReferenceFrozen ?? "—",
            SemJobOnProductionNumber,
            "—",
            row.CmToolReferenceFrozen is not null
                ? $"CM {row.CmToolReferenceFrozen} · Lote {row.CmToolLotFrozen ?? "—"}"
                : row.BqToolReferenceFrozen is not null ? $"BQ {row.BqToolReferenceFrozen}" : $"MF {row.MfToolReferenceFrozen ?? "—"}",
            Utc(row.RegistadoEmUtc),
            string.Create(DmoNumberFormat.Culture, $"{row.Medicoes} medições · corredor ±{row.ToleranceMm:0.00} mm"))).ToList(), total);
    }

    /// <summary>O texto do nº de produção dos registos sem Job On.</summary>
    private const string SemJobOnProductionNumber = PegamentosSheetReadModel.SemJobOnProductionNumber;

    /// <summary>O carimbo lido como UTC (a coluna guarda o instante; nunca uma data inventada).</summary>
    private static DateTime? Utc(DateTime? value) =>
        value is { } instante ? DateTime.SpecifyKind(instante, DateTimeKind.Utc) : null;

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> ComparacoesAsync(
        IQueryable<JobOn> jobs,
        Filtro filtro,
        int? limit,
        CancellationToken cancellationToken)
    {
        // A ferramenta: a CM congelada na comparação. O dia escolhido é o dia
        // de Lisboa; as datas gravadas estão em UTC.
        var comparacoes = ComReferencia(
            _db.PesoComparacoes.AsNoTracking(),
            filtro.Ferramenta,
            x => x.CmToolReferenceFrozen);
        if (filtro.Dia is { } dia)
        {
            var (desde, ate) = dia;
            comparacoes = comparacoes.Where(x => x.CreatedAtUtc >= desde && x.CreatedAtUtc < ate);
        }

        var (rows, total) = await ReadAsync(
            from x in comparacoes
            join c in _db.Controlos.AsNoTracking() on x.ControloId equals c.Id
            join j in jobs on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            orderby x.CreatedAtUtc descending, x.Id descending
            select new
            {
                RegistroId = x.Id,
                ControloId = c.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                x.CmToolReferenceFrozen,
                x.CmToolLotFrozen,
                x.CreatedAtUtc
            }, limit, cancellationToken);

        if (rows.Count == 0)
        {
            return (Array.Empty<ControloHistorySearchItem>(), total);
        }

        var comparacaoIds = rows.Select(r => r.RegistroId).ToList();
        var counts = await _db.PesoComparacaoMeasurements
            .AsNoTracking()
            .Where(x => comparacaoIds.Contains(x.ComparacaoId))
            .GroupBy(x => x.ComparacaoId)
            .Select(g => new { ComparacaoId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var contagem = counts.ToDictionary(x => x.ComparacaoId, x => x.Count);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Comparacao,
            row.RegistroId,
            row.ControloId,
            row.Reference,
            row.ProductionNumber,
            row.Machine,
            $"CM {row.CmToolReferenceFrozen ?? "—"} · Lote {row.CmToolLotFrozen ?? "—"}",
            row.CreatedAtUtc,
            $"{(contagem.TryGetValue(row.RegistroId, out var n) ? n : 0)} contra-moldes")).ToList(), total);
    }
}
