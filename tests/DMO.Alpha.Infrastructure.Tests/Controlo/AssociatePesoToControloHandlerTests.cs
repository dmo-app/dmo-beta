using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a associação posterior do Peso pré-produção (spec PESO.md L62-101;
/// o Owner 2026-10-16: «quando existir um jobon com CM_ID que usa o tool_id
/// perguntar se quero associar»): só a CM do Job On é candidata; associar
/// mantém o MESMO peso_id; uma produção por Peso e, na produção, um Peso por
/// CM+BQ. Depois de uma troca, a CM e a BQ do Peso podem ser as atuais ou as
/// que a produção teve antes (resposta R16-6 do especialista, 18/10/2026).
/// </summary>
public sealed class AssociatePesoToControloHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly AssociatePesoToControloHandler _handler;
    private readonly PesoAssociationQuery _query;

    public AssociatePesoToControloHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new AssociatePesoToControloHandler(_db);
        _query = new PesoAssociationQuery(_db);

        _db.Tools.AddRange(
            new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "6018", Lot = "66" },
            new Tool { ToolId = "cm-b", Type = ToolType.Cm, Reference = "6018", Lot = "83" },
            new Tool { ToolId = "bq-a", Type = ToolType.Bq, Reference = "Y232", Lot = "165" },
            new Tool { ToolId = "bq-b", Type = ToolType.Bq, Reference = "Y232", Lot = "171" },
            new Tool { ToolId = "bq-c", Type = ToolType.Bq, Reference = "Y300", Lot = "12" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private async Task<(int JobOnId, int ControloId)> SeedProductionAsync(
        string productionNumber, string? cmToolId, bool withControlo = true, string? bqToolId = "bq-a")
    {
        var jobOn = new JobOn
        {
            Reference = "6018Y232",
            ProductionNumber = productionNumber,
            Machine = new Machine { Code = "C2" }
        };
        _db.JobOns.Add(jobOn);
        if (cmToolId is not null)
        {
            _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = cmToolId });
        }

        if (bqToolId is not null)
        {
            _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = bqToolId });
        }

        DMO.Alpha.Core.Controlo.Controlo? controlo = null;
        if (withControlo)
        {
            controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn };
            _db.Controlos.Add(controlo);
        }

        await _db.SaveChangesAsync();
        return (jobOn.Id, controlo?.Id ?? 0);
    }

    /// <summary>A troca da CM a meio da produção: o contexto anterior fica ligado por ReplacedJobOnId.</summary>
    private async Task SwapCmAsync(int jobOnId, string newCmToolId)
    {
        var current = await _db.CmContexts.SingleAsync(x => x.JobOnId == jobOnId);
        current.JobOnId = null;
        current.ReplacedJobOnId = jobOnId;
        current.ReplacedAtUtc = DateTime.UtcNow;
        _db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = newCmToolId });
        await _db.SaveChangesAsync();
    }

    private async Task<int> SeedPesoAsync(string cmToolId, int? controloId = null, string bqToolId = "bq-a")
    {
        var peso = new Peso
        {
            CmToolId = cmToolId,
            BqToolId = bqToolId,
            Process = "PS",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 12.3m,
            VolumePuncaoCm3 = 70m,
            GlassDensityGramsPerCm3 = 2.4231m,
            CmToolReferenceFrozen = "6018",
            CmToolLotFrozen = cmToolId == "cm-a" ? "66" : "83",
            ControloId = controloId
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    [Fact]
    public async Task Associate_SameCmTool_KeepsSamePesoAndLinksProduction()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.Success, outcome.Result);
        var stored = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(pesoId, stored.Id);
        Assert.Equal(controloId, stored.ControloId);
        Assert.Equal("66", stored.CmToolLotFrozen);
    }

    [Fact]
    public async Task Associate_Twice_IsIdempotent()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a");

        await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));
        var second = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.Success, second.Result);
    }

    [Fact]
    public async Task Associate_OtherLotOfSameReference_IsRefusedAsCmMismatch()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-b");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.CmMismatch, outcome.Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync()).ControloId);
    }

    [Fact]
    public async Task Associate_PesoOfAnotherProduction_IsRefused()
    {
        var (_, first) = await SeedProductionAsync("202503", "cm-a");
        var (_, second) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a", first);

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(second, pesoId));

        Assert.Equal(AssociatePesoToControloResult.PesoAlreadyAssociated, outcome.Result);
        Assert.Equal(first, (await _db.Pesos.AsNoTracking().SingleAsync()).ControloId);
    }

    [Fact]
    public async Task Associate_ProductionThatAlreadyHasPeso_IsRefused()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        await SeedPesoAsync("cm-a", controloId);
        var loose = await SeedPesoAsync("cm-a");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, loose));

        Assert.Equal(AssociatePesoToControloResult.ControloAlreadyHasPeso, outcome.Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == loose)).ControloId);
    }

    [Fact]
    public async Task Associate_UnknownIdsAndMissingCm_AreTypedRefusals()
    {
        var (_, withoutCm) = await SeedProductionAsync("202616", null);
        var pesoId = await SeedPesoAsync("cm-a");

        Assert.Equal(AssociatePesoToControloResult.ControloNotFound,
            (await _handler.HandleAsync(new AssociatePesoToControlo(999, pesoId))).Result);
        Assert.Equal(AssociatePesoToControloResult.PesoNotFound,
            (await _handler.HandleAsync(new AssociatePesoToControlo(withoutCm, 999))).Result);
        Assert.Equal(AssociatePesoToControloResult.ContextMissingCm,
            (await _handler.HandleAsync(new AssociatePesoToControlo(withoutCm, pesoId))).Result);
    }

    [Fact]
    public async Task Candidates_ForControlo_AreOnlyUnassociatedPesosOfTheJobOnCm()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        var (_, otherControlo) = await SeedProductionAsync("202503", "cm-a");
        var loose = await SeedPesoAsync("cm-a");
        await SeedPesoAsync("cm-b");
        await SeedPesoAsync("cm-a", otherControlo);

        var candidates = await _query.FindCandidatesForControloAsync(controloId);

        var candidate = Assert.Single(candidates);
        Assert.Equal(loose, candidate.PesoId);
        Assert.Equal("6018", candidate.CmReference);
        Assert.Equal("66", candidate.CmLot);
        Assert.Equal("Y232", candidate.BqReference);

        // Depois de associado, a produção já tem Peso: sem candidatos.
        await _handler.HandleAsync(new AssociatePesoToControlo(controloId, loose));
        Assert.Empty(await _query.FindCandidatesForControloAsync(controloId));
    }

    [Fact]
    public async Task Productions_ForPeso_AreJobOnsWithThatCmAndWithoutPeso()
    {
        var (open, _) = await SeedProductionAsync("202616", "cm-a");
        var (preCut, _) = await SeedProductionAsync("202617", "cm-a", withControlo: false);
        var (_, taken) = await SeedProductionAsync("202503", "cm-a");
        await SeedProductionAsync("202618", "cm-b");
        await SeedPesoAsync("cm-a", taken);
        var loose = await SeedPesoAsync("cm-a");

        var productions = await _query.FindProductionsForPesoAsync(loose);

        Assert.Equal(new[] { preCut, open }, productions.Select(p => p.JobOnId).ToArray());
        Assert.Equal("C2", productions[0].Machine);

        // Um Peso já associado não pergunta nada.
        var associated = await _db.Pesos.AsNoTracking().Where(p => p.ControloId == taken).Select(p => p.Id).SingleAsync();
        Assert.Empty(await _query.FindProductionsForPesoAsync(associated));
    }

    [Fact]
    public async Task Associate_AfterCmSwap_PesoOfTheEarlierCm_IsAcceptedOncePerCombination()
    {
        var (jobOnId, controloId) = await SeedProductionAsync("202616", "cm-a");
        await SwapCmAsync(jobOnId, "cm-b");
        var atual = await SeedPesoAsync("cm-b", controloId);
        var anterior = await SeedPesoAsync("cm-a");
        var repetido = await SeedPesoAsync("cm-a");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, anterior));

        Assert.Equal(AssociatePesoToControloResult.Success, outcome.Result);
        Assert.Equal(controloId, (await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == anterior)).ControloId);
        Assert.Equal(controloId, (await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == atual)).ControloId);

        // A combinação (cm-a, bq-a) já tem Peso nesta produção.
        Assert.Equal(AssociatePesoToControloResult.ControloAlreadyHasPeso,
            (await _handler.HandleAsync(new AssociatePesoToControlo(controloId, repetido))).Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync(p => p.Id == repetido)).ControloId);
    }

    [Fact]
    public async Task Associate_PesoOfAnotherBq_IsRefusedAsBqMismatch()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a", bqToolId: "bq-c");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.BqMismatch, outcome.Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync()).ControloId);
    }

    [Fact]
    public async Task Associate_ProductionWithoutBq_IsRefusedAsContextMissingBq()
    {
        var (_, controloId) = await SeedProductionAsync("202616", "cm-a", bqToolId: null);
        var pesoId = await SeedPesoAsync("cm-a");

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.ContextMissingBq, outcome.Result);
        Assert.Null((await _db.Pesos.AsNoTracking().SingleAsync()).ControloId);
    }

    [Fact]
    public async Task Candidates_AfterSwapWithoutCurrentPeso_AreLoosePesosOfTheToolsTheProductionHadOrHas()
    {
        var (jobOnId, controloId) = await SeedProductionAsync("202616", "cm-a");
        await SeedPesoAsync("cm-a", controloId);
        await SwapCmAsync(jobOnId, "cm-b");
        var novo = await SeedPesoAsync("cm-b");
        await SeedPesoAsync("cm-a");                    // a combinação já tem Peso
        await SeedPesoAsync("cm-b", bqToolId: "bq-c");  // outra BQ

        var candidate = Assert.Single(await _query.FindCandidatesForControloAsync(controloId));
        Assert.Equal(novo, candidate.PesoId);
        Assert.Equal("83", candidate.CmLot);

        // Com o Peso das ferramentas atuais, não há nada a propor.
        await _handler.HandleAsync(new AssociatePesoToControlo(controloId, novo));
        Assert.Empty(await _query.FindCandidatesForControloAsync(controloId));
    }

    [Fact]
    public async Task Productions_ForPeso_IncludeJobOnsThatHadTheCmBeforeASwap()
    {
        var (trocada, controloId) = await SeedProductionAsync("202616", "cm-a");
        await SeedPesoAsync("cm-b", controloId);
        await SwapCmAsync(trocada, "cm-b");
        var (outraBq, _) = await SeedProductionAsync("202617", "cm-a", bqToolId: "bq-c");
        var loose = await SeedPesoAsync("cm-a");

        var productions = await _query.FindProductionsForPesoAsync(loose);

        Assert.Equal(new[] { trocada }, productions.Select(p => p.JobOnId).ToArray());
        Assert.DoesNotContain(outraBq, productions.Select(p => p.JobOnId));
    }

    // A BQ conta pela referência, qualquer lote (o Diogo, 27/10/2026: «o vol
    // da BQ é sempre igual, independentemente se é lote 6 ou 59»): o Peso de
    // outro lote da mesma BQ associa-se e fica com o lote da produção.
    [Fact]
    public async Task Associate_PesoOfAnotherLotOfTheSameBq_IsAcceptedAndTakesTheProductionLot()
    {
        var (jobOnId, controloId) = await SeedProductionAsync("202616", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a", bqToolId: "bq-b");

        Assert.Equal(new[] { jobOnId }, (await _query.FindProductionsForPesoAsync(pesoId)).Select(p => p.JobOnId).ToArray());
        Assert.Equal(pesoId, Assert.Single(await _query.FindCandidatesForControloAsync(controloId)).PesoId);

        var outcome = await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(AssociatePesoToControloResult.Success, outcome.Result);
        var stored = await _db.Pesos.AsNoTracking().SingleAsync();
        Assert.Equal(controloId, stored.ControloId);
        Assert.Equal("bq-a", stored.BqToolId);
        Assert.Equal("165", stored.BqToolLotFrozen);
    }
}
