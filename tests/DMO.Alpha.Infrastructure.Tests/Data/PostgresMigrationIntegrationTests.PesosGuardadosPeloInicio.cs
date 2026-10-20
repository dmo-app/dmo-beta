using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Deep sweep 2 (18/10/2026): o «Comparar Pesos guardados» no PostgreSQL. A
/// referência da produção ou da CM pelo início dela ou da parte BQ
/// (ReferenceMatch), nunca a meio; o nº, a máquina e o lote por parte do
/// texto. O InMemory avalia o LINQ em C# e não apanha uma expressão que o
/// Npgsql não traduz. Os dados têm um prefixo de algarismos ao acaso, para a
/// base partilhada pelos testes não interferir; as máquinas, os nºs e a CM das
/// produções só têm letras, para não conterem o prefixo.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task PesosGuardados_AReferenciaPeloInicio_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var rInicio = $"{p}1C{p}";   // começa pelo prefixo
        var rMeio = $"9{p}5Z{p}";    // o prefixo só a meio; parte BQ «Z…»
        var machine = new Machine { Code = $"PG-{letras}" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = $"CM-PGI-{p}", Type = ToolType.Cm, Reference = $"K{letras}", Lot = "L" },
            new Tool { ToolId = $"CM-PGS-{p}", Type = ToolType.Cm, Reference = $"{p}7Y{p}", Lot = "L" },
            new Tool { ToolId = $"BQ-PGI-{p}", Type = ToolType.Bq, Reference = $"W{letras}", Lot = "L" });
        await db.SaveChangesAsync();

        // Por esta ordem (os ids crescem).
        var pesos = new List<Peso>();
        async Task<Peso> NovoPesoAsync(string cmToolId, int? controloId)
        {
            var peso = new Peso
            {
                CmToolId = cmToolId, BqToolId = $"BQ-PGI-{p}", Process = "NNPB",
                WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m,
                GlassDensityGramsPerCm3 = 2.5m, ControloId = controloId
            };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            pesos.Add(peso);
            return peso;
        }

        async Task<Peso> NovoPesoDaProducaoAsync(string reference, string number)
        {
            var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            db.Controlos.Add(controlo);
            await db.SaveChangesAsync();
            return await NovoPesoAsync($"CM-PGI-{p}", controlo.Id);
        }

        var inicio = await NovoPesoDaProducaoAsync(rInicio, $"NA{letras}");
        var meio = await NovoPesoDaProducaoAsync(rMeio, $"NB{letras}");
        var semProducao = await NovoPesoAsync($"CM-PGS-{p}", null);

        try
        {
            var comparacao = new PesoComparisonQuery(db, new PesoQuery(db));
            async Task<int[]> ListarAsync(string referencia) =>
                (await comparacao.ListarPesosPorReferenciaAsync(referencia)).Select(o => o.PesoId).ToArray();
            async Task<int[]> ProcurarAsync(string texto, string? referencia = null) =>
                (await comparacao.ProcurarPesosAsync(texto, referencia, 10)).Select(o => o.PesoId).ToArray();

            // O segundo passo pela referência: a da produção ou, sem ela, a da CM.
            Assert.Equal(new[] { semProducao.Id, inicio.Id }, await ListarAsync(p));
            Assert.Equal(new[] { inicio.Id }, await ListarAsync(rInicio.ToLowerInvariant()));
            Assert.Equal(new[] { meio.Id }, await ListarAsync($"z{p}"));
            Assert.Equal(new[] { semProducao.Id }, await ListarAsync($"y{p}"));
            Assert.Empty(await ListarAsync($"{p}5Z"));

            // A procura ao escrever: as referências pelo início; o nº por parte.
            Assert.Equal(new[] { semProducao.Id, inicio.Id }, await ProcurarAsync(p));
            Assert.Equal(new[] { meio.Id }, await ProcurarAsync($"z{p}"));
            Assert.Empty(await ProcurarAsync($"{p}5Z"));
            Assert.Equal(new[] { inicio.Id }, await ProcurarAsync($"a{letras}"));

            // Com a referência indicada, pelo início dela.
            Assert.Equal(new[] { inicio.Id }, await ProcurarAsync("k", p));
            Assert.Equal(new[] { semProducao.Id }, await ProcurarAsync("y", p));
            Assert.Empty(await ProcurarAsync("k", $"{p}5Z"));

            // As opções dos Pesos escolhidos (a página reabre com eles).
            var opcoes = await comparacao.OpcoesDosPesosAsync(new[] { inicio.Id, semProducao.Id });
            Assert.Equal(rInicio, Assert.Single(opcoes, o => o.PesoId == inicio.Id).Reference);
            Assert.Null(Assert.Single(opcoes, o => o.PesoId == semProducao.Id).Reference);
        }
        finally
        {
            db.ChangeTracker.Clear();
            var pesoIds = pesos.Select(x => x.Id).ToList();
            await db.Pesos.Where(x => pesoIds.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Controlos.Where(c => db.JobOns.Any(j => j.Id == c.JobOnId && j.MachineId == machine.Id)).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId.EndsWith(p)).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}
