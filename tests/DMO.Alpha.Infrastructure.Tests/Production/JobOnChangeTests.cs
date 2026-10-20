using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O que mudou num Job On (o proprietário, 17/10/2026): cada edição grava uma
/// linha por campo alterado, com o valor antes e depois como se lê na ficha,
/// quem e quando; uma edição sem mudanças e uma recusa não gravam nada.
/// </summary>
public sealed class JobOnChangeTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly CreateJobOnHandler _create;
    private readonly UpdateJobOnHandler _update;
    private readonly JobOnChangeQuery _query;

    public JobOnChangeTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _create = new CreateJobOnHandler(_db);
        _update = new UpdateJobOnHandler(_db);
        _query = new JobOnChangeQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(int JobOnId, int B1, int B2)> SeedAsync()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        _db.Machines.AddRange(b1, b2);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM-1", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = "CM-2", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "5810", Lot = "1" },
            new Tool { ToolId = "BQ-6", Type = ToolType.Bq, Reference = "T194", Lot = "6" },
            new Tool { ToolId = "BQ-7", Type = ToolType.Bq, Reference = "T194", Lot = "7" });
        await _db.SaveChangesAsync();

        var created = await _create.HandleAsync(new CreateJobOn(
            "202603", "5810", b1.Id, "CM-1", "MF-1", "BQ-6",
            InicioEm: new DateOnly(2026, 3, 2), FimEm: new DateOnly(2026, 3, 9), CaloteMm: 4.5m));
        Assert.Equal(CreateJobOnResult.Success, created.Result);
        return (created.JobOn!.JobOnId, b1.Id, b2.Id);
    }

    [Fact]
    public async Task Update_RecordsOneLinePerChangedField_WithHumanValuesAndWho()
    {
        var (jobOnId, _, b2) = await SeedAsync();

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, b2, "CM-2", "MF-1", "BQ-7",
            InicioEm: new DateOnly(2026, 3, 2), FimEm: new DateOnly(2026, 3, 12), CaloteMm: null,
            ChangedBy: "Diogo"));
        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);

        var rows = await _db.JobOnChanges.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(
            new (string, string?, string?)[]
            {
                (JobOnChangeFields.Machine, "B1", "B2"),
                (JobOnChangeFields.End, "09/03/2026", "12/03/2026"),
                (JobOnChangeFields.Calote, "4.5 mm", "Usar referência da Tool"),
                (JobOnChangeFields.Cm, "5447 · lote 2", "5447 · lote 3"),
                (JobOnChangeFields.Bq, "T194 · lote 6", "T194 · lote 7")
            },
            rows.Select(r => (r.Field, r.OldValueFrozen, r.NewValueFrozen)).ToArray());
        Assert.All(rows, r => Assert.Equal("Diogo", r.ChangedByNameFrozen));
        Assert.All(rows, r => Assert.Equal(jobOnId, r.JobOnId));
        Assert.Single(rows.Select(r => r.ChangedAtUtc).Distinct());

        var views = await _query.ForJobOnAsync(jobOnId);
        Assert.Contains(views, v => v.Text == "Mudou a BQ: T194 · lote 6 → T194 · lote 7");
        Assert.Contains(views, v => v.Text == "Mudou a máquina: B1 → B2");

        var porProducao = await _query.AfterStartAsync(new[] { jobOnId, jobOnId + 99 });
        Assert.Equal(5, porProducao[jobOnId].Count);
        Assert.False(porProducao.ContainsKey(jobOnId + 99));
        Assert.Equal(
            "Mudança Máquina · Sai B1 entra B2; Fim · Sai 09/03/2026 entra 12/03/2026; Calote · Sai 4.5 mm entra Usar referência da Tool; CM · Sai 2 entra 3; BQ · Sai 6 entra 7",
            JobOnChangeFields.Summarize(porProducao[jobOnId]));
    }

    [Fact]
    public async Task Update_WithNothingChanged_RecordsNothing()
    {
        var (jobOnId, b1, _) = await SeedAsync();

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, b1, "CM-1", "MF-1", "BQ-6",
            InicioEm: new DateOnly(2026, 3, 2), FimEm: new DateOnly(2026, 3, 9), CaloteMm: 4.5m,
            ChangedBy: "Diogo"));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        Assert.False(await _db.JobOnChanges.AnyAsync());
    }

    [Fact]
    public async Task Update_Refused_RecordsNothing()
    {
        var (jobOnId, b1, _) = await SeedAsync();

        var outcome = await _update.HandleAsync(new UpdateJobOn(
            jobOnId, b1, "CM-1", "MF-1", "BQ-NAO-EXISTE", ChangedBy: "Diogo"));

        Assert.Equal(UpdateJobOnResult.BqToolNotFound, outcome.Result);
        Assert.False(await _db.JobOnChanges.AnyAsync());
    }

    private static readonly DateTime RecordedAt = new(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Uma alteração antes do registo, uma depois em cada campo vigiado e uma no fim.</summary>
    private async Task SeedChangesAsync(int jobOnId)
    {
        void Add(string field, DateTime at) => _db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = jobOnId, Field = field, OldValueFrozen = "a", NewValueFrozen = "b",
            ChangedByNameFrozen = "Diogo", ChangedAtUtc = at
        });
        Add(JobOnChangeFields.Cm, RecordedAt.AddHours(-1));
        Add(JobOnChangeFields.Cm, RecordedAt.AddHours(1));
        Add(JobOnChangeFields.Mf, RecordedAt.AddHours(2));
        Add(JobOnChangeFields.Bq, RecordedAt.AddHours(3));
        Add(JobOnChangeFields.Calote, RecordedAt.AddHours(4));
        Add(JobOnChangeFields.Machine, RecordedAt.AddHours(5));
        Add(JobOnChangeFields.End, RecordedAt.AddHours(6));
        await _db.SaveChangesAsync();
    }

    private static string[] Fields(JobOnChangeNotice? notice) =>
        notice!.Changes.Select(c => c.Field).OrderBy(f => f, StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Notices_ShowOnlyChangesAfterTheRecord_ThatTheModuleWatches()
    {
        var (jobOnId, _, _) = await SeedAsync();
        var controlo = await _db.Controlos.SingleAsync(c => c.JobOnId == jobOnId);

        // Sem aprovação, sem Peso, sem Pegamentos e sem movimentos: nenhum aviso.
        await SeedChangesAsync(jobOnId);
        var bqContextId = await _db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        Assert.Null(await _query.ForControloApprovalAsync(controlo.Id));
        Assert.Null(await _query.ForPegamentosSheetAsync(0));
        Assert.Null(await _query.ForBqContextAsync(bqContextId));

        controlo.ApprovedAtUtc = RecordedAt;
        controlo.ApprovedByOperatorId = "7";
        var peso = new Peso { CmToolId = "CM-1", BqToolId = "BQ-6", Process = "SG", ControloId = controlo.Id, RegistadoEmUtc = RecordedAt };
        _db.Pesos.Add(peso);
        var pegamentos = new Pegamentos { ControloId = controlo.Id, RegistadoEmUtc = RecordedAt };
        _db.Pegamentos.Add(pegamentos);
        var trace = new BqRepairTrace { BqContextId = bqContextId };
        _db.BqRepairTraces.Add(trace);
        await _db.SaveChangesAsync();
        _db.BqMovements.Add(new BqMovement
        {
            BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 10,
            OccurredAtUtc = RecordedAt, CreatedAtUtc = RecordedAt
        });
        await _db.SaveChangesAsync();

        var approval = await _query.ForControloApprovalAsync(controlo.Id);
        Assert.Equal(jobOnId, approval!.JobOnId);
        Assert.Equal(new[] { "BQ", "CM", "Calote", "MF" }, Fields(approval));
        Assert.Single(approval.Changes, c => c.Field == JobOnChangeFields.Cm);
        Assert.Equal(new[] { "BQ", "CM", "Calote" }, Fields(await _query.ForPesoAsync(peso.Id)));
        Assert.Equal(new[] { "BQ", "CM", "MF" }, Fields(await _query.ForPegamentosSheetAsync(pegamentos.Id)));
        Assert.Equal(new[] { "BQ", "Fim", "Maquina" }, Fields(await _query.ForBqContextAsync(bqContextId)));
    }

    [Fact]
    public async Task PegamentosNotice_UsesTheDateOfTheSheetShown()
    {
        // Duas folhas na mesma produção (a CM mudou a meio; Diogo,
        // 18/10/2026): cada uma avisa só do que mudou depois de ela ser
        // gravada. A troca da CM que começou a segunda não aparece nela.
        var (jobOnId, _, _) = await SeedAsync();
        var controlo = await _db.Controlos.SingleAsync(c => c.JobOnId == jobOnId);
        await SeedChangesAsync(jobOnId);
        var first = new Pegamentos { ControloId = controlo.Id, RegistadoEmUtc = RecordedAt };
        var second = new Pegamentos { ControloId = controlo.Id, RegistadoEmUtc = RecordedAt.AddMinutes(90) };
        _db.Pegamentos.AddRange(first, second);
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { "BQ", "CM", "MF" }, Fields(await _query.ForPegamentosSheetAsync(first.Id)));
        Assert.Equal(new[] { "BQ", "MF" }, Fields(await _query.ForPegamentosSheetAsync(second.Id)));
    }

    [Fact]
    public async Task Notices_WithNoChangeAfterTheRecord_ShowNothing()
    {
        var (jobOnId, _, _) = await SeedAsync();
        var controlo = await _db.Controlos.SingleAsync(c => c.JobOnId == jobOnId);
        await SeedChangesAsync(jobOnId);
        controlo.ApprovedAtUtc = RecordedAt.AddDays(1);
        var peso = new Peso { CmToolId = "CM-1", BqToolId = "BQ-6", Process = "SG", ControloId = controlo.Id, RegistadoEmUtc = null };
        _db.Pesos.Add(peso);
        await _db.SaveChangesAsync();

        Assert.Null(await _query.ForControloApprovalAsync(controlo.Id));
        // Um Peso sem a data de registo (anterior a 17/10/2026) não inventa um aviso.
        Assert.Null(await _query.ForPesoAsync(peso.Id));
    }

    [Theory]
    [InlineData(JobOnChangeFields.Cm, JobOnFollowUp.Peso, "Verificar o Peso")]
    [InlineData(JobOnChangeFields.Calote, JobOnFollowUp.Peso, "Verificar o Peso")]
    [InlineData(JobOnChangeFields.Mf, JobOnFollowUp.Pegamentos, "Verificar os Pegamentos")]
    [InlineData(JobOnChangeFields.Bq, JobOnFollowUp.Controlo, "Rever o Controlo")]
    [InlineData(JobOnChangeFields.Machine, JobOnFollowUp.Boquilhas, "Confirmar nas Boquilhas")]
    [InlineData(JobOnChangeFields.End, JobOnFollowUp.Boquilhas, "Confirmar nas Boquilhas")]
    public void FollowUp_LeadsToTheModuleTheChangeAffects(string field, JobOnFollowUp expected, string label)
    {
        Assert.Equal(expected, JobOnChangeFields.FollowUp(field));
        Assert.Equal(label, JobOnChangeFields.FollowUpLabel(expected));
    }
}
