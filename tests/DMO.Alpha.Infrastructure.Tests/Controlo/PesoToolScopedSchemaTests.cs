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
/// Prova a persistência aditiva do corte Peso tool-scoped no modelo
/// relacional (Npgsql, sem servidor): as novas tabelas (Pesos,
/// PesoMeasurements, ToolTechnicalContexts, GlassDensities) têm a forma
/// canónica decidida — o peso_id com âncoras FK Restrict às Tools reais, as
/// leituras pertencendo ao Peso em cascade, o contexto técnico 1:1 com a
/// Tool e a configuração de densidade por processo — e o Peso NÃO transporta
/// nenhuma coluna de produção (nenhum jobon_id/controlo_id/cm_id é
/// fabricado). A migration AddPesoToolScoped é ADITIVA: apenas CreateTable
/// das novas tabelas e CreateIndex — nenhuma operação Alter/Drop/Rename em
/// tabela existente.
/// </summary>
public sealed class PesoToolScopedSchemaTests
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

    [Fact]
    public void CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables()
    {
        // O modelo corrente do corte Peso tool-scoped tinha 15 tabelas; o
        // corte Controlo ensure+Resumo (migration #5 aditiva) acrescentou a
        // 16.ª (Controlos); o corte Pegamentos production-scoped (migration
        // #6 aditiva) acrescentou as 17.ª–19.ª (Pegamentos,
        // PegamentosMeasurements, PegamentosTolerances); o corte A
        // Ferramentas (migration #7 aditiva) acrescentou a 20.ª
        // (ToolMachineLinks); o corte I Admin (migration #8 aditiva)
        // acrescentou as 21.ª–22.ª (Templates, TemplateAssignments); o corte
        // C (migration #9 aditiva) é coluna/índice em Pesos; o corte PDF
        // (migration #10 aditiva) acrescentou a 23.ª (ControloSettings — o
        // directório de saída dos documentos, spec CONFIGURATION.md). As
        // quatro do Peso (Pesos, PesoMeasurements, ToolTechnicalContexts,
        // GlassDensities) continuam presentes; nenhuma outra tabela
        // aparece nem desaparece.
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
                "ArmazemPosicoes", "Reparadores", "ArmazemMovimentos", "ArmazemOcupacoes", "FerramentaLocalizacoes", "FerramentaEstados"
            },
            tables);
    }

    [Fact]
    public void PesoEntity_HasNoProductionIdentityColumns()
    {
        // Regra central EVOLUÍDA pelo corte C (spec PESO.md «Production
        // association cardinality»): o Peso ganhou a ÚNICA coluna de
        // produção legítima — a associação OPCIONAL ControloId (desde 18/10/2026, um
        // Peso por controlo para cada CM+BQ, índice único filtrado; a
        // associação consome o Peso). Continam PROIBIDAS: jobon_id, cm_id/mf_id/bq_id (os
        // contextos de produção nunca são âncoras do Peso — as âncoras são
        // as TOOLS). As restantes colunas: a identidade, as âncoras de
        // Tool, os inputs consumidos congelados e os FATOS HUMANOS
        // congelados do corte A.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(Peso));
        Assert.NotNull(entityType);

        Assert.Equal(
            new HashSet<string>
            {
                "Id",
                "CmToolId",
                "BqToolId",
                "Process",
                "WaterTemperatureC",
                "WaterDensityGramsPerCm3",
                "VolumeMarisaCm3",
                "VolumePuncaoCm3",
                "NominalWeightGrams",
                "GlassDensityGramsPerCm3",
                "CmToolReferenceFrozen",
                "CmToolLotFrozen",
                "BqToolReferenceFrozen",
                "BqToolLotFrozen",
                "ControloId",
                // #27 AddControloRevisao: quando e quem gravou.
                "RegistadoEmUtc",
                "RegistadoPor",
                // O autor pelo utilizador (fundações do full).
                "RegistadoPorUserId",
                // A produção anterior (o Owner 2026-10-17): também um
                // controlo, nunca um jobon_id, e o peso do SAP escrito à mão.
                "ProducaoAnteriorControloId",
                "ProducaoAnteriorPesoGramas"
            },
            entityType!.GetProperties().Select(p => p.Name).ToHashSet());

        Assert.DoesNotContain(
            entityType.GetProperties(),
            p => p.Name.Contains("JobOn", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("CmContext", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("CmId", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("MfId", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("BqId", StringComparison.OrdinalIgnoreCase));

        // Relações: as âncoras canónicas às Tools reais (CmToolId/BqToolId)
        // + a associação OPCIONAL ControloId do corte C (um Peso por
        // controlo para cada CM+BQ desde 18/10/2026). Nenhuma outra entidade de
        // produção é referenciada.
        Assert.Equal(
            // + a produção anterior (o Owner 2026-10-17), também um controlo.
            // + o autor pelo utilizador (fundações do full), para Users.
            new[] { "BqToolId", "CmToolId", "ControloId", "ProducaoAnteriorControloId", "RegistadoPorUserId" },
            entityType.GetForeignKeys()
                .SelectMany(fk => fk.Properties)
                .Select(p => p.Name)
                .OrderBy(name => name)
                .ToArray());
    }

    [Fact]
    public void PesoEntity_AnchorsToolsWithRestrict_MeasurementsCascade()
    {
        // O Peso referencia as Tools reais com ON DELETE RESTRICT (uma Tool
        // referenciada por Pesos não desaparece silenciosamente); as
        // leituras pertencem ao Peso (cascade, como os movimentos ao trace).
        using var db = CreateNpgsqlContext();

        var peso = db.Model.FindEntityType(typeof(Peso))!;

        foreach (var (navigation, column) in new[] { ("CmTool", "CmToolId"), ("BqTool", "BqToolId") })
        {
            var fk = peso.GetForeignKeys().Single(f => f.DependentToPrincipal?.Name == navigation);
            Assert.Equal([column], fk.Properties.Select(p => p.Name));
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }

        var measurement = db.Model.FindEntityType(typeof(PesoMeasurement))!;
        var measurementFk = measurement.GetForeignKeys().Single();
        Assert.Equal(["PesoId"], measurementFk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, measurementFk.DeleteBehavior);
    }

    [Fact]
    public void ToolTechnicalContext_OneToOneWithTool_Restrict()
    {
        // O contexto técnico é 1:1 com a Tool (a linha é do tool_id owner) e
        // todos os valores técnicos são nullable: um valor técnico não tem
        // de existir quando o tool_id é criado.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(ToolTechnicalContext))!;
        Assert.Equal(["ToolId"], entityType.FindPrimaryKey()!.Properties.Select(p => p.Name));

        var fk = entityType.GetForeignKeys().Single();
        Assert.Equal(["ToolId"], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);

        foreach (var column in new[]
                 {
                     "Process", "NominalWeightGrams", "VolumePuncaoCm3", "VolumeMarisaCm3",
                     "DiametroGargaloMm", "TpDiametroMm", "TpRaioMm", "TpSagittaMm",
                     "TpRaioBaseMm", "TpRaioEsferaMm", "TpVolumeMm3"
                 })
        {
            Assert.True(entityType.FindProperty(column)!.IsNullable);
        }
    }

    [Fact]
    public void GlassDensity_ProcessIsPrimaryKey()
    {
        // A configuração da densidade do vidro tem o processo como chave:
        // uma linha por processo de fabrico.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(GlassDensity))!;
        Assert.Equal(["Process"], entityType.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.False(entityType.FindProperty("DensityGramsPerCm3")!.IsNullable);
    }

}
