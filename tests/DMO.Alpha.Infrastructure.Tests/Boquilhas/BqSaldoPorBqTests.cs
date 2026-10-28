using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// O Saldo é da BQ em todas as produções (Diogo, 28/10/2026: «só deve criar
/// saldo quando entra mais do que saiu»). A BQ Y232 lote 167: saiu na
/// produção anterior e voltou registada na seguinte; não pode ficar a mais.
/// </summary>
public sealed class BqSaldoPorBqTests : IDisposable
{
    private static readonly DateTime Dia = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
    private readonly DmoDbContext _db;
    private readonly RegisterBqMovementHandler _handler;

    private sealed class NoSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    public BqSaldoPorBqTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RegisterBqMovementHandler(_db, new NoSettings());
    }

    public void Dispose() => _db.Dispose();

    private async Task<BqContext> ProducaoAsync(string numero)
    {
        var jobOn = new JobOn { Reference = "6018Y232", ProductionNumber = numero, Machine = new Machine { Code = "C2-" + numero } };
        var bq = new BqContext { JobOn = jobOn, ToolId = "BQ-Y232" };
        _db.JobOns.Add(jobOn);
        _db.BqContexts.Add(bq);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return bq;
    }

    private async Task<RegisteredBqMovement> RegistarAsync(BqContext bq, string tipo, int quantidade, int dias)
    {
        var outcome = await _handler.HandleForContextAsync(new RegisterBqMovementForContext(bq.Id, tipo, quantidade, OccurredAtUtc: Dia.AddDays(dias)));
        Assert.Equal(RegisterBqMovementResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        return outcome.Movement!;
    }

    [Fact]
    public async Task BqQueSaiuNumJobOn_EVoltaNoutro_NaoFicaComSaldo()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "167", BqAccountedQuantity = 120 });
        await _db.SaveChangesAsync();
        var anterior = await ProducaoAsync("202602");
        var atual = await ProducaoAsync("202603");

        await RegistarAsync(anterior, "saida", 8, 0);
        await RegistarAsync(atual, "entrada", 2, 4);
        await RegistarAsync(atual, "saida", 5, 6);
        await RegistarAsync(atual, "entrada", 5, 6);
        await RegistarAsync(atual, "entrada", 6, 7);
        await RegistarAsync(atual, "saida", 12, 8);

        var registo = (await new BoquilhasRegistoQuery(_db).FindByContextAsync(atual.Id))!.Trace!;

        Assert.All(registo.Movements, m => Assert.Null(m.Discrepancy));
        Assert.Null(registo.AccumulatedDiscrepancy);
        // Fora: 8 + 5 + 12 saíram, 2 + 5 + 6 voltaram.
        Assert.Equal(12, registo.QuantityLegitimatelyOut);
        Assert.Equal(0, await _db.BqMovements.CountAsync(m => m.Discrepancy != null));
    }

    [Fact]
    public async Task EntraMaisDoQueSaiu_EmTodasAsProducoes_FicaComSaldo()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "167" });
        await _db.SaveChangesAsync();
        var anterior = await ProducaoAsync("202602");
        var atual = await ProducaoAsync("202603");

        await RegistarAsync(anterior, "saida", 3, 0);
        var entrada = await RegistarAsync(atual, "entrada", 5, 1);

        var registo = (await new BoquilhasRegistoQuery(_db).FindByContextAsync(atual.Id))!.Trace!;

        Assert.Equal(-2, registo.Movements.Single(m => m.MovementId == entrada.MovementId).Discrepancy);
        Assert.Equal(-2, registo.AccumulatedDiscrepancy);
        Assert.Equal(-2, (await _db.BqMovements.AsNoTracking().SingleAsync(m => m.Id == entrada.MovementId)).Discrepancy);
    }
}
