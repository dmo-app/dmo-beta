using System.Text.Json;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Production-sheet rules from PRODUCTION_CONTEXT and the Peso contract:
/// current versus explicitly selected sheets, frozen values, optional support,
/// and explicit association. Every load checks the persisted state again.
/// </summary>
public sealed class PesoFolhaQueryTests : IDisposable
{
    private readonly ReadOnlyGuard _guard = new();
    private readonly DbContextOptions<DmoDbContext> _options;
    private readonly DmoDbContext _db;
    private readonly CapSupportCalls _support;

    public PesoFolhaQueryTests()
    {
        _options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_guard)
            .Options;
        _db = new DmoDbContext(_options);
        _support = new CapSupportCalls(new PesoCapSupportQuery(_db));
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public async Task SemEscolhaValida_AbreOPesoAtualDaProducao(int? requested)
    {
        var data = await SeedAsync();
        var sheet = await LoadAsync(data.ControloId, requested);

        Assert.Equal(data.Current!.Id, sheet.Peso!.PesoId);
        Assert.True(sheet.Peso.IsCurrent);
        Assert.Equal(new[] { data.Old!.Id, data.Current.Id }, sheet.Pesos.Select(p => p.PesoId));
        Assert.Empty(sheet.AssociationCandidates);
        Assert.Empty(_support.Requests);
    }

    [Fact]
    public async Task PesoDeOutraProducao_NaoSubstituiOPesoDestaProducao()
    {
        var data = await SeedAsync();
        var sheet = await LoadAsync(data.ControloId, data.Foreign.Id);

        Assert.Equal(data.Current!.Id, sheet.Peso!.PesoId);
        Assert.All(sheet.Pesos, p => Assert.Equal(data.ControloId, p.ControloId));
    }

    [Fact]
    public async Task PesoAnteriorEscolhido_MantemValoresCongeladosEUsaASuaCmNoApoio()
    {
        var data = await SeedAsync();
        var sheet = await LoadAsync(data.ControloId, data.Old!.Id);

        Assert.Equal(data.Old.Id, sheet.Peso!.PesoId);
        Assert.False(sheet.Peso.IsCurrent);
        Assert.Equal("CM congelada", sheet.Peso.CmToolReference);
        Assert.Equal(7m, sheet.Peso.VolumePuncaoCm3);
        Assert.Equal(175m, sheet.Peso.NominalWeightGrams);
        Assert.Equal("5448", sheet.ToolFacts!.CmTool!.Reference);
        Assert.Equal(9m, sheet.ToolFacts.CmTechnical!.VolumePuncaoCm3);
        Assert.Equal("CM-antiga", Assert.Single(_support.Requests).CmToolId);
        Assert.Equal(40m, sheet.CapSupport!.DiametroMm);
        Assert.Equal(9m, sheet.CapSupport.AlturaEfetivaMm);
        Assert.Empty(sheet.AssociationCandidates);
    }

    [Fact]
    public async Task DepoisDaTroca_SemPesoAtual_NaoAbreOUltimoENaoAssociaSozinho()
    {
        var data = await SeedAsync(current: false);
        var sheet = await LoadAsync(data.ControloId);

        Assert.Null(sheet.Peso);
        Assert.Equal(data.Old!.Id, Assert.Single(sheet.Pesos).PesoId);
        Assert.False(sheet.Pesos[0].IsCurrent);
        Assert.Equal(data.Candidates.Reverse().Select(p => p.Id), sheet.AssociationCandidates.Select(p => p.PesoId));
        Assert.Equal("5448", sheet.ToolFacts!.CmTool!.Reference);
        Assert.Empty(_support.Requests);
    }

    [Fact]
    public async Task AntesDoPrimeiroPeso_MostraFactosDaProducaoECandidatosSemCriarNada()
    {
        var data = await SeedAsync(old: false, current: false);
        var sheet = await LoadAsync(data.ControloId);

        Assert.Null(sheet.Peso);
        Assert.Empty(sheet.Pesos);
        Assert.Equal("202605", sheet.ProductionContext!.ProductionNumber);
        Assert.Equal("CM-atual", sheet.ToolFacts!.CmTool!.ToolId);
        Assert.Equal("BQ", sheet.ToolFacts.BqTool!.ToolId);
        Assert.Equal("MF", sheet.ToolFacts.MfTool!.ToolId);
        Assert.Equal(12m, sheet.ToolFacts.BqTechnical!.VolumeMarisaCm3);
        Assert.Equal(2.4m, sheet.ToolFacts.CmGlassDensity);
        Assert.Equal(2, sheet.AssociationCandidates.Count);
    }

    [Fact]
    public async Task ControloInexistente_DevolveAFolhaVaziaSemInventarContexto()
    {
        await SeedAsync();
        var sheet = await LoadAsync(int.MaxValue);

        Assert.Null(sheet.ProductionContext);
        Assert.Null(sheet.Peso);
        Assert.Null(sheet.ToolFacts);
        Assert.Null(sheet.CapSupport);
        Assert.Empty(sheet.Pesos);
        Assert.Empty(sheet.AssociationCandidates);
    }

    [Fact]
    public async Task TecnicosEmFalta_NaoImpedemAbrirAFolhaNemInventamValores()
    {
        var data = await SeedAsync(technical: false);
        var sheet = await LoadAsync(data.ControloId);

        Assert.Equal(data.Current!.Id, sheet.Peso!.PesoId);
        Assert.NotNull(sheet.ToolFacts!.CmTool);
        Assert.Null(sheet.ToolFacts.CmTechnical);
        Assert.Null(sheet.ToolFacts.BqTechnical);
        Assert.Null(sheet.ToolFacts.CmGlassDensity);
        Assert.False(sheet.CapSupport!.TemReferencia);
        Assert.Contains("O Peso pode continuar sem este apoio", sheet.CapSupport.EstadoMensagem);
        Assert.Empty(_support.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    public async Task CaloteSimulada_MudaSoAApresentacaoSemRelerTecnicos(int simulated)
    {
        var data = await SeedAsync();
        var sheet = await LoadAsync(data.ControloId, simulated: simulated);

        Assert.Equal("Simulação", sheet.CapSupport!.OrigemCalote);
        Assert.Equal((decimal)simulated, sheet.CapSupport.AlturaEfetivaMm);
        Assert.Equal(9m, sheet.ProductionContext!.CaloteMm);
        Assert.Empty(_support.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" CM-atual ")]
    public async Task IdentificadorNaoCanonico_MantemORecursoAQueryDoApoio(string toolId)
    {
        var data = await SeedAsync();
        var context = new ContextWithToolId(new ProductionContextQuery(_db), toolId);
        var sheet = await LoadAsync(data.ControloId, context: context);

        Assert.Equal(toolId, Assert.Single(_support.Requests).CmToolId);
        Assert.Equal(toolId.Length > 0, sheet.CapSupport!.TemReferencia);
    }

    [Fact]
    public async Task CancelarOCarregamento_PropagaOCancelamentoSemGravar()
    {
        var data = await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var before = await SnapshotAsync();
        _db.ChangeTracker.Clear();
        _guard.Reading = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Query().CarregarAsync(
            data.ControloId, null, null, cancellation.Token));

        Assert.Equal(before, await SnapshotAsync());
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private PesoFolhaQuery Query(IProductionContextQuery? context = null) => new(
        context ?? new ProductionContextQuery(_db), new PesoQuery(_db),
        new PesoToolFactsQuery(_db), _support, new PesoAssociationQuery(_db));

    private async Task<PesoFolhaInicial> LoadAsync(
        int controloId, int? requested = null, decimal? simulated = null, IProductionContextQuery? context = null)
    {
        var before = await SnapshotAsync();
        _db.ChangeTracker.Clear();
        _guard.Reading = true;
        var result = await Query(context).CarregarAsync(controloId, requested, simulated);
        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Equal(before, await SnapshotAsync());
        return result;
    }

    private async Task<string> SnapshotAsync()
    {
        await using var fresh = new DmoDbContext(_options);
        return JsonSerializer.Serialize(new
        {
            Pesos = await fresh.Pesos.AsNoTracking().OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.ControloId, p.CmToolId, p.BqToolId, p.VolumePuncaoCm3, p.NominalWeightGrams }).ToListAsync(),
            Tecnicos = await fresh.ToolTechnicalContexts.AsNoTracking().OrderBy(t => t.ToolId)
                .Select(t => new { t.ToolId, t.Process, t.VolumePuncaoCm3, t.VolumeMarisaCm3, t.TpSagittaMm }).ToListAsync(),
            Producoes = await fresh.JobOns.AsNoTracking().OrderBy(j => j.Id)
                .Select(j => new { j.Id, j.CaloteMm }).ToListAsync(),
            Controlos = await fresh.Controlos.AsNoTracking().Select(c => c.Id).OrderBy(id => id).ToListAsync(),
            Leituras = await fresh.PesoMeasurements.AsNoTracking().CountAsync()
        });
    }

    private sealed record Seed(int ControloId, Peso? Old, Peso? Current, Peso Foreign, Peso[] Candidates);

    private async Task<Seed> SeedAsync(bool old = true, bool current = true, bool technical = true)
    {
        var machine = new Machine { Code = "B1" };
        var production = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "202605", CaloteMm = 9m };
        var foreign = new JobOn { Machine = machine, Reference = "OUTRA", ProductionNumber = "202606" };
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = production };
        var otherControlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = foreign };
        _db.Controlos.AddRange(controlo, otherControlo);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-antiga", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "CM-atual", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" },
            new Tool { ToolId = "MF", Type = ToolType.Mf, Reference = "M88", Lot = "4" });
        _db.CmContexts.Add(new CmContext { JobOn = production, ToolId = "CM-atual" });
        _db.BqContexts.Add(new BqContext { JobOn = production, ToolId = "BQ" });
        _db.MfContexts.Add(new MfContext { JobOn = production, ToolId = "MF" });
        _db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4m });
        if (technical)
        {
            _db.ToolTechnicalContexts.AddRange(
                new ToolTechnicalContext { ToolId = "CM-antiga", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 8m, TpDiametroMm = 40m, TpSagittaMm = 6m },
                new ToolTechnicalContext { ToolId = "CM-atual", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 9m, TpDiametroMm = 50m, TpSagittaMm = 8m },
                new ToolTechnicalContext { ToolId = "BQ", VolumeMarisaCm3 = 12m });
        }
        await _db.SaveChangesAsync();
        var oldPeso = old ? AddPeso(controlo.Id, "CM-antiga") : null;
        var currentPeso = current ? AddPeso(controlo.Id, "CM-atual") : null;
        var foreignPeso = AddPeso(otherControlo.Id, "CM-atual");
        var candidates = new[] { AddPeso(null, "CM-atual"), AddPeso(null, "CM-atual") };
        await _db.SaveChangesAsync();
        return new Seed(controlo.Id, oldPeso, currentPeso, foreignPeso, candidates);
    }

    private Peso AddPeso(int? controloId, string cmToolId)
    {
        var peso = new Peso
        {
            ControloId = controloId, CmToolId = cmToolId, BqToolId = "BQ", Process = "NNPB",
            WaterTemperatureC = 20, WaterDensityGramsPerCm3 = 0.9982m, GlassDensityGramsPerCm3 = 2.4m,
            VolumePuncaoCm3 = 7m, VolumeMarisaCm3 = 12m, NominalWeightGrams = 175m,
            CmToolReferenceFrozen = "CM congelada", CmToolLotFrozen = "1"
        };
        _db.Pesos.Add(peso);
        return peso;
    }

    private sealed class ReadOnlyGuard : SaveChangesInterceptor
    {
        public bool Reading { get; set; }
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            Reading ? throw new InvalidOperationException("Sheet loading must not save changes.") : result;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SavingChanges(eventData, result));
    }

    private sealed class CapSupportCalls(IPesoCapSupportQuery inner) : IPesoCapSupportQuery
    {
        public List<PesoCapSupportRequest> Requests { get; } = [];
        public Task<PesoCapSupportReadModel> GetAsync(PesoCapSupportRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return inner.GetAsync(request, cancellationToken);
        }
    }

    private sealed class ContextWithToolId(IProductionContextQuery inner, string toolId) : IProductionContextQuery
    {
        public Task<ProductionContextReadModel?> GetAsync(int jobOnId, CancellationToken cancellationToken = default) =>
            inner.GetAsync(jobOnId, cancellationToken);
        public async Task<ProductionContextReadModel?> GetByControloAsync(int controloId, CancellationToken cancellationToken = default) =>
            (await inner.GetByControloAsync(controloId, cancellationToken))! with { CmToolId = toolId };
    }
}
