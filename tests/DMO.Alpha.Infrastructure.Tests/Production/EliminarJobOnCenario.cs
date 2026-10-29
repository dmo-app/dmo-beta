using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// Uma produção com tudo o que o «Eliminar Job On» apaga: CM, MF e BQ atuais
/// e uma CM e uma BQ trocadas, o controlo com Peso (e leitura), Pegamentos (e
/// leitura), Punções, a decisão da Revisão, as alterações, e os movimentos das
/// Boquilhas nos dois bq_id. Ao lado, outra produção da mesma máquina com um
/// Peso que tem esta como produção anterior, que tem de ficar.
/// </summary>
internal static class EliminarJobOnCenario
{
    public sealed record Ids(int JobOnId, int ControloId, int OutraJobOnId, int OutroPesoId, int ComparacaoCmDecisaoId, IReadOnlyList<string> ToolIds);

    public static async Task<Ids> SeedAsync(DmoDbContext db, string marker, DateOnly? inicio)
    {
        var machine = new Machine { Code = "E-" + marker };
        db.Machines.Add(machine);
        string T(string slot) => $"{marker}-{slot}";
        var tools = new[] { ("CM1", ToolType.Cm), ("CM2", ToolType.Cm), ("MF", ToolType.Mf), ("BQ1", ToolType.Bq), ("BQ2", ToolType.Bq) };
        foreach (var (slot, type) in tools)
        {
            db.Tools.Add(new Tool { ToolId = T(slot), Type = type, Reference = "R" + marker, Lot = slot });
        }

        var jobOn = new JobOn { Reference = "5447" + marker, ProductionNumber = "A" + marker, Machine = machine, InicioEm = inicio };
        var outra = new JobOn { Reference = "5447" + marker, ProductionNumber = "B" + marker, Machine = machine };
        db.JobOns.AddRange(jobOn, outra);
        await db.SaveChangesAsync();

        var trocadaEm = DateTime.UtcNow.AddDays(-1);
        db.CmContexts.Add(new CmContext { ReplacedJobOnId = jobOn.Id, ReplacedAtUtc = trocadaEm, ToolId = T("CM1") });
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = T("CM2") });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = T("MF") });
        var bqTrocada = new BqContext { ReplacedJobOnId = jobOn.Id, ReplacedAtUtc = trocadaEm, ToolId = T("BQ1") };
        var bqAtual = new BqContext { JobOnId = jobOn.Id, ToolId = T("BQ2") };
        db.BqContexts.AddRange(bqTrocada, bqAtual);
        db.JobOnChanges.Add(new JobOnChange { JobOnId = jobOn.Id, Field = JobOnChangeFields.Cm, ChangedAtUtc = trocadaEm });
        await db.SaveChangesAsync();

        foreach (var bq in new[] { bqTrocada, bqAtual })
        {
            var trace = new BqRepairTrace { BqContextId = bq.Id };
            db.BqRepairTraces.Add(trace);
            await db.SaveChangesAsync();
            db.BqMovements.Add(new BqMovement
            {
                BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 4,
                OccurredAtUtc = trocadaEm, CreatedAtUtc = trocadaEm, MachineCodeFrozen = machine.Code
            });
        }

        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id, ApprovedAtUtc = DateTime.UtcNow, ApprovedByOperatorId = "1" };
        var outroControlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = outra.Id };
        db.Controlos.AddRange(controlo, outroControlo);
        await db.SaveChangesAsync();

        var peso = NewPeso(controlo.Id, T("CM2"), T("BQ2"));
        peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 10m });
        var outroPeso = NewPeso(outroControlo.Id, T("CM2"), T("BQ2"));
        outroPeso.ProducaoAnteriorControloId = controlo.Id;
        db.Pesos.AddRange(peso, outroPeso);
        var pegamentos = new Pegamentos { ControloId = controlo.Id, CmToolId = T("CM2"), BqToolId = T("BQ2"), MfToolId = T("MF"), ToleranceMm = 0.5m };
        pegamentos.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 0.1m });
        db.Pegamentos.Add(pegamentos);
        db.Puncoes.Add(new Puncoes { ControloId = controlo.Id, PunchReference = "P" + marker, Lot = "1", UpdatedAtUtc = DateTime.UtcNow });
        db.ControloDecisoes.Add(new ControloDecisao { ControloId = controlo.Id, Parte = ControloParte.Peso, Aprovado = true, OperatorId = "1", EmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // DEL-01: uma comparação de Peso com uma escolha «Colocar de parte»
        // (a FK da decisão para a leitura é RESTRICT — tem de sair primeiro).
        var comparacao = new PesoComparacao
        {
            ControloId = controlo.Id, PesoInicialId = peso.Id, CmToolId = T("CM2"),
            WaterTemperatureC = 20, WaterDensityGramsPerCm3 = 1m, GlassDensityGramsPerCm3 = 2.5m,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, CreatedAtUtc = DateTime.UtcNow
        };
        comparacao.Measurements.Add(new PesoComparacaoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 11m });
        db.PesoComparacoes.Add(comparacao);
        await db.SaveChangesAsync();
        var decisao = new PesoComparacaoCmDecisao
        {
            MeasurementId = comparacao.Measurements.Single().Id, DeParte = true, EscolhidoPor = "Diogo", EscolhidoEmUtc = DateTime.UtcNow
        };
        db.PesoComparacaoCmDecisoes.Add(decisao);
        await db.SaveChangesAsync();

        return new Ids(jobOn.Id, controlo.Id, outra.Id, outroPeso.Id, decisao.Id, tools.Select(t => T(t.Item1)).ToList());
    }

    private static Peso NewPeso(int controloId, string cm, string bq) => new()
    {
        ControloId = controloId, CmToolId = cm, BqToolId = bq, Process = "NNPB",
        WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.5m
    };

    /// <summary>
    /// Lido de volta sem tracking: nada da produção eliminada ficou, os dois
    /// movimentos ficaram no rasto com quem eliminou, e a outra produção, o
    /// Peso dela (sem a produção anterior) e as ferramentas ficaram.
    /// </summary>
    public static async Task AssertDeletedAsync(DmoDbContext db, Ids ids, string? deletedBy = "Diogo")
    {
        db.ChangeTracker.Clear();
        Assert.False(await db.JobOns.AnyAsync(j => j.Id == ids.JobOnId));
        Assert.False(await db.Controlos.AnyAsync(c => c.Id == ids.ControloId));
        Assert.False(await db.CmContexts.AnyAsync(c => c.JobOnId == ids.JobOnId || c.ReplacedJobOnId == ids.JobOnId));
        Assert.False(await db.MfContexts.AnyAsync(c => c.JobOnId == ids.JobOnId || c.ReplacedJobOnId == ids.JobOnId));
        Assert.False(await db.BqContexts.AnyAsync(c => c.JobOnId == ids.JobOnId || c.ReplacedJobOnId == ids.JobOnId));
        Assert.False(await db.Pesos.AnyAsync(p => p.ControloId == ids.ControloId));
        Assert.False(await db.Pegamentos.AnyAsync(p => p.ControloId == ids.ControloId));
        Assert.False(await db.Puncoes.AnyAsync(p => p.ControloId == ids.ControloId));
        Assert.False(await db.ControloDecisoes.AnyAsync(d => d.ControloId == ids.ControloId));
        Assert.False(await db.PesoComparacoes.AnyAsync(c => c.ControloId == ids.ControloId));
        Assert.False(await db.PesoComparacaoCmDecisoes.AnyAsync(d => d.Id == ids.ComparacaoCmDecisaoId));
        Assert.False(await db.JobOnChanges.AnyAsync(c => c.JobOnId == ids.JobOnId));
        var machine = "E-" + ids.ToolIds[0].Split('-')[0];
        Assert.False(await db.BqMovements.AnyAsync(m => m.MachineCodeFrozen == machine));
        var rasto = await db.BqMovementDeletions.Where(d => d.MachineCodeFrozen == machine).ToListAsync();
        Assert.Equal(2, rasto.Count);
        Assert.All(rasto, d => Assert.Equal(deletedBy, d.DeletedByNameFrozen));

        Assert.True(await db.JobOns.AnyAsync(j => j.Id == ids.OutraJobOnId));
        var outroPeso = await db.Pesos.SingleAsync(p => p.Id == ids.OutroPesoId);
        Assert.Null(outroPeso.ProducaoAnteriorControloId);
        Assert.Equal(ids.ToolIds.Count, await db.Tools.CountAsync(t => ids.ToolIds.Contains(t.ToolId)));
    }
}
