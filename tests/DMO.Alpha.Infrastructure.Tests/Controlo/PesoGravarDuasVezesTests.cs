using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>Audits externos de 16/11/2026: associar o Peso duas vezes (S5 M1) e gravar a produção anterior ao mesmo tempo (S4 F6, DMO-760).</summary>
public sealed class PesoGravarDuasVezesTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly AssociatePesoToControloHandler _handler;

    public PesoGravarDuasVezesTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _handler = new AssociatePesoToControloHandler(_db);
        _db.Tools.AddRange(
            new Tool { ToolId = "cm-a", Type = ToolType.Cm, Reference = "6018", Lot = "66" },
            new Tool { ToolId = "bq-a", Type = ToolType.Bq, Reference = "Y232", Lot = "165" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private async Task<(int JobOnId, int ControloId)> SeedProductionAsync(string productionNumber, string cmToolId)
    {
        var jobOn = new JobOn { Reference = "6018Y232", ProductionNumber = productionNumber, Machine = new Machine { Code = "C2" } };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = cmToolId });
        _db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "bq-a" });
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        return (jobOn.Id, controlo.Id);
    }

    private async Task<int> SeedPesoAsync(string cmToolId)
    {
        var peso = new Peso
        {
            CmToolId = cmToolId, BqToolId = "bq-a", Process = "PS", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 12.3m, VolumePuncaoCm3 = 70m, GlassDensityGramsPerCm3 = 2.4231m,
            CmToolReferenceFrozen = "6018", CmToolLotFrozen = "66"
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    /// <summary>
    /// Garante: associar o mesmo Peso duas vezes deixa um só Peso na produção,
    /// o mesmo peso_id (audit externo S5, 16/11/2026, M1: um handler que
    /// duplicasse a linha passava nos testes).
    /// </summary>
    [Fact]
    public async Task Associar_DuasVezes_FicaUmSoPeso()
    {
        var (_, controloId) = await SeedProductionAsync("202617", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a");
        var antes = await _db.Pesos.CountAsync();

        await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));
        await _handler.HandleAsync(new AssociatePesoToControlo(controloId, pesoId));

        Assert.Equal(antes, await _db.Pesos.CountAsync());
        var daProducao = await _db.Pesos.AsNoTracking().Where(p => p.ControloId == controloId).ToListAsync();
        Assert.Equal(pesoId, Assert.Single(daProducao).Id);
    }

    /// <summary>
    /// Garante: gravar a produção anterior com a versão que a página leu,
    /// depois de outra pessoa já ter gravado, não escreve por cima e diz
    /// «MudouEntretanto»; com a versão certa, grava (DMO-760).
    /// </summary>
    [Fact]
    public async Task ProducaoAnterior_OutraPessoaGravouEntretanto_NaoEscrevePorCima()
    {
        var (_, controloA) = await SeedProductionAsync("202618", "cm-a");
        var (_, controloB) = await SeedProductionAsync("202619", "cm-a");
        var pesoId = await SeedPesoAsync("cm-a");
        var anterior = new PesoProducaoAnteriorQuery(_db);
        var lidaPorAmbos = (await anterior.FindAsync(pesoId))!.Versao;

        Assert.Equal(GuardarProducaoAnteriorResult.Success, await anterior.SaveAsync(pesoId, controloA, 120.5m, versaoLida: lidaPorAmbos));
        Assert.Equal(GuardarProducaoAnteriorResult.MudouEntretanto, await anterior.SaveAsync(pesoId, controloB, 99m, versaoLida: lidaPorAmbos));

        var guardada = (await anterior.FindAsync(pesoId))!;
        Assert.Equal(controloA, guardada.ControloId);
        Assert.Equal(120.5m, guardada.PesoGramas);

        Assert.Equal(GuardarProducaoAnteriorResult.Success, await anterior.SaveAsync(pesoId, controloB, 99m, versaoLida: guardada.Versao));
        Assert.Equal(controloB, (await anterior.FindAsync(pesoId))!.ControloId);
    }
}
