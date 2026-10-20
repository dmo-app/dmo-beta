using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O estado da aprovação de um controlo (sweep de 19/10/2026, C-1; o
/// especialista, R28): sai da decisão do Peso, a única parte que se aprova
/// (Diogo, 19/10/2026: «remover aprovaçao do spegamentos, manter apenas no
/// peso»), e dos Pesos registados depois dela.
/// </summary>
public sealed class ControloAprovacaoTests : IDisposable
{
    private static readonly DateTime RegistadoEm = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _handler;

    public ControloAprovacaoTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.SaveChanges();
        _handler = new ControloApprovalHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task UmaDecisaoAntigaDosPegamentos_NaoConta_OPesoDecide()
    {
        // Antes os Pegamentos também se aprovavam: um «Não aprovar» antigo
        // deixava o controlo não aprovado mesmo com o Peso aprovado.
        var (controloId, _) = await NovaProducaoAsync("2026101", comPeso: true, comPegamentos: true);
        _db.ControloDecisoes.Add(new ControloDecisao
        {
            ControloId = controloId, Parte = (ControloParte)2, Aprovado = false, OperatorId = "4801",
            EmUtc = RegistadoEm, Nota = "Repetir os pegamentos."
        });
        await _db.SaveChangesAsync();
        Assert.Null((await _handler.GetAsync(controloId))!.NotApprovedByOperatorId);

        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);

        var estado = await _handler.GetAsync(controloId);
        Assert.Equal("4802", estado!.ApprovedByOperatorId);
        Assert.Null(estado.NotApprovedByOperatorId);
        Assert.Null(estado.ReviewNote);
        Assert.Equal(ControloParte.Peso, Assert.Single(await _handler.PartsAsync(controloId)).Parte);
    }

    [Fact]
    public async Task PegamentosRegistadosDepoisDeAprovarOPeso_ContinuaAprovado()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026103", comPeso: true);
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        // A calote muda depois da aprovação: o aviso aparece enquanto está aprovado.
        _db.JobOnChanges.Add(new JobOnChange
        {
            JobOnId = jobOnId, Field = JobOnChangeFields.Calote, OldValueFrozen = "9.5", NewValueFrozen = "10",
            ChangedAtUtc = DateTime.UtcNow.AddMinutes(1)
        });
        await _db.SaveChangesAsync();
        var avisos = new JobOnChangeQuery(_db);

        // À tarde o operador mede os Pegamentos: não se aprovam, o controlo
        // continua aprovado.
        _db.Pegamentos.Add(NovaFolha(controloId));
        await _db.SaveChangesAsync();

        Assert.Equal("4802", (await _handler.GetAsync(controloId))!.ApprovedByOperatorId);
        Assert.Equal("4802", (await _handler.FindByJobOnAsync(jobOnId))!.Value.Approval.ApprovedByOperatorId);
        var revisao = await new ControloRevisaoQuery(_db).FindByJobOnAsync(jobOnId);
        Assert.Equal(ControloRevisaoEstado.Aprovado, revisao!.Estado);
        Assert.NotNull(await avisos.ForControloApprovalAsync(controloId));
    }

    [Fact]
    public async Task UmPesoNovoDepoisDeAprovar_FicaPorAprovar()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026110", comPeso: true);
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);

        // A CM trocou: o Peso novo tem aprovação própria (C7).
        _db.Pesos.Add(new Peso
        {
            CmToolId = "T3", BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controloId, RegistadoEmUtc = DateTime.UtcNow.AddMinutes(1)
        });
        await _db.SaveChangesAsync();

        var estado = await _handler.GetAsync(controloId);
        Assert.Null(estado!.ApprovedByOperatorId);
        Assert.Null(estado.NotApprovedByOperatorId);
        var resumo = Assert.Single((await new ControloHistorySearchQuery(_db).SearchPageAsync(
            null, "2026110", null, ControloHistoryKind.Resumo, null, 1, 50)).Items);
        Assert.Equal("Por aprovar", resumo.Resumo);
        Assert.Equal(ControloRevisaoEstado.Pendente, (await new ControloRevisaoQuery(_db).FindByJobOnAsync(jobOnId))!.Estado);
    }

    [Fact]
    public async Task UmaFolhaNovaDepoisDeAprovarOsPegamentos_ContinuaAprovado()
    {
        var (controloId, _) = await NovaProducaoAsync("2026104", comPeso: true, comPegamentos: true);
        await _handler.ApproveAndGetAsync(controloId, "4803", "Ok.");

        _db.Pegamentos.Add(NovaFolha(controloId));
        await _db.SaveChangesAsync();

        var estado = await _handler.GetAsync(controloId);
        Assert.Equal("4803", estado!.ApprovedByOperatorId);
        Assert.Equal("Ok.", estado.ReviewNote);
    }

    [Fact]
    public async Task SemDecisoesPorParte_ValeOResumoGravado()
    {
        // Os controlos de antes das decisões por parte (a migração copiou-as)
        // e os resumos escritos diretamente continuam a ler-se.
        var (controloId, _) = await NovaProducaoAsync("2026105", comPeso: true);
        var controlo = await _db.Controlos.SingleAsync(c => c.Id == controloId);
        controlo.ApprovedByOperatorId = "4221";
        controlo.ApprovedAtUtc = RegistadoEm;
        await _db.SaveChangesAsync();

        var estado = await _handler.GetAsync(controloId);
        Assert.Equal("4221", estado!.ApprovedByOperatorId);
        Assert.Equal(RegistadoEm, estado.ApprovedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NaoAprovarSemNota_NaoGravaNada_OControloFicaPorAprovar(string? nota)
    {
        // O «Não aprovar» exige a nota que diz o que tem de mudar
        // (17/10/2026): sem ela não fica nenhuma decisão.
        var (controloId, _) = await NovaProducaoAsync("2026106", comPeso: true, comPegamentos: true);

        var estado = await _handler.NotApproveAndGetAsync(controloId, "4801", nota);

        Assert.Null(estado!.NotApprovedByOperatorId);
        Assert.Null(estado.ApprovedByOperatorId);
        await NadaGravadoAsync(controloId);
    }

    [Theory]
    [InlineData(ControloParte.Peso, null)]
    [InlineData(ControloParte.Peso, "")]
    [InlineData(ControloParte.Peso, "   ")]
    public async Task NaoAprovarUmaParteSemNota_NaoGravaNada_OControloFicaPorAprovar(ControloParte parte, string? nota)
    {
        var (controloId, _) = await NovaProducaoAsync("2026107", comPeso: true, comPegamentos: true);

        var estado = await _handler.DecidePartAsync(controloId, parte, aprovado: false, "4801", nota);

        Assert.Null(estado!.NotApprovedByOperatorId);
        Assert.Null(estado.ApprovedByOperatorId);
        await NadaGravadoAsync(controloId);
    }

    [Fact]
    public async Task NaoAprovarComNota_GravaADecisaoDoPeso_EANota()
    {
        var (controloId, _) = await NovaProducaoAsync("2026108", comPeso: true, comPegamentos: true);

        await _handler.NotApproveAndGetAsync(controloId, "4801", " Mudar a CM. ");

        _db.ChangeTracker.Clear();
        var d = Assert.Single(await _db.ControloDecisoes.AsNoTracking().Where(d => d.ControloId == controloId).ToListAsync());
        Assert.Equal(ControloParte.Peso, d.Parte);
        Assert.False(d.Aprovado);
        Assert.Equal("4801", d.OperatorId);
        Assert.Equal("Mudar a CM.", d.Nota);
        var controlo = await _db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
        Assert.Equal("4801", controlo.NotApprovedByOperatorId);
        Assert.NotNull(controlo.NotApprovedAtUtc);
        Assert.Equal("Mudar a CM.", controlo.ReviewNote);
        Assert.Null(controlo.ApprovedByOperatorId);
    }

    [Theory]
    [InlineData(ControloParte.Peso)]
    public async Task NaoAprovarUmaParteComNota_GravaADecisaoDessaParte_EANota(ControloParte parte)
    {
        var (controloId, _) = await NovaProducaoAsync("2026109", comPeso: true, comPegamentos: true);

        await _handler.DecidePartAsync(controloId, parte, aprovado: false, "4801", "Repetir.");

        _db.ChangeTracker.Clear();
        var decisao = Assert.Single(await _db.ControloDecisoes.AsNoTracking().Where(d => d.ControloId == controloId).ToListAsync());
        Assert.Equal(parte, decisao.Parte);
        Assert.False(decisao.Aprovado);
        Assert.Equal("4801", decisao.OperatorId);
        Assert.Equal("Repetir.", decisao.Nota);
        var controlo = await _db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
        Assert.Equal("4801", controlo.NotApprovedByOperatorId);
        Assert.Equal("Repetir.", controlo.ReviewNote);
    }

    // Uma recusa não deixa decisões nem muda as colunas do controlo (que
    // nascem vazias: por aprovar).
    private async Task NadaGravadoAsync(int controloId)
    {
        _db.ChangeTracker.Clear();
        Assert.Equal(0, await _db.ControloDecisoes.AsNoTracking().CountAsync(d => d.ControloId == controloId));
        var controlo = await _db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId);
        Assert.Null(controlo.ApprovedByOperatorId);
        Assert.Null(controlo.ApprovedAtUtc);
        Assert.Null(controlo.NotApprovedByOperatorId);
        Assert.Null(controlo.NotApprovedAtUtc);
        Assert.Null(controlo.ReviewNote);
    }

    private async Task<(int ControloId, int JobOnId)> NovaProducaoAsync(string numero, bool comPeso, bool comPegamentos = false)
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = numero, MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        if (comPeso)
        {
            _db.Pesos.Add(new Peso
            {
                CmToolId = "T3", BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
                ControloId = controlo.Id, RegistadoEmUtc = RegistadoEm
            });
        }

        if (comPegamentos)
        {
            _db.Pegamentos.Add(NovaFolha(controlo.Id));
        }

        await _db.SaveChangesAsync();
        return (controlo.Id, jobOn.Id);
    }

    private static Pegamentos NovaFolha(int controloId) => new()
    {
        ControloId = controloId, CmToolId = "T3", ToleranceMm = 0.20m, RegistadoEmUtc = RegistadoEm
    };
}
