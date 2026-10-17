using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A Comparação_B (corte C, spec PESO.md): os Pesos baseline das produções
/// da MESMA referência (a mesma base do conjunto) e o diferencial pela
/// POSIÇÃO física das leituras. O ranking dos candidatos é AJUDA (a regra
/// do Owner: a mesma MÁQUINA em primeiro — o desgaste por máquina —, depois
/// a recência); a seleção é sempre humana e nunca substituída.
/// </summary>
public sealed class PesoComparisonQuery : IPesoComparisonQuery
{
    private readonly DmoDbContext _db;
    private readonly IPesoQuery _pesoQuery;

    public PesoComparisonQuery(DmoDbContext db, IPesoQuery pesoQuery)
    {
        _db = db;
        _pesoQuery = pesoQuery;
    }

    public async Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        int controloId,
        CancellationToken cancellationToken = default)
    {
        // A produção atual (o controlo → o Job On: a referência, a máquina
        // e a CM do contexto — a ferramenta cuja FICHA decide o que pode
        // ser comparado).
        var current = await _db.Controlos
            .Where(c => c.Id == controloId)
            .Select(c => new
            {
                JobOnReference = _db.JobOns
                    .Where(j => j.Id == c.JobOnId)
                    .Select(j => j.Reference)
                    .FirstOrDefault(),
                JobOnMachineId = _db.JobOns
                    .Where(j => j.Id == c.JobOnId)
                    .Select(j => (int?)j.MachineId)
                    .FirstOrDefault(),
                CmToolId = _db.CmContexts
                    .Where(cm => cm.JobOnId == c.JobOnId)
                    .Select(cm => cm.ToolId)
                    .FirstOrDefault(),
                // As máquinas ligadas à CM da produção, na mesma consulta.
                LinkedMachineIds = _db.CmContexts
                    .Where(cm => cm.JobOnId == c.JobOnId)
                    .SelectMany(cm => _db.ToolMachineLinks.Where(link => link.ToolId == cm.ToolId))
                    .Select(link => link.MachineId)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null || current.JobOnReference is null)
        {
            return Array.Empty<PesoComparisonCandidate>();
        }

        // A FOLHA DAS FERRAMENTAS DECIDE O QUE PODE SER COMPARADO (o
        // Owner 2026-10-12: «se a tool aceita B1 e C3 e o ultimo for C3
        // pode ser comparado, se a tool trabalha com B1 apenas, e o
        // ultimo for B3 ai ja nao pode ser... Usar a informaçao que esta
        // na folha das ferramentas para decidir o que pode ser
        // comparado»): as MÁQUINAS do ToolMachineLink da CM corrente da
        // produção («máquinas em que pode trabalhar» — o link n-n da
        // ficha) são o conjunto PERMITIDO — as produções da mesma
        // referência cuja máquina esteja no link aparecem no dropdown (a
        // cruzada PERMITIDA: a tool [B1, C3] também mostra as da C3); as
        // outras simplesmente não aparecem (a opção não existe — nunca
        // um bloqueio em runtime). Sem CM no contexto, ou com a ficha
        // ainda SEM máquinas ligadas, não há informação de
        // compatibilidade: o conjunto completo da referência continua
        // (função antes de proteção — completa a ficha depois).
        List<int>? comparisonMachineIds = null;
        if (current.CmToolId is { Length: > 0 } && current.LinkedMachineIds.Count > 0)
        {
            comparisonMachineIds = current.LinkedMachineIds;
        }

        // Os Pesos baseline das OUTRAS produções com a mesma referência.
        var rows = await _db.Pesos
            .Where(p => p.ControloId != null && p.ControloId != controloId)
            .Select(p => new
            {
                PesoId = p.Id,
                ControloId = p.ControloId!.Value,
                p.CmToolId,
                ProductionNumber = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.ProductionNumber)
                    .FirstOrDefault() ?? string.Empty,
                JobOnMachineId = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => (int?)j.MachineId)
                    .FirstOrDefault(),
                JobOnReference = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.Reference)
                    .FirstOrDefault() ?? string.Empty,
                MachineCode = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Join(_db.Machines, j => j.MachineId, m => m.Id, (j, m) => m.Code)
                    .FirstOrDefault(),
                p.WaterTemperatureC,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.GlassDensityGramsPerCm3,
                Readings = p.Measurements
                    .Select(m => new { m.RowPosition, m.WaterWeightGrams })
                    .OrderBy(m => m.RowPosition)
                    .ToList()
            })
            .Where(x => x.JobOnReference == current.JobOnReference)
            .ToListAsync(cancellationToken);

        // A FILTRAGEM PELO LINK DA FERRAMENTA (a folha decide o que pode
        // ser comparado): só as produções da referência cuja MÁQUINA
        // está no ToolMachineLink da CM corrente — o dropdown contém o
        // que a ferramenta aceita comparar (a tool [B1, C3] mostra B1 e
        // C3; a tool [B1] não mostra B3 — a opção simplesmente não
        // existe, nunca um bloqueio em runtime).
        if (comparisonMachineIds is { } allowedMachines)
        {
            rows = rows
                .Where(x => x.JobOnMachineId is int machineId && allowedMachines.Contains(machineId))
                .ToList();
        }

        // O lote da CM e as máquinas compatíveis da sua ficha (o Owner
        // 2026-10-17: «ao procurar as produções pode aparecer as máquinas
        // que estão no tool_id»), para o dropdown mostrar o contexto certo.
        var cmToolIds = rows.Select(x => x.CmToolId).Distinct().ToList();
        var lots = await _db.Tools
            .Where(t => cmToolIds.Contains(t.ToolId))
            .Select(t => new { t.ToolId, t.Lot })
            .ToDictionaryAsync(t => t.ToolId, t => t.Lot, cancellationToken);
        var links = await _db.ToolMachineLinks
            .Where(l => cmToolIds.Contains(l.ToolId))
            .Join(_db.Machines, l => l.MachineId, m => m.Id, (l, m) => new { l.ToolId, m.Code })
            .ToListAsync(cancellationToken);
        var compatible = links
            .GroupBy(l => l.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(l => l.Code).OrderBy(code => code, StringComparer.Ordinal).ToList());

        return rows
            .Select(x =>
            {
                var machineCode = x.JobOnMachineId is not null ? x.MachineCode ?? string.Empty : string.Empty;
                // A densidade da água resolve pela tabela canónica para o grau
                // congelado (invariante validada na criação — o padrão da
                // PesoQuery).
                var density = WaterDensityTable.Resolve(x.WaterTemperatureC)
                    ?? throw new InvalidOperationException(
                        $"O grau de temperatura da água congelado no Peso {x.PesoId} " +
                        "está fora do intervalo suportado pela tabela canónica de densidade da água.");
                var count = x.Readings.Count;
                decimal? avgWater = count > 0
                    ? x.Readings.Average(r => r.WaterWeightGrams)
                    : null;
                decimal? avgCapacity = null;
                decimal? avgGlass = null;
                if (avgWater is not null)
                {
                    // O mesmo cálculo por leitura da PesoQuery, para a procura
                    // mostrar os mesmos números que a tabela da comparação.
                    var capacities = x.Readings.Select(r => PesoCalculation.CapacityCm3(r.WaterWeightGrams, density)).ToList();
                    avgCapacity = capacities.Average();
                    avgGlass = capacities
                        .Select(cap => PesoCalculation.GlassWeightGrams(cap, x.VolumeMarisaCm3, x.VolumePuncaoCm3, x.GlassDensityGramsPerCm3))
                        .Average();
                }
                return new PesoComparisonCandidate(
                    x.PesoId,
                    x.ProductionNumber,
                    x.JobOnReference,
                    machineCode,
                    SameMachine: x.JobOnMachineId == current.JobOnMachineId,
                    ReadingCount: count,
                    avgWater is not null ? Math.Round(avgWater.Value, 2, MidpointRounding.AwayFromZero) : null,
                    avgCapacity,
                    avgGlass,
                    lots.GetValueOrDefault(x.CmToolId),
                    compatible.GetValueOrDefault(x.CmToolId));
            })
            // Só a recência, a mais recente primeiro (o Owner 2026-10-17: «na
            // mesma máquina não está bem, pq o mesmo lote pode funcionar em
            // máquinas diferentes usando a mesma ferramenta, basta apenas
            // produção»). As máquinas permitidas continuam a vir da ficha da CM.
            .OrderByDescending(c => c.PesoId)
            .ToList();
    }

    public async Task<PesoComparisonView?> CompareAsync(
        int currentPesoId,
        int historicalPesoId,
        CancellationToken cancellationToken = default)
    {
        if (currentPesoId == historicalPesoId)
        {
            return null;
        }

        var current = await _pesoQuery.FindPesoAsync(currentPesoId, cancellationToken);
        return current is null
            ? null
            : await CompareAsync(current, historicalPesoId, cancellationToken);
    }

    public async Task<PesoComparisonView?> CompareAsync(
        PesoReadModel current,
        int historicalPesoId,
        CancellationToken cancellationToken = default)
    {
        var currentPesoId = current.PesoId;
        if (currentPesoId == historicalPesoId)
        {
            return null;
        }

        var historical = await _pesoQuery.FindPesoAsync(historicalPesoId, cancellationToken);
        if (historical is null)
        {
            return null;
        }

        // Os factos da produção histórica (o nº de produção e a máquina do
        // seu Job On — fatos humanos, ids fora das folhas).
        var historicalFacts = await _db.Pesos
            .Where(p => p.Id == historicalPesoId && p.ControloId != null)
            .Select(p => new
            {
                ProductionNumber = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.ProductionNumber)
                    .FirstOrDefault() ?? string.Empty,
                MachineCode = (
                    from c in _db.Controlos
                    join j in _db.JobOns on c.JobOnId equals j.Id
                    join m in _db.Machines on j.MachineId equals m.Id
                    where c.Id == p.ControloId
                    select m.Code)
                    .FirstOrDefault() ?? string.Empty,
                InicioEm = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.InicioEm)
                    .FirstOrDefault(),
                FimEm = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(c => c.Id == p.ControloId)
                        .Select(c => c.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.FimEm)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // O matching pela POSIÇÃO física (a regra da spec — nunca pelo
        // número/CM: ele é apenas indicativo). As posições presentes nos DOIS
        // Pesos participam; as sem contraparte ficam de fora (válido).
        var currentByPosition = current.Measurements
            .GroupBy(m => m.RowPosition)
            .ToDictionary(g => g.Key, g => g.First());
        var historicalByPosition = historical.Measurements
            .GroupBy(m => m.RowPosition)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = currentByPosition.Keys
            .Where(position => historicalByPosition.ContainsKey(position))
            .OrderBy(position => position)
            .Select(position =>
            {
                var c = currentByPosition[position];
                var h = historicalByPosition[position];
                return new PesoComparisonRow(
                    position,
                    c.CmSubject,
                    c.GlassWeightGrams,
                    h.CmSubject,
                    h.GlassWeightGrams,
                    c.CapacityCm3,
                    h.CapacityCm3,
                    Math.Round(c.GlassWeightGrams - h.GlassWeightGrams, 2, MidpointRounding.AwayFromZero),
                    Math.Round(c.CapacityCm3 - h.CapacityCm3, 2, MidpointRounding.AwayFromZero),
                    h.GlassWeightGrams != 0m
                        ? Math.Round((c.GlassWeightGrams - h.GlassWeightGrams) / h.GlassWeightGrams * 100m, 2, MidpointRounding.AwayFromZero)
                        : null);
            })
            .ToList();

        decimal? waterDelta = current.Averages is { } ca && historical.Averages is { } ha
            ? ca.AverageWaterWeightGrams - ha.AverageWaterWeightGrams
            : null;
        decimal? capacityDelta = current.Averages is { } ca2 && historical.Averages is { } ha2
            ? ca2.AverageCapacityCm3 - ha2.AverageCapacityCm3
            : null;
        decimal? glassDelta = current.Averages is { } ca3 && historical.Averages is { } ha3
            ? ca3.AverageGlassWeightGrams - ha3.AverageGlassWeightGrams
            : null;
        decimal? glassVariation = glassDelta is decimal delta && historical.Averages is { } ha4 && ha4.AverageGlassWeightGrams != 0
            ? delta / ha4.AverageGlassWeightGrams * 100m
            : null;

        return new PesoComparisonView(
            currentPesoId,
            historicalPesoId,
            historicalFacts?.ProductionNumber ?? string.Empty,
            historicalFacts?.MachineCode ?? string.Empty,
            current.Averages,
            historical.Averages,
            new PesoComparisonDeltas(
                waterDelta is not null ? Math.Round(waterDelta.Value, 2, MidpointRounding.AwayFromZero) : null,
                capacityDelta is not null ? Math.Round(capacityDelta.Value, 2, MidpointRounding.AwayFromZero) : null,
                glassDelta is not null ? Math.Round(glassDelta.Value, 2, MidpointRounding.AwayFromZero) : null,
                glassVariation is not null ? Math.Round(glassVariation.Value, 2, MidpointRounding.AwayFromZero) : null),
            rows,
            historicalFacts?.InicioEm,
            historicalFacts?.FimEm,
            historicalFacts?.InicioEm is { } inicio && historicalFacts.FimEm is { } fim && fim >= inicio
                ? fim.DayNumber - inicio.DayNumber + 1
                : null);
    }

    // -------------------------------------------------------------------
    // AS COMPARAÇÕES TODAS (o dmo-html: a comparação integrada acima
    // mantém-se intacta; os métodos abaixo servem a superfície própria
    // «Comparação de Peso», o «Comparar Pesos guardados» do histórico e o
    // registo de comparação guardado — o Owner: «comparaçao até o
    // historico, ja utiliza as diferentes comparaçoes»).
    // -------------------------------------------------------------------

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarIniciaisAsync(
        int jobOnId,
        CancellationToken cancellationToken = default)
    {
        // Os Pesos production-scoped do controlo da produção, do mais antigo
        // para o mais recente (o html ordena por created_at — o primeiro é
        // o peso inicial). Os factos humanos da CM vêm do tool_id do peso.
        var rows = await (
            from p in _db.Pesos.AsNoTracking()
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            where j.Id == jobOnId
            orderby p.Id ascending
            select new
            {
                PesoId = p.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                ToolId = t.ToolId,
                ToolReference = t.Reference,
                ToolLot = t.Lot
            })
            .ToListAsync(cancellationToken);

        return await ComOpcoesAsync(
            rows.Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot)),
            cancellationToken);
    }

    public Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosGuardadosAsync(
        CancellationToken cancellationToken = default) =>
        ListarPesosAsync(null, null, null, cancellationToken);

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosPorReferenciaAsync(
        string? referencia,
        CancellationToken cancellationToken = default)
    {
        var termo = referencia?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(termo))
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        // O filtro e o corte na base, antes de calcular os pesos médios.
        var rows = await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id into controlos
            from c in controlos.DefaultIfEmpty()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id into jobOns
            from j in jobOns.DefaultIfEmpty()
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id into machines
            from m in machines.DefaultIfEmpty()
            where (p.ControloId != null && j != null && m != null && j.Reference.ToUpper().Contains(termo))
                || (p.ControloId == null && t.Reference.ToUpper().Contains(termo))
            orderby p.Id descending
            select new
            {
                PesoId = p.Id,
                Reference = j != null ? (string?)j.Reference : null,
                ProductionNumber = j != null ? (string?)j.ProductionNumber : null,
                Machine = m != null ? (string?)m.Code : null,
                ToolId = (string?)t.ToolId,
                ToolReference = (string?)t.Reference,
                ToolLot = (string?)t.Lot
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(
            rows.Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot)),
            cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> OpcoesDosPesosAsync(
        IReadOnlyCollection<int> pesoIds,
        CancellationToken cancellationToken = default)
    {
        if (pesoIds.Count == 0)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        var rows = await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id into controlos
            from c in controlos.DefaultIfEmpty()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id into jobOns
            from j in jobOns.DefaultIfEmpty()
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id into machines
            from m in machines.DefaultIfEmpty()
            where pesoIds.Contains(p.Id)
            select new
            {
                PesoId = p.Id,
                Reference = j != null ? (string?)j.Reference : null,
                ProductionNumber = j != null ? (string?)j.ProductionNumber : null,
                Machine = m != null ? (string?)m.Code : null,
                ToolId = (string?)t.ToolId,
                ToolReference = (string?)t.Reference,
                ToolLot = (string?)t.Lot
            })
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(
            rows.Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot)),
            cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ProcurarPesosAsync(
        string? termo,
        string? referencia,
        int limite,
        CancellationToken cancellationToken = default)
    {
        var texto = termo?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(texto))
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        var refFiltro = referencia?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(refFiltro))
        {
            refFiltro = null;
        }

        var rows = await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id into controlos
            from c in controlos.DefaultIfEmpty()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id into jobOns
            from j in jobOns.DefaultIfEmpty()
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id into machines
            from m in machines.DefaultIfEmpty()
            where (refFiltro == null
                    || (j != null && j.Reference.ToUpper().Contains(refFiltro))
                    || (p.ControloId == null && t.Reference.ToUpper().Contains(refFiltro)))
                && ((j != null && (j.ProductionNumber.ToUpper().Contains(texto) || j.Reference.ToUpper().Contains(texto)))
                    || (m != null && m.Code.ToUpper().Contains(texto))
                    || t.Lot.ToUpper().Contains(texto)
                    || t.Reference.ToUpper().Contains(texto)
                    || _db.ToolMachineLinks.Any(link => link.ToolId == t.ToolId
                        && link.Machine.Code.ToUpper().Contains(texto)))
            orderby p.Id descending
            select new
            {
                PesoId = p.Id,
                Reference = j != null ? (string?)j.Reference : null,
                ProductionNumber = j != null ? (string?)j.ProductionNumber : null,
                Machine = m != null ? (string?)m.Code : null,
                ToolId = (string?)t.ToolId,
                ToolReference = (string?)t.Reference,
                ToolLot = (string?)t.Lot
            })
            .Take(limite)
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(
            rows.Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot)),
            cancellationToken), cancellationToken);
    }

    /// <summary>
    /// As máquinas onde a CM de cada opção trabalha (as ligações da
    /// ferramenta), como na procura da folha do Peso.
    /// </summary>
    private async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ComMaquinasDaCmAsync(
        IReadOnlyList<PesoComparacaoPesoOpcao> opcoes,
        CancellationToken cancellationToken)
    {
        var toolIds = opcoes.Select(o => o.CmToolId).OfType<string>().Distinct().ToList();
        if (toolIds.Count == 0)
        {
            return opcoes;
        }

        var maquinas = (await _db.ToolMachineLinks.AsNoTracking()
                .Where(link => toolIds.Contains(link.ToolId))
                .Join(_db.Machines.AsNoTracking(), link => link.MachineId, machine => machine.Id,
                    (link, machine) => new { link.ToolId, machine.Code })
                .ToListAsync(cancellationToken))
            .ToLookup(link => link.ToolId, link => link.Code);

        return opcoes
            .Select(opcao => opcao with
            {
                CmMachines = opcao.CmToolId is { } toolId
                    ? maquinas[toolId].Distinct().OrderBy(code => code, StringComparer.Ordinal).ToList()
                    : Array.Empty<string>()
            })
            .ToList();
    }

    public async Task<string?> ReferenciaDoPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            where p.Id == pesoId
            select new
            {
                CmReference = t.Reference,
                ProductionReference = _db.Controlos
                    .Where(c => c.Id == p.ControloId)
                    .Join(_db.JobOns, c => c.JobOnId, j => j.Id, (c, j) => j.Reference)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : row.ProductionReference ?? row.CmReference;
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosComparaveisAsync(
        int pesoId,
        CancellationToken cancellationToken = default)
    {
        // A referência da produção (quando o Peso tem produção) e a referência
        // da CM do Peso: os dois factos que tornam a comparação significativa.
        var current = await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            where p.Id == pesoId
            select new
            {
                CmReference = t.Reference,
                ProductionReference = _db.Controlos
                    .Where(c => c.Id == p.ControloId)
                    .Join(_db.JobOns, c => c.JobOnId, j => j.Id, (c, j) => j.Reference)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        return await ListarPesosAsync(pesoId, current.ProductionReference, current.CmReference, cancellationToken);
    }

    private async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosAsync(
        int? excludePesoId,
        string? productionReference,
        string? cmReference,
        CancellationToken cancellationToken)
    {
        var filtered = excludePesoId is not null;
        // TODOS os Pesos guardados (o histórico do html lista também os
        // pré-produção): os production-scoped com os factos da produção; os
        // tool-scoped identificados pela própria CM (o html: «Pré-produção»).
        var merged = (await (
            from p in _db.Pesos.AsNoTracking()
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id into controlos
            from c in controlos.DefaultIfEmpty()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id into jobOns
            from j in jobOns.DefaultIfEmpty()
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id into machines
            from m in machines.DefaultIfEmpty()
            where (p.ControloId != null && j != null && m != null
                    && (!filtered
                        || (p.Id != excludePesoId
                            && (j.Reference == productionReference || t.Reference == cmReference))))
                || (p.ControloId == null
                    && (!filtered || (p.Id != excludePesoId && t.Reference == cmReference)))
            orderby p.Id descending // do mais recente para o mais antigo, nos dois grupos
            select new
            {
                PesoId = p.Id,
                Reference = j != null ? (string?)j.Reference : null,
                ProductionNumber = j != null ? (string?)j.ProductionNumber : null,
                Machine = m != null ? (string?)m.Code : null,
                ToolId = (string?)t.ToolId,
                ToolReference = (string?)t.Reference,
                ToolLot = (string?)t.Lot
            })
            .ToListAsync(cancellationToken))
            .Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot))
            .ToList();

        return await ComOpcoesAsync(merged, cancellationToken);
    }

    /// <summary>
    /// As opções com as leituras e o peso médio calculados a partir dos
    /// inputs congelados + as leituras persistidas (o padrão canónico do
    /// histórico: a densidade resolve pela TABELA da fábrica, o cálculo é o
    /// <see cref="PesoCalculation"/> — nunca SQL, nunca segunda fórmula).
    /// </summary>
    private async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ComOpcoesAsync(
        IEnumerable<(int PesoId, string? Reference, string? ProductionNumber, string? Machine, string? ToolId, string? ToolReference, string? ToolLot)> rows,
        CancellationToken cancellationToken)
    {
        var lista = rows.ToList();
        if (lista.Count == 0)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        // Os inputs congelados e as leituras de cada Peso numa só consulta.
        var pesoIds = lista.Select(r => r.PesoId).ToList();
        var pesos = await _db.Pesos
            .AsNoTracking()
            .Where(p => pesoIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.WaterTemperatureC,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.GlassDensityGramsPerCm3,
                Weights = p.Measurements.Select(m => m.WaterWeightGrams).ToList()
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var porPeso = pesos.Values
            .Where(p => p.Weights.Count > 0)
            .ToDictionary(p => p.Id, p => p.Weights);

        return lista.Select(row =>
        {
            decimal? pesoMedio = null;
            if (porPeso.TryGetValue(row.PesoId, out var weights) && weights.Count > 0
                && pesos.TryGetValue(row.PesoId, out var peso)
                && WaterDensityTable.Resolve(peso.WaterTemperatureC) is { } density)
            {
                var total = 0m;
                foreach (var weight in weights)
                {
                    var capacity = PesoCalculation.CapacityCm3(weight, density);
                    total += PesoCalculation.GlassWeightGrams(
                        capacity, peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.GlassDensityGramsPerCm3);
                }

                pesoMedio = Math.Round(total / weights.Count, 2, MidpointRounding.AwayFromZero);
            }

            return new PesoComparacaoPesoOpcao(
                row.PesoId,
                row.Reference,
                row.ProductionNumber,
                row.Machine,
                row.ToolId,
                row.ToolReference,
                row.ToolLot,
                porPeso.TryGetValue(row.PesoId, out var leituras) ? leituras.Count : 0,
                pesoMedio);
        }).ToList();
    }

    public async Task<PesoComparacaoLeiturasResultado> CompararLeiturasAsync(
        PesoComparacaoLeiturasPedido pedido,
        CancellationToken cancellationToken = default)
    {
        if (pedido.PesoInicialId <= 0)
        {
            return Recusada(PesoComparacaoResultado.PesoInicialNaoSelecionado, pedido.PesoInicialId);
        }

        var inicial = await _pesoQuery.FindPesoAsync(pedido.PesoInicialId, cancellationToken);
        if (inicial is null)
        {
            return Recusada(PesoComparacaoResultado.PesoInicialNaoEncontrado, pedido.PesoInicialId);
        }

        // O contra-molde: o selecionado; na falta, o CM do próprio Peso
        // inicial (o fallback do html: «initial.context?.cm»). Nenhum dos
        // dois → recusa tipada (o CM por escolher).
        var cmToolId = pedido.CmToolId?.Trim();
        if (string.IsNullOrEmpty(cmToolId))
        {
            cmToolId = inicial.CmToolId;
        }

        if (string.IsNullOrWhiteSpace(cmToolId))
        {
            return Recusada(PesoComparacaoResultado.ContraMoldePorEscolher, pedido.PesoInicialId);
        }

        var cmTool = await _db.Tools.AsNoTracking()
            .Where(t => t.ToolId == cmToolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (cmTool is null)
        {
            return Recusada(PesoComparacaoResultado.ContraMoldePorEscolher, pedido.PesoInicialId);
        }

        // A temperatura: o grau inteiro arredondado e a tabela canónica (o
        // padrão do Peso); fora do intervalo 5–35 °C a recusa é tipada.
        var temperatureWholeDegree = WaterDensityTable.RoundToWholeDegree(pedido.WaterTemperatureC);
        var waterDensity = WaterDensityTable.Resolve(temperatureWholeDegree);
        if (waterDensity is not { } resolvedWaterDensity)
        {
            return Recusada(PesoComparacaoResultado.TemperaturaInvalida, pedido.PesoInicialId);
        }

        if (pedido.Leituras.Count == 0)
        {
            return Recusada(PesoComparacaoResultado.SemLeituras, pedido.PesoInicialId);
        }

        // A base consumida: a densidade do vidro da CM (pelo processo); na
        // sua falta, a CONGELADA do Peso inicial (o html: «cm.process ||
        // initial.result.glass_density»); o volume_marisa do Peso inicial
        // congelado; o volume_punção da CM corrente ou, na falta, o
        // congelado do Peso inicial.
        var cmTechnical = await _db.ToolTechnicalContexts.AsNoTracking()
            .Where(t => t.ToolId == cmToolId)
            .Select(t => new { t.Process, t.VolumePuncaoCm3 })
            .FirstOrDefaultAsync(cancellationToken);

        decimal glassDensity = inicial.GlassDensityGramsPerCm3;
        if (cmTechnical?.Process is { } process)
        {
            glassDensity = await _db.GlassDensities.AsNoTracking()
                .Where(g => g.Process == process)
                .Select(g => (decimal?)g.DensityGramsPerCm3)
                .FirstOrDefaultAsync(cancellationToken)
                ?? inicial.GlassDensityGramsPerCm3;
        }

        decimal? volumePuncao = cmTechnical?.VolumePuncaoCm3 ?? (decimal?)inicial.VolumePuncaoCm3;
        if (volumePuncao is not { } puncao)
        {
            return Recusada(PesoComparacaoResultado.VolumePuncaoIndisponivel, pedido.PesoInicialId);
        }

        var volumeMarisa = inicial.VolumeMarisaCm3;

        // A regra do proprietário (16/10/2026: «tentar sempre warning,
        // desde que não parta nada»): uma leitura com massa ≤ 0 ou com um
        // resultado não estritamente positivo (o baseline §8) já não anula
        // a comparação toda — essa linha fica DE FORA (nenhum valor dela é
        // exibido como facto nem gravado), as outras comparam-se e o aviso
        // diz qual ficou de fora.
        var leituras = new List<PesoComparacaoLeituraReadModel>(pedido.Leituras.Count);
        var ignoradas = new List<PesoComparacaoLeituraIgnorada>();
        var posicao = 1;
        var indice = 0;
        foreach (var leitura in pedido.Leituras)
        {
            indice++;
            if (leitura.WaterWeightGrams <= 0m)
            {
                ignoradas.Add(new PesoComparacaoLeituraIgnorada(
                    indice, leitura.CmSubject, leitura.WaterWeightGrams, PesoComparacaoMotivoIgnorada.MassaNaoPositiva));
                continue;
            }

            var capacity = PesoCalculation.CapacityCm3(leitura.WaterWeightGrams, resolvedWaterDensity);
            var glass = PesoCalculation.GlassWeightGrams(capacity, volumeMarisa, puncao, glassDensity);
            if (capacity <= 0m || glass <= 0m)
            {
                ignoradas.Add(new PesoComparacaoLeituraIgnorada(
                    indice, leitura.CmSubject, leitura.WaterWeightGrams, PesoComparacaoMotivoIgnorada.ResultadoNaoPositivo));
                continue;
            }

            leituras.Add(new PesoComparacaoLeituraReadModel(
                posicao++, leitura.CmSubject, leitura.WaterWeightGrams, capacity, glass));
        }

        if (leituras.Count == 0)
        {
            // Nenhuma leitura ficou utilizável: não há nada a comparar nem a
            // gravar. O motivo é o das leituras (todas com massa ≤ 0, ou pelo
            // menos uma com resultado não positivo) e a lista segue para o aviso.
            var motivo = ignoradas.All(i => i.Motivo == PesoComparacaoMotivoIgnorada.MassaNaoPositiva)
                ? PesoComparacaoResultado.MassaAguaNaoPositiva
                : PesoComparacaoResultado.ResultadoNaoPositivo;
            return Recusada(motivo, pedido.PesoInicialId, ignoradas);
        }

        var medias = new PesoAveragesReadModel(
            leituras.Average(l => l.WaterWeightGrams),
            leituras.Average(l => l.CapacityCm3),
            leituras.Average(l => l.GlassWeightGrams));

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(
            leituras.Select(l => new PesoComparacaoLado(l.RowPosition, l.CmSubject, l.GlassWeightGrams, l.CapacityCm3)).ToList(),
            inicial.Measurements.Select(m => new PesoComparacaoLado(m.RowPosition, m.CmSubject, m.GlassWeightGrams, m.CapacityCm3)).ToList());

        // O aviso do html (a referência ou as máquinas possíveis diferem das
        // do CM inicial — a comparação continua manualmente, sem bloqueio).
        // Sem links registados numa das tools não há informação de
        // compatibilidade: sem aviso (o padrão da casa — função antes de
        // proteção; completa a ficha depois).
        var ferramentaDiferente =
            !string.Equals(cmTool.Reference, inicial.CmToolReference, StringComparison.Ordinal);
        if (!ferramentaDiferente)
        {
            var linksSelecionada = await MaquinasDaToolAsync(cmToolId, cancellationToken);
            var linksInicial = await MaquinasDaToolAsync(inicial.CmToolId, cancellationToken);
            if (linksSelecionada.Count > 0 && linksInicial.Count > 0
                && !linksSelecionada.Overlaps(linksInicial))
            {
                ferramentaDiferente = true;
            }
        }

        return new PesoComparacaoLeiturasResultado(
            PesoComparacaoResultado.Sucesso,
            inicial.PesoId,
            cmToolId,
            cmTool.Reference,
            cmTool.Lot,
            inicial.CmToolReference,
            inicial.CmToolLot,
            inicial.Measurements.Count,
            inicial.Averages?.AverageGlassWeightGrams,
            temperatureWholeDegree,
            resolvedWaterDensity,
            glassDensity,
            volumeMarisa,
            volumePuncao.Value,
            ferramentaDiferente,
            leituras,
            medias,
            pares,
            ignoradas);
    }

    public async Task<PesoComparacaoGuardadosView?> CompararGuardadosAsync(
        int primeiroPesoId,
        int segundoPesoId,
        CancellationToken cancellationToken = default)
    {
        if (primeiroPesoId <= 0 || segundoPesoId <= 0)
        {
            return null;
        }

        // O MESMO Peso nos dois lados (beta.65, A18): já não é recusa — a
        // comparação faz-se e cada par mostra Δ = 0 (a página avisa).
        var primeiro = await _pesoQuery.FindPesoAsync(primeiroPesoId, cancellationToken);
        var segundo = primeiroPesoId == segundoPesoId
            ? primeiro
            : await _pesoQuery.FindPesoAsync(segundoPesoId, cancellationToken);
        if (primeiro is null || segundo is null)
        {
            return null;
        }

        // A compatibilidade (o html: a mesma referência de CM e máquinas
        // possíveis em comum). Sem links numa das tools não há informação de
        // compatibilidade: compatível (o padrão da casa — a ficha decide
        // depois; nunca um bloqueio).
        var compativel = string.Equals(primeiro.CmToolReference, segundo.CmToolReference, StringComparison.Ordinal);
        if (compativel)
        {
            var maquinasPrimeiro = await MaquinasDaToolAsync(primeiro.CmToolId, cancellationToken);
            var maquinasSegundo = await MaquinasDaToolAsync(segundo.CmToolId, cancellationToken);
            if (maquinasPrimeiro.Count > 0 && maquinasSegundo.Count > 0
                && !maquinasPrimeiro.Overlaps(maquinasSegundo))
            {
                compativel = false;
            }
        }

        // As leituras de cada lado pela ordem de registo: a tabela compara CM
        // com CM por ordem, sem pares (Diogo, 17/10/2026: «nao precisa de ser
        // par»).
        var linhasPrimeiro = primeiro.Measurements.OrderBy(m => m.RowPosition)
            .Select(m => new PesoComparacaoLado(m.RowPosition, m.CmSubject, m.GlassWeightGrams, m.CapacityCm3)).ToList();
        var linhasSegundo = segundo.Measurements.OrderBy(m => m.RowPosition)
            .Select(m => new PesoComparacaoLado(m.RowPosition, m.CmSubject, m.GlassWeightGrams, m.CapacityCm3)).ToList();

        return new PesoComparacaoGuardadosView(
            primeiro.PesoId,
            segundo.PesoId,
            await RotuloAsync(primeiro, cancellationToken),
            await RotuloAsync(segundo, cancellationToken),
            primeiro.Averages?.AverageGlassWeightGrams,
            segundo.Averages?.AverageGlassWeightGrams,
            primeiro.Measurements.Count,
            segundo.Measurements.Count,
            compativel,
            PesoComparacaoCalculation.CalcularParesPorCm(linhasPrimeiro, linhasSegundo),
            linhasPrimeiro,
            linhasSegundo,
            primeiro.Averages,
            segundo.Averages);
    }

    public async Task<PesoComparacaoReadModel?> ObterComparacaoGuardadaAsync(
        int comparacaoId,
        CancellationToken cancellationToken = default)
    {
        if (comparacaoId <= 0)
        {
            return null;
        }

        var registo = await _db.PesoComparacoes.AsNoTracking()
            .Where(c => c.Id == comparacaoId)
            .Select(c => new
            {
                c.Id,
                c.ControloId,
                Reference = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(x => x.Id == c.ControloId)
                        .Select(x => x.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.Reference)
                    .FirstOrDefault() ?? string.Empty,
                ProductionNumber = _db.JobOns
                    .Where(j => j.Id == _db.Controlos
                        .Where(x => x.Id == c.ControloId)
                        .Select(x => x.JobOnId)
                        .FirstOrDefault())
                    .Select(j => j.ProductionNumber)
                    .FirstOrDefault() ?? string.Empty,
                Machine = (
                    from x in _db.Controlos
                    join j in _db.JobOns on x.JobOnId equals j.Id
                    join m in _db.Machines on j.MachineId equals m.Id
                    where x.Id == c.ControloId
                    select m.Code)
                    .FirstOrDefault() ?? string.Empty,
                c.PesoInicialId,
                c.CmToolId,
                c.CmToolReferenceFrozen,
                c.CmToolLotFrozen,
                c.WaterTemperatureC,
                c.WaterDensityGramsPerCm3,
                c.GlassDensityGramsPerCm3,
                c.VolumeMarisaCm3,
                c.VolumePuncaoCm3,
                c.CreatedAtUtc,
                c.Note
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registo is null)
        {
            return null;
        }

        var leiturasRows = await _db.PesoComparacaoMeasurements.AsNoTracking()
            .Where(m => m.ComparacaoId == comparacaoId)
            .OrderBy(m => m.RowPosition)
            .Select(m => new { m.RowPosition, m.CmSubject, m.WaterWeightGrams })
            .ToListAsync(cancellationToken);

        var leituras = leiturasRows
            .Select(m =>
            {
                var capacity = PesoCalculation.CapacityCm3(m.WaterWeightGrams, registo.WaterDensityGramsPerCm3);
                var glass = PesoCalculation.GlassWeightGrams(
                    capacity, registo.VolumeMarisaCm3, registo.VolumePuncaoCm3, registo.GlassDensityGramsPerCm3);
                return new PesoComparacaoLeituraReadModel(
                    m.RowPosition, m.CmSubject, m.WaterWeightGrams, capacity, glass);
            })
            .ToList();

        PesoAveragesReadModel? medias = leituras.Count > 0
            ? new PesoAveragesReadModel(
                leituras.Average(l => l.WaterWeightGrams),
                leituras.Average(l => l.CapacityCm3),
                leituras.Average(l => l.GlassWeightGrams))
            : null;

        // Os pares contra o Peso inicial: as leituras do peso congelado
        // (factos históricos), calculadas pela fonte única.
        var inicial = await _pesoQuery.FindPesoAsync(registo.PesoInicialId, cancellationToken);
        var pares = PesoComparacaoCalculation.CalcularParesPorCm(
            leituras.Select(l => new PesoComparacaoLado(l.RowPosition, l.CmSubject, l.GlassWeightGrams, l.CapacityCm3)).ToList(),
            (inicial?.Measurements ?? new List<PesoMeasurementReadModel>())
                .Select(m => new PesoComparacaoLado(m.RowPosition, m.CmSubject, m.GlassWeightGrams, m.CapacityCm3)).ToList());

        return new PesoComparacaoReadModel(
            registo.Id,
            registo.ControloId,
            registo.Reference,
            registo.ProductionNumber,
            registo.Machine,
            registo.PesoInicialId,
            registo.CmToolReferenceFrozen,
            registo.CmToolLotFrozen,
            registo.WaterTemperatureC,
            registo.WaterDensityGramsPerCm3,
            registo.GlassDensityGramsPerCm3,
            registo.VolumeMarisaCm3,
            registo.VolumePuncaoCm3,
            leituras,
            medias,
            pares,
            registo.CreatedAtUtc,
            registo.Note);
    }

    private async Task<HashSet<int>> MaquinasDaToolAsync(string toolId, CancellationToken cancellationToken) =>
        (await _db.ToolMachineLinks.AsNoTracking()
            .Where(link => link.ToolId == toolId)
            .Select(link => link.MachineId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    /// <summary>O rótulo humano do peso (factos apenas — nunca um id): a produção quando existe, senão a CM.</summary>
    private async Task<string> RotuloAsync(PesoReadModel peso, CancellationToken cancellationToken)
    {
        if (peso.ControloId is { } controloId)
        {
            var factos = await _db.Controlos.AsNoTracking()
                .Where(c => c.Id == controloId)
                .Select(c => new
                {
                    Reference = _db.JobOns
                        .Where(j => j.Id == c.JobOnId)
                        .Select(j => j.Reference)
                        .FirstOrDefault() ?? string.Empty,
                    ProductionNumber = _db.JobOns
                        .Where(j => j.Id == c.JobOnId)
                        .Select(j => j.ProductionNumber)
                        .FirstOrDefault() ?? string.Empty,
                    Machine = (
                        from j in _db.JobOns
                        join m in _db.Machines on j.MachineId equals m.Id
                        where j.Id == c.JobOnId
                        select m.Code)
                        .FirstOrDefault() ?? string.Empty
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (factos is { } f)
            {
                return $"{f.Reference} · {f.ProductionNumber} · {f.Machine}".Trim(' ', '·');
            }
        }

        return $"CM {peso.CmToolReference ?? peso.CmToolId} · Lote {peso.CmToolLot}";
    }

    private static PesoComparacaoLeiturasResultado Recusada(
        PesoComparacaoResultado refusal,
        int pesoInicialId,
        IReadOnlyList<PesoComparacaoLeituraIgnorada>? ignoradas = null) =>
        new(refusal, pesoInicialId, null, null, null, null, null, 0, null, 0, null, null, null, null, false,
            Array.Empty<PesoComparacaoLeituraReadModel>(), null, null,
            ignoradas ?? Array.Empty<PesoComparacaoLeituraIgnorada>());
}

/// <summary>
/// Cria um registo de comparação de pesos (o registo do histórico tipo
/// «Comparação» do dmo-html): as novas leituras dos contra-moldes contra o
/// Peso inicial de uma produção, guardado sob o controlo_id da produção. A
/// validação e o cálculo vêm da FONTE ÚNICA (a
/// <see cref="IPesoComparisonQuery.CompararLeiturasAsync"/> — o mesmo caminho
/// da comparação ao vivo); o handler persiste o registo com os inputs
/// consumidos CONGELADOS (o padrão do Peso: o grau da temperatura, as
/// densidades, os volumes e os fatos humanos da ferramenta — o histórico
/// sobrevive à edição/eliminação da Tool). Todas as recusas são tipadas e
/// verificadas ANTES de qualquer escrita: uma recusa não deixa registos
/// parciais. O registo é append-only: nenhuma comparação é reescrita.
/// </summary>
public sealed class CreatePesoComparacaoHandler : ICreatePesoComparacaoHandler
{
    private readonly DmoDbContext _db;
    private readonly IPesoComparisonQuery _comparison;

    public CreatePesoComparacaoHandler(
        DmoDbContext db,
        IPesoComparisonQuery comparison)
    {
        _db = db;
        _comparison = comparison;
    }

    public async Task<CreatePesoComparacaoOutcome> HandleAsync(
        CreatePesoComparacao command,
        CancellationToken cancellationToken = default)
    {
        if (command.PesoInicialId <= 0)
        {
            return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.PesoInicialNaoSelecionado);
        }

        // O cálculo e as recusas pela fonte única (o mesmo caminho da
        // comparação ao vivo): nenhuma segunda implementação da matemática.
        var resultado = await _comparison.CompararLeiturasAsync(
            new PesoComparacaoLeiturasPedido(
                command.PesoInicialId,
                command.CmToolId,
                command.WaterTemperatureC,
                command.Leituras),
            cancellationToken);

        if (resultado.Refusal != PesoComparacaoResultado.Sucesso || resultado.CmToolId is null)
        {
            return new CreatePesoComparacaoOutcome(resultado.Refusal, Ignoradas: resultado.Ignoradas);
        }

        // O controlo_id da produção vem do PRÓPRIO Peso inicial (a comparação
        // é production-scoped: os iniciais da superfície são sempre Pesos de
        // produção). Sem controlo no peso inicial não há registo a criar.
        int controloId;
        if (await _db.Pesos.AsNoTracking()
                .Where(p => p.Id == command.PesoInicialId && p.ControloId != null)
                .Select(p => p.ControloId!.Value)
                .FirstOrDefaultAsync(cancellationToken) is { } resolvedControloId)
        {
            controloId = resolvedControloId;
        }
        else
        {
            return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.PesoInicialNaoEncontrado);
        }

        var registo = new PesoComparacao
        {
            ControloId = controloId,
            PesoInicialId = command.PesoInicialId,
            CmToolId = resultado.CmToolId,
            WaterTemperatureC = resultado.WaterTemperatureC,
            WaterDensityGramsPerCm3 = resultado.WaterDensityGramsPerCm3!.Value,
            GlassDensityGramsPerCm3 = resultado.GlassDensityGramsPerCm3!.Value,
            VolumeMarisaCm3 = resultado.VolumeMarisaCm3!.Value,
            VolumePuncaoCm3 = resultado.VolumePuncaoCm3!.Value,
            CmToolReferenceFrozen = resultado.CmToolReference,
            CmToolLotFrozen = resultado.CmToolLot,
            Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Measurements = resultado.Leituras
                .Select(l => new PesoComparacaoMeasurement
                {
                    RowPosition = l.RowPosition,
                    CmSubject = l.CmSubject,
                    WaterWeightGrams = l.WaterWeightGrams
                })
                .ToList()
        };

        _db.PesoComparacoes.Add(registo);
        await _db.SaveChangesAsync(cancellationToken);

        // O read model do registo guardado (a resposta do PRG renderiza o
        // resultado a partir do PERSISTIDO, nunca do rascunho).
        var readModel = await _comparison.ObterComparacaoGuardadaAsync(registo.Id, cancellationToken);
        // As leituras deixadas de fora não são gravadas; voltam para o aviso.
        return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.Sucesso, readModel, resultado.Ignoradas);
    }
}

/// <summary>
/// A procura do HISTÓRICO DO CONTROLO ao uso do dmo-html (a secção history):
/// os registos guardados dos TRÊS tipos (Peso, Pegamentos, Comparação) com
/// os filtros da oficina — a referência do conjunto (parcial conta), o nº de
/// produção (parcial conta), a ferramenta (a referência da CM congelada ou
/// corrente — parcial conta), o tipo e a data. Os Pesos e os Pegamentos
/// guardados não têm data persistida: a procura por data devolve as
/// comparações (as únicas com data registada — o aviso vive na
/// apresentação). A ordenação é do mais recente para o mais antigo: os
/// registos COM data por data, os sem data pela ordem de gravação (Id
/// descendente) — nenhuma data é inventada.
/// </summary>
public sealed class ControloHistorySearchQuery : IControloHistorySearchQuery
{
    private readonly DmoDbContext _db;

    public ControloHistorySearchQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ControloHistorySearchItem>> SearchAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        CancellationToken cancellationToken = default,
        bool semJobOn = false)
    {
        var (items, _) = await SearchCoreAsync(reference, productionNumber, tool, tipo, data, semJobOn, null, cancellationToken);
        return items;
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
        bool semJobOn = false)
    {
        // A PAGINAÇÃO NA QUERY sobre três tabelas: cada tipo devolve a sua
        // contagem e, no máximo, as linhas até ao fim da página pedida (já
        // na ordem final de cada tipo); a junção ordena e corta a página.
        // Lê a página pedida com a contagem; só volta a ler quando a página
        // pedida está fora do intervalo e é ajustada.
        var requested = Math.Clamp(pageNumber ?? 1, 1, int.MaxValue / pageSize);
        var (items, total) = await SearchCoreAsync(
            reference, productionNumber, tool, tipo, data, semJobOn, requested * pageSize, cancellationToken);
        var page = ListPaging.ClampPage(pageNumber, total, pageSize);
        if (page != requested)
        {
            (items, _) = await SearchCoreAsync(
                reference, productionNumber, tool, tipo, data, semJobOn, page * pageSize, cancellationToken);
        }
        return new PagedList<ControloHistorySearchItem>(
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList(), total, page, pageSize);
    }

    /// <param name="limit">null = sem limite; N = no máximo N linhas de cada tipo (0 = só a contagem).</param>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> SearchCoreAsync(
        string? reference,
        string? productionNumber,
        string? tool,
        ControloHistoryKind? tipo,
        DateOnly? data,
        bool semJobOn,
        int? limit,
        CancellationToken cancellationToken)
    {
        var refFilter = reference?.Trim();
        var prodFilter = productionNumber?.Trim();
        var toolFilter = tool?.Trim();

        var items = new List<ControloHistorySearchItem>();
        var total = 0;

        // «Sem Job On associado»: só o que ficou por associar (Pesos e
        // Pegamentos sem controlo_id), pela ferramenta; não têm referência,
        // produção nem data.
        if (semJobOn)
        {
            if (tipo is null or ControloHistoryKind.Peso)
            {
                var (pesos, count) = await PesosSemJobOnAsync(toolFilter, limit, cancellationToken);
                items.AddRange(pesos);
                total += count;
            }

            if (tipo is null or ControloHistoryKind.Pegamentos)
            {
                var (pegamentos, count) = await PegamentosSemJobOnAsync(toolFilter, limit, cancellationToken);
                items.AddRange(pegamentos);
                total += count;
            }

            return (items.OrderByDescending(i => i.Id).ToList(), total);
        }

        if (tipo is null or ControloHistoryKind.Peso)
        {
            // Os Pesos guardados não têm data persistida: com um filtro de
            // data nenhum pode corresponder (o aviso vive na apresentação).
            if (data is null)
            {
                var (pesos, count) = await PesosAsync(refFilter, prodFilter, toolFilter, limit, cancellationToken);
                items.AddRange(pesos);
                total += count;
            }
        }

        if (tipo is null or ControloHistoryKind.Pegamentos)
        {
            if (data is null)
            {
                var (pegamentos, count) = await PegamentosAsync(refFilter, prodFilter, toolFilter, limit, cancellationToken);
                items.AddRange(pegamentos);
                total += count;
            }
        }

        if (tipo is null or ControloHistoryKind.Comparacao)
        {
            var (comparacoes, count) = await ComparacoesAsync(refFilter, prodFilter, toolFilter, data, limit, cancellationToken);
            items.AddRange(comparacoes);
            total += count;
        }

        if (tipo is null or ControloHistoryKind.Resumo)
        {
            var (resumos, count) = await ResumosAsync(refFilter, prodFilter, toolFilter, data, limit, cancellationToken);
            items.AddRange(resumos);
            total += count;
        }

        // Do mais recente para o mais antigo: os registos COM data (as
        // comparações) por data; os sem data (pesos/pegamentos) pela ordem
        // de gravação — nenhuma data é inventada.
        return (items
            .OrderByDescending(i => i.DataUtc ?? DateTime.MinValue)
            .ThenByDescending(i => i.Id)
            .ToList(), total);
    }

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

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PesosAsync(
        string? refFilter,
        string? prodFilter,
        string? toolFilter,
        int? limit,
        CancellationToken cancellationToken)
    {
        var (rows, total) = await ReadAsync(
            from p in _db.Pesos.AsNoTracking()
            where p.ControloId != null
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            where string.IsNullOrEmpty(refFilter) || j.Reference.ToUpper().Contains(refFilter.ToUpper())
            where string.IsNullOrEmpty(prodFilter) || j.ProductionNumber.ToUpper().Contains(prodFilter.ToUpper())
            where string.IsNullOrEmpty(toolFilter)
                || (p.CmToolReferenceFrozen != null && p.CmToolReferenceFrozen!.ToUpper().Contains(toolFilter.ToUpper()))
                || t.Reference.ToUpper().Contains(toolFilter.ToUpper())
            orderby p.Id descending
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
                p.WaterTemperatureC,
                p.VolumeMarisaCm3,
                p.VolumePuncaoCm3,
                p.GlassDensityGramsPerCm3
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
                && WaterDensityTable.Resolve(row.WaterTemperatureC) is { } density)
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

            var toolLabel = $"{row.CmToolReferenceFrozen ?? row.ToolReference} · Lote {row.CmToolLotFrozen ?? row.ToolLot}";
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
                DataUtc: null,
                resumo);
        }).ToList(), total);
    }

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PegamentosAsync(
        string? refFilter,
        string? prodFilter,
        string? toolFilter,
        int? limit,
        CancellationToken cancellationToken)
    {
        var (rows, total) = await ReadAsync(
            from g in _db.Pegamentos.AsNoTracking()
            join c in _db.Controlos.AsNoTracking() on g.ControloId equals c.Id
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            where string.IsNullOrEmpty(refFilter) || j.Reference.ToUpper().Contains(refFilter.ToUpper())
            where string.IsNullOrEmpty(prodFilter) || j.ProductionNumber.ToUpper().Contains(prodFilter.ToUpper())
            where string.IsNullOrEmpty(toolFilter)
                || (g.CmToolReferenceFrozen != null && g.CmToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
                || (g.BqToolReferenceFrozen != null && g.BqToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
                || (g.MfToolReferenceFrozen != null && g.MfToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
            orderby g.Id descending
            select new
            {
                RegistroId = g.Id,
                ControloId = c.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                g.CmToolReferenceFrozen,
                g.CmToolLotFrozen,
                g.ToleranceMm
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
            $"CM {row.CmToolReferenceFrozen ?? "—"} · Lote {row.CmToolLotFrozen ?? "—"}",
            DataUtc: null,
            string.Create(DmoNumberFormat.Culture,
                $"{(contagem.TryGetValue(row.RegistroId, out var n) ? n : 0)} medições · corredor ±{row.ToleranceMm:0.00} mm"))).ToList(), total);
    }

    /// <summary>
    /// O Resumo de cada produção (um por controlo_id): a CM do Job On e o
    /// estado da aprovação. O Id da linha é o jobon_id (o Resumo abre por
    /// ele); o ControloId é o do controlo. A data é a da aprovação (sem aprovação, um
    /// filtro de data não o encontra).
    /// </summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> ResumosAsync(
        string? refFilter,
        string? prodFilter,
        string? toolFilter,
        DateOnly? data,
        int? limit,
        CancellationToken cancellationToken)
    {
        var fromUtc = data is { } day ? DmoBusinessTime.ToUtc(day.ToDateTime(TimeOnly.MinValue)) : DateTime.MinValue;
        var toUtc = data is { } nextDay ? DmoBusinessTime.ToUtc(nextDay.AddDays(1).ToDateTime(TimeOnly.MinValue)) : DateTime.MaxValue;
        var (rows, total) = await ReadAsync(
            from c in _db.Controlos.AsNoTracking()
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join cm in _db.CmContexts.AsNoTracking() on j.Id equals cm.JobOnId into cmGroup
            from cm in cmGroup.DefaultIfEmpty()
            join t in _db.Tools.AsNoTracking().Where(x => x.ToolId != ToolRegistryConstants.RemovedToolId) on cm.ToolId equals t.ToolId into toolGroup
            from t in toolGroup.DefaultIfEmpty()
            where string.IsNullOrEmpty(refFilter) || j.Reference.ToUpper().Contains(refFilter.ToUpper())
            where string.IsNullOrEmpty(prodFilter) || j.ProductionNumber.ToUpper().Contains(prodFilter.ToUpper())
            where string.IsNullOrEmpty(toolFilter) || (t != null && t.Reference.ToUpper().Contains(toolFilter.ToUpper()))
            where data == null || (c.ApprovedAtUtc != null && c.ApprovedAtUtc >= fromUtc && c.ApprovedAtUtc < toUtc)
            // A ordem final da lista: os aprovados (com data) primeiro, pela data; depois pelo Id da linha.
            orderby c.ApprovedAtUtc == null, c.ApprovedAtUtc descending, j.Id descending
            select new
            {
                c.Id,
                JobOnId = j.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                ToolReference = t != null ? t.Reference : null,
                ToolLot = t != null ? t.Lot : null,
                c.ApprovedAtUtc,
                c.ApprovedByOperatorId
            }, limit, cancellationToken);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Resumo,
            row.JobOnId,
            row.Id,
            row.Reference,
            row.ProductionNumber,
            row.Machine,
            row.ToolReference is null ? null : $"CM {row.ToolReference} · Lote {row.ToolLot ?? "—"}",
            row.ApprovedAtUtc,
            row.ApprovedAtUtc is null ? "Por aprovar" : $"Aprovado por {row.ApprovedByOperatorId ?? "—"}")).ToList(), total);
    }

    /// <summary>Os Pesos sem Job On (ControloId null), pela ferramenta (CM ou BQ, congelada ou corrente).</summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PesosSemJobOnAsync(
        string? toolFilter,
        int? limit,
        CancellationToken cancellationToken)
    {
        var (rows, total) = await ReadAsync(
            from p in _db.Pesos.AsNoTracking()
            where p.ControloId == null
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId into toolGroup
            from t in toolGroup.DefaultIfEmpty()
            where string.IsNullOrEmpty(toolFilter)
                || (p.CmToolReferenceFrozen != null && p.CmToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
                || (t != null && t.Reference.ToUpper().Contains(toolFilter.ToUpper()))
            orderby p.Id descending
            select new
            {
                p.Id,
                CmReference = p.CmToolReferenceFrozen ?? (t != null ? t.Reference : null),
                CmLot = p.CmToolLotFrozen ?? (t != null ? t.Lot : null),
                Leituras = _db.PesoMeasurements.Count(x => x.PesoId == p.Id)
            }, limit, cancellationToken);

        return (rows.Select(row => new ControloHistorySearchItem(
            ControloHistoryKind.Peso,
            row.Id,
            null,
            row.CmReference ?? "—",
            SemJobOnProductionNumber,
            "—",
            $"CM {row.CmReference ?? "—"} · Lote {row.CmLot ?? "—"}",
            DataUtc: null,
            row.Leituras == 0 ? "sem leituras" : $"{row.Leituras} leituras")).ToList(), total);
    }

    /// <summary>Os Pegamentos sem Job On (ControloId null), pela ferramenta (CM, BQ ou MF congeladas).</summary>
    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> PegamentosSemJobOnAsync(
        string? toolFilter,
        int? limit,
        CancellationToken cancellationToken)
    {
        var (rows, total) = await ReadAsync(
            from g in _db.Pegamentos.AsNoTracking()
            where g.ControloId == null
            where string.IsNullOrEmpty(toolFilter)
                || (g.CmToolReferenceFrozen != null && g.CmToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
                || (g.BqToolReferenceFrozen != null && g.BqToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
                || (g.MfToolReferenceFrozen != null && g.MfToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
            orderby g.Id descending
            select new
            {
                g.Id,
                g.CmToolReferenceFrozen,
                g.CmToolLotFrozen,
                g.BqToolReferenceFrozen,
                g.MfToolReferenceFrozen,
                g.ToleranceMm,
                Medicoes = _db.PegamentosMeasurements.Count(x => x.PegamentosId == g.Id)
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
            DataUtc: null,
            string.Create(DmoNumberFormat.Culture, $"{row.Medicoes} medições · corredor ±{row.ToleranceMm:0.00} mm"))).ToList(), total);
    }

    /// <summary>O texto do nº de produção dos registos sem Job On.</summary>
    private const string SemJobOnProductionNumber = PegamentosSheetReadModel.SemJobOnProductionNumber;

    private async Task<(IReadOnlyList<ControloHistorySearchItem> Items, int Total)> ComparacoesAsync(
        string? refFilter,
        string? prodFilter,
        string? toolFilter,
        DateOnly? data,
        int? limit,
        CancellationToken cancellationToken)
    {
        // O dia escolhido é o dia de Lisboa; as datas gravadas estão em UTC.
        var fromUtc = data is { } day ? DmoBusinessTime.ToUtc(day.ToDateTime(TimeOnly.MinValue)) : DateTime.MinValue;
        var toUtc = data is { } nextDay ? DmoBusinessTime.ToUtc(nextDay.AddDays(1).ToDateTime(TimeOnly.MinValue)) : DateTime.MaxValue;
        var (rows, total) = await ReadAsync(
            from x in _db.PesoComparacoes.AsNoTracking()
            join c in _db.Controlos.AsNoTracking() on x.ControloId equals c.Id
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            where string.IsNullOrEmpty(refFilter) || j.Reference.ToUpper().Contains(refFilter.ToUpper())
            where string.IsNullOrEmpty(prodFilter) || j.ProductionNumber.ToUpper().Contains(prodFilter.ToUpper())
            where string.IsNullOrEmpty(toolFilter)
                || (x.CmToolReferenceFrozen != null && x.CmToolReferenceFrozen.ToUpper().Contains(toolFilter.ToUpper()))
            where data == null || (x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
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
