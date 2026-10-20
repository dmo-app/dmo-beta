using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Implementação de <see cref="IControloRevisaoQuery"/>: uma consulta por
/// pedido sobre Controlos, Job On, máquina, Peso e Pegamentos. O dia de cada
/// controlo é o primeiro registo gravado (o primeiro Peso, ou os Pegamentos
/// quando não há Peso), em hora de Lisboa. Uma linha por controlo, mesmo
/// com vários Pesos (um por CM+BQ, 18/10/2026): as ferramentas e o peso
/// médio da linha são os do Peso das ferramentas atuais, senão do mais
/// recente; a procura encontra o controlo por qualquer um deles.
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
        string? maquina = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = Bounds(day, day.AddDays(1));
        var rows = await Filter(Rows(), procura, estado, maquina)
            .Where(r => r.RegistadoEmUtc >= fromUtc && r.RegistadoEmUtc < toUtc)
            .OrderByDescending(r => r.RegistadoEmUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToItem).ToList();
    }

    public async Task<IReadOnlyList<ControloRevisaoItem>> PeriodAsync(
        DateOnly from,
        DateOnly toExclusive,
        string? procura,
        ControloRevisaoEstado? estado,
        string? maquina,
        int max,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = Bounds(from, toExclusive);
        var rows = await Filter(Rows(), procura, estado, maquina)
            .Where(r => r.RegistadoEmUtc >= fromUtc && r.RegistadoEmUtc < toUtc)
            .OrderByDescending(r => r.RegistadoEmUtc)
            .Take(Math.Max(1, max))
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
        string? maquina = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await Filter(Rows(), procura, estado, maquina)
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
        // A regra da comparação do Peso (ProducaoAnterior; sweep de
        // 19/10/2026, C-2, o especialista R29): pelo início e não pela ordem
        // de registo, só nas máquinas da ficha da CM atual da produção.
        var current = await _db.JobOns.AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new
            {
                j.Reference,
                j.InicioEm,
                MaquinasDaCm = _db.CmContexts
                    .Where(cm => cm.JobOnId == j.Id)
                    .SelectMany(cm => _db.ToolMachineLinks.Where(link => link.ToolId == cm.ToolId))
                    .Select(link => link.MachineId)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }

        var reference = current.Reference;
        var previousJobOnId = await _db.JobOns.AsNoTracking()
            .Where(j => j.Reference == reference && j.Id != jobOnId
                && _db.Controlos.Any(c => c.JobOnId == j.Id
                    && _db.PesoMeasurements.Any(m => m.Peso.ControloId == c.Id)))
            .SoAnteriores(j => j, jobOnId, current.InicioEm)
            .SoMaquinasDaCm(j => j, current.MaquinasDaCm)
            .MaisRecentePrimeiro(j => j)
            .Select(j => (int?)j.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return previousJobOnId is int previous
            ? await FindByJobOnAsync(previous, cancellationToken)
            : null;
    }

    public Task<string?> OperatorNameAsync(string operatorId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking()
            .Where(u => u.OperatorId == operatorId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Os controlos com Peso ou Pegamentos (os que há para rever).</summary>
    private IQueryable<Row> Rows() =>
        AllControlos().Where(r => r.HasPeso || r.HasPegamentos);

    // O estado de cada controlo sai da regra única (ControloAprovacao; sweep
    // de 19/10/2026, C-1): das decisões das partes e dos registos que
    // existem, e não da ordem dos cliques.
    private IQueryable<Row> AllControlos() =>
        from c in ControloAprovacao.Estados(_db)
        join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
        join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
        // O Peso mostrado na linha (um só, para não repetir o controlo): o da
        // CM e da BQ que a produção tem agora (o mais antigo dessa
        // combinação), senão o mais recente.
        let cmAtual = _db.CmContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => x.ToolId).FirstOrDefault()
        let bqAtual = _db.BqContexts.Where(x => x.JobOnId == c.JobOnId).Select(x => x.ToolId).FirstOrDefault()
        from p in _db.Pesos.AsNoTracking()
            .Where(x => x.ControloId == c.ControloId)
            .OrderByDescending(x => (cmAtual == null || x.CmToolId == cmAtual) && (bqAtual == null || x.BqToolId == bqAtual))
            .ThenBy(x => (cmAtual == null || x.CmToolId == cmAtual) && (bqAtual == null || x.BqToolId == bqAtual) ? x.Id : -x.Id)
            .Take(1)
            .DefaultIfEmpty()
        let pegamentosEm = _db.Pegamentos.Where(g => g.ControloId == c.ControloId).Min(g => g.RegistadoEmUtc)
        let pesosEm = _db.Pesos.Where(x => x.ControloId == c.ControloId).Min(x => x.RegistadoEmUtc)
        select new Row
        {
            ControloId = c.ControloId,
            JobOnId = j.Id,
            Reference = j.Reference,
            ProductionNumber = j.ProductionNumber,
            Machine = m.Code,
            HasPeso = p != null,
            PesoCount = _db.Pesos.Count(x => x.ControloId == c.ControloId),
            HasPegamentos = _db.Pegamentos.Any(g => g.ControloId == c.ControloId),
            CmReference = p != null ? (p.CmToolReferenceFrozen ?? p.CmTool.Reference) : null,
            BqReference = p != null ? (p.BqToolReferenceFrozen ?? p.BqTool.Reference) : null,
            BqLot = p != null ? (p.BqToolLotFrozen ?? p.BqTool.Lot) : null,
            WaterTemperatureC = p != null ? p.WaterTemperatureC : 0,
            WaterDensity = p != null ? p.WaterDensityGramsPerCm3 : null,
            VolumeMarisaCm3 = p != null ? p.VolumeMarisaCm3 : 0m,
            VolumePuncaoCm3 = p != null ? p.VolumePuncaoCm3 : 0m,
            GlassDensity = p != null ? p.GlassDensityGramsPerCm3 : 0m,
            AverageWaterGrams = p != null ? p.Measurements.Average(x => (decimal?)x.WaterWeightGrams) : null,
            // As leituras de todos os Pesos do controlo (a «produção
            // anterior com Peso» da comparação é a que tem leituras).
            PesoMeasurementCount = _db.PesoMeasurements.Count(x => x.Peso.ControloId == c.ControloId),
            // O dia é o do primeiro registo: o primeiro Peso, senão os
            // Pegamentos.
            RegistadoEmUtc = pesosEm != null ? pesosEm : pegamentosEm,
            RegistadoPor = pesosEm != null
                ? _db.Pesos.Where(x => x.ControloId == c.ControloId && x.RegistadoEmUtc != null)
                    .OrderBy(x => x.RegistadoEmUtc).Select(x => x.RegistadoPor).FirstOrDefault()
                : _db.Pegamentos.Where(g => g.ControloId == c.ControloId && g.RegistadoEmUtc != null)
                    .OrderBy(g => g.RegistadoEmUtc).Select(g => g.RegistadoPor).FirstOrDefault(),
            ApprovedAtUtc = c.ApprovedAtUtc,
            NotApprovedAtUtc = c.NotApprovedAtUtc,
            // Quem decidiu: o nome do operador, quando existe (19/10/2026).
            DecididoPor = c.ApprovedByOperatorId ?? c.NotApprovedByOperatorId,
            DecididoPorNome = _db.Users
                .Where(u => u.OperatorId == (c.ApprovedByOperatorId ?? c.NotApprovedByOperatorId))
                .Select(u => u.Name)
                .FirstOrDefault()
        };

    private IQueryable<Row> Filter(IQueryable<Row> rows, string? procura, ControloRevisaoEstado? estado, string? maquina = null)
    {
        if (!string.IsNullOrWhiteSpace(maquina))
        {
            var code = maquina.Trim().ToUpperInvariant();
            rows = rows.Where(r => r.Machine.ToUpper() == code);
        }

        if (ReferenceMatch.Normalize(procura) is { } term)
        {
            // As referências (da produção, e da CM e da BQ de QUALQUER Peso do
            // controlo: um controlo com dois Pesos aparece pela BQ de qualquer
            // um) pelo início delas ou da parte BQ, nunca a meio
            // (ReferenceMatch, Diogo 18/10/2026); o nº, a máquina e o lote
            // continuam por parte do texto.
            var pesosQueCorrespondem = _db.Pesos
                .Where(ReferenceMatch.Predicate<Peso>(x => x.CmToolReferenceFrozen ?? x.CmTool.Reference, term)
                    .Or(ReferenceMatch.Predicate<Peso>(x => x.BqToolReferenceFrozen ?? x.BqTool.Reference, term))
                    .Or(x => (x.BqToolLotFrozen ?? x.BqTool.Lot) != null && (x.BqToolLotFrozen ?? x.BqTool.Lot)!.ToUpper().Contains(term)))
                .Where(x => x.ControloId != null)
                .Select(x => x.ControloId!.Value);
            rows = rows.Where(ReferenceMatch.Predicate<Row>(r => r.Reference, term)
                .Or(r => r.ProductionNumber.ToUpper().Contains(term)
                    || r.Machine.ToUpper().Contains(term)
                    || pesosQueCorrespondem.Contains(r.ControloId)));
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
        if (r.AverageWaterGrams is { } water && WaterDensityTable.ForPeso(r.WaterDensity, r.WaterTemperatureC) is { } density)
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
            estado)
        {
            PesoCount = r.PesoCount,
            DecididoPor = r.DecididoPorNome ?? r.DecididoPor,
            DecididoEmUtc = (r.ApprovedAtUtc ?? r.NotApprovedAtUtc) is { } decidido
                ? DateTime.SpecifyKind(decidido, DateTimeKind.Utc)
                : null
        };
    }

    private sealed class Row
    {
        public int ControloId { get; init; }
        public int JobOnId { get; init; }
        public string Reference { get; init; } = "";
        public string ProductionNumber { get; init; } = "";
        public string Machine { get; init; } = "";
        public bool HasPeso { get; init; }
        public int PesoCount { get; init; }
        public bool HasPegamentos { get; init; }
        public string? CmReference { get; init; }
        public string? BqReference { get; init; }
        public string? BqLot { get; init; }
        public int WaterTemperatureC { get; init; }
        public decimal? WaterDensity { get; init; }
        public decimal VolumeMarisaCm3 { get; init; }
        public decimal VolumePuncaoCm3 { get; init; }
        public decimal GlassDensity { get; init; }
        public decimal? AverageWaterGrams { get; init; }
        public int PesoMeasurementCount { get; init; }
        public DateTime? RegistadoEmUtc { get; init; }
        public string? RegistadoPor { get; init; }
        public DateTime? ApprovedAtUtc { get; init; }
        public DateTime? NotApprovedAtUtc { get; init; }
        public string? DecididoPor { get; init; }
        public string? DecididoPorNome { get; init; }
    }
}
