using System.Data.Common;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Sweep de 19/10/2026, J-4: o Criar e o Editar do Job On verificavam a
    // CM, o MF e a BQ com uma consulta às ferramentas cada (três por
    // gravação). Agora é uma só, com os três tool_id.
    [SkippableFact]
    public async Task JobOn_CriarEEditar_VerificamAsTresFerramentasNumaSoConsulta()
    {
        SkipWithoutConnection();
        var marker = "J4-" + Guid.NewGuid().ToString("N")[..8];
        var (cm, mf, bq) = (marker + "-CM", marker + "-MF", marker + "-BQ");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var machine = new Machine { Code = marker };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = cm, Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = mf, Type = ToolType.Mf, Reference = "5447", Lot = "1" },
            new Tool { ToolId = bq, Type = ToolType.Bq, Reference = "5447T173", Lot = "1" });
        await db.SaveChangesAsync();
        try
        {
            var criar = new ConsultasAsFerramentas();
            int jobOnId;
            await using (var ctx = CreateContextWith(criar))
            {
                var created = await new CreateJobOnHandler(ctx).HandleAsync(new CreateJobOn(marker, "5447T173", machine.Id, cm, mf, bq));
                Assert.Equal(CreateJobOnResult.Success, created.Result);
                jobOnId = created.JobOn!.JobOnId;
            }

            Assert.Equal(1, criar.Vezes);

            var editar = new ConsultasAsFerramentas();
            await using (var ctx = CreateContextWith(editar))
            {
                var edited = await new UpdateJobOnHandler(ctx).HandleAsync(new UpdateJobOn(jobOnId, machine.Id, cm, mf, bq, CaloteMm: 2m));
                Assert.Equal(UpdateJobOnResult.Success, edited.Result);
            }

            Assert.Equal(1, editar.Vezes);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"JobOnChanges\" WHERE \"JobOnId\" IN (SELECT \"Id\" FROM \"JobOns\" WHERE \"MachineId\" = {machine.Id})");
            foreach (var table in new[] { "CmContexts", "MfContexts", "BqContexts" })
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM \"{table}\" WHERE \"ToolId\" IN ({{0}}, {{1}}, {{2}})", cm, mf, bq);
            }

            await DeleteJobOnAsync(db, machine.Id);
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" IN ({cm}, {mf}, {bq})");
        }
    }

    /// <summary>Conta as consultas que leem a tabela das ferramentas.</summary>
    private sealed class ConsultasAsFerramentas : DbCommandInterceptor
    {
        public int Vezes { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Tools\"", StringComparison.Ordinal))
            {
                Vezes++;
            }

            return ValueTask.FromResult(result);
        }
    }
}
