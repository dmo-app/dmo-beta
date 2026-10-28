using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// O histórico de produções de uma boquilha: os BqContexts com o ToolId
/// pedido (os context ids — a âncora da cascata) → os Job Ons em que a
/// ferramenta foi planeada/usada, do mais recente para o mais antigo.
/// Projeção mínima (os fatos humanos), AsNoTracking, sem scans.
/// </summary>
public sealed class BqToolHistoryQuery : IBqToolHistoryQuery
{
    private readonly DmoDbContext _db;

    public BqToolHistoryQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<PagedList<BqToolJobHistoryItem>> ListJobHistoryAsync(
        string toolId,
        int? pageNumber,
        int pageSize = ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return PagedList<BqToolJobHistoryItem>.Empty;
        }

        return await (
            from c in _db.BqContexts.AsNoTracking()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId ?? c.ReplacedJobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            where c.ToolId == toolId
            orderby j.Id descending, c.Id descending
            select new BqToolJobHistoryItem(
                j.Id,
                j.ProductionNumber,
                j.Reference,
                m.Code))
            .ToPagedListAsync(pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Os dias do mês com movimentos (os pontos do calendário) — contados
    /// pela DATA REAL do movimento (o OccurredAtUtc), nunca pela data
    /// técnica do registo: o movimento antigo do backfill conta no seu
    /// dia.
    /// </summary>
    public async Task<IReadOnlyList<BqHistoryDayCount>> ListMonthCountsAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var zone = DMO.Alpha.Core.Runtime.DmoBusinessTime.Zone;
        var firstLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var lastLocal = firstLocal.AddMonths(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(firstLocal, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(lastLocal, zone);

        var rows = await _db.BqMovements
            .AsNoTracking()
            .Where(mov => mov.OccurredAtUtc >= startUtc && mov.OccurredAtUtc < endUtc)
            .Select(mov => mov.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(utc => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone)))
            .Select(g => new BqHistoryDayCount(g.Key, g.Count()))
            .OrderBy(d => d.Day)
            .ToList();
    }

    /// <summary>
    /// Os MOVIMENTOS DETALHADOS do Histórico (o dmo-html): o intervalo
    /// desde/até (inclusivo, em hora local; null = sem limite) sobre a DATA
    /// REAL do movimento, com os filtros do html: a máquina, o tipo (o token
    /// canónico; um token desconhecido não filtra), o reparador (o nome
    /// congelado ou o nº do operador) e a pesquisa de texto (a boquilha e a
    /// referência da produção pelo início delas ou da parte BQ; o lote, o nº
    /// da produção, a máquina e o reparador por parte do texto; sem distinguir
    /// maiúsculas). Com <paramref name="toolId"/>, só os movimentos dessa
    /// ferramenta (a BQ e o lote: o «Histórico BQ» das Opções do registo).
    /// Do mais recente para o mais antigo.
    /// </summary>
    public async Task<IReadOnlyList<BqHistoryMovementItem>> ListMovementsAsync(
        DateOnly? desde,
        DateOnly? ate,
        string? machine,
        string? type,
        string? operatorId,
        string? text,
        CancellationToken cancellationToken = default,
        int? limit = null,
        string? toolId = null,
        int? jobOnId = null,
        bool onlyAn = false)
    {
        var (startUtc, endUtc) = UtcBounds(desde, ate);

        var filtered = Filter(Rows(), startUtc, endUtc, machine, ParseType(type), operatorId, text, toolId: toolId);
        // Uma produção (o painel das linhas de quem só vê): pelo id do Job On, exato.
        if (jobOnId is int wantedJobOn)
        {
            filtered = filtered.Where(row => row.JobOnId == wantedJobOn);
        }

        // Só os movimentos com AN (Diogo, 28/10/2026: onde param os AN de cada lote).
        if (onlyAn)
        {
            filtered = filtered.Where(row => row.AnQuantity != null);
        }

        var rows = filtered
            .OrderByDescending(row => row.OccurredAtUtc);
        return await ToItems(limit is int max ? rows.Take(max) : rows)
            .ToListAsync(cancellationToken);
    }

    private static (DateTime StartUtc, DateTime EndUtc) UtcBounds(DateOnly? desde, DateOnly? ate)
    {
        var zone = DMO.Alpha.Core.Runtime.DmoBusinessTime.Zone;
        var startUtc = desde is { } d
            ? TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), zone)
            : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        var endUtc = ate is { } a
            ? TimeZoneInfo.ConvertTimeToUtc(a.ToDateTime(TimeOnly.MinValue).AddDays(1), zone)
            : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        return (startUtc, endUtc);
    }

    /// <summary>O token canónico do tipo; um valor desconhecido não filtra.</summary>
    private static BqMovementType? ParseType(string? type) => type?.Trim() switch
    {
        "saida" => BqMovementType.Saida,
        "entrada" => BqMovementType.Entrada,
        "entrada_sem_reparacao" => BqMovementType.EntradaSemReparacao,
        _ => null
    };

    /// <summary>
    /// Uma linha do histórico: o movimento → o trace → a ferramenta (o
    /// tool_id próprio OU o do contexto associado) → a produção e a máquina.
    /// </summary>
    private sealed class HistoryRow
    {
        public int Id { get; set; }
        public int TraceId { get; set; }
        public BqMovementType Type { get; set; }
        public int Quantity { get; set; }
        public int? AnQuantity { get; set; }
        public int? Discrepancy { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? RegisteredByOperatorId { get; set; }
        public string? RepairerName { get; set; }
        public string? ToolReference { get; set; }
        public string? ToolLot { get; set; }
        public string? ToolId { get; set; }
        public string? MachineCode { get; set; }
        public string? ProductionNumber { get; set; }
        public string? ProductionReference { get; set; }
        public int? JobOnId { get; set; }
    }

    private IQueryable<HistoryRow> Rows() =>
        from mov in _db.BqMovements.AsNoTracking()
        join tr in _db.BqRepairTraces.AsNoTracking() on mov.BqRepairTraceId equals tr.Id
        join ctx in _db.BqContexts.AsNoTracking() on tr.BqContextId equals ctx.Id into ctxGroup
        from ctx in ctxGroup.DefaultIfEmpty()
        join j in _db.JobOns.AsNoTracking() on (ctx != null ? ctx.JobOnId ?? ctx.ReplacedJobOnId : null) equals j.Id into jobGroup
        from j in jobGroup.DefaultIfEmpty()
        join m in _db.Machines.AsNoTracking() on (j != null ? j.MachineId : 0) equals m.Id into machineGroup
        from m in machineGroup.DefaultIfEmpty()
        join t in _db.Tools.AsNoTracking().Where(x => x.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId) on (ctx != null ? ctx.ToolId : tr.ToolId) equals t.ToolId into toolGroup
        from t in toolGroup.DefaultIfEmpty()
        select new HistoryRow
        {
            Id = mov.Id,
            TraceId = mov.BqRepairTraceId,
            Type = mov.Type,
            Quantity = mov.Quantity,
            AnQuantity = mov.AnQuantity,
            Discrepancy = mov.Discrepancy,
            OccurredAtUtc = mov.OccurredAtUtc,
            RegisteredByOperatorId = mov.RegisteredByOperatorId,
            RepairerName = mov.BqRepairerNameFrozen,
            ToolReference = t != null ? t.Reference : null,
            ToolLot = t != null ? t.Lot : null,
            ToolId = t != null ? t.ToolId : null,
            // A MÁQUINA EFETIVA: a congelada no movimento; senão a da
            // produção; o registo avulso só resolve uma compatibilidade
            // única (nunca escolhe entre várias).
            MachineCode = mov.MachineCodeFrozen != null
                ? mov.MachineCodeFrozen
                : j != null && m != null
                ? m.Code
                : _db.ToolMachineLinks.Count(link => link.ToolId == tr.ToolId) == 1
                    ? _db.ToolMachineLinks.AsNoTracking()
                        .Where(link => link.ToolId == tr.ToolId)
                        .OrderBy(link => link.MachineId)
                        .Select(link => link.Machine.Code)
                        .FirstOrDefault()
                    : null,
            ProductionNumber = j != null ? j.ProductionNumber : null,
            ProductionReference = j != null ? j.Reference : null,
            JobOnId = j != null ? j.Id : (int?)null
        };

    /// <summary>
    /// Os filtros do histórico, num só sítio: o período sobre a data real e,
    /// quando indicados, o tipo, a máquina, o reparador, a referência da
    /// produção e a pesquisa de texto (sem distinguir maiúsculas; o nº do
    /// operador distingue). As referências seguem a regra de
    /// <see cref="ReferenceMatch"/>: pelo início, ou pela parte BQ, nunca a meio.
    /// </summary>
    private static IQueryable<HistoryRow> Filter(
        IQueryable<HistoryRow> rows,
        DateTime startUtc,
        DateTime endUtc,
        string? machine,
        BqMovementType? type,
        string? operatorId,
        string? text,
        string? productionReference = null,
        string? toolId = null)
    {
        rows = rows.Where(row => row.OccurredAtUtc >= startUtc && row.OccurredAtUtc < endUtc);

        if (toolId?.Trim() is { Length: > 0 } toolFilter)
        {
            rows = rows.Where(row => row.ToolId == toolFilter);
        }

        if (type is BqMovementType wantedType)
        {
            rows = rows.Where(row => row.Type == wantedType);
        }

        if (machine?.Trim().ToUpperInvariant() is { Length: > 0 } machineFilter)
        {
            rows = rows.Where(row => row.MachineCode != null && row.MachineCode.ToUpper().Contains(machineFilter));
        }

        if (operatorId?.Trim() is { Length: > 0 } operatorFilter)
        {
            var operatorUpper = operatorFilter.ToUpperInvariant();
            rows = rows.Where(row =>
                (row.RegisteredByOperatorId != null && row.RegisteredByOperatorId.Contains(operatorFilter))
                || (row.RepairerName != null && row.RepairerName.ToUpper().Contains(operatorUpper)));
        }

        if (ReferenceMatch.Normalize(productionReference) is { } referenceFilter)
        {
            rows = rows.Where(ReferenceMatch.Predicate<HistoryRow>(row => row.ProductionReference, referenceFilter));
        }

        if (ReferenceMatch.Normalize(text) is { } textFilter)
        {
            rows = rows.Where(ReferenceMatch.Predicate<HistoryRow>(row => row.ToolReference, textFilter)
                .Or(ReferenceMatch.Predicate<HistoryRow>(row => row.ProductionReference, textFilter))
                .Or(row => (row.ToolLot != null && row.ToolLot.ToUpper().Contains(textFilter))
                    || (row.ProductionNumber != null && row.ProductionNumber.ToUpper().Contains(textFilter))
                    || (row.MachineCode != null && row.MachineCode.ToUpper().Contains(textFilter))
                    || (row.RepairerName != null && row.RepairerName.ToUpper().Contains(textFilter))
                    || (row.RegisteredByOperatorId != null && row.RegisteredByOperatorId.ToUpper().Contains(textFilter))));
        }

        return rows;
    }

    private static IQueryable<BqHistoryMovementItem> ToItems(IQueryable<HistoryRow> rows) =>
        rows.Select(row => new BqHistoryMovementItem(
            row.Id,
            row.TraceId,
            row.Type.ToString(),
            row.Quantity,
            row.Discrepancy,
            row.OccurredAtUtc,
            row.RegisteredByOperatorId,
            row.ToolReference,
            row.ToolLot,
            row.MachineCode,
            row.ProductionNumber,
            row.ProductionReference,
            row.RepairerName,
            row.ToolId,
            row.AnQuantity));
}
