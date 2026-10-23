using System.Data.Common;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// PERF-05: a produção corrente das Boquilhas no PostgreSQL. A base de dados
/// devolve as produções datadas que cobrem o dia e, das sem datas, só a
/// última (maior Id) de cada linha — nunca todas para a memória. O resultado
/// é o mesmo de antes: a datada ganha à sem datas, o dia da troca mostra as
/// duas sem aviso, a sobreposição avisa, a linha sem produção fica vazia.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    // 20/05/2030 às 12h (Lisboa, verão): o dia operacional é 20/05/2030.
    private static readonly DateOnly DiaBq = new(2030, 5, 20);
    private static readonly DateTimeOffset AgoraBq = new(2030, 5, 20, 11, 0, 0, TimeSpan.Zero);

    private sealed class RelogioBq(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }

    /// <summary>Guarda a consulta das candidatas (a que lê os Job Ons).</summary>
    private sealed class ConsultaDasCandidatas : DbCommandInterceptor
    {
        public string? Sql { get; private set; }
        public List<(string Nome, object? Valor)> Parametros { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"JobOns\"", StringComparison.Ordinal))
            {
                Sql = command.CommandText;
                Parametros.Clear();
                foreach (DbParameter p in command.Parameters)
                {
                    Parametros.Add((p.ParameterName, p.Value));
                }
            }

            return ValueTask.FromResult(result);
        }
    }

    [SkippableFact]
    public async Task BoquilhasCorrente_NoPostgres_EscolheAsMesmasProducoes()
    {
        SkipWithoutConnection();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "P5-" + Guid.NewGuid().ToString("N")[..8];
        var bqTool = marker + "-BQ";

        // A: só sem datas, várias (a última ganha; a sem BQ também conta).
        // B: sem datas e uma datada que cobre o dia (a datada ganha).
        // C: dia da troca (sai a 20, entra a 20) e uma sem datas: as duas
        //    datadas, sem aviso. D: sem produção. E: sobreposição real.
        // F: datada que acabou antes do dia e sem datas: a sem datas.
        var m = new[] { "A", "B", "C", "D", "E", "F" }
            .ToDictionary(x => x, x => new Machine { Code = marker + x });
        db.Machines.AddRange(m.Values);
        db.Tools.Add(new Tool { ToolId = bqTool, Type = ToolType.Bq, Reference = marker + "T194", Lot = "1" });
        JobOn Job(string linha, string numero, DateOnly? inicio = null, DateOnly? fim = null) =>
            new() { Machine = m[linha], Reference = marker, ProductionNumber = numero, InicioEm = inicio, FimEm = fim };
        var a1 = Job("A", "A1"); var a2 = Job("A", "A2"); var a3 = Job("A", "A3");
        db.JobOns.AddRange(a1, a2);
        await db.SaveChangesAsync();
        db.JobOns.Add(a3); // o maior Id da linha A, sem BQ
        db.JobOns.AddRange(
            Job("B", "B-sem"), Job("B", "B-datada", DiaBq.AddDays(-3), DiaBq.AddDays(3)),
            Job("C", "C-sai", DiaBq.AddDays(-5), DiaBq), Job("C", "C-entra", DiaBq, DiaBq.AddDays(5)), Job("C", "C-sem"),
            Job("E", "E1", DiaBq.AddDays(-2), DiaBq.AddDays(2)), Job("E", "E2", DiaBq.AddDays(-1), DiaBq.AddDays(1)), Job("E", "E-sem"),
            Job("F", "F-acabou", DiaBq.AddDays(-9), DiaBq.AddDays(-1)), Job("F", "F-sem1"), Job("F", "F-sem2"));
        await db.SaveChangesAsync();
        db.BqContexts.Add(new BqContext { JobOnId = a2.Id, ToolId = bqTool });
        await db.SaveChangesAsync();
        try
        {
            var linhas = (await new BoquilhasCurrentQuery(db, new NoRouteSettings(), new RelogioBq(AgoraBq)).ListAsync())
                .Where(l => l.MachineCode.StartsWith(marker, StringComparison.Ordinal))
                .ToList();
            string[] Producoes(string linha) => linhas.Where(l => l.MachineCode == marker + linha)
                .Select(l => l.ProductionNumber ?? "-").ToArray();

            Assert.Equal(["A3"], Producoes("A"));
            Assert.Null(linhas.Single(l => l.MachineCode == marker + "A").BqToolId);
            Assert.Equal(["B-datada"], Producoes("B"));
            Assert.Equal(["C-entra", "C-sai"], Producoes("C"));
            Assert.All(linhas.Where(l => l.MachineCode == marker + "C"), l => Assert.False(l.Overlap));
            Assert.Equal(["-"], Producoes("D"));
            Assert.Equal(["E2", "E1"], Producoes("E"));
            Assert.All(linhas.Where(l => l.MachineCode == marker + "E"), l => Assert.True(l.Overlap));
            Assert.Equal(["F-sem2"], Producoes("F"));

            // A sem datas mais antiga com BQ não ganha à última sem BQ; sem a
            // última, a anterior volta a ser a corrente com a sua BQ.
            await db.JobOns.Where(j => j.Id == a3.Id).ExecuteDeleteAsync();
            var a = (await new BoquilhasCurrentQuery(db, new NoRouteSettings(), new RelogioBq(AgoraBq)).ListAsync())
                .Single(l => l.MachineCode == marker + "A");
            Assert.Equal("A2", a.ProductionNumber);
            Assert.Equal(bqTool, a.BqToolId);
            Assert.Equal(marker + "T194", a.BqReference);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"BqContexts\" WHERE \"ToolId\" = {bqTool}");
            foreach (var machine in m.Values)
            {
                await DeleteJobOnAsync(db, machine.Id);
            }

            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" = {bqTool}");
        }
    }

    [SkippableFact]
    public async Task BoquilhasCorrente_NaoTrazTodasAsSemDatasParaAMemoria()
    {
        SkipWithoutConnection();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "P5N-" + Guid.NewGuid().ToString("N")[..8];
        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        for (var i = 0; i < 40; i++)
        {
            db.JobOns.Add(new JobOn { Machine = machine, Reference = marker, ProductionNumber = $"{marker}-{i:00}" });
        }

        await db.SaveChangesAsync();
        try
        {
            var consulta = new ConsultaDasCandidatas();
            await using (var ctx = CreateContextWith(consulta))
            {
                var linha = (await new BoquilhasCurrentQuery(ctx, new NoRouteSettings(), new RelogioBq(AgoraBq)).ListAsync())
                    .Single(l => l.MachineId == machine.Id);
                Assert.Equal($"{marker}-39", linha.ProductionNumber);
            }

            // A mesma consulta, só as linhas desta máquina: uma (a última),
            // não as 40 sem datas.
            Assert.NotNull(consulta.Sql);
            await using var conn = new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder(Connection!) { Pooling = false }.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM ({consulta.Sql}) AS q WHERE q.\"MachineId\" = {machine.Id}";
            foreach (var (nome, valor) in consulta.Parametros)
            {
                cmd.Parameters.Add(new Npgsql.NpgsqlParameter(nome, valor ?? DBNull.Value));
            }

            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await DeleteJobOnAsync(db, machine.Id);
        }
    }
}
