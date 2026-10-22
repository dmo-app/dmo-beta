using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// A Comparação_B (corte C, spec PESO.md): os Pesos baseline das produções
/// da MESMA referência (a mesma base do conjunto) anteriores à atual e o
/// diferencial pela POSIÇÃO física das leituras. O ranking dos candidatos é
/// AJUDA (desde 17/10/2026 só a recência; deep sweep 2, F-12: só as
/// anteriores, as últimas 5); a seleção é sempre humana e nunca substituída.
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

    public Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        int controloId,
        int? compararPesoId = null,
        CancellationToken cancellationToken = default) =>
        FindCandidatesCoreAsync(controloId, cmToolIdDoPeso: null, compararPesoId, cancellationToken);

    public async Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        PesoReadModel peso,
        int? compararPesoId = null,
        CancellationToken cancellationToken = default)
    {
        // A comparação usa a CM do Peso aberto (18/10/2026): depois de uma
        // troca, o Peso anterior compara-se com as máquinas da CM dele.
        if (peso.ControloId is not int controloId)
        {
            return Array.Empty<PesoComparisonCandidate>();
        }

        // Os Pesos criados só para comparar com este (o Diogo, 22/10/2026:
        // sem Pesos anteriores, cria-se um) vêm primeiro: foram feitos para ele.
        var paraComparacao = (await _pesoQuery.ListParaComparacaoAsync(peso.PesoId, cancellationToken))
            .Select(p => new PesoComparisonCandidate(
                p.PesoId,
                RotuloParaComparacao,
                string.Empty,
                string.Empty,
                SameMachine: false,
                ReadingCount: p.Measurements.Count,
                p.Averages?.AverageWaterWeightGrams is { } agua ? Math.Round(agua, 2, MidpointRounding.AwayFromZero) : null,
                p.Averages?.AverageCapacityCm3,
                p.Averages?.AverageGlassWeightGrams,
                p.CmToolLot));
        var daReferencia = await FindCandidatesCoreAsync(controloId, peso.CmToolId, compararPesoId, cancellationToken);
        return [.. paraComparacao, .. daReferencia];
    }

    /// <summary>O nome de um Peso só para comparação, no dropdown e na tabela (não tem produção).</summary>
    public const string RotuloParaComparacao = "Peso para comparação";

    private async Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesCoreAsync(
        int controloId,
        string? cmToolIdDoPeso,
        int? compararPesoId,
        CancellationToken cancellationToken)
    {
        // A produção atual (o controlo → o Job On: a referência, a máquina
        // e a CM do contexto — a ferramenta cuja FICHA decide o que pode
        // ser comparado; a do Peso aberto quando é dada).
        var current = await _db.Controlos
            .Where(c => c.Id == controloId)
            .Select(c => new
            {
                c.JobOnId,
                JobOnReference = _db.JobOns
                    .Where(j => j.Id == c.JobOnId)
                    .Select(j => j.Reference)
                    .FirstOrDefault(),
                // O início da produção atual decide quais são as anteriores
                // (deep sweep 2, F-12).
                JobOnInicioEm = _db.JobOns
                    .Where(j => j.Id == c.JobOnId)
                    .Select(j => j.InicioEm)
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
        var linkedMachineIds = cmToolIdDoPeso is null || cmToolIdDoPeso == current.CmToolId
            ? current.LinkedMachineIds
            : (await MaquinasDaToolAsync(cmToolIdDoPeso, cancellationToken)).ToList();

        // Os Pesos baseline das OUTRAS produções com a mesma referência:
        // juntos ao controlo, ao Job On e à máquina numa só consulta, com a
        // referência e as máquinas permitidas filtradas no SQL.
        var reference = current.JobOnReference;
        var query =
            from p in _db.Pesos
            where p.ControloId != null && p.ControloId != controloId
            join c in _db.Controlos on p.ControloId equals c.Id
            join j in _db.JobOns on c.JobOnId equals j.Id
            where j.Reference == reference
            join m in _db.Machines on j.MachineId equals m.Id
            select new { p, j, MachineCode = m.Code };

        // A FILTRAGEM PELO LINK DA FERRAMENTA (a folha decide o que pode
        // ser comparado): só as produções da referência cuja MÁQUINA
        // está no ToolMachineLink da CM corrente — o dropdown contém o
        // que a ferramenta aceita comparar (a tool [B1, C3] mostra B1 e
        // C3; a tool [B1] não mostra B3 — a opção simplesmente não
        // existe, nunca um bloqueio em runtime). Sem CM, ou com a ficha sem
        // máquinas, fica toda a referência.
        query = query.SoMaquinasDaCm(x => x.j, linkedMachineIds);

        // SÓ AS ANTERIORES À ATUAL (deep sweep 2, 18/10/2026, F-12, a
        // resposta R18 do especialista): esta é a Comparação_B, que o PDF do
        // Peso chama «Comparação com o controlo anterior» e a Revisão
        // «Controlo anterior»; uma produção posterior saía com o nome
        // «anterior», o que é falso. O critério é o da «Produção anterior» e
        // do «Controlo anterior» da Revisão, num só sítio (ProducaoAnterior):
        // as que começaram antes da atual (no mesmo dia, as registadas antes);
        // sem início na atual, as registadas antes dela, com as sem início no
        // fim.
        var anteriores = query.SoAnteriores(x => x.j, current.JobOnId, current.JobOnInicioEm);

        // AS ÚLTIMAS 5 (deep sweep 2, F-12: o dropdown tinha todas as
        // produções da referência, e lia as leituras de todas): a mais
        // recente primeiro, cortadas na base depois das máquinas permitidas
        // (ficam 5 que se podem comparar) e antes de ler as leituras. O Peso
        // pedido (o «comparar» de um link, do «Guardar» ou do «Supomos») fica
        // mesmo fora delas, mais antigo ou posterior, se for da mesma
        // referência e de uma máquina permitida: uma comparação já guardada
        // continua a aparecer, e a tabela não fica com o select em «Procurar
        // produção…». O corte é de 5 Pesos: uma produção com a CM ou a BQ
        // trocada (beta.243) conta um por combinação, o mais recente primeiro.
        var ultimas = anteriores
            .MaisRecentePrimeiro(x => x.j)
            .ThenByDescending(x => x.p.Id)
            .Select(x => x.p.Id)
            .Take(IProductionPickerQuery.MaxResults);
        query = compararPesoId is int pedido
            ? query.Where(x => ultimas.Contains(x.p.Id) || x.p.Id == pedido)
            : query.Where(x => ultimas.Contains(x.p.Id));

        var rows = await query
            .Select(x => new
            {
                PesoId = x.p.Id,
                ControloId = x.p.ControloId!.Value,
                x.p.CmToolId,
                x.j.ProductionNumber,
                JobOnId = x.j.Id,
                x.j.InicioEm,
                // Fora das 5 só pode estar o pedido, que vai para o fim.
                NasUltimas = ultimas.Contains(x.p.Id),
                JobOnMachineId = (int?)x.j.MachineId,
                JobOnReference = x.j.Reference,
                x.MachineCode,
                x.p.WaterTemperatureC,
                x.p.WaterDensityGramsPerCm3,
                x.p.VolumeMarisaCm3,
                x.p.VolumePuncaoCm3,
                x.p.GlassDensityGramsPerCm3,
                Readings = x.p.Measurements
                    .Select(m => new { m.RowPosition, m.WaterWeightGrams })
                    .OrderBy(m => m.RowPosition)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

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

        // Só a recência, a mais recente das anteriores primeiro (o Owner
        // 2026-10-17: «na mesma máquina não está bem, pq o mesmo lote pode
        // funcionar em máquinas diferentes usando a mesma ferramenta, basta
        // apenas produção»), pela mesma ordem do corte; o pedido fora das 5
        // fica no fim, como a guardada na «Produção anterior» (beta.239). As
        // máquinas permitidas continuam a vir da ficha da CM.
        return rows
            .OrderBy(x => !x.NasUltimas)
            .ThenBy(x => x.InicioEm is null)
            .ThenByDescending(x => x.InicioEm)
            .ThenByDescending(x => x.JobOnId)
            .ThenByDescending(x => x.PesoId)
            .Select(x =>
            {
                var machineCode = x.JobOnMachineId is not null ? x.MachineCode ?? string.Empty : string.Empty;
                // A densidade da água resolve pela tabela canónica para o grau
                // congelado (invariante validada na criação — o padrão da
                // PesoQuery).
                var density = WaterDensityTable.ForPeso(x.WaterDensityGramsPerCm3, x.WaterTemperatureC)
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
        var historicalFacts = await (
            from p in _db.Pesos
            where p.Id == historicalPesoId && p.ControloId != null
            join c in _db.Controlos on p.ControloId equals c.Id
            join j in _db.JobOns on c.JobOnId equals j.Id
            join m in _db.Machines on j.MachineId equals m.Id
            select new
            {
                j.ProductionNumber,
                MachineCode = m.Code,
                j.InicioEm,
                j.FimEm
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
            historicalFacts?.ProductionNumber
                ?? (historical.ComparacaoDoPesoId is not null ? RotuloParaComparacao : string.Empty),
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
        // Depois de uma troca da CM ou da BQ a produção tem vários Pesos
        // (18/10/2026): cada opção diz se é o Peso atual e, com mais do
        // que um, a BQ do Peso, para se distinguirem.
        var rows = await (
            from p in _db.Pesos.AsNoTracking()
            join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id
            join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id
            join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id
            join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
            join bq in _db.Tools.AsNoTracking() on p.BqToolId equals bq.ToolId
            where j.Id == jobOnId
            orderby p.Id ascending
            select new
            {
                PesoId = p.Id,
                ControloId = c.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = m.Code,
                ToolId = t.ToolId,
                ToolReference = t.Reference,
                ToolLot = t.Lot,
                BqReference = p.BqToolReferenceFrozen ?? bq.Reference,
                BqLot = p.BqToolLotFrozen ?? bq.Lot
            })
            .ToListAsync(cancellationToken);

        var opcoes = await ComOpcoesAsync(
            rows.Select(r => ((int, string?, string?, string?, string?, string?, string?))
                (r.PesoId, r.Reference, r.ProductionNumber, r.Machine, r.ToolId, r.ToolReference, r.ToolLot)),
            cancellationToken);
        if (rows.Count < 2)
        {
            return opcoes;
        }

        var atual = await PesoCombinations.CurrentPesoIdAsync(_db, rows[0].ControloId, cancellationToken);
        var porId = rows.ToDictionary(r => r.PesoId);
        return opcoes
            .Select(o => o with
            {
                IsCurrent = o.PesoId == atual,
                BqLabel = porId.TryGetValue(o.PesoId, out var r) ? $"BQ {r.BqReference ?? "—"} · Lote {r.BqLot ?? "—"}" : null
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosPorReferenciaAsync(
        string? referencia,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceMatch.Normalize(referencia) is not { } termo)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        // O filtro e o corte na base, antes de calcular os pesos médios. A
        // referência da produção (ou, sem produção, a da CM) pelo INÍCIO dela
        // ou da parte BQ, nunca a meio (ReferenceMatch, Diogo 18/10/2026):
        // «075» já não traz a 1075C142.
        var rows = await LinhasDosPesos()
            .Where(ReferenceMatch.Predicate<LinhaPeso>(r => r.ControloId != null && r.Machine != null ? r.Reference : null, termo)
                .Or(ReferenceMatch.Predicate<LinhaPeso>(r => r.ControloId == null ? r.ToolReference : null, termo)))
            .OrderByDescending(r => r.PesoId)
            .Take(100)
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(rows.Select(r => r.Tuplo), cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> OpcoesDosPesosAsync(
        IReadOnlyCollection<int> pesoIds,
        CancellationToken cancellationToken = default)
    {
        if (pesoIds.Count == 0)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        var rows = await LinhasDosPesos()
            .Where(r => pesoIds.Contains(r.PesoId))
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(rows.Select(r => r.Tuplo), cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ProcurarPesosAsync(
        string? termo,
        string? referencia,
        int limite,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceMatch.Normalize(termo) is not { } texto)
        {
            return Array.Empty<PesoComparacaoPesoOpcao>();
        }

        var rows = LinhasDosPesos();
        if (ReferenceMatch.Normalize(referencia) is { } refFiltro)
        {
            // Só os Pesos da referência indicada: a da produção ou, sem
            // produção, a da CM, pelo início (ReferenceMatch).
            rows = rows.Where(ReferenceMatch.Predicate<LinhaPeso>(r => r.Reference, refFiltro)
                .Or(ReferenceMatch.Predicate<LinhaPeso>(r => r.ControloId == null ? r.ToolReference : null, refFiltro)));
        }

        // As referências (da produção e da CM) pelo início delas ou da parte
        // BQ, nunca a meio (ReferenceMatch, Diogo 18/10/2026); o nº, a
        // máquina (da produção ou da CM) e o lote continuam por parte do texto.
        var found = await rows
            .Where(ReferenceMatch.Predicate<LinhaPeso>(r => r.Reference, texto)
                .Or(ReferenceMatch.Predicate<LinhaPeso>(r => r.ToolReference, texto))
                .Or(r => (r.ProductionNumber != null && r.ProductionNumber.ToUpper().Contains(texto))
                    || (r.Machine != null && r.Machine.ToUpper().Contains(texto))
                    || r.ToolLot.ToUpper().Contains(texto)
                    || _db.ToolMachineLinks.Any(link => link.ToolId == r.ToolId
                        && link.Machine.Code.ToUpper().Contains(texto))))
            .OrderByDescending(r => r.PesoId)
            .Take(limite)
            .ToListAsync(cancellationToken);

        return await ComMaquinasDaCmAsync(await ComOpcoesAsync(found.Select(r => r.Tuplo), cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Cada Peso com a CM e, quando tem, a produção (controlo → Job On →
    /// máquina), numa linha com nome: as condições do <see cref="ReferenceMatch"/>
    /// precisam de um tipo, e o EF traduz um Where sobre ela (um tipo com
    /// propriedades, não um record posicional).
    /// </summary>
    private IQueryable<LinhaPeso> LinhasDosPesos() =>
        from p in _db.Pesos.AsNoTracking()
        join t in _db.Tools.AsNoTracking() on p.CmToolId equals t.ToolId
        join c in _db.Controlos.AsNoTracking() on p.ControloId equals c.Id into controlos
        from c in controlos.DefaultIfEmpty()
        join j in _db.JobOns.AsNoTracking() on c.JobOnId equals j.Id into jobOns
        from j in jobOns.DefaultIfEmpty()
        join m in _db.Machines.AsNoTracking() on j.MachineId equals m.Id into machines
        from m in machines.DefaultIfEmpty()
        select new LinhaPeso
        {
            PesoId = p.Id,
            ControloId = p.ControloId,
            Reference = j != null ? j.Reference : null,
            ProductionNumber = j != null ? j.ProductionNumber : null,
            Machine = m != null ? m.Code : null,
            ToolId = t.ToolId,
            ToolReference = t.Reference,
            ToolLot = t.Lot
        };

    /// <summary>Um Peso com a CM e a produção, para as procuras do «Comparar Pesos guardados».</summary>
    private sealed class LinhaPeso
    {
        public int PesoId { get; init; }
        public int? ControloId { get; init; }
        public string? Reference { get; init; }
        public string? ProductionNumber { get; init; }
        public string? Machine { get; init; }
        public string ToolId { get; init; } = null!;
        public string ToolReference { get; init; } = null!;
        public string ToolLot { get; init; } = null!;

        public (int, string?, string?, string?, string?, string?, string?) Tuplo =>
            (PesoId, Reference, ProductionNumber, Machine, ToolId, ToolReference, ToolLot);
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
                p.WaterDensityGramsPerCm3,
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
                && WaterDensityTable.ForPeso(peso.WaterDensityGramsPerCm3, peso.WaterTemperatureC) is { } density)
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
            ignoradas)
        {
            // D12: a punção ou a densidade mudou desde o Peso inicial →
            // «não comparável», com aviso e sem recusa.
            Comparabilidade = new PesoComparabilidade(
                inicial.VolumePuncaoCm3, puncao, inicial.GlassDensityGramsPerCm3, glassDensity)
        };
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
            registo.Note)
        {
            Comparabilidade = inicial is null
                ? null
                : new PesoComparabilidade(
                    inicial.VolumePuncaoCm3, registo.VolumePuncaoCm3,
                    inicial.GlassDensityGramsPerCm3, registo.GlassDensityGramsPerCm3)
        };
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
