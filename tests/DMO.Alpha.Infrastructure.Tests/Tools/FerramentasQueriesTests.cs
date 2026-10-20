using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Prova as queries canónicas do corte A (Ferramentas) no registo real
/// (InMemory, sem servidor): a FICHA por tool_id (ToolSheetQuery — factos +
/// máquinas ordenadas por código + contexto técnico quando existe; null
/// para desconhecida e para a DUMMY), o AVISO de eliminação
/// (ToolDeletionImpactQuery — produções cujos contextos referem o tool_id
/// + contagens dos registos de consumo), a compatibilidade de máquinas da
/// lista (máquinas por ferramenta, sem N+1), a descoberta estendida
/// (ToolCandidateQuery: filtro por máquina via ligações e por referência
/// MF/produção — worked example blueprint L70-93: a CM encontrada PELA
/// associação explícita quando a sua referência canónica difere) e o
/// lookup canónico (a DUMMY nunca resolve como Tool real). Todas são só
/// de leitura (AsNoTracking) e excluem a DUMMY por CONSTANTE.
/// </summary>
public sealed class FerramentasQueriesTests : IDisposable
{
    private readonly DmoDbContext _db;

    public FerramentasQueriesTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(Machine B1, Machine B3)> SeedMachinesAsync()
    {
        var b1 = new Machine { Code = "B1" };
        var b3 = new Machine { Code = "B3" };
        _db.Machines.AddRange(b1, b3);
        await _db.SaveChangesAsync();
        return (b1, b3);
    }

    // ------------------------------------------------------------------
    // ToolSheetQuery — a ficha purpose-specific.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ToolSheet_ComposesFactsMachinesAndTechnical_DummyNeverResolves()
    {
        await SeedDummyAsync();
        var (b1, b3) = await SeedMachinesAsync();

        var handler = new CreateToolHandler(_db, new ToolLotQuantitySettings(_db));
        var created = await handler.HandleAsync(new CreateTool("CM", "5447", "3", "5810", null, new[] { b1.Id, b3.Id }));
        var toolId = created.Tool!.ToolId;
        await new EditToolTechnicalValuesHandler(_db).HandleAsync(new EditToolTechnicalValues(
            toolId,
            ProcessToken: "NNPB",
            NominalWeightGrams: 520m,
            TpDiametroMm: 28.45m,
            TpVolumeMm3: 1438.3860504277206m));

        var sheet = await new ToolSheetQuery(_db).FindByToolIdAsync(toolId);

        Assert.NotNull(sheet);
        Assert.Equal(toolId, sheet!.ToolId);
        Assert.Equal(ToolType.Cm, sheet.Type);
        Assert.Equal("5447", sheet.Reference);
        Assert.Equal("3", sheet.Lot);
        Assert.Equal("5810", sheet.AssociatedMfReference);
        Assert.Null(sheet.BqAccountedQuantity);

        // Máquinas ordenadas por código (FATOS HUMANOS: «CM · 5447 ·
        // lote 3 · B1»).
        Assert.Equal(new[] { "B1", "B3" }, sheet.Machines.Select(m => m.Code).ToArray());

        // Contexto técnico projetado com a geometria TP completa.
        Assert.NotNull(sheet.Technical);
        Assert.Equal(CmProcess.Nnpb, sheet.Technical!.Process);
        Assert.Equal(520m, sheet.Technical.NominalWeightGrams);
        Assert.Equal(28.45m, sheet.Technical.TpDiametroMm);
        Assert.Equal(1438.3860504277206m, sheet.Technical.TpVolumeMm3);

        // A DUMMY nunca resolve ficha (excluída por constante) e a
        // desconhecida idem — nenhuma ficha é inventada.
        Assert.Null(await new ToolSheetQuery(_db).FindByToolIdAsync(ToolRegistryConstants.RemovedToolId));
        Assert.Null(await new ToolSheetQuery(_db).FindByToolIdAsync("Taaaaaaaaaa"));
        Assert.Null(await new ToolSheetQuery(_db).FindByToolIdAsync(" "));
    }

    [Fact]
    public async Task ToolSheet_TechnicalAbsenceIsValidState()
    {
        var created = await new CreateToolHandler(_db, new ToolLotQuantitySettings(_db)).HandleAsync(new CreateTool("BQ", "T194", "12", null, 144));
        var sheet = await new ToolSheetQuery(_db).FindByToolIdAsync(created.Tool!.ToolId);

        // Sem linha técnica: a ausência é estado válido (null), a ficha
        // mostra os factos na mesma.
        Assert.NotNull(sheet);
        Assert.Null(sheet!.Technical);
        Assert.Equal(144, sheet.BqAccountedQuantity);
        Assert.Empty(sheet.Machines);
    }

    // ------------------------------------------------------------------
    // ToolDeletionImpactQuery — o aviso antes da confirmação.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ToolDeletionImpact_ListsAffectedProductionsAndCounts()
    {
        await SeedDummyAsync();
        var (b1, _) = await SeedMachinesAsync();

        var toolId = "T0000000001";
        _db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        await _db.SaveChangesAsync();

        // Produção cujo contexto CM referencia a tool.
        var jobOn = new JobOn { Reference = "5810", ProductionNumber = "J10", Machine = b1 };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = toolId });
        await _db.SaveChangesAsync();

        // Registos de consumo que serão reatribuídos/limpos.
        _db.Pesos.Add(new Peso
        {
            CmToolId = toolId, BqToolId = toolId, Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 2.5m, VolumePuncaoCm3 = 1.4m,
            GlassDensityGramsPerCm3 = 2.5m
        });
        _db.Pegamentos.Add(new Pegamentos { ControloId = 1, CmToolId = toolId, ToleranceMm = 0.20m });
        _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = toolId, BqContextId = null });
        _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = toolId, BqContextId = null });
        await _db.SaveChangesAsync();

        var impact = await new ToolDeletionImpactQuery(_db).FindByToolIdAsync(toolId);

        Assert.NotNull(impact);
        var production = Assert.Single(impact!.Productions);
        Assert.Equal("J10", production.ProductionNumber);
        Assert.Equal("5810", production.Reference);
        Assert.Equal("B1", production.MachineCode);
        Assert.Equal(1, impact.PesoCount);
        Assert.Equal(1, impact.PegamentosCount);
        Assert.Equal(2, impact.UnresolvedTraceCount);

        // A existência de histórico NÃO remove a opção (blueprint L145):
        // o impacto é aviso, nunca recusa — a query devolve os factos e o
        // delete decide o humano. Desconhecida/DUMMY: null, nada inventado.
        Assert.Null(await new ToolDeletionImpactQuery(_db).FindByToolIdAsync("Taaaaaaaaaa"));
        Assert.Null(await new ToolDeletionImpactQuery(_db).FindByToolIdAsync(ToolRegistryConstants.RemovedToolId));
    }

    // ------------------------------------------------------------------
    // A lista do Ferramentas traz as máquinas de cada ferramenta.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ToolCandidatePage_ProjectsMachineCodesPerTool()
    {
        var (b1, b3) = await SeedMachinesAsync();

        var t1 = new Tool { ToolId = "T0000000001", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        var t2 = new Tool { ToolId = "T0000000002", Type = ToolType.Bq, Reference = "T194", Lot = "12" };
        _db.Tools.AddRange(t1, t2);
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = t1.ToolId, MachineId = b3.Id },
            new ToolMachineLink { ToolId = t1.ToolId, MachineId = b1.Id },
            new ToolMachineLink { ToolId = t2.ToolId, MachineId = b1.Id });
        await _db.SaveChangesAsync();

        var page = await new ToolCandidateQuery(_db).FindPageAsync(null, 1, 10);

        Assert.Equal(
            new[] { ("T0000000001", "B1 · B3"), ("T0000000002", "B1") },
            page.Items.Where(c => c.ToolId.StartsWith("T000")).Select(c => (c.ToolId, c.MachineCodes ?? "")).ToArray());
    }

    // ------------------------------------------------------------------
    // ToolCandidateQuery estendida — máquina e referência MF (worked
    // example blueprint L70-93).
    // ------------------------------------------------------------------

    [Fact]
    public async Task ToolCandidateQuery_MachineFilter_MatchesViaPersistedLinks()
    {
        var (b1, b3) = await SeedMachinesAsync();

        _db.Tools.AddRange(
            new Tool { ToolId = "T0000000001", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "T0000000002", Type = ToolType.Cm, Reference = "5448", Lot = "4" },
            new Tool { ToolId = "T0000000003", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.ToolMachineLinks.AddRange(
            new ToolMachineLink { ToolId = "T0000000001", MachineId = b1.Id },
            new ToolMachineLink { ToolId = "T0000000002", MachineId = b3.Id });
        await _db.SaveChangesAsync();

        var query = new ToolCandidateQuery(_db);

        // O filtro por máquina REDUZ candidatos (via ligações): apenas as
        // Tools ligadas a B1 — a BQ sem ligações sai.
        var onB1 = await query.FindCandidatesAsync(new ToolCandidateFilter(MachineId: b1.Id));
        Assert.Equal(new[] { "T0000000001" }, onB1.Select(c => c.ToolId).ToArray());

        // Máquina sem ligações: zero candidatos, sem inferir identidade.
        var none = await query.FindCandidatesAsync(new ToolCandidateFilter(MachineId: b3.Id));
        Assert.Equal(new[] { "T0000000002" }, none.Select(c => c.ToolId).ToArray());
    }

    [Fact]
    public async Task ToolCandidateQuery_MfReferenceMatchesCanonicalOrAssociated_WorkedExample()
    {
        // Worked example blueprint L70-93: a CM T2 tem referência canónica
        // 5809 e associação explícita de referência MF 5810; a CM T1 tem
        // referência canónica 5810. A procura por referência MF/produção
        // 5810 apresenta AMBAS — a seleção é humana explícita sobre o
        // tool_id e a identidade de T2 NUNCA muda.
        _db.Tools.AddRange(
            new Tool { ToolId = "T1", Type = ToolType.Cm, Reference = "5810", Lot = "L1" },
            new Tool
            {
                ToolId = "T2",
                Type = ToolType.Cm,
                Reference = "5809",
                Lot = "L2",
                AssociatedMfReference = "5810"
            },
            new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5809", Lot = "L3" });
        await _db.SaveChangesAsync();

        var query = new ToolCandidateQuery(_db);
        var candidates = await query.FindCandidatesAsync(new ToolCandidateFilter(MfReference: "5810"));

        // T1 (referência direta) E T2 (associação explícita); T3 não —
        // nenhuma correspondência difusa nem silenciosa.
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ToolId == "T1" && c.Reference == "5810");
        Assert.Contains(candidates, c => c.ToolId == "T2" && c.Reference == "5809");

        // A seleção de T2 deixa a identidade intacta (o lookup canónico
        // devolve a MESMA tool com a referência canónica 5809).
        var resolved = await new ToolLookupQuery(_db).FindByToolIdAsync("T2");
        Assert.NotNull(resolved);
        Assert.Equal("5809", resolved!.Reference);
        Assert.Equal("5810", (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "T2")).AssociatedMfReference);
    }

    // Teste do agente de 19/10/2026 (T21ce22c): no Job On, com a CM 1075C142
    // escolhida, «Associar MF» filtra pela referência toda e tem de continuar
    // a mostrar o MF antigo registado só com o modelo (1075), como a lista
    // pela referência da produção; um MF de outro modelo não.
    [Fact]
    public async Task ToolCandidateQuery_FullMfReference_AlsoFindsTheToolRegisteredWithTheModelOnly()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "MF-OLD", Type = ToolType.Mf, Reference = "1075", Lot = "L1" },
            new Tool { ToolId = "MF-NEW", Type = ToolType.Mf, Reference = "1075C142", Lot = "L2" },
            new Tool { ToolId = "MF-OTHER", Type = ToolType.Mf, Reference = "1076", Lot = "L3" },
            new Tool { ToolId = "BQ-MODEL", Type = ToolType.Bq, Reference = "1075", Lot = "L4" });
        await _db.SaveChangesAsync();
        var query = new ToolCandidateQuery(_db);

        var byFull = await query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Mf, MfReference: "1075C142"));
        Assert.Equal(new[] { "MF-NEW", "MF-OLD" }, byFull.Select(c => c.ToolId).Order().ToArray());

        var byModel = await query.FindCandidatesAsync(new ToolCandidateFilter(MfReference: "1075"));
        Assert.Equal(new[] { "BQ-MODEL", "MF-OLD" }, byModel.Select(c => c.ToolId).Order().ToArray());
        var bqByFull = await query.FindCandidatesAsync(new ToolCandidateFilter(Type: ToolType.Bq, MfReference: "1075C142"));
        Assert.Empty(bqByFull);
    }

    // ------------------------------------------------------------------
    // ToolLookupQuery — a DUMMY nunca resolve como Tool real.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ToolLookupQuery_DummyRemovedNeverResolves()
    {
        await SeedDummyAsync();
        _db.Tools.Add(new Tool { ToolId = "T0000000001", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        await _db.SaveChangesAsync();

        var query = new ToolLookupQuery(_db);

        var real = await query.FindByToolIdAsync("T0000000001");
        Assert.NotNull(real);
        Assert.Equal("T194", real!.Reference);

        // A tombstone é excluída por constante: nunca é utilizável como
        // Tool real em nenhum fluxo.
        Assert.Null(await query.FindByToolIdAsync(ToolRegistryConstants.RemovedToolId));
    }

    private async Task SeedDummyAsync()
    {
        _db.Tools.Add(new Tool
        {
            ToolId = ToolRegistryConstants.RemovedToolId,
            Type = ToolType.Cm,
            Reference = "REMOVED",
            Lot = "REMOVED"
        });
        await _db.SaveChangesAsync();
    }
}
