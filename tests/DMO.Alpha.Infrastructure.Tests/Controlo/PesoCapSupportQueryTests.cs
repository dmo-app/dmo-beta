using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a resolução do apoio TP/calote: a geometria TP é FACTO DA TOOL
/// (ToolTechnicalContext — D, h_ref), a calote efetiva é a do
/// JOB ON quando existe (o html: <c>ctx.production?.calote</c>), senão a
/// h_ref da Tool («Tool CM»), e a simulação sobrepõe-se APENAS na exibição
/// (nunca gravada). A Tool sem TP é o estado «Referência por completar» —
/// o apoio não existe e NADA bloqueia.
/// </summary>
public sealed class PesoCapSupportQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly PesoCapSupportQuery _query;

    public PesoCapSupportQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new PesoCapSupportQuery(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// O contexto canónico: a máquina, a CM T3 (5447 · lote 3) com a
    /// geometria TP de referência (D 50 · R desenho 24,11 · h_ref 10 — os
    /// valores da folha, registados tal como vêm), a produção e o
    /// controlo_id; a calote do Job On é opcional (null por defeito).
    /// </summary>
    private async Task<(int ControloId, string CmToolId)> SeedProducaoAsync(decimal? caloteMm = null)
    {
        var machine = new Machine { Code = "B1" };
        var cmTool = new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" };
        _db.Machines.Add(machine);
        _db.Tools.Add(cmTool);
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            Process = CmProcess.Nnpb,
            TpDiametroMm = 50m,
            TpRaioMm = 24.11m,
            TpSagittaMm = 10m
        });

        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = $"PN-{Guid.NewGuid():N}"[..12],
            Machine = machine,
            CaloteMm = caloteMm
        };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T3" });
        await _db.SaveChangesAsync();

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();

        return (controlo.Id, "T3");
    }

    [Fact]
    public async Task GetAsync_ProducaoComCalote_OrigemJobOn()
    {
        // A calote efetiva é a DO JOB ON (9 mm): o html usa
        // ctx.production.calote. ΔR = 39,2222… − 36,25 = +2,9722…;
        // ΔV = 9,2174… − 10,34107… = −1,1236….
        var (controloId, cmToolId) = await SeedProducaoAsync(caloteMm: 9m);

        var readModel = await _query.GetAsync(new PesoCapSupportRequest(cmToolId, controloId));

        Assert.True(readModel.TemReferencia);
        Assert.Null(readModel.EstadoMensagem);
        Assert.Equal("Job On", readModel.OrigemCalote);
        Assert.Equal(50m, readModel.DiametroMm);
        Assert.Equal(10m, readModel.AlturaReferenciaMm);
        Assert.Equal(36.25m, readModel.RaioReferenciaMm);
        Assert.Equal(9m, readModel.AlturaEfetivaMm);
        Assert.InRange(readModel.RaioCalculadoMm!.Value, 39.2222m, 39.2223m);
        Assert.InRange(readModel.DeltaRaioMm!.Value, 2.9721m, 2.9723m);
        Assert.InRange(readModel.VolumeEfetivoCm3!.Value, 9.2170m, 9.2180m);
        Assert.InRange(readModel.DeltaVolumeCm3!.Value, -1.124m, -1.123m);
    }

    [Fact]
    public async Task GetAsync_ProducaoSemCalote_CaiNaReferenciaDaTool()
    {
        // Job On sem calote (null por defeito — a migração aditiva): a
        // calote efetiva é a h_ref da Tool («Tool CM») e ΔR/ΔV são zero.
        var (controloId, cmToolId) = await SeedProducaoAsync(caloteMm: null);

        var readModel = await _query.GetAsync(new PesoCapSupportRequest(cmToolId, controloId));

        Assert.True(readModel.TemReferencia);
        Assert.Equal("Tool CM", readModel.OrigemCalote);
        Assert.Equal(10m, readModel.AlturaEfetivaMm);
        Assert.Equal(36.25m, readModel.RaioCalculadoMm);
        Assert.Equal(0m, readModel.DeltaRaioMm);
        Assert.Equal(0m, readModel.DeltaVolumeCm3);
    }

    [Fact]
    public async Task GetAsync_Simulacao_SobrepoeApenasNaExibicao()
    {
        // A calote simulada (o campo do diálogo do html) sobrepõe-se na
        // exibição — o Job On NUNCA é alterado (zero escrita na query).
        var (controloId, cmToolId) = await SeedProducaoAsync(caloteMm: 9m);

        var readModel = await _query.GetAsync(new PesoCapSupportRequest(cmToolId, controloId, 8m));

        Assert.True(readModel.TemReferencia);
        Assert.Equal("Simulação", readModel.OrigemCalote);
        Assert.Equal(8m, readModel.AlturaEfetivaMm);
        Assert.InRange(readModel.RaioCalculadoMm!.Value, 43.0624m, 43.0626m); // (625+64)/16 = 43,0625

        var jobOn = await _db.JobOns.AsNoTracking().SingleAsync();
        Assert.Equal(9m, jobOn.CaloteMm); // o facto do Job On ficou intacto
    }

    [Fact]
    public async Task GetAsync_CaloteZero_CalotePlanaHonesta()
    {
        // Calote 0 (calote plana): R calculado indefinido (null), volume 0 —
        // os factos honestos, nunca zeros inventados no R.
        var (controloId, cmToolId) = await SeedProducaoAsync(caloteMm: 0m);

        var readModel = await _query.GetAsync(new PesoCapSupportRequest(cmToolId, controloId));

        Assert.True(readModel.TemReferencia);
        Assert.Equal("Job On", readModel.OrigemCalote);
        Assert.Equal(0m, readModel.AlturaEfetivaMm);
        Assert.Null(readModel.RaioCalculadoMm);
        Assert.Equal(0m, readModel.VolumeEfetivoCm3);
        Assert.Null(readModel.DeltaRaioMm);
    }

    [Fact]
    public async Task GetAsync_CaloteNegativaNoJobOn_SemApoioSemBloqueio()
    {
        // Um valor impossível no Job On (nunca deveria existir): sem apoio
        // (o estado honesto), nunca um valor inventado nem uma exceção.
        var (controloId, cmToolId) = await SeedProducaoAsync(caloteMm: -1m);

        var readModel = await _query.GetAsync(new PesoCapSupportRequest(cmToolId, controloId));

        Assert.False(readModel.TemReferencia);
        Assert.Contains("O Peso pode continuar sem este apoio", readModel.EstadoMensagem);
    }

    [Fact]
    public async Task GetAsync_ToolSemTP_ReferenciaPorCompletar()
    {
        // A CM sem a geometria TP registada: o apoio mostra o estado
        // «Referência por completar» — o Peso pode continuar sem ele.
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T4", Process = CmProcess.Nnpb });
        var jobOn = new JobOn { Reference = "1075C142", ProductionNumber = "PN-SEM-TP", Machine = machine };
        _db.JobOns.Add(jobOn);
        _db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T4" });
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();

        var readModel = await _query.GetAsync(new PesoCapSupportRequest("T4", controlo.Id));

        Assert.False(readModel.TemReferencia);
        Assert.Contains("Complete o diâmetro e a calote do tampão na Folha do Job On (bloco TP)", readModel.EstadoMensagem);
        Assert.Null(readModel.RaioCalculadoMm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("T-UNKNOWN")]
    public async Task GetAsync_ToolDesconhecidaOuVazia_SemApoioSemBloqueio(string? toolId)
    {
        var readModel = await _query.GetAsync(new PesoCapSupportRequest(toolId!));

        Assert.False(readModel.TemReferencia);
        Assert.Contains("O Peso pode continuar sem este apoio", readModel.EstadoMensagem);
    }

    [Fact]
    public async Task GetAsync_SemControlo_UsaAReferenciaDaTool()
    {
        // Pesagem antes da produção (sem controlo/Job On): a calote efetiva
        // é a h_ref da Tool — o fallback do html («Tool CM»). Sem raio do
        // desenho: D e h_ref chegam (o raio deixou de ser pedido).
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.ToolTechnicalContexts.Add(new ToolTechnicalContext
        {
            ToolId = "T3",
            TpDiametroMm = 50m,
            TpSagittaMm = 10m
        });
        await _db.SaveChangesAsync();

        var readModel = await _query.GetAsync(new PesoCapSupportRequest("T3"));

        Assert.True(readModel.TemReferencia);
        Assert.Equal("Tool CM", readModel.OrigemCalote);
        Assert.Equal(10m, readModel.AlturaEfetivaMm);
        Assert.Equal(36.25m, readModel.RaioReferenciaMm);
    }
}
