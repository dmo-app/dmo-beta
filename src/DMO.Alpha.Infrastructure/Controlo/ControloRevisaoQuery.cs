using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Implementação de <see cref="IControloRevisaoQuery"/>: uma consulta por
/// pedido sobre Controlos, Job On, máquina, Peso e Pegamentos. O dia de cada
/// controlo é o primeiro registo gravado (o Peso, ou os Pegamentos quando
/// não há Peso), em hora de Lisboa.
/// </summary>
public sealed class ControloRevisaoQuery : IControloRevisaoQuery
{
    private const int MaxUndated = 50;

    private readonly DmoDbContext _db;

    public ControloRevisaoQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ControloRevisaoDia>> MonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = Bounds(new DateOnly(year, month, 1), new DateOnly(year, month, 1).AddMonths(1));
        var rows = await Rows()
            .Where(r => r.RegistadoEmUtc >= fromUtc && r.RegistadoEmUtc < toUtc)
            .Select(r => new { r.RegistadoEmUtc, r.ApprovedAtUtc, r.NotApprovedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => DmoBusinessTime.ToLocal(r.RegistadoEmUtc!.Value).Day)
            .Select(g => new ControloRevisaoDia(
                g.Key,
                g.Count(),
                g.Count(r => r.ApprovedAtUtc is null && r.NotApprovedAtUtc is null)))
            .OrderBy(d => d.Day)
            .ToList();
    }

    public async Task<IReadOnlyList<ControloRevisaoItem>> DayAsync(
        DateOnly day,
        string? procura,
        ControloRevisaoEstado? estado,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = Bounds(day, day.AddDays(1));
        var rows = await Filter(Rows(), procura, estado)
            .Where(r => r.RegistadoEmUtc >= fromUtc && r.RegistadoEmUtc < toUtc)
            .OrderByDescending(r => r.RegistadoEmUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToItem).ToList();
    }

    public Task<int> CountUndatedPendingAsync(CancellationToken cancellationToken = default) =>
        Rows()
            .Where(r => r.RegistadoEmUtc == null && r.ApprovedAtUtc == null && r.NotApprovedAtUtc == null)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ControloRevisaoItem>> UndatedAsync(
        string? procura,
        ControloRevisaoEstado? estado,
        CancellationToken cancellationToken = default)
    {
        var rows = await Filter(Rows(), procura, estado)
            .Where(r => r.RegistadoEmUtc == null)
            .OrderByDescending(r => r.JobOnId)
            .Take(MaxUndated)
            .ToListAsync(cancellationToken);
        return rows.Select(ToItem).ToList();
    }

    public async Task<ControloRevisaoItem?> FindByJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var row = await AllControlos()
            .Where(r => r.JobOnId == jobOnId)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToItem(row);
    }

    public async Task<ControloRevisaoItem?> FindPreviousWithPesoAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var reference = await _db.JobOns.AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => j.Reference)
            .FirstOrDefaultAsync(cancellationToken);
        if (reference is null)
        {
            return null;
        }

        var row = await AllControlos()
            .Where(r => r.Reference == reference && r.JobOnId < jobOnId && r.PesoMeasurementCount > 0)
            .OrderByDescending(r => r.JobOnId)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToItem(row);
    }

    public Task<string?> OperatorNameAsync(string operatorId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking()
            .Where(u => u.OperatorId == operatorId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Os controlos com Peso ou Pegamentos (os que há para rever).</summary>
    private IQueryable<Row> Rows() =>
        AllControlos().Where(r => r.HasPeso || r.HasPegamentos);

    private IQueryable<Row> AllControlos() =>
        from c in _db.Controlos.AsNoTracking()
        join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
        join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
        join p in _db.Pesos.AsNoTracking() on (int?)c.Id equals p.ControloId into pesos
        from p in pesos.DefaultIfEmpty()
        let pegamentosEm = _db.Pegamentos.Where(g => g.ControloId == c.Id).Min(g => g.RegistadoEmUtc)
        select new Row
        {
            ControloId = c.Id,
            JobOnId = j.Id,
            Reference = j.Reference,
            ProductionNumber = j.ProductionNumber,
            Machine = m.Code,
            HasPeso = p != null,
            HasPegamentos = _db.Pegamentos.Any(g => g.ControloId == c.Id),
            CmReference = p != null ? (p.CmToolReferenceFrozen ?? p.CmTool.Reference) : null,
            BqReference = p != null ? (p.BqToolReferenceFrozen ?? p.BqTool.Reference) : null,
            BqLot = p != null ? (p.BqToolLotFrozen ?? p.BqTool.Lot) : null,
            WaterTemperatureC = p != null ? p.WaterTemperatureC : 0,
            VolumeMarisaCm3 = p != null ? p.VolumeMarisaCm3 : 0m,
            VolumePuncaoCm3 = p != null ? p.VolumePuncaoCm3 : 0m,
            GlassDensity = p != null ? p.GlassDensityGramsPerCm3 : 0m,
            AverageWaterGrams = p != null ? p.Measurements.Average(x => (decimal?)x.WaterWeightGrams) : null,
            PesoMeasurementCount = p != null ? p.Measurements.Count : 0,
            RegistadoEmUtc = p != null && p.RegistadoEmUtc != null ? p.RegistadoEmUtc : pegamentosEm,
            RegistadoPor = p != null && p.RegistadoEmUtc != null
                ? p.RegistadoPor
                : _db.Pegamentos.Where(g => g.ControloId == c.Id && g.RegistadoEmUtc != null)
                    .OrderBy(g => g.RegistadoEmUtc).Select(g => g.RegistadoPor).FirstOrDefault(),
            ApprovedAtUtc = c.ApprovedAtUtc,
            NotApprovedAtUtc = c.NotApprovedAtUtc
        };

    private static IQueryable<Row> Filter(IQueryable<Row> rows, string? procura, ControloRevisaoEstado? estado)
    {
        var term = procura?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            rows = rows.Where(r =>
                r.Reference.ToUpper().Contains(term)
                || r.ProductionNumber.ToUpper().Contains(term)
                || r.Machine.ToUpper().Contains(term)
                || (r.CmReference != null && r.CmReference.ToUpper().Contains(term))
                || (r.BqReference != null && r.BqReference.ToUpper().Contains(term))
                || (r.BqLot != null && r.BqLot.ToUpper().Contains(term)));
        }

        return estado switch
        {
            ControloRevisaoEstado.Pendente => rows.Where(r => r.ApprovedAtUtc == null && r.NotApprovedAtUtc == null),
            ControloRevisaoEstado.Aprovado => rows.Where(r => r.ApprovedAtUtc != null),
            ControloRevisaoEstado.NaoAprovado => rows.Where(r => r.NotApprovedAtUtc != null),
            _ => rows
        };
    }

    private static (DateTime FromUtc, DateTime ToUtc) Bounds(DateOnly from, DateOnly to) =>
        (DateTime.SpecifyKind(DmoBusinessTime.ToUtc(from.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc),
         DateTime.SpecifyKind(DmoBusinessTime.ToUtc(to.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc));

    private static ControloRevisaoItem ToItem(Row r)
    {
        decimal? average = null;
        // A média do peso do vidro é linear na massa de água: calcula-se da
        // média da água com os mesmos valores congelados do Peso.
        if (r.AverageWaterGrams is { } water && WaterDensityTable.Resolve(r.WaterTemperatureC) is { } density)
        {
            average = PesoCalculation.GlassWeightGrams(
                PesoCalculation.CapacityCm3(water, density),
                r.VolumeMarisaCm3,
                r.VolumePuncaoCm3,
                r.GlassDensity);
        }

        var estado = r.ApprovedAtUtc is not null
            ? ControloRevisaoEstado.Aprovado
            : r.NotApprovedAtUtc is not null ? ControloRevisaoEstado.NaoAprovado : ControloRevisaoEstado.Pendente;
        return new ControloRevisaoItem(
            r.ControloId, r.JobOnId, r.Reference, r.ProductionNumber, r.Machine,
            r.CmReference, r.BqReference, r.BqLot, average, r.HasPegamentos,
            r.RegistadoEmUtc is { } em ? DateTime.SpecifyKind(em, DateTimeKind.Utc) : null,
            r.RegistadoPor,
            estado);
    }

    private sealed class Row
    {
        public int ControloId { get; init; }
        public int JobOnId { get; init; }
        public string Reference { get; init; } = "";
        public string ProductionNumber { get; init; } = "";
        public string Machine { get; init; } = "";
        public bool HasPeso { get; init; }
        public bool HasPegamentos { get; init; }
        public string? CmReference { get; init; }
        public string? BqReference { get; init; }
        public string? BqLot { get; init; }
        public int WaterTemperatureC { get; init; }
        public decimal VolumeMarisaCm3 { get; init; }
        public decimal VolumePuncaoCm3 { get; init; }
        public decimal GlassDensity { get; init; }
        public decimal? AverageWaterGrams { get; init; }
        public int PesoMeasurementCount { get; init; }
        public DateTime? RegistadoEmUtc { get; init; }
        public string? RegistadoPor { get; init; }
        public DateTime? ApprovedAtUtc { get; init; }
        public DateTime? NotApprovedAtUtc { get; init; }
    }
}
