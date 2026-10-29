using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// DMO-574 (A-0263): o Saldo do Histórico e dos PDFs é refeito com a regra de
/// agora («só há saldo quando entra mais do que saiu», a BQ em todas as
/// produções), e não o valor que ficou gravado de antes da regra.
/// </summary>
public sealed class BqSaldoNoHistoricoCoberturaA2Tests : IDisposable
{
    private static readonly DateTime Dia = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
    private readonly DmoDbContext _db;
    private readonly RegisterBqMovementHandler _handler;

    private sealed class NoSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    public BqSaldoNoHistoricoCoberturaA2Tests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
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

    private async Task GravarSaldoAntigoAsync(int movimento, int saldo)
    {
        var linha = await _db.BqMovements.SingleAsync(m => m.Id == movimento);
        linha.Discrepancy = saldo;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<IReadOnlyList<BqHistoryMovementItem>> HistoricoAsync() =>
        await new BqToolHistoryQuery(_db).ListMovementsAsync(null, null, null, null, null, null);

    [Fact]
    public async Task Historico_BqQueSaiuNumJobOnEVoltaNoutro_NaoMostraSaldo_MesmoComUmSaldoAntigoGravado()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "167" });
        await _db.SaveChangesAsync();
        var anterior = await ProducaoAsync("202602");
        var atual = await ProducaoAsync("202603");
        await RegistarAsync(anterior, "saida", 8, 0);
        var entrada = await RegistarAsync(atual, "entrada", 8, 3);
        // O que a regra antiga tinha gravado nesta entrada.
        await GravarSaldoAntigoAsync(entrada.MovementId, -3);

        var historico = await HistoricoAsync();

        Assert.Equal(2, historico.Count);
        Assert.All(historico, m => Assert.Null(m.Discrepancy));
        // E o PDF mostra «—» no Saldo, nunca o -3 antigo.
        var bytes = new BqHistoryPdfGenerator().GenerateMovementList(new(
            "Boquilhas · Movimentos", "Todos os dias", historico));
        var texto = PdfText.Of(bytes);
        Assert.Contains("202603 · 6018Y232 —", texto);
        Assert.DoesNotMatch("[-−]3", texto);
    }

    [Fact]
    public async Task Historico_EntraMaisDoQueSaiu_MostraOSaldoNaEntrada_ENoPdf()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-Y232", Type = ToolType.Bq, Reference = "Y232", Lot = "167" });
        await _db.SaveChangesAsync();
        var anterior = await ProducaoAsync("202602");
        var atual = await ProducaoAsync("202603");
        await RegistarAsync(anterior, "saida", 3, 0);
        var entrada = await RegistarAsync(atual, "entrada", 5, 1);
        // Um valor de antes da regra, diferente do de agora.
        await GravarSaldoAntigoAsync(entrada.MovementId, -9);

        var historico = await HistoricoAsync();

        Assert.Equal(-2, historico.Single(m => m.MovementId == entrada.MovementId).Discrepancy);
        Assert.All(historico.Where(m => m.MovementId != entrada.MovementId), m => Assert.Null(m.Discrepancy));
        var texto = PdfText.Of(new BqHistoryPdfGenerator().GenerateMovementList(new(
            "Boquilhas · Movimentos", "Todos os dias", historico)));
        Assert.Matches("202603 · 6018Y232 [-−]2", texto);
        Assert.DoesNotMatch("[-−]9", texto);
    }
}
