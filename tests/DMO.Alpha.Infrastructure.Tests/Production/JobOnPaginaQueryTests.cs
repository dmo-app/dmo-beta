using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// The lookups the Job On page used to run against the DbContext itself:
/// same answers as before, and nothing is written.
/// </summary>
public sealed class JobOnPaginaQueryTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly JobOnPaginaQuery _query;

    public JobOnPaginaQueryTests() => _query = new JobOnPaginaQuery(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ProducaoDoHistorico_EscolheAMaisRecenteComEsseNumero()
    {
        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        _db.JobOns.AddRange(
            new JobOn { Machine = b1, Reference = "5447T173", ProductionNumber = "202601" },
            new JobOn { Machine = c3, Reference = "5447T173", ProductionNumber = "202601" },
            new JobOn { Machine = b1, Reference = "5447T173", ProductionNumber = "202602" });
        await _db.SaveChangesAsync();

        var qualquer = await _query.ProducaoDoHistoricoAsync("5447T173", "202601", null, null);
        var daB1 = await _query.ProducaoDoHistoricoAsync("5447T173", "202601", null, b1.Id);
        var outraReferencia = await _query.ProducaoDoHistoricoAsync("5447T173", "202601", "6018Y232", null);
        var inexistente = await _query.ProducaoDoHistoricoAsync("5447T173", "999999", null, null);

        Assert.Equal(new JobOnProductionOption("202601", "5447T173"), qualquer);
        Assert.Equal(new JobOnProductionOption("202601", "5447T173"), daB1);
        Assert.Null(outraReferencia);
        Assert.Null(inexistente);
        Assert.NotNull(await _query.ProducaoDoHistoricoAsync("5447T173", "202601", null, c3.Id));
    }

    [Fact]
    public async Task ProducaoDoHistorico_ComVariasReferenciasDoMesmoInicio_EscolheAMaisRecente()
    {
        var machine = new Machine { Code = "B1" };
        _db.JobOns.AddRange(
            new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "202601" },
            new JobOn { Machine = machine, Reference = "5447T200", ProductionNumber = "202601" });
        await _db.SaveChangesAsync();

        var lida = await _query.ProducaoDoHistoricoAsync("5447", "202601", null, null);

        Assert.Equal(new JobOnProductionOption("202601", "5447T200"), lida);
    }

    [Fact]
    public async Task AvisoDeSobreposicao_ProducaoInexistenteNaoDaAviso()
    {
        Assert.Null(await _query.AvisoDeSobreposicaoAsync(12345, "B1", null, null));
    }

    [Fact]
    public async Task AvisoDeSobreposicao_EntreProducoesDaMesmaLinhaNaoAvisa()
    {
        var machine = new Machine { Code = "B1" };
        var producao = new JobOn
        {
            Machine = machine, Reference = "5447T173", ProductionNumber = "202601",
            InicioEm = new DateOnly(2026, 10, 1), FimEm = new DateOnly(2026, 10, 10)
        };
        _db.JobOns.AddRange(producao, new JobOn
        {
            Machine = machine, Reference = "5447T173", ProductionNumber = "202602",
            InicioEm = new DateOnly(2026, 10, 5), FimEm = new DateOnly(2026, 10, 15)
        });
        await _db.SaveChangesAsync();

        // Diogo, 25/10/2026: «so se aplica as bqs»; a janela cruza a de outra
        // produção da mesma linha e mesmo assim não há aviso.
        Assert.Null(await _query.AvisoDeSobreposicaoAsync(producao.Id, "B1", producao.InicioEm, producao.FimEm));
    }

    [Fact]
    public async Task ReferenciasDeMf_SoParaAFerramentaDoTipoCerto()
    {
        _db.Tools.AddRange(
            new Tool { ToolId = "MF1", Type = ToolType.Mf, Reference = "8447", Lot = "1" },
            new Tool { ToolId = "CM1", Type = ToolType.Cm, Reference = "5448", Lot = "1" },
            new Tool { ToolId = "CM2", Type = ToolType.Cm, Reference = "5449", Lot = "1", AssociatedMfReference = "8450" });
        await _db.SaveChangesAsync();

        Assert.Equal("8447", await _query.ReferenciaDaMfAsync("MF1"));
        Assert.Null(await _query.ReferenciaDaMfAsync("CM1"));
        Assert.Null(await _query.ReferenciaDaMfAsync("NAO-EXISTE"));
        Assert.Equal("5448", await _query.ReferenciaMfDaCmAsync("CM1"));
        Assert.Equal("8450", await _query.ReferenciaMfDaCmAsync("CM2"));
        Assert.Null(await _query.ReferenciaMfDaCmAsync("MF1"));
    }

    [Fact]
    public async Task ProducaoComReferenciaENumero_DevolveFerramentasEDatas()
    {
        var machine = new Machine { Code = "B1" };
        var producao = new JobOn
        {
            Machine = machine, Reference = "5447T173", ProductionNumber = "202601", CaloteMm = 9m,
            InicioEm = new DateOnly(2026, 10, 1), FimEm = new DateOnly(2026, 10, 10)
        };
        _db.Tools.AddRange(
            new Tool { ToolId = "CM", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = "BQ", Type = ToolType.Bq, Reference = "T173", Lot = "3" });
        _db.JobOns.Add(producao);
        _db.CmContexts.Add(new CmContext { JobOn = producao, ToolId = "CM" });
        _db.BqContexts.Add(new BqContext { JobOn = producao, ToolId = "BQ" });
        await _db.SaveChangesAsync();

        var lida = await _query.ProducaoComReferenciaENumeroAsync("5447T173", "202601");

        Assert.Equal(new JobOnExistente(
            producao.Id, machine.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10), 9m, "CM", null, "BQ"), lida);
        Assert.Null(await _query.ProducaoComReferenciaENumeroAsync("5447T173", "999999"));
        Assert.Null(await _query.ProducaoComReferenciaENumeroAsync("5447t173", "202601"));

        _db.JobOns.Add(new JobOn { Machine = machine, Reference = "abc1", ProductionNumber = "7" });
        await _db.SaveChangesAsync();
        Assert.NotNull(await _query.ProducaoComReferenciaENumeroAsync("ABC1", "7"));
    }

    [Fact]
    public async Task CaloteECodigoDaMaquina_LeemOGuardadoEDevolvemNuloSeNaoExiste()
    {
        var machine = new Machine { Code = "B1" };
        var comCalote = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "1", CaloteMm = 9.5m };
        var semCalote = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "2" };
        _db.JobOns.AddRange(comCalote, semCalote);
        await _db.SaveChangesAsync();

        Assert.Equal(9.5m, await _query.CaloteGuardadaAsync(comCalote.Id));
        Assert.Null(await _query.CaloteGuardadaAsync(semCalote.Id));
        Assert.Null(await _query.CaloteGuardadaAsync(-1));
        Assert.Equal("B1", await _query.CodigoDaMaquinaAsync(machine.Id));
        Assert.Null(await _query.CodigoDaMaquinaAsync(-1));
    }

    [Fact]
    public async Task OsPedidos_NaoGravamNada()
    {
        var machine = new Machine { Code = "B1" };
        var producao = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "202601" };
        _db.JobOns.Add(producao);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _query.ProducaoDoHistoricoAsync("5447T173", "202601", null, null);
        await _query.AvisoDeSobreposicaoAsync(producao.Id, "B1", null, null);
        await _query.ReferenciaDaMfAsync("MF1");
        await _query.ReferenciaMfDaCmAsync("CM1");
        await _query.ProducaoComReferenciaENumeroAsync("5447T173", "202601");
        await _query.CaloteGuardadaAsync(producao.Id);
        await _query.CodigoDaMaquinaAsync(machine.Id);

        Assert.Empty(_db.ChangeTracker.Entries());
    }
}
