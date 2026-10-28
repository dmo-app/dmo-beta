using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O «Comparar peso» com mais de 500 Pesos da mesma referência (relatório do
/// dmo-Improvements de 28/10/2026, S06): o Peso de onde se parte fica sempre
/// do lado atual, e uma CM desta máquina não desaparece por os 500 mais
/// recentes serem de outra máquina.
/// </summary>
public sealed class PesoCompararLimiteTests : IDisposable
{
    private static readonly DateTime Dia = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly PesoCompararQuery _query;

    public PesoCompararLimiteTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoCompararQuery(_db, new PesoQuery(_db));
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task OPesoDeOndeSeParte_ForaDos500MaisRecentes_FicaDoLadoAtual()
    {
        var m21 = await MaquinaAsync("M21");
        await CmAsync("CM-5447", "ST100");
        await CmAsync("CM-5448", "ST100");
        var antigo = (await PesosAsync("CM-5447", m21, 1, minutos: 0))[0];
        await PesosAsync("CM-5448", m21, PesoCompararQuery.MaxPesos, minutos: 10);

        var r = await _query.ListarAsync(new PesoCompararPedido(antigo));

        Assert.Equal(antigo, r.PesoBaseId);
        Assert.Equal(antigo, r.Linhas[0].PesoId);
        Assert.Equal(["CM-5447", "CM-5448"], r.Linhas.Select(l => l.CmToolId));
    }

    [Fact]
    public async Task Os500MaisRecentesNoutraMaquina_ACmDestaMaquinaContinuaACompararSe()
    {
        var m21 = await MaquinaAsync("M21");
        var m23 = await MaquinaAsync("M23");
        await CmAsync("CM-5447", "ST100");
        await CmAsync("CM-5448", "ST100");
        await CmAsync("CM-5512", "ST100");
        var outraCm = await PesosAsync("CM-5448", m21, 2, minutos: 0);
        await PesosAsync("CM-5512", m23, PesoCompararQuery.MaxPesos, minutos: 10);
        var este = (await PesosAsync("CM-5447", m21, 1, minutos: 100_000))[0];

        var r = await _query.ListarAsync(new PesoCompararPedido(este));

        Assert.Equal("M21", r.Maquina);
        Assert.Equal([este, outraCm[1]], r.Linhas.Select(l => l.PesoId));
    }

    private async Task<Machine> MaquinaAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task CmAsync(string toolId, string reference)
    {
        _db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = reference, Lot = toolId[^4..] });
        await _db.SaveChangesAsync();
    }

    // Pesos da CM na máquina, um por minuto a partir do minuto dado, cada um
    // com o seu Job On; devolve os ids pela ordem em que foram registados.
    private async Task<List<int>> PesosAsync(string cmToolId, Machine machine, int quantos, int minutos)
    {
        var pesos = new List<Peso>();
        for (var i = 0; i < quantos; i++)
        {
            var controlo = new DMO.Alpha.Core.Controlo.Controlo
            {
                JobOn = new JobOn { Reference = "1075C142", ProductionNumber = $"{cmToolId}-{minutos + i}", MachineId = machine.Id }
            };
            var peso = new Peso
            {
                CmToolId = cmToolId, BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.5m,
                Controlo = controlo, RegistadoEmUtc = Dia.AddMinutes(minutos + i)
            };
            _db.Pesos.Add(peso);
            pesos.Add(peso);
        }

        await _db.SaveChangesAsync();
        return pesos.Select(p => p.Id).ToList();
    }
}
