using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// What the Peso comparison screen reads around the readings, now outside the
/// page: the two-step production picker, the base Peso with its influence
/// figures, and the counter-mould candidates. Nothing is written.
/// </summary>
public sealed class PesoComparacaoEstadoQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly PesoComparacaoEstadoQuery _query;

    public PesoComparacaoEstadoQueryTests()
    {
        _query = new PesoComparacaoEstadoQuery(
            new ProductionPickerQuery(_db), new PesoQuery(_db), new ToolCandidateQuery(_db));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SemReferenciaNemProducaoPedida_DevolveOSelectVazio()
    {
        await SeedProducoesAsync(1);

        var lidas = await _query.ProducoesDaReferenciaAsync(null, null);

        Assert.Equal(PesoComparacaoProducoes.Vazio, lidas);
        Assert.Null(lidas.Referencia);
        Assert.False(lidas.SemProducoes);
        Assert.Empty(lidas.Producoes);
        Assert.Null(lidas.SelecionadaJobOnId);
    }

    [Fact]
    public async Task ReferenciaEscritaSemProducoes_DizQueNaoHaProducoes()
    {
        await SeedProducoesAsync(1);

        var lidas = await _query.ProducoesDaReferenciaAsync("9999X999", null);

        Assert.Equal("9999X999", lidas.Referencia);
        Assert.True(lidas.SemProducoes);
        Assert.Empty(lidas.Producoes);
        Assert.Null(lidas.SelecionadaJobOnId);
    }

    [Fact]
    public async Task ProducaoPedida_SemReferenciaEscrita_UsaAReferenciaDela()
    {
        var ids = await SeedProducoesAsync(3);

        var lidas = await _query.ProducoesDaReferenciaAsync(null, ids[1]);

        Assert.Equal("5447T173", lidas.Referencia);
        Assert.False(lidas.SemProducoes);
        Assert.Equal(ids[1], lidas.SelecionadaJobOnId);
        Assert.Contains(lidas.Producoes, p => p.JobOnId == ids[1]);
    }

    [Fact]
    public async Task ProducaoPedidaDeOutraReferencia_NaoFicaEscolhida()
    {
        var ids = await SeedProducoesAsync(2);
        var outra = await SeedOutraReferenciaAsync();

        var lidas = await _query.ProducoesDaReferenciaAsync("5447T173", outra);

        Assert.Equal("5447T173", lidas.Referencia);
        Assert.Null(lidas.SelecionadaJobOnId);
        Assert.Equal(ids.Count, lidas.Producoes.Count);
        Assert.DoesNotContain(lidas.Producoes, p => p.JobOnId == outra);
    }

    [Fact]
    public async Task ProducaoAntigaAlemDasUltimasCinco_FicaNoSelectEEscolhida()
    {
        var ids = await SeedProducoesAsync(IProductionPickerQuery.MaxResults + 2);
        var antiga = ids[0];

        var lidas = await _query.ProducoesDaReferenciaAsync("5447T173", antiga);

        Assert.False(lidas.SemProducoes);
        Assert.Equal(antiga, lidas.SelecionadaJobOnId);
        Assert.Equal(IProductionPickerQuery.MaxResults + 1, lidas.Producoes.Count);
        Assert.Contains(lidas.Producoes, p => p.JobOnId == antiga);
    }

    [Fact]
    public async Task Base_Inexistente_DevolveBaseNulaSemInfluencias()
    {
        var lida = await _query.BaseAsync(12345, null);

        Assert.Null(lida.Base);
        Assert.Empty(lida.InfluenciaDaBase);
        Assert.Empty(lida.InfluenciaDasNovas);
    }

    [Fact]
    public async Task Base_SemLeituras_MostraAPesoMasSemInfluencias()
    {
        var pesoId = await SeedPesoAsync(leituras: 0);

        var lida = await _query.BaseAsync(pesoId, null);

        Assert.Equal(pesoId, lida.Base!.PesoId);
        Assert.Empty(lida.InfluenciaDaBase);
        Assert.Empty(lida.InfluenciaDasNovas);
    }

    [Fact]
    public async Task Base_ComLeituras_CalculaAInfluenciaDaBasePelaOrdemDasPosicoes()
    {
        var pesoId = await SeedPesoAsync(leituras: 3);

        var lida = await _query.BaseAsync(pesoId, null);

        var esperado = PesoComparacaoInfluenciaCalculation.DaBase(
            lida.Base!.Measurements.OrderBy(m => m.RowPosition)
                .Select(m => (m.CmSubject, m.GlassWeightGrams)).ToList());
        Assert.Equal(3, lida.InfluenciaDaBase.Count);
        Assert.Equal(esperado, lida.InfluenciaDaBase);
        Assert.Equal(new[] { "1", "2", "3" }, lida.InfluenciaDaBase.Select(i => i.CmSubject));
        Assert.Empty(lida.InfluenciaDasNovas);
    }

    [Fact]
    public async Task Base_ComPares_CalculaAInfluenciaDasNovasContraAMediaDaBase()
    {
        var pesoId = await SeedPesoAsync(leituras: 3);
        var pares = new PesoComparacaoPares(1, 0, null, null,
            [new PesoComparacaoParRow(1, "1", 80m, "1", null, null, null)]);

        var lida = await _query.BaseAsync(pesoId, pares);

        var esperado = PesoComparacaoInfluenciaCalculation.Novas(
            pares.Rows, lida.Base!.Averages!.AverageGlassWeightGrams, lida.Base.Measurements.Count);
        Assert.Single(lida.InfluenciaDasNovas);
        Assert.Equal(esperado, lida.InfluenciaDasNovas);
    }

    [Fact]
    public async Task ContraMoldes_AReferenciaDoInicialVemPrimeiroDepoisPorReferencia()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-C", Type = ToolType.Cm, Reference = "5900", Lot = "1" },
            new Tool { ToolId = "CM-A", Type = ToolType.Cm, Reference = "5100", Lot = "1" },
            new Tool { ToolId = "CM-B", Type = ToolType.Cm, Reference = "5448", Lot = "1" },
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        await _db.SaveChangesAsync();

        var comInicial = await _query.ContraMoldesAsync("5448");
        var semInicial = await _query.ContraMoldesAsync(null);

        Assert.Equal(new[] { "5448", "5100", "5900" }, comInicial.Select(t => t.Reference));
        Assert.Equal(new[] { "5100", "5448", "5900" }, semInicial.Select(t => t.Reference));
        Assert.All(comInicial, t => Assert.Equal(ToolType.Cm, t.Type));
    }

    [Fact]
    public async Task OsTresPedidos_NaoGravamNada()
    {
        var ids = await SeedProducoesAsync(2);
        var pesoId = await SeedPesoAsync(leituras: 2);
        _db.ChangeTracker.Clear();

        await _query.ProducoesDaReferenciaAsync("5447T173", ids[0]);
        await _query.BaseAsync(pesoId, null);
        await _query.ContraMoldesAsync(null);

        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private async Task<List<int>> SeedProducoesAsync(int count)
    {
        var machine = new Machine { Code = "B1" };
        var producoes = Enumerable.Range(1, count)
            .Select(i => new JobOn
            {
                Machine = machine, Reference = "5447T173", ProductionNumber = (202600 + i).ToString(),
                InicioEm = new DateOnly(2026, 1, 1).AddDays(i)
            }).ToList();
        _db.JobOns.AddRange(producoes);
        await _db.SaveChangesAsync();
        return producoes.Select(p => p.Id).ToList();
    }

    private async Task<int> SeedOutraReferenciaAsync()
    {
        var outra = new JobOn { Machine = new Machine { Code = "C3" }, Reference = "6018Y232", ProductionNumber = "202699" };
        _db.JobOns.Add(outra);
        await _db.SaveChangesAsync();
        return outra.Id;
    }

    private async Task<int> SeedPesoAsync(int leituras)
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "CM", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" });
        var peso = new Peso
        {
            CmToolId = "CM", BqToolId = "BQ", Process = "NNPB", WaterTemperatureC = 20,
            WaterDensityGramsPerCm3 = 0.9982m, GlassDensityGramsPerCm3 = 2.4m,
            VolumePuncaoCm3 = 7m, VolumeMarisaCm3 = 12m
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        for (var i = 1; i <= leituras; i++)
        {
            _db.PesoMeasurements.Add(new PesoMeasurement
            {
                PesoId = peso.Id, RowPosition = i, CmSubject = i.ToString(), WaterWeightGrams = 290m + 10m * i
            });
        }
        await _db.SaveChangesAsync();
        return peso.Id;
    }
}
