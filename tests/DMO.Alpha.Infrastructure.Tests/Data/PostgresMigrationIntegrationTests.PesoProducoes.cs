using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-11 e F-12): as produções da folha do Peso no
/// PostgreSQL. A «Produção anterior» só com as anteriores à atual, as últimas
/// 5; no PostgreSQL os null vêm primeiro num DESC, e o InMemory (que os põe no
/// fim) não o apanha. A «Produção a comparar» com as últimas 5 cortadas na
/// base, só as anteriores à atual com o mesmo critério (a resposta R18), e o
/// Peso pedido mais antigo ou posterior. Os dados têm um prefixo ao acaso,
/// para a base partilhada pelos testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task PesoProducoes_AnteriorEAComparar_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var comInicio = $"{p}1Y{p}";
        var semInicio = $"{p}2T{p}";
        var comparaSemInicio = $"{p}3K{p}";
        var permitida = new Machine { Code = $"PA-{letras}" };
        var outraMaquina = new Machine { Code = $"PB-{letras}" };
        db.Machines.AddRange(permitida, outraMaquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"CM-PES-{p}", Type = ToolType.Cm, Reference = p, Lot = "1" },
            new Tool { ToolId = $"BQ-PES-{p}", Type = ToolType.Bq, Reference = $"Y{p}", Lot = "1" });
        await db.SaveChangesAsync();
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = $"CM-PES-{p}", MachineId = permitida.Id });
        await db.SaveChangesAsync();

        // Por esta ordem (os ids crescem).
        async Task<JobOn> NovoJobOnAsync(string reference, string number, DateOnly? inicio, Machine? machine = null)
        {
            var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = (machine ?? permitida).Id, InicioEm = inicio };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            return jobOn;
        }

        // A «Produção anterior» com início na atual: nem a posterior, nem a sem datas.
        await NovoJobOnAsync(comInicio, "2020090", new DateOnly(2020, 11, 2));
        await NovoJobOnAsync(comInicio, "2020091", null);
        var atualComInicio = await NovoJobOnAsync(comInicio, "2021017", new DateOnly(2021, 1, 11));
        await NovoJobOnAsync(comInicio, "2026138", new DateOnly(2026, 10, 19));
        await NovoJobOnAsync(comInicio, "2026153", null);
        await NovoJobOnAsync(comInicio, "2020100", new DateOnly(2020, 12, 7));

        // Sem início na atual: as registadas antes, as sem início no fim.
        await NovoJobOnAsync(semInicio, "202502", null);
        await NovoJobOnAsync(semInicio, "202503", null);
        await NovoJobOnAsync(semInicio, "202616", new DateOnly(2026, 9, 9));
        var atualSemInicio = await NovoJobOnAsync(semInicio, "202617", null);
        await NovoJobOnAsync(semInicio, "202618", new DateOnly(2026, 10, 16));

        // A «Produção a comparar»: 7 produções com Peso na máquina permitida,
        // uma noutra máquina, a atual e, depois dela, três posteriores (deep
        // sweep 2, F-12): uma mais tarde, uma no mesmo dia e uma sem início.
        var comPeso = new List<JobOn>();
        foreach (var n in Enumerable.Range(1, 7))
        {
            comPeso.Add(await NovoJobOnAsync(comInicio, $"P-20260{n}", new DateOnly(2026, n, 1)));
        }
        comPeso.Add(await NovoJobOnAsync(comInicio, "P-202608", new DateOnly(2026, 8, 1), outraMaquina));
        var atualComPeso = await NovoJobOnAsync(comInicio, "P-202609", new DateOnly(2026, 9, 1));
        comPeso.Add(atualComPeso);
        var posterior = await NovoJobOnAsync(comInicio, "P-202610", new DateOnly(2026, 10, 1));
        comPeso.Add(posterior);
        comPeso.Add(await NovoJobOnAsync(comInicio, "P-202609-DEPOIS", new DateOnly(2026, 9, 1)));
        comPeso.Add(await NovoJobOnAsync(comInicio, "P-SEM-INICIO", null));

        // Sem início na atual: as registadas antes, as sem início no fim (no
        // PostgreSQL vinham primeiro num DESC e tiravam o lugar às datadas).
        var semInicioComPeso = new List<JobOn>
        {
            await NovoJobOnAsync(comparaSemInicio, "Q-SEM-1", null),
            await NovoJobOnAsync(comparaSemInicio, "Q-2026-01", new DateOnly(2026, 1, 5)),
            await NovoJobOnAsync(comparaSemInicio, "Q-SEM-2", null),
            await NovoJobOnAsync(comparaSemInicio, "Q-2026-02", new DateOnly(2026, 2, 5)),
            await NovoJobOnAsync(comparaSemInicio, "Q-2026-03", new DateOnly(2026, 3, 5)),
            await NovoJobOnAsync(comparaSemInicio, "Q-2026-04", new DateOnly(2026, 4, 5))
        };
        var atualSemInicioComPeso = await NovoJobOnAsync(comparaSemInicio, "Q-ATUAL", null);
        semInicioComPeso.Add(atualSemInicioComPeso);
        var posteriorSemInicio = await NovoJobOnAsync(comparaSemInicio, "Q-DEPOIS", new DateOnly(2025, 12, 1));
        semInicioComPeso.Add(posteriorSemInicio);
        semInicioComPeso.Add(await NovoJobOnAsync(comparaSemInicio, "Q-SEM-DEPOIS", null));
        comPeso.AddRange(semInicioComPeso);

        var pesos = new List<Peso>();
        foreach (var jobOn in comPeso)
        {
            db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"CM-PES-{p}" });
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            var peso = new Peso
            {
                CmToolId = $"CM-PES-{p}", BqToolId = $"BQ-PES-{p}", Process = "NNPB",
                WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.5m, ControloId = controlo.Id
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            db.PesoMeasurements.Add(new PesoMeasurement { PesoId = peso.Id, RowPosition = 1, CmSubject = "1", WaterWeightGrams = 200m });
            await db.SaveChangesAsync();
            pesos.Add(peso);
        }
        Peso PesoDe(JobOn jobOn) => pesos[comPeso.IndexOf(jobOn)];
        var controloAtual = PesoDe(atualComPeso).ControloId!.Value;

        try
        {
            var anterior = new PesoProducaoAnteriorQuery(db);
            async Task<string[]> AnterioresAsync(int jobOnId) =>
                (await anterior.ListOpcoesAsync(jobOnId)).Select(o => o.ProductionNumber).ToArray();
            Assert.Equal(new[] { "2020100", "2020090" }, await AnterioresAsync(atualComInicio.Id));
            Assert.Equal(new[] { "202616", "202503", "202502" }, await AnterioresAsync(atualSemInicio.Id));
            // As últimas 5 (a máquina não conta para a anterior).
            Assert.Equal(
                new[] { "P-202608", "P-202607", "P-202606", "P-202605", "P-202604" },
                await AnterioresAsync(atualComPeso.Id));

            var comparar = new PesoComparisonQuery(db, new PesoQuery(db));
            var ultimas = await comparar.FindCandidatesAsync(controloAtual);
            Assert.Equal(
                new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603" },
                ultimas.Select(c => c.ProductionNumber));
            Assert.All(ultimas, c => Assert.Equal(1, c.ReadingCount));
            Assert.Equal(
                new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603", "P-202601" },
                (await comparar.FindCandidatesAsync(controloAtual, pesos[0].Id)).Select(c => c.ProductionNumber));
            // O da outra máquina (fora da ficha da CM) não entra, mesmo pedido.
            Assert.Equal(5, (await comparar.FindCandidatesAsync(controloAtual, pesos[7].Id)).Count);
            // A posterior pedida (uma comparação já guardada) fica, no fim.
            Assert.Equal(
                new[] { "P-202607", "P-202606", "P-202605", "P-202604", "P-202603", "P-202610" },
                (await comparar.FindCandidatesAsync(controloAtual, PesoDe(posterior).Id)).Select(c => c.ProductionNumber));

            // Sem início na atual: as registadas antes, com as sem início no fim.
            var controloSemInicio = PesoDe(atualSemInicioComPeso).ControloId!.Value;
            var semInicioUltimas = new[] { "Q-2026-04", "Q-2026-03", "Q-2026-02", "Q-2026-01", "Q-SEM-2" };
            Assert.Equal(
                semInicioUltimas,
                (await comparar.FindCandidatesAsync(controloSemInicio)).Select(c => c.ProductionNumber));
            Assert.Equal(
                semInicioUltimas.Append("Q-DEPOIS"),
                (await comparar.FindCandidatesAsync(controloSemInicio, PesoDe(posteriorSemInicio).Id)).Select(c => c.ProductionNumber));
            Assert.Equal(
                semInicioUltimas.Append("Q-SEM-1"),
                (await comparar.FindCandidatesAsync(controloSemInicio, PesoDe(semInicioComPeso[0]).Id)).Select(c => c.ProductionNumber));
        }
        finally
        {
            db.ChangeTracker.Clear();
            var pesoIds = pesos.Select(x => x.Id).ToList();
            await db.PesoMeasurements.Where(m => pesoIds.Contains(m.PesoId)).ExecuteDeleteAsync();
            await db.Pesos.Where(x => pesoIds.Contains(x.Id)).ExecuteDeleteAsync();
            var controloIds = pesos.Select(x => x.ControloId!.Value).ToList();
            await db.Controlos.Where(c => controloIds.Contains(c.Id)).ExecuteDeleteAsync();
            await db.CmContexts.Where(c => c.ToolId == $"CM-PES-{p}").ExecuteDeleteAsync();
            await db.ToolMachineLinks.Where(l => l.ToolId == $"CM-PES-{p}").ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == permitida.Id || j.MachineId == outraMaquina.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId.EndsWith(p)).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == permitida.Id || m.Id == outraMaquina.Id).ExecuteDeleteAsync();
        }
    }
}
