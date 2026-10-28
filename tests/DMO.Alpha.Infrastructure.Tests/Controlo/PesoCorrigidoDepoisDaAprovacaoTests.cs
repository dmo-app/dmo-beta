using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Uma leitura do Peso corrigida ou apagada depois da aprovação (relatório do
/// dmo-Improvements de 28/10/2026, S05): a aprovação mantém-se e a Revisão
/// avisa «Corrigido depois da aprovação» (C9). A data vem do rasto das
/// leituras; antes era sempre null e o aviso nunca aparecia.
/// </summary>
public sealed class PesoCorrigidoDepoisDaAprovacaoTests : IDisposable
{
    private static readonly DateTime Antes = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _handler;
    private readonly CorrectPesoMeasurementHandler _corrigir;

    public PesoCorrigidoDepoisDaAprovacaoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new RegistoCarimboInterceptor(new Operador()))
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.SaveChanges();
        _handler = new ControloApprovalHandler(_db);
        _corrigir = new CorrectPesoMeasurementHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SemCorrecoes_AUltimaCorrecaoFicaVazia()
    {
        var (controloId, _, _) = await NovaProducaoAsync("2026301", leituras: 2);

        Assert.Null((await _handler.RegistosAsync(controloId))[ControloParte.Peso].UltimaCorrecao);
    }

    [Fact]
    public async Task UmaLeituraCorrigidaDepoisDaAprovacao_MantemAAprovacao_EFicaComAData()
    {
        var (controloId, jobOnId, leituras) = await NovaProducaoAsync("2026302", leituras: 2);
        var aprovadoEm = await AprovarAsync(controloId);

        Assert.Equal(CorrectPesoMeasurementResult.Success, await _corrigir.EditAsync(
            new EditPesoMeasurement(new PesoMeasurementAnchor(leituras[0].Id, leituras[0].PesoId), "1", 251m)));

        var registos = (await _handler.RegistosAsync(controloId))[ControloParte.Peso];
        Assert.True(registos.UltimaCorrecao > aprovadoEm);
        Assert.False(registos.UltimoRegistoNovo > aprovadoEm);
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
    }

    [Fact]
    public async Task ApagarAUnicaLeituraDepoisDaAprovacao_FicaComAData()
    {
        var (controloId, jobOnId, leituras) = await NovaProducaoAsync("2026303", leituras: 1);
        var aprovadoEm = await AprovarAsync(controloId);

        Assert.Equal(CorrectPesoMeasurementResult.Success, await _corrigir.DeleteAsync(
            new PesoMeasurementAnchor(leituras[0].Id, leituras[0].PesoId)));

        var registos = (await _handler.RegistosAsync(controloId))[ControloParte.Peso];
        Assert.Null(registos.UltimaLeitura);
        Assert.True(registos.UltimaCorrecao > aprovadoEm);
        Assert.Equal(ControloRevisaoEstado.Aprovado, await EstadoAsync(jobOnId));
    }

    [Fact]
    public async Task UmaCorrecaoAntesDeAprovar_NaoContaDepoisDaAprovacao()
    {
        var (controloId, _, leituras) = await NovaProducaoAsync("2026304", leituras: 2);
        await _corrigir.EditAsync(new EditPesoMeasurement(new PesoMeasurementAnchor(leituras[1].Id, leituras[1].PesoId), "2", 249m));
        var aprovadoEm = await AprovarAsync(controloId);

        var registos = (await _handler.RegistosAsync(controloId))[ControloParte.Peso];
        Assert.NotNull(registos.UltimaCorrecao);
        Assert.False(registos.UltimaCorrecao > aprovadoEm);
    }

    [Fact]
    public async Task AsCorrecoesDeOutroControlo_NaoContam()
    {
        var (controloId, _, _) = await NovaProducaoAsync("2026305", leituras: 1);
        var (_, _, outras) = await NovaProducaoAsync("2026306", leituras: 1);
        await _corrigir.DeleteAsync(new PesoMeasurementAnchor(outras[0].Id, outras[0].PesoId));

        Assert.Null((await _handler.RegistosAsync(controloId))[ControloParte.Peso].UltimaCorrecao);
    }

    private async Task<DateTime> AprovarAsync(int controloId)
    {
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        return Assert.Single(await _handler.PartsAsync(controloId)).EmUtc;
    }

    private async Task<ControloRevisaoEstado> EstadoAsync(int jobOnId)
    {
        _db.ChangeTracker.Clear();
        return (await new ControloRevisaoQuery(_db).FindByJobOnAsync(jobOnId))!.Estado;
    }

    private async Task<(int ControloId, int JobOnId, List<PesoMeasurement> Leituras)> NovaProducaoAsync(string numero, int leituras)
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = numero, MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        var peso = new Peso
        {
            CmToolId = "T3", BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id, RegistadoEmUtc = Antes
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        var lista = Enumerable.Range(1, leituras)
            .Select(i => new PesoMeasurement { PesoId = peso.Id, RowPosition = i, CmSubject = i.ToString(), WaterWeightGrams = 250m })
            .ToList();
        _db.PesoMeasurements.AddRange(lista);
        await _db.SaveChangesAsync();
        return (controlo.Id, jobOn.Id, lista);
    }

    private sealed class Operador : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, OperatorId = "4801", DisplayName = "Operador" };
    }
}
