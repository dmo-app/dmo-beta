using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a persistência aditiva do corte Pegamentos production-scoped no
/// modelo relacional (Npgsql, sem servidor): as três novas tabelas
/// (Pegamentos — o registo durável sob o controlo com a tolerância
/// congelada; PegamentosMeasurements — as leituras append-only do gesto em
/// cruz com a contra costura NULL-ável (manual §7 L326: a ausência nunca
/// gera bloqueio na base de dados nem 23502); PegamentosTolerances — a
/// configuração singleton da tolerância, padrão InstallationState) e a
/// coluna aditiva diametro_gargalo (numeric NULL) no contexto técnico
/// Tool-owned — o valor nasce NULL na base real e é completado no fluxo,
/// gravando no tool_id owner. A migration AddPegamentosForControlo é
/// ADITIVA: AddColumn + CreateTable ×3 + CreateIndex — nenhuma operação
/// Alter/Drop/Rename em tabela existente. A cardinalidade canónica
/// «normalmente um registo por produção» NÃO é imposta como restrição
/// absoluta: o índice de ControloId existe mas NÃO é único (spec L26-28).
/// </summary>
public sealed class PegamentosSchemaTests
{
    // Ligação nunca contactada: os testes operam sobre o modelo e as
    // operations, sem servidor PostgreSQL.
    private const string TestConnectionString =
        "Host=localhost;Database=dmo_alpha_test;Username=dmo;Password=dmo";

    private static DmoDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        return new DmoDbContext(options);
    }

    private static Migration CreatePegamentosMigration(DmoDbContext db)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var entry = migrationsAssembly.Migrations
            .Single(m => m.Value.Name == "Baseline");

        return migrationsAssembly.CreateMigration(entry.Value, db.Database.ProviderName!);
    }

    [Fact]
    public void CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables()
    {
        // O modelo corrente cresce de 16 para 19 tabelas no corte Pegamentos
        // production-scoped, para 20 no corte A Ferramentas (ToolMachineLinks
        // — migration #7 aditiva), para 22 no corte I Admin (Templates +
        // TemplateAssignments — migration #8 aditiva) e para EXACTAMENTE 23
        // no corte PDF (ModuleSettings — migration #10 aditiva: o
        // directório de saída dos documentos, spec CONFIGURATION.md):
        // nenhuma outra tabela aparece nem desaparece.
        using var db = CreateNpgsqlContext();

        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName()!)
            .ToHashSet();

        Assert.Equal(
            new HashSet<string>
            {
                "Users",
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
                "ModuleSettings", "ControloViewings", "ControloAreaStates", "ControloAreaStateHistorico",
                // ControloDecisoes: a decisão do Peso e dos Pegamentos na Revisão.
                "ControloDecisoes",
                "Pegamentos",
                "PegamentosMeasurements", "PegamentosMeasurementHistorico",
                "PegamentosTolerances",
                "ToolMachineLinks",
                // #25 AddDataProtectionKeys: o keyring do DataProtection.
                "DataProtectionKeys",
                // #26 AddBqMovementAudit: o rasto das eliminações de movimentos BQ.
                "BqMovementDeletions",
                // #27 AddPuncoesNnpb: o registo das punções NNPB e as punções.
                "Puncoes", "PuncaoInspecoes",
                // O envio do PDF do Peso por email (D9): as listas, os modelos e o registo.
                "EmailListas", "EmailModelos", "EmailEnvios",
                // #29 AddJobOnLotNotes: as mudanças de lote durante a produção.
                "JobOnLotNotes",
                "JobOnChanges",
                // O envio único guardado na base (fundações do full).
                "OperacoesRegistadas",
                // O Armazém do full (0.2.0-beta.8).
                "ArmazemPosicoes", "Reparadores", "ArmazemMovimentos", "ArmazemLotesNovos", "ReparacaoProgramadaListas", "ReparacaoProgramadaItens", "UserSettings", "ArmazemOcupacoes", "FerramentaLocalizacoes", "FerramentaEstados",
                // A utilização das ferramentas e o lembrete.
                "FerramentaUtilizacoes", "FerramentaLembretesUtilizacao",
                // A folha da fábrica do Job On (AddJobOnFolha).
                "JobOnFolhas", "JobOnFolhaCalLinhas",
                // O relatório de fabrico do SAP (0.2.0-beta.20).
                "SapFabricos",
                // A Reparação Interna do full.
                "ReparacoesInternas",
                // O rasto de corrigir e anular da Reparação Interna.
                "ReparacoesInternasRasto"
            },
            tables);
    }

    [Fact]
    public void PegamentosEntity_RegistroUnderControlo_ToleranceFrozen_ToolsInherited()
    {
        // A forma exata do registo durável: a identidade (pegamentos_id),
        // a âncora ao controlo, as tools da combinação herdadas (nullable —
        // o contexto pode não ter um componente), a tolerância congelada e
        // os FATOS HUMANOS congelados das tools da combinação (corte A:
        // referência/lote por componente — as âncoras humanas vivem no
        // REGISTO, não nas medições; o histórico sobrevive à
        // edição/eliminação da Tool).
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(Pegamentos))!;
        Assert.NotNull(entityType);

        Assert.Equal(
            new HashSet<string>
            {
                "Id",
                "ControloId",
                "CmToolId",
                "BqToolId",
                "MfToolId",
                "ToleranceMm",
                "CmToolReferenceFrozen",
                "CmToolLotFrozen",
                "BqToolReferenceFrozen",
                "BqToolLotFrozen",
                "MfToolReferenceFrozen",
                "MfToolLotFrozen",
                // #27 AddControloRevisao: quando e quem gravou.
                "RegistadoEmUtc",
                "RegistadoPor",
                // O autor pelo utilizador (fundações do full).
                "RegistadoPorUserId",
                // ControloRevisaoDepoisDaAprovacao: a última correção (C9).
                "CorrigidoEmUtc"
            },
            entityType.GetProperties().Select(p => p.Name).ToHashSet());

        // A âncora ao controlo é obrigatória com ON DELETE RESTRICT: um
        // controlo referenciado por uma folha de Pegamentos não
        // desaparece silenciosamente.
        var controloFk = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(DMO.Alpha.Core.Controlo.Controlo));
        Assert.Equal(["ControloId"], controloFk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Restrict, controloFk.DeleteBehavior);

        // As âncoras às Tools reais da combinação são opcionais (o contexto
        // pode não ter um componente) com Restrict.
        foreach (var (navigation, column) in new[]
                 {
                     ("CmTool", "CmToolId"),
                     ("BqTool", "BqToolId"),
                     ("MfTool", "MfToolId")
                 })
        {
            var fk = entityType.GetForeignKeys().Single(f => f.DependentToPrincipal?.Name == navigation);
            Assert.Equal([column], fk.Properties.Select(p => p.Name));
            Assert.True(fk.Properties.All(p => p.IsNullable));
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }

        // A tolerância congelada é obrigatória (o valor efetivo consumido
        // sempre existe no registo).
        Assert.False(entityType.FindProperty("ToleranceMm")!.IsNullable);
    }

    [Fact]
    public void PegamentosControloIdIndex_IsNotUnique_CardinalityIsNotADomainRestriction()
    {
        // A cardinalidade da spec é «normalmente um registo por produção»
        // e NÃO uma restrição absoluta um-só-registo: o índice de
        // ControloId existe (resolução da folha) mas NÃO é único — um
        // adicional EXPLÍCITO continua possível sem bloqueio material.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(Pegamentos))!;
        var index = entityType.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["ControloId"]));
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void PegamentosMeasurementEntity_AppendOnlyFacts_ContraCosturaNullable()
    {
        // A leitura dimensional: pertence obrigatoriamente ao
        // pegamentos_id (cascade — factos históricos da folha), o
        // componente persiste por token canónico, a costura é o facto
        // base obrigatório, a contra costura é NULL-ável (o estado válido
        // do eixo único — sem bloqueio de base de dados, sem 23502) e o
        // nominal gargalo congelado na linha é NULL-ável (NotEvaluable).
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(PegamentosMeasurement))!;

        Assert.Equal(
            new HashSet<string>
            {
                "Id",
                "PegamentosId",
                "RowPosition",
                "Component",
                "CosturaMm",
                "ContraCosturaMm",
                "NominalGargaloMm",
                // ControloRevisaoDepoisDaAprovacao: quando a leitura foi gravada (C9).
                "RegistadoEmUtc",
                // PegamentosNumeroFerramenta: o número da CM/BQ/MF lido (21/10/2026).
                "NumeroFerramenta"
            },
            entityType.GetProperties().Select(p => p.Name).ToHashSet());

        var fk = entityType.GetForeignKeys().Single();
        Assert.Equal(["PegamentosId"], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);

        Assert.False(entityType.FindProperty("CosturaMm")!.IsNullable);
        Assert.True(entityType.FindProperty("ContraCosturaMm")!.IsNullable);
        Assert.True(entityType.FindProperty("NominalGargaloMm")!.IsNullable);
        Assert.False(entityType.FindProperty("RowPosition")!.IsNullable);
    }

    [Fact]
    public void ToolTechnicalContext_GargaloColumnIsNullable()
    {
        // diametro_gargalo vive no contexto técnico Tool-owned (tool_
        // technical_values): coluna aditiva NULL-ável — nasce NULL na base
        // real e é completada no fluxo (gravando no tool_id owner).
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(ToolTechnicalContext))!;
        var gargalo = entityType.FindProperty("DiametroGargaloMm");
        Assert.NotNull(gargalo);
        Assert.True(gargalo!.IsNullable);
    }

    [Fact]
    public void PegamentosToleranceEntity_SingletonShape()
    {
        // A configuração da tolerância é um registo singleton à maneira
        // da InstallationState: discriminador constante com default de
        // base de dados 1 e índice único FILTRADO — uma segunda linha é
        // impossível (verificado pelas operations compiladas da
        // migration, como o padrão InstallationState do
        // MigrationFoundationTests).
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(PegamentosTolerance))!;
        Assert.Equal(["Id"], entityType.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.False(entityType.FindProperty("ToleranceMm")!.IsNullable);

        var migration = CreatePegamentosMigration(db);
        var table = migration.UpOperations.OfType<CreateTableOperation>()
            .Single(t => t.Name == "PegamentosTolerances");

        Assert.Equal(["Id"], table.PrimaryKey!.Columns);

        var discriminator = table.Columns.Single(c => c.Name == "SingletonDiscriminator");
        Assert.Equal(typeof(int), discriminator.ClrType);
        Assert.False(discriminator.IsNullable);
        Assert.Equal(1, (int)discriminator.DefaultValue!);

        var singletonIndex = migration.UpOperations.OfType<CreateIndexOperation>()
            .Single(i => i.Name == "IX_PegamentosTolerances_SingletonDiscriminator");
        Assert.Equal(["SingletonDiscriminator"], singletonIndex.Columns);
        Assert.True(singletonIndex.IsUnique);
        Assert.Equal("\"SingletonDiscriminator\" IS NOT NULL", singletonIndex.Filter);
    }

    private static string TableFragment(string sql, string table)
    {
        var start = sql.IndexOf($"CREATE TABLE \"{table}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"CREATE TABLE \"{table}\" não encontrado no SQL gerado.");

        var end = sql.IndexOf(");", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Fim da tabela \"{table}\" não encontrado no SQL gerado.");

        return sql[start..end];
    }
}
