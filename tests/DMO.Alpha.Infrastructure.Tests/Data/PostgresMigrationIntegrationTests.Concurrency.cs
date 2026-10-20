using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Gravações iguais ao mesmo tempo (duplo envio, duas pessoas), contra um
/// PostgreSQL real: «existe? se não, cria» nunca cria dois registos nem dá
/// erro (varredura de 18/10/2026, B1–B3).
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Concurrency_CreateTheSameToolTwiceAtOnce_KeepsOneTool()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var reference = "CC" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            for (var round = 0; round < 5; round++)
            {
                await using var a = CreateContext();
                await using var b = CreateContext();
                var outcomes = await Task.WhenAll(
                    new CreateToolHandler(a, new ToolLotQuantitySettings(a)).HandleAsync(new CreateTool("CM", reference, round.ToString())),
                    new CreateToolHandler(b, new ToolLotQuantitySettings(b)).HandleAsync(new CreateTool("CM", reference, round.ToString())));

                Assert.All(outcomes, o => Assert.Equal(CreateToolResult.Success, o.Result));
                Assert.Equal(outcomes[0].Tool!.ToolId, outcomes[1].Tool!.ToolId);
            }

            Assert.Equal(5, await db.Tools.AsNoTracking().CountAsync(t => t.Reference == reference));
        }
        finally
        {
            await db.Tools.Where(t => t.Reference == reference).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Concurrency_CreateTheSameRepairerTwiceAtOnce_OneSucceeds_TheOtherIsATypedRefusal()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var name = "Reparador " + Guid.NewGuid().ToString("N")[..6];
        try
        {
            for (var round = 0; round < 5; round++)
            {
                await using var a = CreateContext();
                await using var b = CreateContext();
                var outcomes = await Task.WhenAll(
                    new CreateBqRepairerHandler(a).HandleAsync(new CreateBqRepairer(name + round)),
                    new CreateBqRepairerHandler(b).HandleAsync(new CreateBqRepairer(name + round)));

                Assert.Single(outcomes, o => o.Result == BqRepairerWriteResult.Success);
                Assert.Single(outcomes, o => o.Result == BqRepairerWriteResult.NameAlreadyInUse);
            }
        }
        finally
        {
            await db.BqRepairers.Where(r => r.Name.StartsWith(name)).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Concurrency_TwoFirstPegamentosReadingsAtOnce_ShareOneSheet()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CC-" + Guid.NewGuid().ToString("N")[..8];
        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = marker + "-MF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = marker + "-CM", DiametroGargaloMm = 70.00m });
        await db.SaveChangesAsync();
        var controloIds = new List<int>();
        try
        {
            for (var round = 0; round < 5; round++)
            {
                var created = await new DMO.Alpha.Infrastructure.Production.CreateJobOnHandler(db).HandleAsync(
                    new CreateJobOn(marker + "-" + round, "5810C142", machine.Id, marker + "-CM", marker + "-MF"));
                Assert.Equal(CreateJobOnResult.Success, created.Result);
                db.ChangeTracker.Clear();
                var controloId = await db.Controlos.AsNoTracking()
                    .Where(c => c.JobOnId == created.JobOn!.JobOnId).Select(c => c.Id).SingleAsync();
                controloIds.Add(controloId);

                await using var a = CreateContext();
                await using var b = CreateContext();
                var outcomes = await Task.WhenAll(
                    new RecordPegamentosMeasurementHandler(a, new PegamentosToleranceConfiguration(a))
                        .HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.10m)),
                    new RecordPegamentosMeasurementHandler(b, new PegamentosToleranceConfiguration(b))
                        .HandleAsync(new RecordPegamentosMeasurement(controloId, "CM", 70.20m)));

                Assert.All(outcomes, o => Assert.Equal(RecordPegamentosMeasurementResult.Success, o.Result));
                Assert.Equal(1, await db.Pegamentos.AsNoTracking().CountAsync(p => p.ControloId == controloId));
                Assert.Equal(outcomes[0].Measurement!.PegamentosId, outcomes[1].Measurement!.PegamentosId);
            }
        }
        finally
        {
            var pegamentos = db.Pegamentos.Where(p => p.ControloId != null && controloIds.Contains(p.ControloId.Value)).Select(p => p.Id);
            await db.PegamentosMeasurements.Where(m => pegamentos.Contains(m.PegamentosId)).ExecuteDeleteAsync();
            await db.Pegamentos.Where(p => p.ControloId != null && controloIds.Contains(p.ControloId.Value)).ExecuteDeleteAsync();
            var jobOns = db.JobOns.Where(j => j.MachineId == machine.Id).Select(j => j.Id);
            await db.Controlos.Where(c => jobOns.Contains(c.JobOnId)).ExecuteDeleteAsync();
            await db.CmContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.MfContexts.Where(c => jobOns.Contains(c.JobOnId!.Value) || jobOns.Contains(c.ReplacedJobOnId!.Value)).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
            await db.ToolTechnicalContexts.Where(t => t.ToolId.StartsWith(marker)).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId.StartsWith(marker)).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Concurrency_StartingTheSameRegisterTwiceAtOnce_KeepsOneEach()
    {
        // B5: dois envios iguais ao mesmo tempo de «Começar registo» nas
        // Punções, no Peso e nos Pegamentos sem produção abrem o mesmo registo.
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var (cm, bq, mf) = ($"CM-CC-{suffix}", $"BQ-CC-{suffix}", $"MF-CC-{suffix}");
        var reference = $"CC{suffix}";
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = $"C{suffix}", Lot = "1" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = $"B{suffix}", Lot = "1" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = $"M{suffix}", Lot = "1" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = cm, Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m },
            new ToolTechnicalContext { ToolId = bq, VolumeMarisaCm3 = 10m });
        if (!await db.GlassDensities.AnyAsync(g => g.Process == CmProcess.Nnpb))
        {
            db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
        }

        await db.SaveChangesAsync();
        try
        {
            for (var round = 0; round < 3; round++)
            {
                await using var a = CreateContext();
                await using var b = CreateContext();
                var puncoes = await Task.WhenAll(
                    new PuncoesHandler(a).CreateStandaloneAsync(reference, "1075", "1", "4301"),
                    new PuncoesHandler(b).CreateStandaloneAsync(reference, "1075", "1", "4301"));
                Assert.Equal(puncoes[0].PuncoesId, puncoes[1].PuncoesId);

                await using var c = CreateContext();
                await using var d = CreateContext();
                var pesos = await Task.WhenAll(
                    new CreateToolScopedPesoHandler(c, new GlassDensityConfiguration(c)).HandleAsync(new CreateToolScopedPeso(cm, bq, 20m)),
                    new CreateToolScopedPesoHandler(d, new GlassDensityConfiguration(d)).HandleAsync(new CreateToolScopedPeso(cm, bq, 20m)));
                Assert.All(pesos, o => Assert.Equal(CreateToolScopedPesoResult.Success, o.Result));
                Assert.Equal(pesos[0].Peso!.PesoId, pesos[1].Peso!.PesoId);

                await using var e = CreateContext();
                await using var f = CreateContext();
                var pegamentos = await Task.WhenAll(
                    new CreateToolScopedPegamentosHandler(e, new PegamentosToleranceConfiguration(e)).HandleAsync(new CreateToolScopedPegamentos(cm, bq, mf)),
                    new CreateToolScopedPegamentosHandler(f, new PegamentosToleranceConfiguration(f)).HandleAsync(new CreateToolScopedPegamentos(cm, bq, mf)));
                Assert.Equal(pegamentos[0].PegamentosId, pegamentos[1].PegamentosId);
            }

            Assert.Equal(1, await db.Puncoes.AsNoTracking().CountAsync(p => p.Reference == reference));
            Assert.Equal(1, await db.Pesos.AsNoTracking().CountAsync(p => p.CmToolId == cm));
            Assert.Equal(1, await db.Pegamentos.AsNoTracking().CountAsync(p => p.CmToolId == cm));
        }
        finally
        {
            await db.Puncoes.Where(p => p.Reference == reference).ExecuteDeleteAsync();
            await db.Pesos.Where(p => p.CmToolId == cm).ExecuteDeleteAsync();
            await db.Pegamentos.Where(p => p.CmToolId == cm).ExecuteDeleteAsync();
            await db.ToolTechnicalContexts.Where(t => t.ToolId == cm || t.ToolId == bq).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == cm || t.ToolId == bq || t.ToolId == mf).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Concurrency_SaveTheSameComparisonThreeTimesAtOnce_KeepsOne()
    {
        // B-peso-2: três cliques em «Guardar comparação» com a rede lenta
        // chegam quase ao mesmo tempo; fica uma comparação e os três abrem-na.
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "CP-" + Guid.NewGuid().ToString("N")[..8];
        var (machineId, jobOnId) = await SeedJobOnAsync(db, marker);
        var (cm, bq) = (marker + "-CM", marker + "-BQ");
        int? pesoId = null;
        try
        {
            db.Tools.AddRange(
                new Tool { ToolId = cm, Type = ToolType.Cm, Reference = "5447", Lot = "3" },
                new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "T194", Lot = "12" });
            db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = cm, Process = CmProcess.Nnpb, VolumePuncaoCm3 = 30m });
            if (!await db.GlassDensities.AnyAsync(g => g.Process == CmProcess.Nnpb))
            {
                db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.50m });
            }

            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOnId };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var peso = new Peso
            {
                CmToolId = cm,
                BqToolId = bq,
                Process = "NNPB",
                WaterTemperatureC = 20,
                VolumeMarisaCm3 = 10m,
                VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.50m,
                ControloId = controlo.Id,
                Measurements = [new PesoMeasurement { RowPosition = 1, CmSubject = "76", WaterWeightGrams = 132.0m }]
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesoId = peso.Id;

            var command = new CreatePesoComparacao(peso.Id, cm, 22m, [new PesoComparacaoLeituraInput("76", 132.2m)], "Nota");
            await using var a = CreateContext();
            await using var b = CreateContext();
            await using var c = CreateContext();
            var outcomes = await Task.WhenAll(
                new CreatePesoComparacaoHandler(a, new PesoComparisonQuery(a, new PesoQuery(a))).HandleAsync(command),
                new CreatePesoComparacaoHandler(b, new PesoComparisonQuery(b, new PesoQuery(b))).HandleAsync(command),
                new CreatePesoComparacaoHandler(c, new PesoComparisonQuery(c, new PesoQuery(c))).HandleAsync(command));

            Assert.All(outcomes, o => Assert.Equal(PesoComparacaoResultado.Sucesso, o.Result));
            Assert.Single(outcomes.Select(o => o.Comparacao!.ComparacaoId).Distinct());
            Assert.Equal(1, await db.PesoComparacoes.AsNoTracking().CountAsync(x => x.PesoInicialId == peso.Id));
        }
        finally
        {
            if (pesoId is int criado)
            {
                var comparacoes = db.PesoComparacoes.Where(x => x.PesoInicialId == criado).Select(x => x.Id);
                await db.PesoComparacaoMeasurements.Where(m => comparacoes.Contains(m.ComparacaoId)).ExecuteDeleteAsync();
                await db.PesoComparacoes.Where(x => x.PesoInicialId == criado).ExecuteDeleteAsync();
                await db.PesoMeasurements.Where(m => m.PesoId == criado).ExecuteDeleteAsync();
                await db.Pesos.Where(p => p.Id == criado).ExecuteDeleteAsync();
            }

            await DeleteJobOnAsync(db, machineId);
            await db.ToolTechnicalContexts.Where(t => t.ToolId == cm).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == cm || t.ToolId == bq).ExecuteDeleteAsync();
        }
    }
}
