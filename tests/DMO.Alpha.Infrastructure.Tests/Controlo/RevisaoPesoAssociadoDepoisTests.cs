using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O card da Revisão segue a regra PESO-B03 (audit externo, 16/11/2026,
/// DMO-762): um Peso pré-produção registado antes da aprovação mas associado
/// depois dela conta como novo, tal como na lista, no Resumo e nos PDFs.
/// </summary>
public sealed class RevisaoPesoAssociadoDepoisTests : IDisposable
{
    private static readonly DateTime Antes = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _handler;

    public RevisaoPesoAssociadoDepoisTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.SaveChanges();
        _handler = new ControloApprovalHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Garante: o último registo novo do Peso (o que o card da Revisão compara com a decisão) inclui a associação feita depois da aprovação.</summary>
    [Fact]
    public async Task PesoAssociadoDepoisDaAprovacao_ContaComoRegistoNovo()
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = "2026301", MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        _db.Pesos.Add(NovoPeso("T3", controlo.Id, Antes, associadoEm: null));
        await _db.SaveChangesAsync();
        await _handler.DecidePartAsync(controlo.Id, ControloParte.Peso, aprovado: true, "4802", null);
        var decisao = (await _handler.PartsAsync(controlo.Id))[0].EmUtc;

        // Um Peso pré-produção, registado antes, só associado depois da aprovação.
        _db.Pesos.Add(NovoPeso("T4", controlo.Id, Antes, associadoEm: decisao.AddMinutes(5)));
        await _db.SaveChangesAsync();

        var registos = await _handler.RegistosAsync(controlo.Id);
        Assert.True(registos[ControloParte.Peso].UltimoRegistoNovo > decisao);
    }

    private static Peso NovoPeso(string cm, int controloId, DateTime registadoEm, DateTime? associadoEm) => new()
    {
        CmToolId = cm, BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
        VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
        ControloId = controloId, RegistadoEmUtc = registadoEm, AssociadoAoControloEmUtc = associadoEm
    };
}
