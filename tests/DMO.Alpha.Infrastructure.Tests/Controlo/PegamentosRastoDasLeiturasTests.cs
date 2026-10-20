using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O rasto das leituras dos Pegamentos corrigidas ou apagadas (sweep de
/// 19/10/2026, 4.6; Decisão 001): fica o valor antigo, o novo, quem e
/// quando, gravado na mesma operação, como nos movimentos BQ apagados.
/// Corrigir e apagar continuam livres. O carimbo de quem corre como na app
/// (o RegistoCarimboInterceptor).
/// </summary>
public sealed class PegamentosRastoDasLeiturasTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CorrectPegamentosMeasurementHandler _corrigir;

    public PegamentosRastoDasLeiturasTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new RegistoCarimboInterceptor(new Operador()))
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.SaveChanges();
        _corrigir = new CorrectPegamentosMeasurementHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CorrigirUmaLeitura_GuardaOValorAntigoONovoQuemEQuando()
    {
        var (controloId, folhaId) = await NovaFolhaAsync("2026301");
        var leituras = await LeiturasAsync(folhaId,
            (PegamentosComponent.Cm, 30m, 30.1m),
            (PegamentosComponent.Bq, 20m, null),
            (PegamentosComponent.Cm, 31m, null));

        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await _corrigir.EditAsync(
            new EditPegamentosMeasurement(new PegamentosMeasurementAnchor(leituras[2], controloId, null), 31.5m, 30.9m)));

        var rasto = Assert.Single(await _db.PegamentosMeasurementHistorico.AsNoTracking().ToListAsync());
        Assert.Equal(folhaId, rasto.PegamentosId);
        Assert.Equal(leituras[2], rasto.MeasurementId);
        // O N.º que a folha mostra: a segunda leitura da CM.
        Assert.Equal(2, rasto.Numero);
        Assert.Equal(PegamentosComponent.Cm, rasto.Component);
        Assert.False(rasto.Apagada);
        Assert.Equal((31m, (decimal?)null), (rasto.CosturaAntesMm, rasto.ContraCosturaAntesMm));
        Assert.Equal(((decimal?)31.5m, (decimal?)30.9m), (rasto.CosturaDepoisMm, rasto.ContraCosturaDepoisMm));
        Assert.Equal("Operador", rasto.Por);
        // A mesma data que a Revisão compara com a aprovação (C9).
        var folha = await _db.Pegamentos.AsNoTracking().SingleAsync(g => g.Id == folhaId);
        Assert.Equal(folha.CorrigidoEmUtc, rasto.EmUtc);

        // A leitura fica com o valor novo.
        var leitura = await _db.PegamentosMeasurements.AsNoTracking().SingleAsync(m => m.Id == leituras[2]);
        Assert.Equal((31.5m, (decimal?)30.9m), (leitura.CosturaMm, leitura.ContraCosturaMm));
    }

    [Fact]
    public async Task ApagarUmaLeitura_GuardaOQueTinha_EAConsultaDoControloMostraSoAsDele()
    {
        var (controloId, folhaId) = await NovaFolhaAsync("2026302");
        var leituras = await LeiturasAsync(folhaId,
            (PegamentosComponent.Bq, 20m, null),
            (PegamentosComponent.Cm, 30m, 30.1m));
        var (outroControlo, outraFolha) = await NovaFolhaAsync("2026303");
        var outras = await LeiturasAsync(outraFolha, (PegamentosComponent.Cm, 40m, null));

        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await _corrigir.DeleteAsync(
            new PegamentosMeasurementAnchor(leituras[1], controloId, null)));
        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await _corrigir.DeleteAsync(
            new PegamentosMeasurementAnchor(outras[0], outroControlo, null)));

        Assert.False(await _db.PegamentosMeasurements.AnyAsync(m => m.Id == leituras[1]));
        var rasto = Assert.Single(await new PegamentosHistoricoQuery(_db).ListByControloAsync(controloId));
        Assert.Equal(leituras[1], rasto.MeasurementId);
        Assert.Equal(1, rasto.Numero);
        Assert.Equal(PegamentosComponent.Cm, rasto.Component);
        Assert.True(rasto.Apagada);
        Assert.Equal((30m, (decimal?)30.1m), (rasto.CosturaAntesMm, rasto.ContraCosturaAntesMm));
        Assert.Null(rasto.CosturaDepoisMm);
        Assert.Null(rasto.ContraCosturaDepoisMm);
        Assert.Equal("Operador", rasto.Por);
    }

    [Fact]
    public async Task GuardarOsMesmosValores_NaoDeixaRastoNemAviso()
    {
        var (controloId, folhaId) = await NovaFolhaAsync("2026304");
        var leituras = await LeiturasAsync(folhaId, (PegamentosComponent.Cm, 30m, 30.1m));

        Assert.Equal(CorrectPegamentosMeasurementResult.Success, await _corrigir.EditAsync(
            new EditPegamentosMeasurement(new PegamentosMeasurementAnchor(leituras[0], controloId, null), 30.00m, 30.10m)));

        Assert.False(await _db.PegamentosMeasurementHistorico.AnyAsync());
        Assert.Null((await _db.Pegamentos.AsNoTracking().SingleAsync(g => g.Id == folhaId)).CorrigidoEmUtc);
    }

    [Fact]
    public async Task EliminarOJobOn_LevaORastoComAFolha()
    {
        var (controloId, folhaId) = await NovaFolhaAsync("2026305");
        var leituras = await LeiturasAsync(folhaId, (PegamentosComponent.Cm, 30m, null));
        await _corrigir.EditAsync(new EditPegamentosMeasurement(new PegamentosMeasurementAnchor(leituras[0], controloId, null), 30.5m, null));
        Assert.True(await _db.PegamentosMeasurementHistorico.AnyAsync());
        _db.ChangeTracker.Clear();

        var jobOnId = await _db.Controlos.Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
        Assert.Equal(DeleteJobOnResult.Success, await new DeleteJobOnHandler(_db).HandleAsync(new DeleteJobOn(jobOnId, "Diogo")));

        _db.ChangeTracker.Clear();
        Assert.False(await _db.PegamentosMeasurementHistorico.AnyAsync());
    }

    private async Task<(int ControloId, int FolhaId)> NovaFolhaAsync(string numero)
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = numero, MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        var folha = new Pegamentos { ControloId = controlo.Id, CmToolId = "T3", ToleranceMm = 0.20m };
        _db.Pegamentos.Add(folha);
        await _db.SaveChangesAsync();
        return (controlo.Id, folha.Id);
    }

    private async Task<int[]> LeiturasAsync(int folhaId, params (PegamentosComponent Component, decimal Costura, decimal? Contra)[] linhas)
    {
        var leituras = linhas.Select((l, i) => new PegamentosMeasurement
        {
            PegamentosId = folhaId, RowPosition = i + 1, Component = l.Component,
            CosturaMm = l.Costura, ContraCosturaMm = l.Contra, NominalGargaloMm = 30m
        }).ToArray();
        _db.PegamentosMeasurements.AddRange(leituras);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return leituras.Select(l => l.Id).ToArray();
    }

    private sealed class Operador : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, OperatorId = "4801", DisplayName = "Operador" };
    }
}
