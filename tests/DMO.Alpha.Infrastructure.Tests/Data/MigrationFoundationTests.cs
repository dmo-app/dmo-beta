using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Prova a fundação de persistência: uma única migração (Baseline) cria
/// exactamente as tabelas do modelo actual do <see cref="DmoDbContext"/>,
/// o snapshot coincide com o modelo e o SQL PostgreSQL gerado contém as
/// constraints canónicas (relações JobOn/contextos, PK textual da Tool,
/// âncoras do trace BQ, um Controlo por Job On, um Peso por Controlo na
/// Baseline — desde a PesoPerToolCombination, um por CM+BQ).
/// </summary>
public sealed class MigrationFoundationTests
{
    // Ligação nunca contactada: os testes operam apenas sobre o modelo e as
    // operations, sem servidor PostgreSQL.
    private const string TestConnectionString =
        "Host=localhost;Database=dmo_alpha_test;Username=dmo;Password=dmo";

    private static readonly HashSet<string> ModelTables =
    [
        "Users",
        "OperacoesRegistadas",
        "ArmazemPosicoes",
        "Reparadores",
        "ArmazemMovimentos",
        "ArmazemLotesNovos",
        "ReparacaoProgramadaListas",
        "ReparacaoProgramadaItens",
        "UserSettings",
        "ArmazemOcupacoes",
        "FerramentaLocalizacoes",
        "FerramentaEstados",
        "FerramentaUtilizacoes",
        "FerramentaLembretesUtilizacao",
        "JobOnFolhas",
        "JobOnFolhaCalLinhas",
        "SapFabricos",
        "ReparacoesInternas",
        "ReparacoesInternasRasto",
        "Templates",
        "TemplateAssignments",
        "Machines",
        "JobOns",
        "CmContexts",
        "MfContexts",
        "BqContexts",
        "Tools",
        "BqRepairTraces",
        "BqRepairers",
        "BqMovements",
        "ToolTechnicalContexts",
        "GlassDensities",
        "Pesos",
        "PesoMeasurements",
        "PesoComparacoes",
        "PesoComparacaoMeasurements",
        "PesoComparacaoCmDecisoes",
        "Controlos",
        "ControloDecisoes",
        "ModuleSettings",
        "ControloViewings",
        "ControloAreaStates",
        "ControloAreaStateHistorico",
        "Pegamentos",
        "PegamentosMeasurements",
        "PegamentosMeasurementHistorico",
        "PegamentosTolerances",
        "ToolMachineLinks",
        "BqMovementDeletions",
        "DataProtectionKeys",
        "JobOnChanges",
        "JobOnLotNotes",
        "Puncoes",
        "PuncaoInspecoes",
        "EmailListas",
        "EmailModelos",
        "EmailEnvios"
    ];

    // As tabelas criadas depois da Baseline, cada uma pela sua migração.
    private static readonly HashSet<string> BaselineTables = [.. ModelTables.Except(["ControloDecisoes", "ControloAreaStateHistorico", "EmailListas", "EmailModelos", "EmailEnvios", "PesoComparacaoCmDecisoes", "PegamentosMeasurementHistorico", "OperacoesRegistadas", "ArmazemPosicoes", "Reparadores", "ArmazemMovimentos", "ArmazemLotesNovos", "ReparacaoProgramadaListas", "ReparacaoProgramadaItens", "UserSettings", "ArmazemOcupacoes", "FerramentaLocalizacoes", "FerramentaEstados", "FerramentaUtilizacoes", "FerramentaLembretesUtilizacao", "JobOnFolhas", "JobOnFolhaCalLinhas", "SapFabricos", "ReparacoesInternas", "ReparacoesInternasRasto"])];

    private static DmoDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        return new DmoDbContext(options);
    }

    private static Migration CreateBaselineMigration(DmoDbContext db)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var entry = migrationsAssembly.Migrations
            .Single(m => m.Value.Name == "Baseline");

        return migrationsAssembly.CreateMigration(entry.Value, db.Database.ProviderName!);
    }

    [Fact]
    public void Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt()
    {
        // As migrações começam na Baseline; cada alteração ao modelo é uma
        // migração nova a seguir a ela (decisão do proprietário de 16/10/2026).
        using var db = CreateNpgsqlContext();

        var keys = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();

        Assert.Equal(
            new[] { "20261017083709_Baseline", "20261017122503_AddBqDiscrepancyReset", "20261017173054_JobOnReferenceProductionNumberUnique",
                "20261018021834_UserSessionVersion", "20261018044547_JobOnMachineRestrict", "20261018062357_RowVersionXmin",
                "20261018122946_BqContextReplaced", "20261018130550_ToolDuplicateWarning", "20261018132451_CmContextReplaced",
                "20261018155710_PesoPerToolCombination", "20261019051808_ControloDecisoes", "20261019062754_MfContextReplaced",
                "20261019092259_ContextToolIdentity", "20261019092325_PegamentosPerToolCombination",
                "20261019121642_ControloRevisaoDepoisDaAprovacao", "20261019125706_EnvioEmailPeso",
                "20261019130614_JobOnCriadoEm",
                "20261019131708_CmDeParteEDensidadeDaAgua", "20261019140057_PegamentosRastoDasLeituras",
                "20261020015832_ControloAreaIdentificacao", "20261020142103_OperacoesRegistadas", "20261020142915_AutorPorUtilizador",
                "20261020152047_ArmazemPosicoesEReparadores", "20261020160836_ArmazemMovimentos", "20261021072622_FerramentaUtilizacao",
                "20261021073002_AddJobOnFolha", "20261021074219_ContextToolSearchPath", "20261021083819_SapFabrico", "20261021085913_SapFabricoPeso",
                "20261021100548_ReparacaoInterna", "20261021104310_ReparacaoInternaRasto", "20261021114659_ReparacaoInternaOutraFerramenta",
                "20261021142502_PegamentosNumeroFerramenta",
                "20261022081924_PesoSoParaComparacao",
                "20261022151113_ArmazemLotesNovos",
                "20261022155553_ReparacaoProgramada",
                "20261022174307_FerramentaFabricaOrigem",
                "20261022185512_DefinicoesPessoais",
                "20261022213128_MovimentosLinhaReparadorJobOn" },
            keys);
    }

    [Fact]
    public void CurrentModel_HasExactlyTheCanonicalTables()
    {
        using var db = CreateNpgsqlContext();

        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName()!)
            .ToHashSet();

        Assert.Equal(ModelTables, tables);
    }

    [Fact]
    public void Snapshot_MatchesCurrentModelExactly()
    {
        // Qualquer diferença entre o snapshot das migrações e o modelo
        // (campo novo, coluna removida, constraint alterada) falha aqui:
        // uma alteração ao modelo exige uma migração nova.
        using var db = CreateNpgsqlContext();

        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        Assert.NotNull(migrationsAssembly.ModelSnapshot);

        var initializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotRawModel = migrationsAssembly.ModelSnapshot!.Model;
        var snapshotModel = snapshotRawModel is IMutableModel mutableModel
            ? initializer.Initialize(mutableModel.FinalizeModel(), designTime: true)
            : initializer.Initialize(snapshotRawModel, designTime: true);

        var currentModel = db.GetService<IDesignTimeModel>().Model;

        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void Baseline_CreatesEveryModelTable_AndDownDropsThem()
    {
        using var db = CreateNpgsqlContext();
        var migration = CreateBaselineMigration(db);

        var createdTables = migration.UpOperations
            .OfType<CreateTableOperation>()
            .Select(t => t.Name)
            .ToHashSet();

        Assert.Equal(BaselineTables, createdTables);
        Assert.Equal(
            createdTables,
            migration.DownOperations
                .OfType<DropTableOperation>()
                .Select(o => o.Name)
                .ToHashSet());
    }

    [Fact]
    public void Baseline_SeedsTheRemovedTool()
    {
        // Pesos e Pegamentos de uma Tool eliminada passam a apontar para a
        // linha REMOVED, que tem de existir numa base nova.
        using var db = CreateNpgsqlContext();
        var migration = CreateBaselineMigration(db);

        var insert = Assert.Single(migration.UpOperations.OfType<InsertDataOperation>());
        Assert.Equal("Tools", insert.Table);
        Assert.Equal(["ToolId", "Type", "Reference", "Lot"], insert.Columns);
        Assert.Equal("REMOVED", insert.Values[0, 0]);
    }

    [Fact]
    public void Baseline_EncodesJobOnAndContextRelationships()
    {
        using var db = CreateNpgsqlContext();
        var migration = CreateBaselineMigration(db);
        var tables = migration.UpOperations.OfType<CreateTableOperation>()
            .ToDictionary(t => t.Name);
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToList();

        var machineFk = Assert.Single(tables["JobOns"].ForeignKeys);
        Assert.Equal(["MachineId"], machineFk.Columns);
        Assert.Equal("Machines", machineFk.PrincipalTable);
        Assert.Equal(ReferentialAction.Cascade, machineFk.OnDelete);

        // Um contexto CM/MF/BQ por Job On; o ToolId é texto sem FK.
        foreach (var tableName in new[] { "CmContexts", "MfContexts", "BqContexts" })
        {
            var contextTable = tables[tableName];
            var jobOnFk = contextTable.ForeignKeys.Single(f => f.PrincipalTable == "JobOns");
            Assert.Contains(contextTable.ForeignKeys, f => f.PrincipalTable == "Tools" && f.OnDelete == ReferentialAction.Restrict);
            Assert.Equal(["JobOnId"], jobOnFk.Columns);
            Assert.Equal("JobOns", jobOnFk.PrincipalTable);
            Assert.False(contextTable.Columns.Single(c => c.Name == "ToolId").IsNullable);
            Assert.True(indexes.Single(i => i.Name == $"IX_{tableName}_JobOnId").IsUnique);
        }

        Assert.True(indexes.Single(i => i.Table == "JobOns" && i.Columns.SequenceEqual(["ProductionNumber"])).IsUnique);

        // Um Controlo por Job On e, na Baseline, no máximo um Peso por
        // Controlo (a PesoPerToolCombination passa-o para um por CM+BQ).
        Assert.True(indexes.Single(i => i.Table == "Controlos" && i.Columns.SequenceEqual(["JobOnId"])).IsUnique);
        Assert.True(indexes.Single(i => i.Table == "Pesos" && i.Columns.SequenceEqual(["ControloId"])).IsUnique);
    }

    [Fact]
    public void PesoPerToolCombination_OnePesoPerControloForEachCmAndBq()
    {
        // Trocar a CM ou a BQ a meio da produção pede um Peso novo (respostas
        // R15 e R16-5 do especialista): o ControloId deixa de ser único e o
        // único passa a ser o controlo com a CM e a BQ, sem as linhas
        // REMOVED (apagar uma ferramenta não colide). O Down volta ao único.
        using var db = CreateNpgsqlContext();
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var entry = migrationsAssembly.Migrations.Single(m => m.Value.Name == "PesoPerToolCombination");
        var migration = migrationsAssembly.CreateMigration(entry.Value, db.Database.ProviderName!);

        var dropped = Assert.Single(migration.UpOperations.OfType<DropIndexOperation>());
        Assert.Equal("IX_Pesos_ControloId", dropped.Name);
        var created = migration.UpOperations.OfType<CreateIndexOperation>().ToList();
        var plain = created.Single(i => i.Name == "IX_Pesos_ControloId");
        Assert.False(plain.IsUnique);
        Assert.Equal(["ControloId"], plain.Columns);
        var combination = created.Single(i => i.Name == "IX_Pesos_ControloId_CmToolId_BqToolId");
        Assert.True(combination.IsUnique);
        Assert.Equal(["ControloId", "CmToolId", "BqToolId"], combination.Columns);
        Assert.Equal(
            "\"ControloId\" IS NOT NULL AND \"CmToolId\" <> 'REMOVED' AND \"BqToolId\" <> 'REMOVED'",
            combination.Filter);
        // Só troca índices: nenhum dado é alterado.
        Assert.All(migration.UpOperations, o => Assert.True(o is DropIndexOperation or CreateIndexOperation));

        var restored = Assert.Single(migration.DownOperations.OfType<CreateIndexOperation>());
        Assert.Equal("IX_Pesos_ControloId", restored.Name);
        Assert.True(restored.IsUnique);
    }

    [Fact]
    public void Baseline_EncodesToolAndTraceAnchors()
    {
        using var db = CreateNpgsqlContext();
        var migration = CreateBaselineMigration(db);
        var tables = migration.UpOperations.OfType<CreateTableOperation>()
            .ToDictionary(t => t.Name);
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToList();

        // A identidade da Tool é o tool_id textual emitido pelo backend.
        Assert.Equal(["ToolId"], tables["Tools"].PrimaryKey!.Columns);

        // Trace → Tool opcional; no máximo um trace não associado por Tool.
        var traces = tables["BqRepairTraces"];
        Assert.True(traces.Columns.Single(c => c.Name == "ToolId").IsNullable);
        var toolIndex = indexes.Single(i => i.Table == "BqRepairTraces" && i.Columns.SequenceEqual(["ToolId"]));
        Assert.True(toolIndex.IsUnique);
        Assert.Equal("\"BqContextId\" IS NULL", toolIndex.Filter);

        // Trace → BqContext opcional, um trace por contexto BQ.
        Assert.True(traces.Columns.Single(c => c.Name == "BqContextId").IsNullable);
        Assert.True(indexes.Single(i => i.Table == "BqRepairTraces" && i.Columns.SequenceEqual(["BqContextId"])).IsUnique);

        // Cada movimento pertence a um trace; o tipo persiste como texto.
        var movements = tables["BqMovements"];
        var traceFk = movements.ForeignKeys.Single(f => f.PrincipalTable == "BqRepairTraces");
        Assert.Equal(["BqRepairTraceId"], traceFk.Columns);
        Assert.Equal(typeof(string), movements.Columns.Single(c => c.Name == "Type").ClrType);
        Assert.True(movements.Columns.Single(c => c.Name == "MachineCodeFrozen").IsNullable);
    }

    [Fact]
    public void Baseline_GeneratesValidPostgreSql()
    {
        using var db = CreateNpgsqlContext();
        var migration = CreateBaselineMigration(db);

        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
        var sql = string.Join("\n", commands.Select(c => c.CommandText));

        foreach (var table in BaselineTables)
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", sql);
        }

        Assert.Contains("CONSTRAINT \"PK_Tools\" PRIMARY KEY (\"ToolId\")", sql);
        Assert.Contains(
            "CREATE UNIQUE INDEX \"IX_BqRepairTraces_ToolId\" ON \"BqRepairTraces\" (\"ToolId\") WHERE \"BqContextId\" IS NULL",
            sql);
        Assert.Contains("INSERT INTO \"Tools\"", sql);
    }
}
