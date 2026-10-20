using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Um Peso por CM+BQ da produção (18/10/2026; respostas R15, R16-2, R16-3 e
/// R16-5 do especialista) num PostgreSQL real: o índice único filtrado aceita
/// dois Pesos da mesma produção com CM diferente, recusa a mesma combinação
/// e deixa apagar as duas CMs (os Pesos passam a REMOVED); dois «Criar Peso»
/// ao mesmo tempo depois de uma troca ficam num só Peso novo; as leituras
/// dos Pesos traduzem no Npgsql; e a Revisão continua com uma linha por
/// controlo.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private sealed record PesoTroca(string Marker, int ControloId, int JobOnId, int MachineId)
    {
        public string Cm1 => Marker + "-CM1";
        public string Cm2 => Marker + "-CM2";
        public string Bq1 => Marker + "-BQ1";
        public string Mf => Marker + "-MF";
    }

    private static async Task<PesoTroca> SeedPesoTrocaAsync(DmoDbContext db, string marker)
    {
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = marker + "-CM2", Type = ToolType.Cm, Reference = "5448", Lot = "2" },
            new Tool { ToolId = marker + "-BQ1", Type = ToolType.Bq, Reference = "T194", Lot = "7" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = marker + "-CM1", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m },
            new ToolTechnicalContext { ToolId = marker + "-CM2", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 32m },
            new ToolTechnicalContext { ToolId = marker + "-BQ1", VolumeMarisaCm3 = 10m });
        if (!await db.GlassDensities.AnyAsync(g => g.Process == CmProcess.Nnpb))
        {
            db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        }

        var machine = new Machine { Code = marker };
        var jobOn = new JobOn { Reference = marker, ProductionNumber = marker, Machine = machine };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = marker + "-CM1" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = marker + "-BQ1" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = marker + "-MF" });
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new PesoTroca(marker, controlo.Id, jobOn.Id, machine.Id);
    }

    private static async Task DeletePesoTrocaAsync(DmoDbContext db, PesoTroca seed)
    {
        db.ChangeTracker.Clear();
        var pesos = db.Pesos.Where(p => p.ControloId == seed.ControloId || p.CmToolId.StartsWith(seed.Marker))
            .Select(p => p.Id);
        await db.PesoMeasurements.Where(m => pesos.Contains(m.PesoId)).ExecuteDeleteAsync();
        await db.Pesos.Where(p => p.ControloId == seed.ControloId || p.CmToolId.StartsWith(seed.Marker)).ExecuteDeleteAsync();
        await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(seed.Marker)).ExecuteDeleteAsync();
        await db.ToolMachineLinks.Where(l => l.ToolId.StartsWith(seed.Marker)).ExecuteDeleteAsync();
        await DeleteProductionAsync(db, seed.Marker);
    }

    private static Peso PesoDe(PesoTroca seed, string cmToolId, int? controloId) => new()
    {
        CmToolId = cmToolId,
        BqToolId = seed.Bq1,
        Process = "NNPB",
        WaterTemperatureC = 20,
        VolumeMarisaCm3 = 10m,
        VolumePuncaoCm3 = 30m,
        GlassDensityGramsPerCm3 = 2.50m,
        ControloId = controloId
    };

    private static CreatePesoForControloHandler PesoCreateHandler(DmoDbContext db) =>
        new(db, new CreateToolScopedPesoHandler(db, new GlassDensityConfiguration(db)));

    private static async Task SwapPesoCmAsync(DmoDbContext db, PesoTroca seed, string cmToolId)
    {
        var outcome = await new UpdateJobOnHandler(db).HandleAsync(new UpdateJobOn(
            seed.JobOnId, seed.MachineId, cmToolId, seed.Mf, seed.Bq1));
        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        db.ChangeTracker.Clear();
    }

    [SkippableFact]
    public async Task Peso_IndiceFiltrado_DoisPesosMesmaProducaoCmDiferente_Aceita()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var seed = await SeedPesoTrocaAsync(db, "PC-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            db.Pesos.AddRange(PesoDe(seed, seed.Cm1, seed.ControloId), PesoDe(seed, seed.Cm2, seed.ControloId));
            await db.SaveChangesAsync();

            // Os Pesos sem produção ficam fora do índice: dois iguais continuam aceites.
            db.Pesos.AddRange(PesoDe(seed, seed.Cm1, null), PesoDe(seed, seed.Cm1, null));
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Pesos.AsNoTracking().CountAsync(p => p.ControloId == seed.ControloId));
        }
        finally
        {
            await DeletePesoTrocaAsync(db, seed);
        }
    }

    [SkippableFact]
    public async Task Peso_IndiceFiltrado_MesmaCombinacaoNaMesmaProducao_Recusa()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var seed = await SeedPesoTrocaAsync(db, "PR-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            db.Pesos.Add(PesoDe(seed, seed.Cm1, seed.ControloId));
            await db.SaveChangesAsync();

            db.Pesos.Add(PesoDe(seed, seed.Cm1, seed.ControloId));
            var refused = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(refused.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("IX_Pesos_ControloId_CmToolId_BqToolId", postgres.ConstraintName);
        }
        finally
        {
            await DeletePesoTrocaAsync(db, seed);
        }
    }

    [SkippableFact]
    public async Task Peso_ApagarAsDuasCms_NaoViolaOIndice_EOAtualEOMaisAntigo()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var seed = await SeedPesoTrocaAsync(db, "PD-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var antigo = PesoDe(seed, seed.Cm1, seed.ControloId);
            db.Pesos.Add(antigo);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await SwapPesoCmAsync(db, seed, seed.Cm2);
            var atual = PesoDe(seed, seed.Cm2, seed.ControloId);
            db.Pesos.Add(atual);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool(seed.Cm1))).Result);
            db.ChangeTracker.Clear();
            Assert.Equal(DeleteToolResult.Success, (await new DeleteToolHandler(db).HandleAsync(new DeleteTool(seed.Cm2))).Result);
            db.ChangeTracker.Clear();

            var pesos = await db.Pesos.AsNoTracking().Where(p => p.ControloId == seed.ControloId).OrderBy(p => p.Id).ToListAsync();
            Assert.Equal(2, pesos.Count);
            Assert.All(pesos, p => Assert.Equal(ToolRegistryConstants.RemovedToolId, p.CmToolId));

            // As duas ficaram com a mesma combinação: o Peso atual é o mais
            // antigo, como nas folhas de Pegamentos.
            var lista = await new PesoQuery(db).ListByControloAsync(seed.ControloId);
            Assert.Equal(new[] { antigo.Id, atual.Id }, lista.Select(p => p.PesoId));
            Assert.Equal(new[] { true, false }, lista.Select(p => p.IsCurrent));
        }
        finally
        {
            await DeletePesoTrocaAsync(db, seed);
        }
    }

    [SkippableFact]
    public async Task Peso_CriarAoMesmoTempoDepoisDaTroca_FicaUmSoPesoNovo_EVoltarRetomaOAnterior()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var seed = await SeedPesoTrocaAsync(db, "PX-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var primeiro = await PesoCreateHandler(db).HandleAsync(new CreatePesoForControlo(seed.ControloId, 20m));
            Assert.Equal(CreatePesoForControloResult.Success, primeiro.Result);
            var pesoA = primeiro.Peso!.PesoId;
            db.ChangeTracker.Clear();
            Assert.Equal(RecordPesoMeasurementResult.Success,
                (await new RecordPesoMeasurementHandler(db).HandleAsync(new RecordPesoMeasurement(pesoA, "1", 200m))).Result);
            db.ChangeTracker.Clear();
            await SwapPesoCmAsync(db, seed, seed.Cm2);

            // Sem o Peso das ferramentas novas: nenhum Peso atual, o anterior
            // recusa leituras novas e a associação propõe só o que aceita.
            Assert.Null(await new PesoQuery(db).FindCurrentByControloAsync(seed.ControloId));
            Assert.Equal(RecordPesoMeasurementResult.PesoNotCurrent,
                (await new RecordPesoMeasurementHandler(db).HandleAsync(new RecordPesoMeasurement(pesoA, "2", 200m))).Result);
            db.ChangeTracker.Clear();
            var solto = PesoDe(seed, seed.Cm2, null);
            db.Pesos.Add(solto);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var associacao = new PesoAssociationQuery(db);
            Assert.Equal(solto.Id, Assert.Single(await associacao.FindCandidatesForControloAsync(seed.ControloId)).PesoId);
            Assert.Equal(seed.JobOnId, Assert.Single(await associacao.FindProductionsForPesoAsync(solto.Id)).JobOnId);
            await db.Pesos.Where(p => p.Id == solto.Id).ExecuteDeleteAsync();

            // Dois «Criar Peso» ao mesmo tempo: o trinco «peso:criar» e o
            // índice deixam um só Peso novo.
            var outro = new OutroPedido<CreatePesoForControloOutcome>(other =>
                PesoCreateHandler(other).HandleAsync(new CreatePesoForControlo(seed.ControloId, 20m)));
            await using var mine = CreateContextWith(outro.AntesDeGravar());
            var este = await PesoCreateHandler(mine).HandleAsync(new CreatePesoForControlo(seed.ControloId, 20m));
            var outroResultado = await outro.Tarefa!;

            Assert.Equal(CreatePesoForControloResult.Success, este.Result);
            Assert.Equal(CreatePesoForControloResult.Success, outroResultado.Result);
            var pesoB = este.Peso!.PesoId;
            Assert.Equal(pesoB, outroResultado.Peso!.PesoId);
            Assert.NotEqual(pesoA, pesoB);
            Assert.Equal(2, await db.Pesos.AsNoTracking().CountAsync(p => p.ControloId == seed.ControloId));

            // As leituras dos Pesos da produção traduzem no Npgsql.
            var query = new PesoQuery(db);
            var lista = await query.ListByControloAsync(seed.ControloId);
            Assert.Equal(new[] { pesoA, pesoB }, lista.Select(p => p.PesoId));
            Assert.Equal(new[] { false, true }, lista.Select(p => p.IsCurrent));
            Assert.Equal(pesoB, (await query.FindCurrentByControloAsync(seed.ControloId))!.PesoId);
            var comparacao = new PesoComparisonQuery(db, query);
            var iniciais = await comparacao.ListarIniciaisAsync(seed.JobOnId);
            Assert.Equal(new[] { false, true }, iniciais.Select(o => o.IsCurrent));
            Assert.All(iniciais, o => Assert.Equal("BQ T194 · Lote 7", o.BqLabel));
            Assert.Empty(await comparacao.FindCandidatesAsync(lista[0]));
            var historico = await new ControloHistorySearchQuery(db).SearchPageAsync(
                null, seed.Marker, null, ControloHistoryKind.Peso, null, 1);
            Assert.Equal(2, historico.Items.Count);
            Assert.All(historico.Items, i => Assert.EndsWith("· BQ T194 · Lote 7", i.ToolLabel));

            // A→B→A: voltar à CM1 retoma o Peso dela.
            await SwapPesoCmAsync(db, seed, seed.Cm1);
            var deVolta = await PesoCreateHandler(db).HandleAsync(new CreatePesoForControlo(seed.ControloId, 20m));
            Assert.Equal(pesoA, deVolta.Peso!.PesoId);
            db.ChangeTracker.Clear();
            Assert.Equal(RecordPesoMeasurementResult.Success,
                (await new RecordPesoMeasurementHandler(db).HandleAsync(new RecordPesoMeasurement(pesoA, "2", 200m))).Result);
            Assert.Equal(2, await db.Pesos.AsNoTracking().CountAsync(p => p.ControloId == seed.ControloId));
        }
        finally
        {
            await DeletePesoTrocaAsync(db, seed);
        }
    }

    [SkippableFact]
    public async Task Peso_Revisao_UmaLinhaPorControlo_MesmoComDoisPesos()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var seed = await SeedPesoTrocaAsync(db, "PV-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var registado = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);
            var antigo = PesoDe(seed, seed.Cm1, seed.ControloId);
            antigo.CmToolReferenceFrozen = "5447";
            antigo.RegistadoEmUtc = registado;
            antigo.RegistadoPor = "João Silva";
            antigo.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 150m });
            db.Pesos.Add(antigo);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var query = new ControloRevisaoQuery(db);
            var dia = new DateOnly(2026, 3, 11);
            var noDiaComUmPeso = (await query.MonthAsync(2026, 3)).Single(d => d.Day == 11).Total;
            await SwapPesoCmAsync(db, seed, seed.Cm2);
            var atual = PesoDe(seed, seed.Cm2, seed.ControloId);
            atual.CmToolReferenceFrozen = "5448";
            atual.RegistadoEmUtc = registado.AddHours(2);
            atual.RegistadoPor = "Ana Costa";
            atual.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 160m });
            db.Pesos.Add(atual);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var item = Assert.Single(await query.DayAsync(dia, seed.Marker, null));
            Assert.Equal(seed.ControloId, item.ControloId);
            Assert.Equal(2, item.PesoCount);
            // A linha mostra o Peso das ferramentas atuais; o dia e quem
            // registou são os do primeiro Peso.
            Assert.Equal("5448", item.CmReference);
            Assert.Equal(registado, item.RegistadoEmUtc);
            Assert.Equal("João Silva", item.RegistadoPor);
            Assert.NotNull(item.AverageGlassWeightGrams);

            // A procura encontra o controlo pela CM de qualquer Peso, uma vez.
            var porCmAntiga = await query.DayAsync(dia, "5447", ControloRevisaoEstado.Pendente);
            Assert.Single(porCmAntiga, i => i.ControloId == seed.ControloId);
            // O calendário conta o controlo uma vez, como com um só Peso.
            Assert.Equal(noDiaComUmPeso, (await query.MonthAsync(2026, 3)).Single(d => d.Day == 11).Total);
            Assert.Equal(seed.ControloId, (await query.FindByJobOnAsync(seed.JobOnId))!.ControloId);

            // Sem data, também uma linha.
            await db.Pesos.Where(p => p.ControloId == seed.ControloId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.RegistadoEmUtc, (DateTime?)null));
            var semData = await query.UndatedAsync(seed.Marker, null);
            Assert.Equal(2, Assert.Single(semData).PesoCount);
        }
        finally
        {
            await DeletePesoTrocaAsync(db, seed);
        }
    }
}
