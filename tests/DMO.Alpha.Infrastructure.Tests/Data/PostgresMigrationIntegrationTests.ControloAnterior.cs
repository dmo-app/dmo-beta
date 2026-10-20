using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// O «Controlo anterior» da Revisão (sweep de 19/10/2026, C-2; o
/// especialista, R29): com as datas livres, uma produção que começou antes
/// pode ser registada depois. A Revisão escolhia pela ordem de registo e
/// mostrava como anterior uma produção que começou depois, ou uma de uma
/// máquina que a ficha da CM não aceita. Agora segue a regra da comparação do
/// Peso: as que começaram antes (no mesmo dia, as registadas antes), só nas
/// máquinas da ficha da CM (sem máquinas, toda a referência), a mais recente
/// primeiro. No PostgreSQL, porque os null vêm primeiro num DESC.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ControloAnterior_PeloInicioEPelasMaquinasDaCm_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var comFicha = $"{p}5C{p}";
        var semFicha = $"{p}6K{p}";
        var daFicha = new Machine { Code = $"CA-{letras}" };
        var foraDaFicha = new Machine { Code = $"CB-{letras}" };
        db.Machines.AddRange(daFicha, foraDaFicha);
        db.Tools.AddRange(
            new Tool { ToolId = $"CM-ANT-{p}", Type = ToolType.Cm, Reference = p, Lot = "1" },
            new Tool { ToolId = $"CM-LIVRE-{p}", Type = ToolType.Cm, Reference = p, Lot = "2" },
            new Tool { ToolId = $"BQ-ANT-{p}", Type = ToolType.Bq, Reference = $"C{p}", Lot = "1" });
        await db.SaveChangesAsync();
        // A CM-ANT só trabalha na máquina da ficha; a CM-LIVRE não tem máquinas.
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = $"CM-ANT-{p}", MachineId = daFicha.Id });
        await db.SaveChangesAsync();

        var controlos = new List<int>();
        // Por esta ordem (os ids crescem): a ordem de registo.
        async Task<JobOn> NovaProducaoAsync(string reference, string number, DateOnly? inicio, Machine machine, string cm, bool comPeso = true)
        {
            var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id, InicioEm = inicio };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = cm });
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            controlos.Add(controlo.Id);
            if (comPeso)
            {
                db.Pesos.Add(new Peso
                {
                    CmToolId = cm, BqToolId = $"BQ-ANT-{p}", Process = "NNPB",
                    WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m,
                    GlassDensityGramsPerCm3 = 2.5m, ControloId = controlo.Id,
                    Measurements = { new PesoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 200m } }
                });
                await db.SaveChangesAsync();
            }

            return jobOn;
        }

        var cm = $"CM-ANT-{p}";
        var primeira = await NovaProducaoAsync(comFicha, "C-0110", new DateOnly(2026, 10, 1), daFicha, cm);
        await NovaProducaoAsync(comFicha, "C-0103-FORA", new DateOnly(2026, 10, 3), foraDaFicha, cm);
        var registadaAntesComecaDepois = await NovaProducaoAsync(comFicha, "C-1010", new DateOnly(2026, 10, 10), daFicha, cm);
        var atual = await NovaProducaoAsync(comFicha, "C-0105", new DateOnly(2026, 10, 5), daFicha, cm);
        var mesmoDiaDepois = await NovaProducaoAsync(comFicha, "C-0105-DEPOIS", new DateOnly(2026, 10, 5), daFicha, cm);

        // Sem início na atual: as registadas antes, primeiro as que têm início;
        // com a ficha da CM sem máquinas, conta toda a referência.
        var livre = $"CM-LIVRE-{p}";
        await NovaProducaoAsync(semFicha, "K-0901-FORA", new DateOnly(2026, 9, 1), foraDaFicha, livre);
        await NovaProducaoAsync(semFicha, "K-SEM", null, daFicha, livre);
        var atualSemInicio = await NovaProducaoAsync(semFicha, "K-ATUAL", null, daFicha, livre, comPeso: false);

        try
        {
            var revisao = new ControloRevisaoQuery(db);
            async Task<string?> AnteriorAsync(JobOn jobOn) =>
                (await revisao.FindPreviousWithPesoAsync(jobOn.Id))?.ProductionNumber;

            // Nem a que começou depois (registada antes), nem a do mesmo dia
            // registada depois, nem a da máquina fora da ficha.
            Assert.Equal("C-0110", await AnteriorAsync(atual));
            // A atual, registada depois, é anterior à que começou a 10/10: a
            // mais recente das que começaram antes, no mesmo dia a registada
            // depois.
            Assert.Equal("C-0105-DEPOIS", await AnteriorAsync(registadaAntesComecaDepois));
            Assert.Equal("C-0105", await AnteriorAsync(mesmoDiaDepois));
            Assert.Null(await AnteriorAsync(primeira));
            Assert.Equal("K-0901-FORA", await AnteriorAsync(atualSemInicio));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.PesoMeasurements.Where(m => m.Peso.ControloId != null && controlos.Contains(m.Peso.ControloId.Value)).ExecuteDeleteAsync();
            await db.Pesos.Where(x => x.ControloId != null && controlos.Contains(x.ControloId.Value)).ExecuteDeleteAsync();
            await db.Controlos.Where(c => controlos.Contains(c.Id)).ExecuteDeleteAsync();
            await db.CmContexts.Where(c => c.ToolId == cm || c.ToolId == livre).ExecuteDeleteAsync();
            await db.ToolMachineLinks.Where(l => l.ToolId == cm).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == daFicha.Id || j.MachineId == foraDaFicha.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId.EndsWith(p)).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == daFicha.Id || m.Id == foraDaFicha.Id).ExecuteDeleteAsync();
        }
    }
}
