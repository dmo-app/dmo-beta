using System.Data;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Aplica a migração Baseline contra um PostgreSQL real quando a ligação de
/// teste está configurada através da variável de ambiente
/// DMO_POSTGRES_CONNECTION, e prova o schema e os handlers que dependem de
/// constraints do PostgreSQL. Só corre numa base descartável cujo nome
/// contenha «test»; a base do DEV no Supabase chama-se «postgres» e é
/// recusada. Sem ligação configurada, os testes saltam — nunca inventam
/// credenciais nem alvos de base de dados.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");

    private static DmoDbContext CreateContext()
    {
        // O pool de ligações global do Npgsql é partilhado por todos os testes
        // desta classe (mesma cadeia de ligação). Estes testes provocam
        // DELIBERADAMENTE erros do servidor (23505 no índice único parcial) e
        // recuam/aplicam migrations com DDL; uma ligação devolvida ao pool
        // nesse estado pode ser reutilizada pelo teste seguinte, que então
        // falha na primeira operação com "Exception while reading from stream"
        // — uma corrida dependente da ordem de execução, não um defeito do
        // produto. Sem pool, cada teste obtém sempre uma ligação limpa.
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(Connection!)
        {
            Pooling = false
        };

        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;

        return new DmoDbContext(options);
    }

    private static void SkipWithoutConnection()
    {
        Skip.If(
            string.IsNullOrWhiteSpace(Connection),
            "DMO_POSTGRES_CONNECTION is not configured (disposable PostgreSQL test database).");
        Skip.IfNot(
            IsDisposableTestDatabase(Connection!),
            "DMO_POSTGRES_CONNECTION must name a disposable database containing «test»: these tests migrate down to InitialCreate.");
    }

    internal static bool IsDisposableTestDatabase(string connection) =>
        new Npgsql.NpgsqlConnectionStringBuilder(connection).Database is { } database
        && database.Contains("test", StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("Host=localhost;Database=dmo_test;Username=postgres", true)]
    [InlineData("Host=db.example.supabase.co;Database=postgres;Username=postgres", false)]
    [InlineData("Host=localhost;Username=postgres", false)]
    public void OnlyDisposableTestDatabasesAreMigrated(string connection, bool expected)
    {
        Assert.Equal(expected, IsDisposableTestDatabase(connection));
    }

    /// <summary>Sem chaves de config: as saídas deste teste gravam sem reparador.</summary>
    private sealed class NoRouteSettings : IModuleSettingsQuery
    {
        public Task<string?> FindValueAsync(string settingKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    [SkippableFact]
    public async Task Migrate_AppliesBaseline_AndCreatesTheCanonicalSchema()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();        await db.Database.MigrateAsync();

        // Uma base nova começa na Baseline e aplica as migrações a seguir a ela.
        var applied = await db.Database.GetAppliedMigrationsAsync();

        Assert.Single(applied, m => m.EndsWith("_Baseline"));
        Assert.Contains(applied, m => m.EndsWith("_AddBqDiscrepancyReset"));

        // A discrepância posta a zero (17/10/2026): as duas colunas do registo.
        var resetColumns = await db.Database
            .SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'BqRepairTraces'
                  AND column_name IN ('DiscrepancyResetAtUtc', 'DiscrepancyResetByNameFrozen')
                """)
            .ToListAsync();
        Assert.Equal(2, resetColumns.Count);

        // Uma produção é a referência + o nº (17/10/2026): o índice único
        // passou do nº sozinho para o par.
        Assert.Contains(applied, m => m.EndsWith("_JobOnReferenceProductionNumberUnique"));
        var jobOnIndexes = await db.Database
            .SqlQuery<string>($"""
                SELECT indexname || ' ' || indexdef AS "Value" FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'JobOns'
                  AND indexname IN ('IX_JobOns_ProductionNumber', 'IX_JobOns_Reference_ProductionNumber')
                """)
            .ToListAsync();
        Assert.Equal(2, jobOnIndexes.Count);
        Assert.Contains(jobOnIndexes, i => i.StartsWith("IX_JobOns_Reference_ProductionNumber ") && i.Contains("UNIQUE"));
        Assert.Contains(jobOnIndexes, i => i.StartsWith("IX_JobOns_ProductionNumber ") && !i.Contains("UNIQUE"));

        // Um Peso por controlo para cada CM+BQ (18/10/2026): o ControloId
        // deixou de ser único; o único é o trio, sem as linhas REMOVED.
        Assert.Contains(applied, m => m.EndsWith("_PesoPerToolCombination"));
        var pesoIndexes = await db.Database
            .SqlQuery<string>($"""
                SELECT indexname || ' ' || indexdef AS "Value" FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'Pesos'
                  AND indexname IN ('IX_Pesos_ControloId', 'IX_Pesos_ControloId_CmToolId_BqToolId')
                """)
            .ToListAsync();
        Assert.Equal(2, pesoIndexes.Count);
        Assert.Contains(pesoIndexes, i => i.StartsWith("IX_Pesos_ControloId ") && !i.Contains("UNIQUE"));
        Assert.Contains(pesoIndexes, i => i.StartsWith("IX_Pesos_ControloId_CmToolId_BqToolId ")
            && i.Contains("UNIQUE") && i.Contains("'REMOVED'"));

        // Um bq_id tem um único trace: índice único persistido no PostgreSQL.
        var traceIndex = await db.Database
            .SqlQuery<string>($"""
                SELECT indexdef AS "Value" FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'BqRepairTraces'
                  AND indexname = 'IX_BqRepairTraces_BqContextId'
                """)
            .ToListAsync();
        Assert.Single(traceIndex);
        Assert.Contains("UNIQUE", traceIndex[0]);
        Assert.Contains("\"BqContextId\"", traceIndex[0]);

        // Índice único parcial sobre ToolId restrito às linhas não
        // resolvidas: no máximo um trace pré-produção não resolvido por
        // tool_id ("BqContextId" IS NULL). Traces históricos associados têm
        // a âncora limpa e não colidem com este índice.
        var toolIndex = await db.Database
            .SqlQuery<string>($"""
                SELECT indexdef AS "Value" FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'BqRepairTraces'
                  AND indexname = 'IX_BqRepairTraces_ToolId'
                """)
            .ToListAsync();
        Assert.Single(toolIndex);
        Assert.Contains("UNIQUE", toolIndex[0]);
        Assert.Contains("\"BqContextId\" IS NULL", toolIndex[0]);

        // PK da Tool é o tool_id textual emitido pelo backend.
        var toolPrimaryKey = await db.Database
            .SqlQuery<string>($"""
                SELECT constraint_type AS "Value" FROM information_schema.table_constraints
                WHERE table_schema = current_schema()
                  AND constraint_name = 'PK_Tools'
                """)
            .ToListAsync();
        Assert.Equal(["PRIMARY KEY"], toolPrimaryKey);

        // Storage corrente do tipo de movimento: tokens textuais.
        var movementType = await db.Database
            .SqlQuery<string>($"""
                SELECT data_type AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'BqMovements'
                  AND column_name = 'Type'
                """)
            .ToListAsync();
        Assert.Equal(["text"], movementType);

        var movementTypeNullability = await db.Database
            .SqlQuery<string>($"""
                SELECT is_nullable AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'BqMovements'
                  AND column_name = 'Type'
                """)
            .ToListAsync();
        Assert.Equal(["NO"], movementTypeNullability);

        // Discrepância: facto histórico opcional (null = sem discrepância).
        var discrepancyNullability = await db.Database
            .SqlQuery<string>($"""
                SELECT is_nullable AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'BqMovements'
                  AND column_name = 'Discrepancy'
                """)
            .ToListAsync();
        Assert.Equal(["YES"], discrepancyNullability);

        // Trace → Tool opcional (âncora direta limpa na associação) e
        // trace → BqContext opcional existem exactamente como constraints
        // de base de dados.
        var traceForeignKeys = await db.Database
            .SqlQuery<string>($"""
                SELECT constraint_name AS "Value" FROM information_schema.table_constraints
                WHERE table_schema = current_schema()
                  AND table_name = 'BqRepairTraces'
                  AND constraint_type = 'FOREIGN KEY'
                """)
            .ToListAsync();
        Assert.Contains("FK_BqRepairTraces_Tools_ToolId", traceForeignKeys);
        Assert.Contains("FK_BqRepairTraces_BqContexts_BqContextId", traceForeignKeys);
        Assert.Equal(2, traceForeignKeys.Count);
    }

    [SkippableFact]
    public async Task WritePath_AgainstPostgres_MatchesCanonicalModelSemantics()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createHandler = new CreateBqRepairTraceHandler(db);
        var movementHandler = new RegisterBqMovementHandler(db, new NoRouteSettings());
        var associateHandler = new AssociateBqRepairTraceToContextHandler(db);

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var toolId = $"BQ-TOOL-PG-{suffix}";

        // Registo canónico da Tool (tool_id emitido pelo backend).
        db.Tools.Add(new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = "PG-WRITE-PATH",
            Lot = "1"
        });
        await db.SaveChangesAsync();

        try
        {
            // Trace pré-produção ancorado ao tool_id, bq_id não resolvido.
            var created = await createHandler.HandleAsync(new CreateBqRepairTrace(toolId));
            Assert.Equal(CreateBqRepairTraceResult.Success, created.Result);
            var traceId = created.Trace!.BqRepairTraceId;

            // Movimentos com a quantidade observada na totalidade e a
            // discrepância produzida como facto histórico.
            var saida = await movementHandler.HandleAsync(new RegisterBqMovement(traceId, "saida", 5));
            Assert.Equal(RegisterBqMovementResult.Success, saida.Result);

            var entrada = await movementHandler.HandleAsync(new RegisterBqMovement(traceId, "entrada", 7));
            Assert.Equal(RegisterBqMovementResult.Success, entrada.Result);
            Assert.Equal(7, entrada.Movement!.Quantity);
            Assert.Equal(-2, entrada.Movement.Discrepancy);

            // Contexto de produção BQ.
            var machine = new Machine { Code = $"PG-{suffix}" };
            var jobOn = new JobOn
            {
                Reference = "PG-WRITE-PATH",
                ProductionNumber = $"PG-PN-{suffix}",
                Machine = machine
            };
            var bq = new BqContext { JobOn = jobOn, ToolId = toolId };
            db.JobOns.Add(jobOn);
            db.BqContexts.Add(bq);
            await db.SaveChangesAsync();

            // O mesmo trace passa a referenciar o seu bq_id e a âncora
            // direta tool_id é limpa.
            var associated = await associateHandler.HandleAsync(
                new AssociateBqRepairTraceToContext(traceId, bq.Id));
            Assert.Equal(AssociateBqRepairTraceToContextResult.Success, associated.Result);
            // O outcome reporta o tool_id EFETIVO (via bq_id → BqContext.ToolId);
            // a âncora DIRETA persistida é limpa (verificada abaixo na entidade).
            Assert.Equal(toolId, associated.Trace!.ToolId);

            // Após a associação a âncora direta foi limpa, pelo que o trace
            // associado já não conta como "não resolvido" para o tool_id:
            // uma nova criação abre um trace pré-produção novo (Success).
            // Um bq_id tem um único bq_repair_trace_id: associar este novo
            // trace ao mesmo bq_id é uma recusa tipada limpa.
            var second = await createHandler.HandleAsync(new CreateBqRepairTrace(toolId));
            Assert.Equal(CreateBqRepairTraceResult.Success, second.Result);
            var refused = await associateHandler.HandleAsync(
                new AssociateBqRepairTraceToContext(second.Trace!.BqRepairTraceId, bq.Id));
            Assert.Equal(AssociateBqRepairTraceToContextResult.BqContextAlreadyTraced, refused.Result);

            // Os tokens persistem como texto canónico no PostgreSQL.
            var storedTypes = await db.Database
                .SqlQuery<string>(
                    $"SELECT \"Type\" AS \"Value\" FROM \"BqMovements\" WHERE \"BqRepairTraceId\" = {traceId} ORDER BY \"Id\"")
                .ToListAsync();
            Assert.Equal(["saida", "entrada"], storedTypes);

            // Leitura relacional: o movimento resolve o trace e, através do
            // bq_id de produção, o tool_id canónico (a âncora direta foi
            // limpa na associação).
            var resolved = await db.BqMovements
                .AsNoTracking()
                .Include(m => m.Trace)
                .ThenInclude(t => t.BqContext!)
                .SingleAsync(m => m.Id == entrada.Movement.MovementId);
            Assert.Equal(traceId, resolved.BqRepairTraceId);
            Assert.Null(resolved.Trace.ToolId);
            Assert.Equal(toolId, resolved.Trace.BqContext!.ToolId);
            Assert.Equal(bq.Id, resolved.Trace.BqContextId);
        }
        finally
        {
            // Limpeza explícita em ordem de dependência: o FK trace → Tool é
            // agora opcional e já NÃO arrasta os traces, pelo que os traces
            // (e os seus movimentos, por cascade) são removidos primeiro —
            // incluindo o trace associado cuja âncora direta foi limpa e que
            // só se identifica via bq_id → BqContext.ToolId. Depois a Tool;
            // depois as produções e por fim a Machine.
            var traces = await db.BqRepairTraces
                .Include(t => t.BqContext)
                .Where(t => t.ToolId == toolId
                    || (t.BqContext != null && t.BqContext.ToolId == toolId))
                .ToListAsync();
            db.BqRepairTraces.RemoveRange(traces);
            await db.SaveChangesAsync();

            // As produções ligam-se à ferramenta (FK): saem antes dela.
            await db.BqContexts.Where(c => c.ToolId == toolId).ExecuteDeleteAsync();
            db.ChangeTracker.Clear();

            var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
            db.Tools.Remove(tool);

            // A máquina já não apaga as produções em cascata: saem antes.
            await db.JobOns.Where(j => j.Machine.Code == $"PG-{suffix}").ExecuteDeleteAsync();
            var machine = await db.Machines.SingleAsync(m => m.Code == $"PG-{suffix}");
            db.Machines.Remove(machine);

            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task PartialUniqueIndex_EnforcesOneUnresolvedPerTool_AllowsMultipleAssociated()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var toolId = $"BQ-TOOL-IDX-{suffix}";

        // Registo canónico da Tool.
        db.Tools.Add(new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = "PG-INDEX-TEST",
            Lot = "1"
        });
        await db.SaveChangesAsync();

        try
        {
            // Primeira inserção de trace não resolvido (ToolId definido, BqContextId null) deve succeed.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"BqRepairTraces\" (\"ToolId\", \"BqContextId\") VALUES ({toolId}, NULL)");

            // Segunda inserção de trace não resolvido para o mesmo tool_id deve falhar com unique violation.
            Npgsql.PostgresException? caughtEx = null;
            try
            {
                await db.Database.ExecuteSqlAsync(
                    $"INSERT INTO \"BqRepairTraces\" (\"ToolId\", \"BqContextId\") VALUES ({toolId}, NULL)");
            }
            catch (Npgsql.PostgresException ex)
            {
                caughtEx = ex;
            }
            
            Assert.NotNull(caughtEx);
            Assert.Equal("23505", caughtEx!.SqlState); // unique_violation
            Assert.Contains("IX_BqRepairTraces_ToolId", caughtEx.Message);

            // Criar contextos de produção para testar traces associados usando raw SQL.
            // Inserir Machine.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Machines\" (\"Code\") VALUES ({($"PG-IDX-{suffix}")}) RETURNING \"Id\"");
            var machineId = await db.Database
                .SqlQuery<int>($"SELECT \"Id\" AS \"Value\" FROM \"Machines\" WHERE \"Code\" = {($"PG-IDX-{suffix}")}")
                .SingleAsync();

            // Inserir JobOns.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"JobOns\" (\"Reference\", \"ProductionNumber\", \"MachineId\") VALUES ('PG-INDEX-TEST', {($"PG-PN-IDX1-{suffix}")}, {machineId})");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"JobOns\" (\"Reference\", \"ProductionNumber\", \"MachineId\") VALUES ('PG-INDEX-TEST', {($"PG-PN-IDX2-{suffix}")}, {machineId})");

            var jobOn1Id = await db.Database
                .SqlQuery<int>($"SELECT \"Id\" AS \"Value\" FROM \"JobOns\" WHERE \"ProductionNumber\" = {($"PG-PN-IDX1-{suffix}")}")
                .SingleAsync();
            var jobOn2Id = await db.Database
                .SqlQuery<int>($"SELECT \"Id\" AS \"Value\" FROM \"JobOns\" WHERE \"ProductionNumber\" = {($"PG-PN-IDX2-{suffix}")}")
                .SingleAsync();

            // Inserir BqContexts.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"BqContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOn1Id}, {toolId})");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"BqContexts\" (\"JobOnId\", \"ToolId\") VALUES ({jobOn2Id}, {toolId})");

            var bq1Id = await db.Database
                .SqlQuery<int>($"SELECT \"Id\" AS \"Value\" FROM \"BqContexts\" WHERE \"JobOnId\" = {jobOn1Id}")
                .SingleAsync();
            var bq2Id = await db.Database
                .SqlQuery<int>($"SELECT \"Id\" AS \"Value\" FROM \"BqContexts\" WHERE \"JobOnId\" = {jobOn2Id}")
                .SingleAsync();

            // Inserir múltiplos traces associados (ToolId = NULL, BqContextId definido) para a mesma Tool.
            // O índice único parcial não cobre estas linhas (WHERE "BqContextId" IS NULL), portanto devem succeed.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"BqRepairTraces\" (\"ToolId\", \"BqContextId\") VALUES (NULL, {bq1Id})");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"BqRepairTraces\" (\"ToolId\", \"BqContextId\") VALUES (NULL, {bq2Id})");

            // Verificar que ambos os traces associados existem.
            var associatedCount = await db.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*) AS "Value" FROM "BqRepairTraces"
                    WHERE "ToolId" IS NULL AND "BqContextId" IN ({bq1Id}, {bq2Id})
                    """)
                .SingleAsync();
            Assert.Equal(2, associatedCount);

            // Verificar que apenas um trace não resolvido existe (o primeiro que foi inserido).
            var unresolvedCount = await db.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*) AS "Value" FROM "BqRepairTraces"
                    WHERE "ToolId" = {toolId} AND "BqContextId" IS NULL
                    """)
                .SingleAsync();
            Assert.Equal(1, unresolvedCount);
        }
        finally
        {
            // Limpeza: remover traces, depois a Tool, por fim a Machine.
            var traces = await db.BqRepairTraces
                .Include(t => t.BqContext)
                .Where(t => t.ToolId == toolId
                    || (t.BqContext != null && t.BqContext.ToolId == toolId))
                .ToListAsync();
            db.BqRepairTraces.RemoveRange(traces);
            await db.SaveChangesAsync();

            // As produções ligam-se à ferramenta (FK): saem antes dela.
            await db.BqContexts.Where(c => c.ToolId == toolId).ExecuteDeleteAsync();
            db.ChangeTracker.Clear();

            var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
            db.Tools.Remove(tool);

            // A máquina já não apaga as produções em cascata: saem antes.
            await db.JobOns.Where(j => j.Machine.Code == $"PG-IDX-{suffix}").ExecuteDeleteAsync();
            var machine = await db.Machines.SingleAsync(m => m.Code == $"PG-IDX-{suffix}");
            db.Machines.Remove(machine);

            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task ToolLookupQuery_AgainstPostgres_RoundTripsProjectionAndToolType()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var toolId = $"BQ-TOOL-PG-LOOKUP-{suffix}";

        // Registo canónico da Tool com o tipo convertido em valor (ToolType).
        db.Tools.Add(new Tool
        {
            ToolId = toolId,
            Type = ToolType.Bq,
            Reference = "9389T194",
            Lot = "12"
        });
        await db.SaveChangesAsync();

        try
        {
            // Prova executada da projeção do read model contra o único
            // provider relacional de produção (Npgsql), incluindo o
            // round-trip do ToolType convertido em valor.
            var result = await new ToolLookupQuery(db).FindByToolIdAsync(toolId);

            Assert.NotNull(result);
            Assert.Equal(toolId, result.ToolId);
            Assert.Equal(ToolType.Bq, result.Type);
            Assert.Equal("9389T194", result.Reference);
            Assert.Equal("12", result.Lot);
        }
        finally
        {
            // Limpeza: apaga apenas a Tool semeada por este teste.
            var tool = await db.Tools.SingleAsync(t => t.ToolId == toolId);
            db.Tools.Remove(tool);

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// DMO-075: a procura em dois passos traduz no Npgsql — as produções e os
    /// Pesos de uma referência, filtrados sem distinguir maiúsculas e cortados
    /// na base (o InMemory não apanha uma tradução que falhe).
    /// </summary>
    [SkippableFact]
    public async Task ProcuraPorReferencia_AgainstPostgres_FiltersProductionsAndPesos()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var reference = $"PGREF{suffix}";
        var machine = new Machine { Code = $"PG{suffix}" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = $"CM-PG-{suffix}", Type = ToolType.Cm, Reference = $"CMPG{suffix}", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = $"BQ-PG-{suffix}", Type = ToolType.Bq, Reference = $"BQPG{suffix}", Lot = "1" });
        await db.SaveChangesAsync();
        var jobOn = new JobOn { Reference = reference, ProductionNumber = $"PN{suffix}", MachineId = machine.Id };
        var other = new JobOn { Reference = $"OUTRA{suffix}", ProductionNumber = $"PO{suffix}", MachineId = machine.Id };
        db.JobOns.AddRange(jobOn, other);
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        var peso = new Peso
        {
            CmToolId = $"CM-PG-{suffix}",
            BqToolId = $"BQ-PG-{suffix}",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id
        };
        db.Pesos.Add(peso);
        var segunda = new JobOn { Reference = reference, ProductionNumber = $"PS{suffix}", MachineId = machine.Id };
        db.JobOns.Add(segunda);
        await db.SaveChangesAsync();
        var segundoControlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = segunda.Id };
        db.Controlos.Add(segundoControlo);
        await db.SaveChangesAsync();

        try
        {
            var producoes = (await new DMO.Alpha.Infrastructure.Production.ProductionPickerQuery(db)
                .ListByReferenceAsync(reference.ToLowerInvariant()))
                .Where(p => p.JobOnId != segunda.Id).ToList();
            var producao = Assert.Single(producoes);
            Assert.Equal(jobOn.Id, producao.JobOnId);
            Assert.Equal(machine.Code, producao.Machine);

            var encontrada = await new DMO.Alpha.Infrastructure.Production.ProductionPickerQuery(db).FindAsync(other.Id);
            Assert.Equal(other.Reference, encontrada?.Reference);

            var comparacao = new DMO.Alpha.Infrastructure.Controlo.PesoComparisonQuery(
                db, new DMO.Alpha.Infrastructure.Controlo.PesoQuery(db));
            var pesos = await comparacao.ListarPesosPorReferenciaAsync(reference.ToLowerInvariant());
            Assert.Equal(peso.Id, Assert.Single(pesos).PesoId);
            Assert.Empty(await comparacao.ListarPesosPorReferenciaAsync($"OUTRA{suffix}"));
            Assert.Equal(reference, await comparacao.ReferenciaDoPesoAsync(peso.Id));

            // A lista de comparação de outra produção da mesma referência
            // traduz os joins no Npgsql e traz o Peso com o nº e a máquina.
            var candidatos = await comparacao.FindCandidatesAsync(segundoControlo.Id);
            var candidato = Assert.Single(candidatos);
            Assert.Equal(peso.Id, candidato.PesoId);
            Assert.Equal(jobOn.ProductionNumber, candidato.ProductionNumber);
            Assert.Equal(machine.Code, candidato.Machine);
        }
        finally
        {
            db.Pesos.Remove(peso);
            db.Controlos.RemoveRange(controlo, segundoControlo);
            db.JobOns.RemoveRange(jobOn, other, segunda);
            await db.SaveChangesAsync();
            db.Tools.RemoveRange(await db.Tools.Where(t => t.ToolId.EndsWith(suffix)).ToListAsync());
            db.Machines.Remove(machine);
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task ControloRevisaoQuery_DiaDoRegisto_ProducaoAnterior_E_SemData_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_Baseline"));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var reference = $"REV{suffix}";
        var machine = new Machine { Code = $"PG-REV-{suffix}" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = $"CM-REV-{suffix}", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = $"BQ-REV-{suffix}", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        var anterior = new JobOn { Reference = reference, ProductionNumber = $"A{suffix}", Machine = machine };
        var atual = new JobOn { Reference = reference, ProductionNumber = $"B{suffix}", Machine = machine };
        db.JobOns.Add(anterior);
        await db.SaveChangesAsync();
        db.JobOns.Add(atual);
        await db.SaveChangesAsync();
        var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
        var controloAtual = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = atual.Id };
        db.Controlos.AddRange(controloAnterior, controloAtual);
        await db.SaveChangesAsync();

        // 16/10/2026 23:30 UTC é já dia 17 em Lisboa (UTC+1).
        var registado = new DateTime(2026, 10, 16, 23, 30, 0, DateTimeKind.Utc);
        Peso NovoPeso(int controloId, DateTime? em) => new()
        {
            CmToolId = $"CM-REV-{suffix}", BqToolId = $"BQ-REV-{suffix}", Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m,
            GlassDensityGramsPerCm3 = 2.5m, ControloId = controloId, RegistadoEmUtc = em, RegistadoPor = "João Silva",
            Measurements = { new PesoMeasurement { RowPosition = 1, CmSubject = "12", WaterWeightGrams = 150m } }
        };
        db.Pesos.AddRange(NovoPeso(controloAnterior.Id, null), NovoPeso(controloAtual.Id, registado));
        await db.SaveChangesAsync();

        try
        {
            var query = new DMO.Alpha.Infrastructure.Controlo.ControloRevisaoQuery(db);

            var mes = await query.MonthAsync(2026, 10);
            Assert.Contains(mes, d => d.Day == 17 && d.Pendentes >= 1);

            var dia = await query.DayAsync(new DateOnly(2026, 10, 17), reference, ControloRevisaoEstado.Pendente);
            var item = Assert.Single(dia);
            Assert.Equal(atual.Id, item.JobOnId);
            Assert.Equal("João Silva", item.RegistadoPor);
            Assert.Equal("T194", item.BqReference);
            Assert.NotNull(item.AverageGlassWeightGrams);
            Assert.Empty(await query.DayAsync(new DateOnly(2026, 10, 16), reference, null));

            var semData = await query.UndatedAsync(reference, null);
            Assert.Equal(anterior.Id, Assert.Single(semData).JobOnId);
            Assert.True(await query.CountUndatedPendingAsync() >= 1);

            var previous = await query.FindPreviousWithPesoAsync(atual.Id);
            Assert.Equal(anterior.Id, previous?.JobOnId);
            Assert.Null(await query.FindPreviousWithPesoAsync(anterior.Id));
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.Pesos.RemoveRange(await db.Pesos.Where(p => p.CmToolId == $"CM-REV-{suffix}").ToListAsync());
            await db.SaveChangesAsync();
            db.Controlos.RemoveRange(await db.Controlos.Where(c => c.JobOnId == anterior.Id || c.JobOnId == atual.Id).ToListAsync());
            await db.SaveChangesAsync();
            db.JobOns.RemoveRange(await db.JobOns.Where(j => j.Reference == reference).ToListAsync());
            db.Tools.RemoveRange(await db.Tools.Where(t => t.ToolId.EndsWith(suffix)).ToListAsync());
            await db.SaveChangesAsync();
            db.Machines.Remove(await db.Machines.SingleAsync(m => m.Code == $"PG-REV-{suffix}"));
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task BqMovementAudit_HandlersWriteWhoAndWhen()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var deletionColumns = await db.Database
            .SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'BqMovementDeletions'
                """)
            .ToListAsync();
        Assert.Equal(
            new HashSet<string>
            {
                "Id", "BqMovementId", "BqRepairTraceId", "Type", "Quantity", "OccurredAtUtc",
                "MachineCodeFrozen", "DeletedByNameFrozen", "DeletedAtUtc"
            },
            deletionColumns.ToHashSet());

        var marker = Guid.NewGuid().ToString("N")[..10];
        var toolId = $"BQ-AUDIT-{marker}";
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "9389T194", Lot = "1" });
        var trace = new BqRepairTrace { ToolId = toolId };
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        var occurred = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var saida = new BqMovement
        {
            BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 3,
            OccurredAtUtc = occurred, CreatedAtUtc = occurred, MachineCodeFrozen = "B1"
        };
        var entrada = new BqMovement
        {
            BqRepairTraceId = trace.Id, Type = BqMovementType.Entrada, Quantity = 3,
            OccurredAtUtc = occurred.AddHours(1), CreatedAtUtc = occurred.AddHours(1)
        };
        db.BqMovements.AddRange(saida, entrada);
        await db.SaveChangesAsync();

        try
        {
            var edited = await new EditBqMovementHandler(db)
                .HandleAsync(new EditBqMovement(trace.Id, entrada.Id, "entrada", 2, null, "Ana Silva"));
            Assert.Equal(EditBqMovementResult.Success, edited.Result);

            var deleted = await new DeleteBqMovementHandler(db)
                .HandleAsync(new DeleteBqMovement(trace.Id, saida.Id));
            Assert.Equal(DeleteBqMovementResult.Success, deleted);

            db.ChangeTracker.Clear();
            var persisted = await db.BqMovements.AsNoTracking().SingleAsync(m => m.Id == entrada.Id);
            Assert.Equal("Ana Silva", persisted.CorrectedByNameFrozen);
            Assert.NotNull(persisted.CorrectedAtUtc);

            var trail = await db.BqMovementDeletions.AsNoTracking().SingleAsync(d => d.BqMovementId == saida.Id);
            Assert.Equal(trace.Id, trail.BqRepairTraceId);
            Assert.Equal(BqMovementType.Saida, trail.Type);
            Assert.Equal(3, trail.Quantity);
            Assert.Equal(occurred, trail.OccurredAtUtc);
            Assert.Equal("B1", trail.MachineCodeFrozen);
            // Sem identidade apaga na mesma, com o «quem» em branco.
            Assert.Null(trail.DeletedByNameFrozen);
        }
        finally
        {
            await db.BqMovementDeletions.Where(d => d.BqRepairTraceId == trace.Id).ExecuteDeleteAsync();
            await db.BqMovements.Where(m => m.BqRepairTraceId == trace.Id).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => t.Id == trace.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == toolId).ExecuteDeleteAsync();
        }
    }
}
