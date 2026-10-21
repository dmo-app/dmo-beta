using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a persistência aditiva do corte Controlo ensure+Resumo no modelo
/// relacional (Npgsql, sem servidor): a tabela nova Controlos tem
/// EXACTAMENTE a identidade e a relação decididas — controlo_id (PK
/// identity emitida pelo backend) + JobOnId (FK NOT NULL → JobOns, ON
/// DELETE RESTRICT) com índice ÚNICO (idempotência material: get-or-create
/// por JobOnId; abrir 2× = 1 controlo_id) e MAIS NADA: sem colunas de
/// factos CM/BQ/MF/PU/CS (edição é corte futuro), sem timestamps, sem
/// estado/revisão, sem cópia do contexto (resolve-se pela relação + query
/// canónica). O JobOn NÃO ganha navegação nem coluna de Controlo (spec
/// Job On L31). A migration #5 (AddControloForJobOn) é ADITIVA: apenas
/// CreateTable + CreateIndex — nenhuma operação Alter/Drop/Rename em
/// tabela existente — e o DDL gerado é PostgreSQL válido.
/// </summary>
public sealed class ControloResumoSchemaTests
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
    public void CurrentModel_ExactlyTheListedTables_IncludingControlos()
    {
        // O modelo corrente cresceu de 15 para 16 tabelas no corte
        // ensure+Resumo (Controlos), para 19 no corte Pegamentos
        // production-scoped (Pegamentos, PegamentosMeasurements,
        // PegamentosTolerances — migration #6 aditiva), para 20 no corte A
        // Ferramentas (ToolMachineLinks — migration #7 aditiva), para 22
        // no corte I Admin (Templates + TemplateAssignments — migration #8
        // aditiva) e para EXACTAMENTE 23 no corte PDF (ModuleSettings —
        // migration #10 aditiva: o directório de saída dos documentos,
        // spec CONFIGURATION.md); nenhuma outra tabela aparece nem
        // desaparece.
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
                "ArmazemPosicoes", "Reparadores", "ArmazemMovimentos", "ArmazemOcupacoes", "FerramentaLocalizacoes", "FerramentaEstados",
                // A utilização das ferramentas e o lembrete.
                "FerramentaUtilizacoes", "FerramentaLembretesUtilizacao",
                // A folha da fábrica do Job On (AddJobOnFolha).
                "JobOnFolhas", "JobOnFolhaCalLinhas"
            },
            tables);
    }

    [Fact]
    public void ControloEntity_ExactlyIdentityAndRelation_NoFactColumns()
    {
        // A forma EXACTA decidida: Id + JobOnId, MAIS NADA — nenhum facto
        // CM/BQ/MF/PU/CS (edição é corte futuro), nenhum timestamp, nenhum
        // estado/revisão, nenhuma cópia do contexto de produção.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(DMO.Alpha.Core.Controlo.Controlo));
        Assert.NotNull(entityType);

        Assert.Equal(
            new HashSet<string>
            {
                "Id", "JobOnId", "ApprovedByOperatorId", "ApprovedAtUtc",
                // #27 AddControloRevisao: o «Não aprovar» e a nota da revisão.
                "NotApprovedByOperatorId", "NotApprovedAtUtc", "ReviewNote",
                // O autor pelo utilizador (fundações do full): o Users.Id ao lado de cada autor.
                "ApprovedByUserId", "NotApprovedByUserId"
            },
            entityType!.GetProperties().Select(p => p.Name).ToHashSet());

        // Nenhuma coluna de factos de controlo nem de contexto copiado. A
        // EXCEÇÃO do corte D: o selo de aprovação (ApprovedByOperatorId e
        // ApprovedAtUtc — o operador e o momento do selo: factos do
        // histórico, nunca timestamps de infraestrutura).
        Assert.DoesNotContain(
            entityType.GetProperties(),
            p => p.Name != "ApprovedAtUtc" && p.Name != "ApprovedByOperatorId"
                && p.Name != "NotApprovedAtUtc" && p.Name != "NotApprovedByOperatorId"
                && (p.Name.Contains("Cm", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Bq", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Mf", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Pu", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Cs", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("State", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Status", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("At", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Revis", StringComparison.OrdinalIgnoreCase)));

        // A ÚNICA relação de produção é a âncora à produção: FK JobOnId →
        // JobOns. As outras duas são os autores do selo para Users (fundações
        // do full).
        Assert.Equal(
            ["ApprovedByUserId", "JobOnId", "NotApprovedByUserId"],
            entityType.GetForeignKeys().SelectMany(k => k.Properties).Select(p => p.Name).OrderBy(n => n));
        var fk = entityType.GetForeignKeys().Single(k => k.Properties.Single().Name == "JobOnId");
        Assert.Equal(["JobOnId"], fk.Properties.Select(p => p.Name));
        Assert.Equal("JobOns", fk.PrincipalEntityType.GetTableName());
        Assert.Equal(["Id"], fk.PrincipalKey.Properties.Select(p => p.Name));
    }

    [Fact]
    public void ControloEntity_JobOnDoesNotGainControloNavigationOrColumn()
    {
        // Spec Job On L31: o Job On não se torna owner dos registos de
        // Controlo por os consumir — a entidade JobOn fica INALTERADA:
        // nenhuma navegação nem coluna de Controlo aparece do lado do
        // JobOn (a FK vive no lado do Controlo).
        using var db = CreateNpgsqlContext();

        var jobOn = db.Model.FindEntityType(typeof(DMO.Alpha.Core.Production.JobOn))!;

        Assert.DoesNotContain(
            jobOn.GetProperties(),
            p => p.Name.Contains("Controlo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            jobOn.GetNavigations(),
            n => n.Name.Contains("Controlo", StringComparison.OrdinalIgnoreCase));

        // As colunas do JobOn permanecem as canónicas + a CALOTE DESTA
        // PRODUÇÃO (CaloteMm — a migração #20 aditiva, null por defeito: o
        // campo «Calote desta produção (mm)» da ficha do dmo-html, consumido
        // pelo apoio TP/calote do Peso — nunca uma coluna de Controlo) + a
        // versão da linha (o xmin do PostgreSQL, para as edições ao mesmo tempo)
        // + quando o Job On foi gravado (CriadoEmUtc, para o Sumário do Planeamento).
        Assert.Equal(
            new HashSet<string> { "Id", "Reference", "ProductionNumber", "MachineId", "InicioEm", "FimEm", "CaloteMm", "CriadoEmUtc", DmoDbContext.RowVersion },
            jobOn.GetProperties().Select(p => p.Name).ToHashSet());
    }

    [Fact]
    public void ControloEntity_UniqueJobOnIdIndex_RestrictForeignKey()
    {
        // Idempotência material: índice ÚNICO sobre a coluna JobOnId —
        // a mesma produção nunca ganha um segundo controlo_id (última
        // linha da verdade persistida; padrão DuplicateProductionNumber).
        // FK obrigatória (NOT NULL) com ON DELETE RESTRICT.
        using var db = CreateNpgsqlContext();

        var entityType = db.Model.FindEntityType(typeof(DMO.Alpha.Core.Controlo.Controlo))!;

        var jobOnIndex = entityType.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["JobOnId"]));
        Assert.True(jobOnIndex.IsUnique);

        var jobOnId = entityType.FindProperty("JobOnId")!;
        Assert.False(jobOnId.IsNullable);

        var fk = entityType.GetForeignKeys().Single(k => k.PrincipalEntityType.ClrType == typeof(DMO.Alpha.Core.Production.JobOn));
        Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict, fk.DeleteBehavior);
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
