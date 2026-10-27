using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O «Comparar peso» (Diogo, 27/10/2026: «escolher CM compatíveis: mesma
/// referência e máquina»): o Peso mais recente de cada CM da mesma referência
/// e da mesma máquina, com o tampão com que se pesou.
/// </summary>
public sealed class PesoCompararQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoCompararQuery _query;
    private static readonly DateTime Dia = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    public PesoCompararQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoCompararQuery(_db, new PesoQuery(_db));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Machine> MaquinaAsync(string code)
    {
        var machine = new Machine { Code = code };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task CmAsync(string toolId, string reference, string lot, decimal? diametro = null, decimal? calote = null)
    {
        _db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Cm, Reference = reference, Lot = lot });
        if (diametro is not null || calote is not null)
        {
            _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = toolId, TpDiametroMm = diametro, TpSagittaMm = calote });
        }

        await _db.SaveChangesAsync();
    }

    private async Task<int> PesoAsync(
        string cmToolId,
        Machine? machine,
        string producao,
        int dias,
        decimal agua = 200m,
        decimal? diametro = 24.95m,
        decimal? calote = 4.5m,
        decimal? jobOnDiametro = null,
        decimal? jobOnCalote = null)
    {
        int? controloId = null;
        if (machine is not null)
        {
            var jobOn = new JobOn
            {
                Reference = "1075C142",
                ProductionNumber = producao,
                MachineId = machine.Id,
                TampaoDiametroMm = jobOnDiametro,
                CaloteMm = jobOnCalote
            };
            _db.JobOns.Add(jobOn);
            await _db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            _db.Controlos.Add(controlo);
            await _db.SaveChangesAsync();
            controloId = controlo.Id;
        }

        var peso = new Peso
        {
            CmToolId = cmToolId,
            BqToolId = "BQ-1",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.5m,
            TampaoDiametroMm = diametro,
            TampaoCaloteMm = calote,
            ControloId = controloId,
            RegistadoEmUtc = Dia.AddDays(dias)
        };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();
        _db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = agua });
        await _db.SaveChangesAsync();
        return peso.Id;
    }

    [Fact]
    public async Task DoPeso_SoAMesmaReferenciaEMaquina_OMaisRecenteDeCadaCm()
    {
        var m21 = await MaquinaAsync("M21");
        var m23 = await MaquinaAsync("M23");
        await CmAsync("CM-5447", "ST100", "5447");
        await CmAsync("CM-5448", "ST100", "5448");
        await CmAsync("CM-5512", "ST100", "5512");
        await CmAsync("CM-9000", "ST200", "9000");
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();

        var antigo = await PesoAsync("CM-5447", m21, "202601", 0);
        var este = await PesoAsync("CM-5447", m21, "202604", 3);
        var outraCm = await PesoAsync("CM-5448", m21, "202605", 2);
        var outraMaquina = await PesoAsync("CM-5512", m23, "202610", 4);
        var outraReferencia = await PesoAsync("CM-9000", m21, "202611", 5);

        var r = await _query.ListarAsync(new PesoCompararPedido(este));

        Assert.Equal("ST100", r.CmReferencia);
        Assert.Equal("M21", r.Maquina);
        Assert.Equal(["M21", "M23"], r.Maquinas);
        Assert.Equal([este, outraCm], r.Linhas.Select(l => l.PesoId));
        Assert.DoesNotContain(r.Linhas, l => l.PesoId == antigo || l.PesoId == outraMaquina || l.PesoId == outraReferencia);
        var linha = r.Linhas[0];
        Assert.Equal("5447", linha.CmLote);
        Assert.Equal("202604", linha.Producao);
        Assert.Equal(new PesoTampao(24.95m, 4.5m), linha.Tampao);
        Assert.Equal(1, linha.Leituras);
        Assert.NotNull(linha.PesoMedioGrams);
    }

    [Fact]
    public async Task DoPesoAntigo_FicaOPedidoNaCmDele()
    {
        var m21 = await MaquinaAsync("M21");
        await CmAsync("CM-5447", "ST100", "5447");
        var antigo = await PesoAsync("CM-5447", m21, "202601", 0);
        await PesoAsync("CM-5447", m21, "202604", 3);

        var r = await _query.ListarAsync(new PesoCompararPedido(antigo));

        Assert.Equal([antigo], r.Linhas.Select(l => l.PesoId));
    }

    [Fact]
    public async Task OutraMaquinaEscolhida_TrocaAsCm()
    {
        var m21 = await MaquinaAsync("M21");
        var m23 = await MaquinaAsync("M23");
        await CmAsync("CM-5447", "ST100", "5447");
        await CmAsync("CM-5512", "ST100", "5512");
        var este = await PesoAsync("CM-5447", m21, "202604", 3);
        var naM23 = await PesoAsync("CM-5512", m23, "202610", 4);

        var r = await _query.ListarAsync(new PesoCompararPedido(este, Maquina: "m23"));

        Assert.Equal("M23", r.Maquina);
        // O Peso de onde se parte fica sempre, para comparar com ele.
        Assert.Equal([este, naM23], r.Linhas.Select(l => l.PesoId));
    }

    [Fact]
    public async Task DoHistorico_PeloInicioDaReferencia_SemJobOnValemAsMaquinasDaFicha()
    {
        var m21 = await MaquinaAsync("M21");
        await CmAsync("CM-5447", "ST100", "5447");
        await CmAsync("CM-5530", "ST100", "5530", diametro: 26m, calote: 4m);
        _db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "CM-5530", MachineId = m21.Id });
        await _db.SaveChangesAsync();
        var comJobOn = await PesoAsync("CM-5447", m21, "202604", 3);
        var semJobOn = await PesoAsync("CM-5530", null, "-", 5, diametro: null, calote: null);

        var r = await _query.ListarAsync(new PesoCompararPedido(CmReferencia: "st1"));

        Assert.Equal("ST100", r.CmReferencia);
        Assert.Equal("M21", r.Maquina);
        Assert.Equal([semJobOn, comJobOn], r.Linhas.Select(l => l.PesoId));
        var linha = r.Linhas[0];
        Assert.Null(linha.Producao);
        Assert.Equal("M21", linha.Maquina);
        // Sem tampão no Peso nem Job On, vale o da ficha da CM.
        Assert.Equal(new PesoTampao(26m, 4m), linha.Tampao);
    }

    [Fact]
    public async Task DoHistorico_SemMaquina_FicaADoPesoMaisRecente()
    {
        var m21 = await MaquinaAsync("M21");
        var m23 = await MaquinaAsync("M23");
        await CmAsync("CM-5447", "ST100", "5447");
        await CmAsync("CM-5512", "ST100", "5512");
        await PesoAsync("CM-5447", m21, "202604", 3);
        var maisRecente = await PesoAsync("CM-5512", m23, "202610", 4);

        var r = await _query.ListarAsync(new PesoCompararPedido(CmReferencia: "ST100"));

        Assert.Equal("M23", r.Maquina);
        Assert.Equal([maisRecente], r.Linhas.Select(l => l.PesoId));
    }

    [Fact]
    public async Task SemTampaoNoPeso_ValeODoJobOn()
    {
        var m21 = await MaquinaAsync("M21");
        await CmAsync("CM-5447", "ST100", "5447", diametro: 30m, calote: 3m);
        var peso = await PesoAsync("CM-5447", m21, "202604", 3, diametro: null, calote: null, jobOnDiametro: 25m, jobOnCalote: 5m);

        var r = await _query.ListarAsync(new PesoCompararPedido(peso));

        Assert.Equal(new PesoTampao(25m, 5m), r.Linhas.Single().Tampao);
    }

    [Fact]
    public async Task VariasReferencias_PedeParaEscolher()
    {
        await CmAsync("CM-1", "ST100", "1");
        await CmAsync("CM-2", "ST105", "2");
        await PesoAsync("CM-1", null, "-", 0);
        await PesoAsync("CM-2", null, "-", 1);

        var r = await _query.ListarAsync(new PesoCompararPedido(CmReferencia: "ST1"));

        Assert.Null(r.CmReferencia);
        Assert.Equal(["ST100", "ST105"], r.Referencias);
        Assert.Empty(r.Linhas);

        var exata = await _query.ListarAsync(new PesoCompararPedido(CmReferencia: "st100"));
        Assert.Equal("ST100", exata.CmReferencia);
    }
}
