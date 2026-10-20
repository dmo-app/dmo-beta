using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a procura UNIFICADA do histórico do Controlo (a secção history do
/// dmo-html): os tipos (Peso, Pegamentos, Comparação e Resumo) com os filtros
/// da oficina — a referência do conjunto (pelo início dela ou da parte BQ),
/// o nº de produção (igual, com a referência da produção escolhida), a
/// ferramenta (a referência da CM congelada ou corrente), o tipo e a data
/// (o dia de Lisboa, pela data gravada de cada registo: o carimbo dos Pesos e
/// dos Pegamentos). Do mais recente para o mais antigo, os sem data no fim.
/// O InMemory não corre o RegistoCarimboInterceptor: os carimbos ficam
/// escritos nos dados.
/// </summary>
public sealed class ControloHistorySearchQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ControloHistorySearchQuery _query;

    public ControloHistorySearchQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new ControloHistorySearchQuery(_db);
    }

    /// <summary>Todas as linhas da procura numa só página (a app pagina-as).</summary>
    private async Task<IReadOnlyList<ControloHistorySearchItem>> SearchAllAsync(
        string? reference, string? productionNumber, string? tool, ControloHistoryKind? tipo, DateOnly? data, bool semJobOn = false,
        string? productionReference = null) =>
        (await _query.SearchPageAsync(reference, productionNumber, tool, tipo, data, 1, 10_000, semJobOn: semJobOn,
            productionReference: productionReference)).Items;

    /// <summary>O carimbo do Peso e dos Pegamentos do <see cref="SeedAsync"/> (17/10/2026, 11:00 e 12:00 em Lisboa).</summary>
    private static readonly DateTime PesoRegistadoEm = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime PegamentosRegistadoEm = new(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc);

    /// <summary>Um Peso da CM 5447 com a BQ T194, com ou sem carimbo do registo.</summary>
    private static Peso NovoPeso(int? controloId, DateTime? registadoEm) => new()
    {
        CmToolId = "T3",
        BqToolId = "BQ-TOOL-9",
        Process = "NNPB",
        WaterTemperatureC = 20,
        VolumeMarisaCm3 = 10m,
        VolumePuncaoCm3 = 30m,
        GlassDensityGramsPerCm3 = 2.50m,
        ControloId = controloId,
        RegistadoEmUtc = registadoEm
    };

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(int ControloId, int PesoId, int PegamentosId, int ComparacaoId)> SeedAsync()
    {
        var machine = new Machine { Code = "B1" };
        var cmTool = new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        var bqTool = new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" };
        _db.Machines.Add(machine);
        _db.Tools.Add(cmTool);
        _db.Tools.Add(bqTool);
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T3", Process = CmProcess.Nnpb });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "BQ-TOOL-9", VolumeMarisaCm3 = 10m });

        var jobOn = new JobOn { Reference = "1075C142", ProductionNumber = "202601", Machine = machine };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-TOOL-9" });
        await _db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();

        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id,
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            RegistadoEmUtc = PesoRegistadoEm
        };
        _db.Pesos.Add(peso);

        var pegamentos = new Pegamentos
        {
            ControloId = controlo.Id,
            CmToolId = "T3",
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            ToleranceMm = 0.20m,
            RegistadoEmUtc = PegamentosRegistadoEm
        };
        _db.Pegamentos.Add(pegamentos);

        var comparacao = new PesoComparacao
        {
            ControloId = controlo.Id,
            PesoInicialId = peso.Id,
            CmToolId = "T3",
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            WaterTemperatureC = 20,
            WaterDensityGramsPerCm3 = 0.99717m,
            GlassDensityGramsPerCm3 = 2.50m,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            CreatedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc)
        };
        _db.PesoComparacoes.Add(comparacao);
        await _db.SaveChangesAsync();
        _db.PesoComparacaoMeasurements.Add(new PesoComparacaoMeasurement { ComparacaoId = comparacao.Id, RowPosition = 1, CmSubject = "12", WaterWeightGrams = 199.64m });
        await _db.SaveChangesAsync();

        return (controlo.Id, peso.Id, pegamentos.Id, comparacao.Id);
    }

    [Fact]
    public async Task SearchAsync_SemFiltros_DevolveOsQuatroTipos()
    {
        var (controloId, pesoId, pegamentosId, comparacaoId) = await SeedAsync();

        var items = await SearchAllAsync(null, null, null, null, null);

        Assert.Equal(4, items.Count);
        var resumo = Assert.Single(items, i => i.Kind == ControloHistoryKind.Resumo);
        Assert.Equal(controloId, resumo.ControloId);
        Assert.Equal("1075C142", resumo.Reference);
        Assert.Contains("5447", resumo.ToolLabel);
        Assert.Equal("Por aprovar", resumo.Resumo);
        Assert.Contains(items, i => i.Kind == ControloHistoryKind.Peso && i.Id == pesoId);
        Assert.Contains(items, i => i.Kind == ControloHistoryKind.Pegamentos && i.Id == pegamentosId);
        Assert.Contains(items, i => i.Kind == ControloHistoryKind.Comparacao && i.Id == comparacaoId);

        // Os factos humanos: a referência/produção do controlo e a CM congelada.
        var pesoItem = items.Single(i => i.Kind == ControloHistoryKind.Peso);
        Assert.Equal("1075C142", pesoItem.Reference);
        Assert.Equal("202601", pesoItem.ProductionNumber);
        Assert.Equal("B1", pesoItem.Machine);
        Assert.Contains("5447", pesoItem.ToolLabel);
        Assert.Equal(PesoRegistadoEm, pesoItem.DataUtc); // a data do registo (o carimbo)
        Assert.Equal(PegamentosRegistadoEm, items.Single(i => i.Kind == ControloHistoryKind.Pegamentos).DataUtc);

        var comparacaoItem = items.Single(i => i.Kind == ControloHistoryKind.Comparacao);
        Assert.NotNull(comparacaoItem.DataUtc); // as comparações têm a data do registo
        Assert.Contains("1 contra-moldes", comparacaoItem.Resumo);
    }

    [Fact]
    public async Task SearchAsync_FiltroTipo_SoOPedido()
    {
        await SeedAsync();

        var pesos = await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, null);
        Assert.Single(pesos);
        Assert.Equal(ControloHistoryKind.Peso, pesos[0].Kind);

        var pegamentos = await SearchAllAsync(null, null, null, ControloHistoryKind.Pegamentos, null);
        Assert.Single(pegamentos);
        Assert.Equal(ControloHistoryKind.Pegamentos, pegamentos[0].Kind);

        var comparacoes = await SearchAllAsync(null, null, null, ControloHistoryKind.Comparacao, null);
        Assert.Single(comparacoes);
        Assert.Equal(ControloHistoryKind.Comparacao, comparacoes[0].Kind);
    }

    [Fact]
    public async Task Pegamentos_DuasFolhasNaMesmaProducao_CadaLinhaDizAsFerramentasDaSua()
    {
        // A BQ mudou a meio (18/10/2026): duas folhas, cada uma com
        // as suas ferramentas; com uma só, a linha fica como sempre («CM ·
        // Lote»).
        var (controloId, _, pegamentosId, _) = await SeedAsync();
        Assert.Equal("CM 5447 · Lote 3",
            Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Pegamentos, null)).ToolLabel);
        var first = await _db.Pegamentos.SingleAsync(p => p.Id == pegamentosId);
        first.BqToolId = "BQ-TOOL-9";
        first.BqToolReferenceFrozen = "T194";
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-10", Type = ToolType.Bq, Reference = "T195", Lot = "1" });
        var second = new Pegamentos
        {
            ControloId = controloId,
            CmToolId = "T3",
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            BqToolId = "BQ-TOOL-10",
            BqToolReferenceFrozen = "T195",
            ToleranceMm = 0.20m,
            // A folha nova regista-se depois da primeira (a lista ordena pela data).
            RegistadoEmUtc = PegamentosRegistadoEm.AddHours(1)
        };
        _db.Pegamentos.Add(second);
        await _db.SaveChangesAsync();

        var items = await SearchAllAsync(null, null, null, ControloHistoryKind.Pegamentos, null);

        Assert.Equal(new[] { second.Id, pegamentosId }, items.Select(i => i.Id));
        Assert.Equal("CM 5447 · Lote 3 · BQ T195", items[0].ToolLabel);
        Assert.Equal("CM 5447 · Lote 3 · BQ T194", items[1].ToolLabel);
        Assert.All(items, i => Assert.Equal(controloId, i.ControloId));
    }

    [Fact]
    public async Task Pegamentos_DuasFolhasQueSoDiferemNoLoteDaBq_CadaLinhaDizOLoteDaSua()
    {
        // Bug (deep sweep 2, 18/10/2026): trocar a BQ por outro lote da
        // mesma BQ dava duas linhas com o mesmo texto («· BQ T194»).
        var (controloId, _, pegamentosId, _) = await SeedAsync();
        var first = await _db.Pegamentos.SingleAsync(p => p.Id == pegamentosId);
        first.BqToolId = "BQ-TOOL-7";
        first.BqToolReferenceFrozen = "T194";
        first.BqToolLotFrozen = "7";
        var second = new Pegamentos
        {
            ControloId = controloId,
            CmToolId = "T3",
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            BqToolId = "BQ-TOOL-8",
            BqToolReferenceFrozen = "T194",
            BqToolLotFrozen = "8",
            ToleranceMm = 0.20m,
            RegistadoEmUtc = PegamentosRegistadoEm.AddHours(1)
        };
        _db.Pegamentos.Add(second);
        await _db.SaveChangesAsync();

        var items = await SearchAllAsync(null, null, null, ControloHistoryKind.Pegamentos, null);

        Assert.Equal(new[] { second.Id, pegamentosId }, items.Select(i => i.Id));
        Assert.Equal("CM 5447 · Lote 3 · BQ T194 · Lote 8", items[0].ToolLabel);
        Assert.Equal("CM 5447 · Lote 3 · BQ T194 · Lote 7", items[1].ToolLabel);
    }

    [Fact]
    public async Task Peso_DoisPesosNaMesmaProducao_CadaLinhaDizABqDoSeu()
    {
        // A BQ mudou a meio (18/10/2026; resposta R16-5 do especialista): um
        // Peso por CM+BQ, cada linha com a BQ do seu; com um só, a linha
        // fica como sempre («CM · Lote»).
        var (controloId, pesoId, _, _) = await SeedAsync();
        Assert.Equal("5447 · Lote 3",
            Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, null)).ToolLabel);
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-10", Type = ToolType.Bq, Reference = "T195", Lot = "1" });
        var second = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-10",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 11m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controloId,
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3",
            BqToolReferenceFrozen = "T195",
            BqToolLotFrozen = "1"
        };
        _db.Pesos.Add(second);
        await _db.SaveChangesAsync();

        var items = await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, null);

        Assert.Equal(2, items.Count);
        Assert.Equal("5447 · Lote 3 · BQ T195 · Lote 1", items.Single(i => i.Id == second.Id).ToolLabel);
        Assert.Equal("5447 · Lote 3 · BQ T194 · Lote 12", items.Single(i => i.Id == pesoId).ToolLabel);
        Assert.All(items, i => Assert.Equal(controloId, i.ControloId));
    }

    [Fact]
    public async Task SearchAsync_FiltroFerramenta_PelaReferenciaDaCM()
    {
        await SeedAsync();

        var porFerramenta = await SearchAllAsync(null, null, "5447", null, null);
        Assert.Equal(4, porFerramenta.Count); // Peso, Pegamentos, Comparação e o Resumo (a CM do Job On)

        var semCorrespondencia = await SearchAllAsync(null, null, "INEXISTENTE", null, null);
        Assert.Empty(semCorrespondencia);
    }

    [Fact]
    public async Task SearchAsync_FiltroReferenciaEProducao()
    {
        await SeedAsync();

        // A referência pelo início dela ou da parte BQ, sem distinguir
        // maiúsculas; nunca um pedaço a meio (deep sweep 2, F-2 e F-6).
        Assert.Equal(4, (await SearchAllAsync("1075", null, null, null, null)).Count);
        Assert.Equal(4, (await SearchAllAsync("1075c142", null, null, null, null)).Count);
        Assert.Equal(4, (await SearchAllAsync("c14", null, null, null, null)).Count);
        Assert.Empty(await SearchAllAsync("075", null, null, null, null));
        Assert.Empty(await SearchAllAsync("OUTRA", null, null, null, null));
        // O nº de produção vem do dropdown: igual, não um pedaço.
        Assert.Equal(4, (await SearchAllAsync("1075", "202601", null, null, null)).Count);
        Assert.Empty(await SearchAllAsync("1075", "2026", null, null, null));
    }

    [Fact]
    public async Task SearchAsync_FiltroFerramenta_PeloInicio()
    {
        await SeedAsync();

        Assert.Equal(4, (await SearchAllAsync(null, null, "54", null, null)).Count);
        Assert.Empty(await SearchAllAsync(null, null, "44", null, null));
    }

    [Fact]
    public async Task SearchAsync_ProducaoComAReferenciaDela_NaoJuntaOMesmoNumeroDeOutraReferencia()
    {
        // O nº só é único dentro da referência: «1075» é o início da 1075C142
        // e da 1075T999, e as duas têm a 202601. Com a referência da
        // produção escolhida, só essa.
        var (controloId, _, _, _) = await SeedAsync();
        var machine = await _db.Machines.SingleAsync();
        var outra = new JobOn { Reference = "1075T999", ProductionNumber = "202601", Machine = machine };
        _db.JobOns.Add(outra);
        await _db.SaveChangesAsync();
        var controloOutra = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = outra.Id };
        _db.Controlos.Add(controloOutra);
        await _db.SaveChangesAsync();

        Assert.Equal(2, (await SearchAllAsync("1075", "202601", null, ControloHistoryKind.Resumo, null)).Count);

        var soUma = Assert.Single(await SearchAllAsync(
            "1075", "202601", null, ControloHistoryKind.Resumo, null, productionReference: "1075t999"));
        Assert.Equal(controloOutra.Id, soUma.ControloId);
        var daPrimeira = await SearchAllAsync("1075", "202601", null, null, null, productionReference: "1075C142");
        Assert.Equal(4, daPrimeira.Count);
        Assert.All(daPrimeira, i => Assert.Equal(controloId, i.ControloId));
    }

    [Fact]
    public async Task ProducaoDaReferenciaAsync_AMaisRecenteComONumero_PelaRegraDaTabela()
    {
        // Deep sweep 2 (F-18): a produção pedida por link que já não está nas
        // últimas do dropdown. O nº sozinho é o da mais recente com esse nº;
        // com a referência, só essa; uma referência que não corresponde ao
        // texto não serve.
        await SeedAsync();
        var machine = await _db.Machines.SingleAsync();
        _db.JobOns.Add(new JobOn { Reference = "1075T999", ProductionNumber = "202601", Machine = machine });
        _db.JobOns.Add(new JobOn { Reference = "2075C142", ProductionNumber = "202699", Machine = machine });
        await _db.SaveChangesAsync();

        Assert.Equal(new JobOnProductionOption("202601", "1075T999"), await _query.ProducaoDaReferenciaAsync("1075", "202601", null));
        Assert.Equal(new JobOnProductionOption("202601", "1075C142"), await _query.ProducaoDaReferenciaAsync("1075", " 202601 ", "1075c142"));
        Assert.Equal(new JobOnProductionOption("202601", "1075C142"), await _query.ProducaoDaReferenciaAsync("c142", "202601", null));
        Assert.Null(await _query.ProducaoDaReferenciaAsync("1075", "2026", null));
        Assert.Null(await _query.ProducaoDaReferenciaAsync("1075", "202699", null));
        Assert.Null(await _query.ProducaoDaReferenciaAsync("1075", "202601", "2075C142"));
        Assert.Null(await _query.ProducaoDaReferenciaAsync(" ", "202601", null));
    }

    [Fact]
    public async Task SearchAsync_FiltroData_PesosEPegamentosPelaDataDoRegisto()
    {
        // Deep sweep 2 (F-4): os Pesos e os Pegamentos têm a data do registo
        // (o carimbo). A data escolhida encontra-os nesse dia, como as
        // comparações e os Resumos aprovados.
        var (_, pesoId, pegamentosId, comparacaoId) = await SeedAsync();

        var dia17 = await SearchAllAsync(null, null, null, null, new DateOnly(2026, 10, 17));
        Assert.Equal(
            new[] { (ControloHistoryKind.Pegamentos, pegamentosId), (ControloHistoryKind.Peso, pesoId) },
            dia17.Select(i => (i.Kind, i.Id)));
        Assert.Equal(pesoId, Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, new DateOnly(2026, 10, 17))).Id);

        var dia13 = Assert.Single(await SearchAllAsync(null, null, null, null, new DateOnly(2026, 10, 13)));
        Assert.Equal(ControloHistoryKind.Comparacao, dia13.Kind);
        Assert.Equal(comparacaoId, dia13.Id);

        Assert.Empty(await SearchAllAsync(null, null, null, null, new DateOnly(2026, 10, 14)));
    }

    [Fact]
    public async Task SearchAsync_FiltroData_PesoPeloDiaDeLisboa_EOsSemCarimboNaoEntram()
    {
        var (controloId, pesoId, _, _) = await SeedAsync();
        var peso = await _db.Pesos.SingleAsync(p => p.Id == pesoId);
        peso.RegistadoEmUtc = new DateTime(2026, 10, 16, 23, 30, 0, DateTimeKind.Utc); // 17/10 00:30 em Lisboa
        var antigo = NovoPeso(controloId, registadoEm: null);
        _db.Pesos.Add(antigo);
        await _db.SaveChangesAsync();

        Assert.Empty(await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, new DateOnly(2026, 10, 16)));
        Assert.Equal(pesoId, Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, new DateOnly(2026, 10, 17))).Id);

        // O Peso anterior ao carimbo aparece sem data e no fim, sem data inventada.
        var pesos = await SearchAllAsync("1075", null, null, ControloHistoryKind.Peso, null);
        Assert.Equal(new[] { pesoId, antigo.Id }, pesos.Select(i => i.Id));
        Assert.Null(pesos[1].DataUtc);
    }

    [Fact]
    public async Task SearchAsync_FiltroData_UsaODiaDeLisboa()
    {
        var (_, _, _, comparacaoId) = await SeedAsync();
        var comparacao = await _db.PesoComparacoes.SingleAsync(x => x.Id == comparacaoId);
        comparacao.CreatedAtUtc = new DateTime(2026, 10, 13, 23, 30, 0, DateTimeKind.Utc); // 14/10 00:30 em Lisboa
        await _db.SaveChangesAsync();

        Assert.Empty(await SearchAllAsync(null, null, null, null, new DateOnly(2026, 10, 13)));
        Assert.Equal(comparacaoId, Assert.Single(await SearchAllAsync(null, null, null, null, new DateOnly(2026, 10, 14))).Id);
    }

    [Fact]
    public async Task SearchAsync_Ordenacao_MaisRecentePrimeiro()
    {
        var (controloId, pesoId, pegamentosId, comparacaoId) = await SeedAsync();
        // Um Resumo aprovado em 2020 e um Peso sem carimbo (anterior a 17/10/2026).
        var controlo = await _db.Controlos.SingleAsync(c => c.Id == controloId);
        controlo.ApprovedAtUtc = new DateTime(2020, 3, 16, 9, 0, 0, DateTimeKind.Utc);
        controlo.ApprovedByOperatorId = "4221";
        var antigo = NovoPeso(controloId, registadoEm: null);
        _db.Pesos.Add(antigo);
        await _db.SaveChangesAsync();

        var items = await SearchAllAsync(null, null, null, null, null);

        // Deep sweep 2 (F-4): todos pela data do registo, o mais recente
        // primeiro; o Peso de 2026 antes do Resumo de 2020; o sem data no fim.
        Assert.Equal(
            new[]
            {
                (ControloHistoryKind.Pegamentos, pegamentosId),
                (ControloHistoryKind.Peso, pesoId),
                (ControloHistoryKind.Comparacao, comparacaoId),
                (ControloHistoryKind.Resumo, controlo.JobOnId),
                (ControloHistoryKind.Peso, antigo.Id)
            },
            items.Select(i => (i.Kind, i.Id)));
    }

    [Fact]
    public async Task SearchPageAsync_ComDatas_AsPaginasSaoAListaCompletaPelaOrdemDasDatas()
    {
        // A página junta as primeiras linhas de cada tipo: cada tipo tem de as
        // ler pela ordem final (a data, os sem data no fim), não pelo Id, senão
        // a página 2 perde ou repete linhas. Aqui o Id mais alto é o mais antigo.
        var (controloId, _, _, _) = await SeedAsync();
        var inicio = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 30; i++)
        {
            _db.Pesos.Add(NovoPeso(controloId, registadoEm: i % 5 == 4 ? null : inicio.AddHours(-i)));
            _db.Pegamentos.Add(new Pegamentos
            {
                ControloId = controloId,
                CmToolId = "T3",
                CmToolReferenceFrozen = "5447",
                ToleranceMm = 0.20m,
                RegistadoEmUtc = i % 7 == 6 ? null : inicio.AddHours(-i).AddMinutes(30)
            });
            await _db.SaveChangesAsync();
        }

        var all = await SearchAllAsync("1075", null, null, null, null);
        var pages = new List<ControloHistorySearchItem>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await _query.SearchPageAsync("1075", null, null, null, null, page);
            Assert.Equal(all.Count, result.TotalCount);
            pages.AddRange(result.Items);
        }

        Assert.Equal(64, all.Count);
        Assert.Equal(all.Select(i => (i.Kind, i.Id)), pages.Select(i => (i.Kind, i.Id)));
        var datas = all.TakeWhile(i => i.DataUtc is not null).Select(i => i.DataUtc!.Value).ToList();
        Assert.Equal(datas.OrderByDescending(d => d), datas);
        Assert.All(all.Skip(datas.Count), i => Assert.Null(i.DataUtc));
        Assert.Equal(64 - 6 - 4 - 1, datas.Count); // 6 Pesos e 4 Pegamentos sem carimbo; o Resumo por aprovar
    }

    [Fact]
    public async Task SearchPageAsync_AsPaginasSaoAListaCompletaCortadaEmBlocosDe25()
    {
        var (controloId, _, _, _) = await SeedAsync();
        for (var i = 0; i < 30; i++)
        {
            _db.Pesos.Add(new Peso
            {
                CmToolId = "T3",
                BqToolId = "BQ-TOOL-9",
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m,
                ControloId = controloId
            });
        }
        await _db.SaveChangesAsync();

        var all = await SearchAllAsync(null, null, null, null, null);
        var page1 = await _query.SearchPageAsync(null, null, null, null, null, 1);
        var page2 = await _query.SearchPageAsync(null, null, null, null, null, 2);
        var beyond = await _query.SearchPageAsync(null, null, null, null, null, 99);

        Assert.Equal(34, all.Count);
        Assert.Equal(34, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(25, page1.Items.Count);
        Assert.Equal(9, page2.Items.Count);
        Assert.Equal(
            all.Select(i => (i.Kind, i.Id)),
            page1.Items.Concat(page2.Items).Select(i => (i.Kind, i.Id)));
        Assert.Equal(2, beyond.PageNumber);
    }

    [Fact]
    public async Task SearchAsync_FiltroData_EncontraOResumoAprovadoNesseDia()
    {
        var (controloId, _, _, _) = await SeedAsync();
        var controlo = await _db.Controlos.SingleAsync(c => c.Id == controloId);
        controlo.ApprovedAtUtc = new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
        controlo.ApprovedByOperatorId = "4221";
        await _db.SaveChangesAsync();

        var resumo = Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Resumo, new DateOnly(2026, 10, 14)));
        Assert.Equal("Aprovado por 4221", resumo.Resumo);
        Assert.Empty(await SearchAllAsync(null, null, null, ControloHistoryKind.Resumo, new DateOnly(2026, 10, 13)));
    }

    [Fact]
    public async Task SearchAsync_SemJobOn_SoOQueFicouPorAssociar()
    {
        await SeedAsync();
        var pesoSolto = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            CmToolReferenceFrozen = "5447",
            CmToolLotFrozen = "3"
        };
        var pegamentosSoltos = new Pegamentos
        {
            BqToolId = "BQ-TOOL-9",
            BqToolReferenceFrozen = "T194",
            BqToolLotFrozen = "12",
            ToleranceMm = 0.20m
        };
        _db.Pesos.Add(pesoSolto);
        _db.Pegamentos.Add(pegamentosSoltos);
        await _db.SaveChangesAsync();

        // Sem o filtro, o que está por associar não aparece na procura da produção.
        Assert.DoesNotContain(await SearchAllAsync("1075", null, null, null, null), i => i.ControloId is null);

        var porAssociar = await SearchAllAsync(null, null, null, null, null, semJobOn: true);
        Assert.Equal(2, porAssociar.Count);
        Assert.All(porAssociar, i => Assert.Null(i.ControloId));
        Assert.All(porAssociar, i => Assert.Equal("Sem Job On", i.ProductionNumber));
        Assert.Contains(porAssociar, i => i.Kind == ControloHistoryKind.Peso && i.Id == pesoSolto.Id && i.Reference == "5447");
        Assert.Contains(porAssociar, i => i.Kind == ControloHistoryKind.Pegamentos && i.Id == pegamentosSoltos.Id && i.Reference == "T194");

        // Pela ferramenta e pelo tipo.
        Assert.Equal(pegamentosSoltos.Id, Assert.Single(await SearchAllAsync(null, null, "T194", null, null, semJobOn: true)).Id);
        Assert.Equal(pesoSolto.Id, Assert.Single(await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, null, semJobOn: true)).Id);
        Assert.Empty(await SearchAllAsync(null, null, null, ControloHistoryKind.Resumo, null, semJobOn: true));

        var pagina = await _query.SearchPageAsync(null, null, null, null, null, 1, semJobOn: true);
        Assert.Equal(2, pagina.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_SemJobOn_AReferenciaEADataFiltram()
    {
        // Deep sweep 2 (F-5): com «Sem Job On associado», a referência e a
        // data continuavam escritas e não filtravam nada. Sem produção, a
        // referência procura-se nas ferramentas do registo (a CM e a BQ do
        // Peso; a CM, a BQ e o MF dos Pegamentos), pelo início ou pela parte
        // BQ; a data é a do registo, no dia de Lisboa.
        await SeedAsync();
        var pesoSolto = NovoPeso(null, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        pesoSolto.CmToolReferenceFrozen = "5447";
        pesoSolto.BqToolReferenceFrozen = "T194";
        var pesoAntigo = NovoPeso(null, registadoEm: null); // anterior ao carimbo, sem as referências congeladas
        var pegamentosSoltos = new Pegamentos
        {
            BqToolId = "BQ-TOOL-9",
            BqToolReferenceFrozen = "T194",
            MfToolReferenceFrozen = "1199",
            ToleranceMm = 0.20m,
            RegistadoEmUtc = new DateTime(2026, 10, 16, 23, 30, 0, DateTimeKind.Utc) // 17/10 00:30 em Lisboa
        };
        _db.Pesos.AddRange(pesoSolto, pesoAntigo);
        _db.Pegamentos.Add(pegamentosSoltos);
        await _db.SaveChangesAsync();

        async Task<(ControloHistoryKind, int)[]> SemJobOnAsync(string? referencia, DateOnly? data = null) =>
            (await SearchAllAsync(referencia, null, null, null, data, semJobOn: true)).Select(i => (i.Kind, i.Id)).ToArray();

        // Do mais recente para o mais antigo; o sem carimbo no fim.
        Assert.Equal(
            new[] { (ControloHistoryKind.Peso, pesoSolto.Id), (ControloHistoryKind.Pegamentos, pegamentosSoltos.Id), (ControloHistoryKind.Peso, pesoAntigo.Id) },
            await SemJobOnAsync(null));
        // A CM (congelada ou a corrente do Peso antigo), a parte BQ e o MF.
        Assert.Equal(new[] { (ControloHistoryKind.Peso, pesoSolto.Id), (ControloHistoryKind.Peso, pesoAntigo.Id) }, await SemJobOnAsync("5447"));
        Assert.Equal(3, (await SemJobOnAsync("t19")).Length);
        Assert.Equal(new[] { (ControloHistoryKind.Pegamentos, pegamentosSoltos.Id) }, await SemJobOnAsync("1199"));
        Assert.Empty(await SemJobOnAsync("447"));
        Assert.Empty(await SemJobOnAsync("1075C142"));
        // A data do registo; o Peso sem carimbo não entra.
        Assert.Equal(
            new[] { (ControloHistoryKind.Peso, pesoSolto.Id), (ControloHistoryKind.Pegamentos, pegamentosSoltos.Id) },
            await SemJobOnAsync(null, new DateOnly(2026, 10, 17)));
        Assert.Empty(await SemJobOnAsync(null, new DateOnly(2026, 10, 16)));
        Assert.Equal(new[] { (ControloHistoryKind.Pegamentos, pegamentosSoltos.Id) }, await SemJobOnAsync("1199", new DateOnly(2026, 10, 17)));
        Assert.Equal(new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc),
            (await SearchAllAsync(null, null, null, ControloHistoryKind.Peso, null, semJobOn: true))[0].DataUtc);
    }
}
