using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O que chega depois da aprovação (Diogo, 19/10/2026). Um Peso novo ou uma
/// folha nova (depois de trocar a CM, a BQ ou o MF) tem aprovação própria:
/// a parte volta a estar por aprovar (C7). Uma leitura registada ou
/// corrigida num registo já aprovado não muda a aprovação: a Revisão só
/// avisa «Registado/Corrigido depois da aprovação» (C9). O carimbo das
/// leituras corre como na app (o RegistoCarimboInterceptor).
/// </summary>
public sealed class RevisaoDepoisDaAprovacaoTests : IDisposable
{
    private static readonly DateTime Antes = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _handler;

    public RevisaoDepoisDaAprovacaoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new RegistoCarimboInterceptor(new Operador()))
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.SaveChanges();
        _handler = new ControloApprovalHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task UmPesoNovoDepoisDeAprovarOPeso_TemAprovacaoPropria_EAParteVoltaAPorAprovar()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026201");
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));

        // A CM trocou: o Peso novo é registado depois da aprovação.
        _db.Pesos.Add(NovoPeso(controloId, "T4", registadoEm: null));
        await _db.SaveChangesAsync();

        Assert.Equal(ControloRevisaoEstado.Pendente, await EstadoAsync(jobOnId));
        // A decisão dada ao primeiro Peso fica registada.
        Assert.True(Assert.Single(await _handler.PartsAsync(controloId)).Aprovado);
        var registos = await _handler.RegistosAsync(controloId);
        Assert.True(registos[ControloParte.Peso].UltimoRegistoNovo > (await _handler.PartsAsync(controloId))[0].EmUtc);

        // Aprovar outra vez cobre o Peso novo.
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
    }

    [Fact]
    public async Task UmaFolhaNovaDepoisDeAprovar_ContinuaAprovado()
    {
        // Os Pegamentos não se aprovam (Diogo, 19/10/2026): uma folha nova
        // não pede aprovação.
        var (controloId, jobOnId) = await NovaProducaoAsync("2026202", comPegamentos: true);
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));

        _db.Pegamentos.Add(new Pegamentos { ControloId = controloId, CmToolId = "T4", ToleranceMm = 0.20m, RegistadoEmUtc = DateTime.UtcNow.AddMinutes(1) });
        await _db.SaveChangesAsync();

        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
        Assert.Equal(ControloParte.Peso, Assert.Single((await _handler.RegistosAsync(controloId)).Keys));
    }

    [Fact]
    public async Task UmPesoNovoDepoisDeNaoAprovar_ContinuaNaoAprovado()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026203");
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: false, "4802", "Repetir.");

        _db.Pesos.Add(NovoPeso(controloId, "T4", registadoEm: null));
        await _db.SaveChangesAsync();

        Assert.Equal(ControloRevisaoEstado.NaoAprovado, await EstadoAsync(jobOnId));
    }

    [Fact]
    public async Task UmaLeituraRegistadaDepoisDaAprovacao_MantemAAprovacao_EFicaComAData()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026204");
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        var aprovadoEm = Assert.Single(await _handler.PartsAsync(controloId)).EmUtc;

        var peso = await _db.Pesos.SingleAsync(p => p.ControloId == controloId);
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 250m });
        await _db.SaveChangesAsync();

        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
        var registos = (await _handler.RegistosAsync(controloId))[ControloParte.Peso];
        Assert.True(registos.UltimaLeitura > aprovadoEm);
        Assert.False(registos.UltimoRegistoNovo > aprovadoEm);
    }

    [Fact]
    public async Task UmaLeituraDosPegamentosCorrigidaOuApagadaDepoisDaAprovacao_MantemAAprovacao()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026205", comPegamentos: true);
        var folha = await _db.Pegamentos.SingleAsync(g => g.ControloId == controloId);
        _db.PegamentosMeasurements.AddRange(
            new PegamentosMeasurement { PegamentosId = folha.Id, RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m },
            new PegamentosMeasurement { PegamentosId = folha.Id, RowPosition = 2, Component = PegamentosComponent.Cm, CosturaMm = 31m });
        await _db.SaveChangesAsync();
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);

        var leituras = await _db.PegamentosMeasurements.OrderBy(m => m.RowPosition).ToListAsync();
        var corrigir = new CorrectPegamentosMeasurementHandler(_db);
        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await corrigir.EditAsync(
            new EditPegamentosMeasurement(new PegamentosMeasurementAnchor(leituras[0].Id, controloId, null), 30.5m, null)));

        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));

        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await corrigir.DeleteAsync(
            new PegamentosMeasurementAnchor(leituras[1].Id, controloId, null)));
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
    }

    private async Task<ControloRevisaoEstado> EstadoAsync(int jobOnId)
    {
        _db.ChangeTracker.Clear();
        return (await new ControloRevisaoQuery(_db).FindByJobOnAsync(jobOnId))!.Estado;
    }

    private async Task<(int ControloId, int JobOnId)> NovaProducaoAsync(string numero, bool comPegamentos = false)
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = numero, MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        _db.Pesos.Add(NovoPeso(controlo.Id, "T3", Antes));
        if (comPegamentos)
        {
            _db.Pegamentos.Add(new Pegamentos { ControloId = controlo.Id, CmToolId = "T3", ToleranceMm = 0.20m, RegistadoEmUtc = Antes });
        }

        await _db.SaveChangesAsync();
        return (controlo.Id, jobOn.Id);
    }

    private static Peso NovoPeso(int controloId, string cm, DateTime? registadoEm) => new()
    {
        CmToolId = cm, BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
        VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
        ControloId = controloId, RegistadoEmUtc = registadoEm
    };

    private sealed class Operador : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, OperatorId = "4801", DisplayName = "Operador" };
    }
}
