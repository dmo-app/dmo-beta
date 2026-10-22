using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Edições ao mesmo tempo (varredura de 18/10/2026, B4): a ficha envia a
/// versão que abriu; uma versão diferente da gravada é recusada sem gravar
/// nada. No InMemory a versão fica sempre 0 (o xmin só existe no PostgreSQL):
/// aqui prova-se a recusa e que nada muda; o PostgreSQL prova a versão real
/// (PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo).
/// </summary>
public sealed class EdicoesAoMesmoTempoTests : IDisposable
{
    private readonly DmoDbContext _db;

    public EdicoesAoMesmoTempoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task<(int JobOnId, int MachineId)> SeedJobOnAsync()
    {
        var b1 = new Machine { Code = "B1" };
        _db.Machines.Add(b1);
        _db.Tools.Add(new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();
        var created = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202603", "5810", b1.Id, MfToolId: "MF-1", FimEm: new DateOnly(2026, 3, 9)));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        return (created.JobOn!.JobOnId, b1.Id);
    }

    [Fact]
    public async Task JobOn_OutraVersao_Recusa_SemGravarNada()
    {
        var (jobOnId, machineId) = await SeedJobOnAsync();

        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(
            jobOnId, machineId, MfToolId: "MF-1", FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: 7));

        Assert.Equal(UpdateJobOnResult.ChangedByAnotherUser, outcome.Result);
        _db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2026, 3, 9), (await _db.JobOns.SingleAsync()).FimEm);
        Assert.Empty(await _db.JobOnChanges.ToListAsync());
    }

    [Fact]
    public async Task JobOn_MesmaVersao_Grava()
    {
        var (jobOnId, machineId) = await SeedJobOnAsync();
        var version = (await new ProductionContextQuery(_db).GetAsync(jobOnId))!.Version;

        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(
            jobOnId, machineId, MfToolId: "MF-1", FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: version));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2026, 3, 20), (await _db.JobOns.SingleAsync()).FimEm);
    }

    [Fact]
    public async Task JobOn_OMesmoEnvioOutraVez_AbreOGravado_SemOutraLinha()
    {
        // Duplo clique em «Guardar alterações» (sweep do Codex de 19/10/2026,
        // J-1): o segundo envio leva a versão de antes da primeira gravação,
        // mas o que chega é o que ficou gravado. Não foi outra pessoa.
        var (jobOnId, machineId) = await SeedJobOnAsync();
        _db.Tools.Add(new Tool { ToolId = "BQ-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        await _db.SaveChangesAsync();
        var envio = new UpdateJobOn(
            jobOnId, machineId, MfToolId: "MF-1", BqToolId: "BQ-1", FimEm: new DateOnly(2026, 3, 20), ExpectedVersion: 0);
        Assert.Equal(UpdateJobOnResult.Success, (await new UpdateJobOnHandler(_db).HandleAsync(envio)).Result);

        // No InMemory a versão fica sempre 0: a de antes é outra qualquer.
        var repetido = await new UpdateJobOnHandler(_db).HandleAsync(envio with { ExpectedVersion = 7 });

        Assert.Equal(UpdateJobOnResult.Success, repetido.Result);
        Assert.Equal(jobOnId, repetido.JobOn!.JobOnId);
        _db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2026, 3, 20), (await _db.JobOns.SingleAsync()).FimEm);
        Assert.Equal("BQ-1", (await _db.BqContexts.SingleAsync()).ToolId);
        var linhas = await _db.JobOnChanges.Select(c => c.Field).ToListAsync();
        // Pôr a BQ num Job On sem BQ não é mudança de boquilhas (Diogo, 22/10/2026): só o fim mudou.
        Assert.Equal(new[] { JobOnChangeFields.End }, linhas);
    }

    [Fact]
    public async Task Ferramenta_OutraVersao_Recusa_FactosETecnica()
    {
        _db.Tools.Add(new Tool { ToolId = "CM-1", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        await _db.SaveChangesAsync();

        var facts = await new EditToolFactsHandler(_db).HandleAsync(
            new EditToolFacts("CM-1", "CM", "5447", "3", ExpectedVersion: 7));
        var technical = await new EditToolTechnicalValuesHandler(_db).HandleAsync(
            new EditToolTechnicalValues("CM-1", NominalWeightGrams: 250m, ExpectedVersion: 7));

        Assert.Equal(EditToolFactsResult.ChangedByAnotherUser, facts.Result);
        Assert.Equal(EditToolTechnicalValuesResult.ChangedByAnotherUser, technical.Result);
        _db.ChangeTracker.Clear();
        Assert.Equal("2", (await _db.Tools.SingleAsync()).Lot);
        Assert.Empty(await _db.ToolTechnicalContexts.ToListAsync());
    }

    [Fact]
    public async Task Ferramenta_SemLinhaTecnica_AVersaoAbertaE0()
    {
        _db.Tools.Add(new Tool { ToolId = "CM-1", Type = ToolType.Cm, Reference = "5447", Lot = "2" });
        await _db.SaveChangesAsync();
        var sheet = (await new ToolSheetQuery(_db).FindByToolIdAsync("CM-1"))!;

        var technical = await new EditToolTechnicalValuesHandler(_db).HandleAsync(
            new EditToolTechnicalValues("CM-1", NominalWeightGrams: 250m, ExpectedVersion: sheet.TechnicalVersion));
        var facts = await new EditToolFactsHandler(_db).HandleAsync(
            new EditToolFacts("CM-1", "CM", "5447", "3", ExpectedVersion: sheet.Version));

        Assert.Equal(0u, sheet.TechnicalVersion);
        Assert.Equal(EditToolTechnicalValuesResult.Success, technical.Result);
        Assert.Equal(EditToolFactsResult.Success, facts.Result);
    }
}
