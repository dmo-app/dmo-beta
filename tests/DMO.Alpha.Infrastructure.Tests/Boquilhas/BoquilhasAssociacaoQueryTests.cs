using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// Os registos de uma BQ e a produção a associar, que a página Boquilhas
/// lia ela própria contra o contexto: as mesmas respostas de antes, e nada
/// é gravado.
/// </summary>
public sealed class BoquilhasAssociacaoQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    private async Task<(JobOn Producao, JobOn Anterior, BqContext Contexto, BqContext Trocado)> SeedAsync()
    {
        var b1 = new Machine { Code = "B1" };
        var c2 = new Machine { Code = "C2" };
        var producao = new JobOn
        {
            Machine = b1, Reference = "5447T194", ProductionNumber = "202605",
            InicioEm = new DateOnly(2026, 10, 1), FimEm = new DateOnly(2026, 10, 20)
        };
        var anterior = new JobOn
        {
            Machine = c2, Reference = "9389T173", ProductionNumber = "202601",
            InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 9, 10)
        };
        _db.Tools.AddRange(
            new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" },
            new Tool { ToolId = "BQ-2", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        _db.JobOns.AddRange(producao, anterior);
        await _db.SaveChangesAsync();
        var contexto = new BqContext { JobOnId = producao.Id, ToolId = "BQ-1" };
        // A BQ trocada: já não está na produção, mas o contexto lembra qual era.
        var trocado = new BqContext { JobOnId = null, ReplacedJobOnId = anterior.Id, ToolId = "BQ-1" };
        _db.BqContexts.AddRange(contexto, trocado);
        await _db.SaveChangesAsync();
        return (producao, anterior, contexto, trocado);
    }

    [Fact]
    public async Task RegistosDaBq_VemOsDaFerramentaEOsAssociadosMaisRecentesPrimeiro_ComProducaoEMovimentos()
    {
        var (producao, anterior, contexto, trocado) = await SeedAsync();
        var avulso = new BqRepairTrace { ToolId = "BQ-1" };
        var associado = new BqRepairTrace { BqContextId = contexto.Id };
        var daTroca = new BqRepairTrace { BqContextId = trocado.Id };
        var deOutraBq = new BqRepairTrace { ToolId = "BQ-2" };
        _db.BqRepairTraces.AddRange(avulso, associado, daTroca, deOutraBq);
        await _db.SaveChangesAsync();
        _db.BqMovements.AddRange(
            Movimento(associado.Id), Movimento(associado.Id), Movimento(avulso.Id), Movimento(deOutraBq.Id));
        await _db.SaveChangesAsync();

        var pagina = await new BoquilhasAssociacaoQuery(_db).RegistosDaBqAsync("BQ-1", null);

        Assert.Equal(3, pagina.TotalCount);
        Assert.Equal(new[] { daTroca.Id, associado.Id, avulso.Id }, pagina.Items.Select(r => r.Id));
        var troca = pagina.Items[0];
        Assert.True(troca.Trocada);
        Assert.Equal(("202601", "9389T173", "C2"), (troca.ProductionNumber, troca.ProductionReference, troca.MachineCode));
        Assert.Equal((anterior.InicioEm, anterior.FimEm), (troca.InicioEm, troca.FimEm));
        Assert.Equal(0, troca.MovementCount);
        var normal = pagina.Items[1];
        Assert.False(normal.Trocada);
        Assert.Equal(("202605", "5447T194", "B1", 2), (normal.ProductionNumber, normal.ProductionReference, normal.MachineCode, normal.MovementCount));
        Assert.Equal((producao.InicioEm, producao.FimEm), (normal.InicioEm, normal.FimEm));
        var semJobOn = pagina.Items[2];
        Assert.Null(semJobOn.BqContextId);
        Assert.Null(semJobOn.ProductionNumber);
        Assert.Equal(1, semJobOn.MovementCount);
    }

    [Fact]
    public async Task RegistosDaBq_PaginaPedidaForaDoFimFicaNaUltima()
    {
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        for (var i = 0; i < ListPaging.DefaultPageSize + 3; i++)
        {
            _db.BqRepairTraces.Add(new BqRepairTrace { ToolId = "BQ-1" });
        }
        await _db.SaveChangesAsync();
        var query = new BoquilhasAssociacaoQuery(_db);

        var primeira = await query.RegistosDaBqAsync("BQ-1", 1);
        var alem = await query.RegistosDaBqAsync("BQ-1", 99);

        Assert.Equal(ListPaging.DefaultPageSize, primeira.Items.Count);
        Assert.Equal(ListPaging.DefaultPageSize + 3, primeira.TotalCount);
        Assert.Equal(2, alem.PageNumber);
        Assert.Equal(3, alem.Items.Count);
    }

    [Fact]
    public async Task RegistoSemJobOn_DevolveOTraceAvulsoComOsMovimentosESemEleDevolveNulo()
    {
        var (_, _, contexto, _) = await SeedAsync();
        var avulso = new BqRepairTrace { ToolId = "BQ-1" };
        var associadoComAncora = new BqRepairTrace { ToolId = "BQ-2", BqContextId = contexto.Id };
        _db.BqRepairTraces.AddRange(avulso, associadoComAncora);
        await _db.SaveChangesAsync();
        _db.BqMovements.AddRange(Movimento(avulso.Id), Movimento(avulso.Id), Movimento(associadoComAncora.Id));
        await _db.SaveChangesAsync();
        var query = new BoquilhasAssociacaoQuery(_db);

        Assert.Equal(new BqRegistoPendente(avulso.Id, 2), await query.RegistoSemJobOnAsync("BQ-1"));
        Assert.Null(await query.RegistoSemJobOnAsync("BQ-2"));
        Assert.Null(await query.RegistoSemJobOnAsync("BQ-X"));
    }

    [Fact]
    public async Task Producao_DevolveIdEReferenciaESemProducaoDevolveNulo()
    {
        var (producao, _, _, _) = await SeedAsync();
        var query = new BoquilhasAssociacaoQuery(_db);

        Assert.Equal(new BqProducaoParaAssociar(producao.Id, "5447T194"), await query.ProducaoAsync(producao.Id));
        Assert.Null(await query.ProducaoAsync(producao.Id + 100));
    }

    [Fact]
    public async Task ProducoesDoNumero_MaisRecentePrimeiro_ComAIndicacaoDeTerAMesmaBq()
    {
        var (producao, _, _, _) = await SeedAsync();
        var maquina = _db.Machines.First(m => m.Code == "C2");
        var mesmoNumeroSemBq = new JobOn { MachineId = maquina.Id, Reference = "4526Y171", ProductionNumber = "202605" };
        var outroNumero = new JobOn { MachineId = maquina.Id, Reference = "1111Y111", ProductionNumber = "999999" };
        _db.JobOns.AddRange(mesmoNumeroSemBq, outroNumero);
        await _db.SaveChangesAsync();
        // A produção com o mesmo número tem outra BQ, não a deste registo.
        _db.BqContexts.Add(new BqContext { JobOnId = mesmoNumeroSemBq.Id, ToolId = "BQ-2" });
        await _db.SaveChangesAsync();
        var query = new BoquilhasAssociacaoQuery(_db);

        var producoes = await query.ProducoesDoNumeroAsync("202605", "BQ-1");

        Assert.Equal(new[] { mesmoNumeroSemBq.Id, producao.Id }, producoes.Select(p => p.Id));
        Assert.Equal(new[] { false, true }, producoes.Select(p => p.TemBq));
        Assert.Equal(new[] { "4526Y171", "5447T194" }, producoes.Select(p => p.Reference));
        Assert.Empty(await query.ProducoesDoNumeroAsync("000000", "BQ-1"));
    }

    [Fact]
    public async Task ContextoDaBqNaProducao_SoDaProducaoEDaFerramentaDoRegisto()
    {
        var (producao, _, contexto, _) = await SeedAsync();
        var query = new BoquilhasAssociacaoQuery(_db);

        Assert.Equal(contexto.Id, await query.ContextoDaBqNaProducaoAsync(producao.Id, "BQ-1"));
        Assert.Null(await query.ContextoDaBqNaProducaoAsync(producao.Id, "BQ-2"));
        Assert.Null(await query.ContextoDaBqNaProducaoAsync(producao.Id + 100, "BQ-1"));
    }

    [Fact]
    public async Task OsPedidos_NaoGravamNada()
    {
        var (producao, _, _, _) = await SeedAsync();
        var query = new BoquilhasAssociacaoQuery(_db);
        _db.ChangeTracker.Clear();

        await query.RegistosDaBqAsync("BQ-1", 1);
        await query.RegistoSemJobOnAsync("BQ-1");
        await query.ProducaoAsync(producao.Id);
        await query.ProducoesDoNumeroAsync("202605", "BQ-1");
        await query.ContextoDaBqNaProducaoAsync(producao.Id, "BQ-1");

        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private static BqMovement Movimento(int traceId) =>
        new()
        {
            BqRepairTraceId = traceId, Type = BqMovementType.Saida, Quantity = 1,
            OccurredAtUtc = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc)
        };
}
