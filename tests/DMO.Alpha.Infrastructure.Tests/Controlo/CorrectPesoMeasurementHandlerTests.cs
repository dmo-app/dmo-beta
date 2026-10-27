using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a correção e o apagar de uma leitura do Peso (Diogo, 27/10/2026;
/// Decisão 001; A-0231): a correção muda a leitura e deixa rasto com o antes
/// e o depois; guardar os mesmos valores não deixa rasto; o fisicamente
/// impossível é recusado como no registo; apagar tira a leitura, deixa rasto
/// e renumera as posições 1..n pela ordem de registo.
/// </summary>
public sealed class CorrectPesoMeasurementHandlerTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CorrectPesoMeasurementHandler _handler;

    public CorrectPesoMeasurementHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new CorrectPesoMeasurementHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // 20 °C, marisa 10, punção 30, vidro 2.50: 1 g de água dá peso do vidro
    // negativo (como em RecordPesoMeasurementHandlerTests).
    private async Task<(Peso Peso, List<PesoMeasurement> Leituras)> SeedAsync(params (string Cm, decimal Agua)[] leituras)
    {
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        var peso = new Peso
        {
            CmToolId = "T3",
            BqToolId = "BQ-TOOL-9",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            NominalWeightGrams = 200m,
            GlassDensityGramsPerCm3 = 2.50m
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();

        var linhas = new List<PesoMeasurement>();
        for (var i = 0; i < leituras.Length; i++)
        {
            var linha = new PesoMeasurement
            {
                PesoId = peso.Id,
                RowPosition = i + 1,
                CmSubject = leituras[i].Cm,
                WaterWeightGrams = leituras[i].Agua
            };
            _db.PesoMeasurements.Add(linha);
            linhas.Add(linha);
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (peso, linhas);
    }

    [Fact]
    public async Task Corrigir_MudaALeitura_EDeixaRastoComOAntesEODepois()
    {
        var (peso, leituras) = await SeedAsync(("12", 199.64m), ("21", 199.10m));

        var result = await _handler.EditAsync(new EditPesoMeasurement(
            new PesoMeasurementAnchor(leituras[1].Id, peso.Id), " 22 ", 198.50m));

        Assert.Equal(CorrectPesoMeasurementResult.Success, result);
        var linha = await _db.PesoMeasurements.AsNoTracking().SingleAsync(m => m.Id == leituras[1].Id);
        Assert.Equal("22", linha.CmSubject);
        Assert.Equal(198.50m, linha.WaterWeightGrams);
        Assert.Equal(2, linha.RowPosition);

        var rasto = await _db.PesoMeasurementHistorico.AsNoTracking().SingleAsync();
        Assert.Equal(peso.Id, rasto.PesoId);
        Assert.Equal(leituras[1].Id, rasto.MeasurementId);
        Assert.Equal(2, rasto.Numero);
        Assert.False(rasto.Apagada);
        Assert.Equal("21", rasto.CmSubjectAntes);
        Assert.Equal(199.10m, rasto.WaterWeightAntesGrams);
        Assert.Equal("22", rasto.CmSubjectDepois);
        Assert.Equal(198.50m, rasto.WaterWeightDepoisGrams);
    }

    [Fact]
    public async Task GuardarOsMesmosValores_NaoDeixaRasto()
    {
        var (peso, leituras) = await SeedAsync(("12", 199.64m));

        var result = await _handler.EditAsync(new EditPesoMeasurement(
            new PesoMeasurementAnchor(leituras[0].Id, peso.Id), "12", 199.64m));

        Assert.Equal(CorrectPesoMeasurementResult.Success, result);
        Assert.Equal(0, await _db.PesoMeasurementHistorico.CountAsync());
    }

    [Fact]
    public async Task CorrigirParaPesoDoVidroZeroOuMenos_ERecusado_ALeituraFicaIgual()
    {
        var (peso, leituras) = await SeedAsync(("12", 199.64m));

        var result = await _handler.EditAsync(new EditPesoMeasurement(
            new PesoMeasurementAnchor(leituras[0].Id, peso.Id), "12", 1m));

        Assert.Equal(CorrectPesoMeasurementResult.ResultNonPositive, result);
        var linha = await _db.PesoMeasurements.AsNoTracking().SingleAsync();
        Assert.Equal(199.64m, linha.WaterWeightGrams);
        Assert.Equal(0, await _db.PesoMeasurementHistorico.CountAsync());
    }

    [Fact]
    public async Task Apagar_TiraALeitura_DeixaRasto_ERenumeraAsPosicoes()
    {
        var (peso, leituras) = await SeedAsync(("12", 199.64m), ("21", 199.10m), ("45", 198.90m), ("46", 199.00m));

        var result = await _handler.DeleteAsync(new PesoMeasurementAnchor(leituras[1].Id, peso.Id));

        Assert.Equal(CorrectPesoMeasurementResult.Success, result);
        var ficam = await _db.PesoMeasurements.AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .OrderBy(m => m.RowPosition)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, ficam.Select(m => m.RowPosition).ToArray());
        Assert.Equal(new[] { "12", "45", "46" }, ficam.Select(m => m.CmSubject).ToArray());

        var rasto = await _db.PesoMeasurementHistorico.AsNoTracking().SingleAsync();
        Assert.True(rasto.Apagada);
        Assert.Equal(2, rasto.Numero);
        Assert.Equal("21", rasto.CmSubjectAntes);
        Assert.Equal(199.10m, rasto.WaterWeightAntesGrams);
        Assert.Null(rasto.CmSubjectDepois);
        Assert.Null(rasto.WaterWeightDepoisGrams);
    }

    [Fact]
    public async Task LeituraDeOutroPeso_OuQueNaoExiste_NaoEncontrada_NadaMuda()
    {
        var (peso, leituras) = await SeedAsync(("12", 199.64m));

        Assert.Equal(
            CorrectPesoMeasurementResult.MeasurementNotFound,
            await _handler.EditAsync(new EditPesoMeasurement(
                new PesoMeasurementAnchor(leituras[0].Id, peso.Id + 1), "12", 198m)));
        Assert.Equal(
            CorrectPesoMeasurementResult.MeasurementNotFound,
            await _handler.DeleteAsync(new PesoMeasurementAnchor(leituras[0].Id + 99, peso.Id)));

        Assert.Equal(1, await _db.PesoMeasurements.CountAsync());
        Assert.Equal(0, await _db.PesoMeasurementHistorico.CountAsync());
    }
}
