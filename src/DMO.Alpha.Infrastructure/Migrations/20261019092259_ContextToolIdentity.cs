using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// A identidade do cm_id, mf_id e bq_id na base (sweep de 19/10/2026,
    /// DB-1). Até aqui só o código a garantia: a 17/10, antes da regra do
    /// cm_id, um cm_id mudou de lote no lugar e a produção esqueceu a CM que
    /// usou. Só acrescenta regras, sem mudar dados: cada contexto pertence a
    /// uma só produção (a atual ou a substituída, com a data), um contexto por
    /// produção e ferramenta, e um contexto nunca passa para outra ferramenta
    /// (só para REMOVED, ao apagar a ferramenta). Trocar a ferramenta cria um
    /// contexto novo, como o Editar do Job On já faz.
    /// </summary>
    public partial class ContextToolIdentity : Migration
    {
        private static readonly string[] Tables = ["CmContexts", "MfContexts", "BqContexts"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_MfContexts_CurrentOrReplaced",
                table: "MfContexts",
                sql: "(\"JobOnId\" IS NULL) <> (\"ReplacedJobOnId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MfContexts_ReplacedWithDate",
                table: "MfContexts",
                sql: "(\"ReplacedJobOnId\" IS NULL) = (\"ReplacedAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CmContexts_CurrentOrReplaced",
                table: "CmContexts",
                sql: "(\"JobOnId\" IS NULL) <> (\"ReplacedJobOnId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CmContexts_ReplacedWithDate",
                table: "CmContexts",
                sql: "(\"ReplacedJobOnId\" IS NULL) = (\"ReplacedAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BqContexts_CurrentOrReplaced",
                table: "BqContexts",
                sql: "(\"JobOnId\" IS NULL) <> (\"ReplacedJobOnId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BqContexts_ReplacedWithDate",
                table: "BqContexts",
                sql: "(\"ReplacedJobOnId\" IS NULL) = (\"ReplacedAtUtc\" IS NULL)");

            // O que o EF não descreve. Um contexto por produção e ferramenta:
            // voltar a uma ferramenta retoma o contexto dela; os REMOVED das
            // ferramentas apagadas não contam.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    CREATE UNIQUE INDEX "IX_{table}_Production_ToolId" ON "{table}"
                        ((COALESCE("JobOnId", "ReplacedJobOnId")), "ToolId")
                        WHERE "ToolId" <> 'REMOVED';
                    """);
            }

            // Um contexto nunca muda de ferramenta; só passa para REMOVED, ao
            // apagar a ferramenta. O gatilho só corre quando o ToolId muda, e
            // depois das chaves estrangeiras (os gatilhos correm pela ordem
            // do nome): um tool_id inventado continua a dar o erro da chave.
            migrationBuilder.Sql("""
                CREATE FUNCTION "dmo_context_tool_fixed"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."ToolId" <> 'REMOVED' THEN
                        RAISE EXCEPTION 'O contexto % de % não pode passar da ferramenta % para %: trocar a ferramenta cria um contexto novo.',
                            OLD."Id", TG_TABLE_NAME, OLD."ToolId", NEW."ToolId"
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NULL;
                END $$;
                """);
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "TR_{table}_ToolFixed" AFTER UPDATE OF "ToolId" ON "{table}"
                        FOR EACH ROW WHEN (OLD."ToolId" IS DISTINCT FROM NEW."ToolId")
                        EXECUTE FUNCTION "dmo_context_tool_fixed"();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""DROP TRIGGER "TR_{table}_ToolFixed" ON "{table}";""");
                migrationBuilder.Sql($"""DROP INDEX "IX_{table}_Production_ToolId";""");
            }

            migrationBuilder.Sql("""DROP FUNCTION "dmo_context_tool_fixed"();""");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MfContexts_CurrentOrReplaced",
                table: "MfContexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MfContexts_ReplacedWithDate",
                table: "MfContexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CmContexts_CurrentOrReplaced",
                table: "CmContexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CmContexts_ReplacedWithDate",
                table: "CmContexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BqContexts_CurrentOrReplaced",
                table: "BqContexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BqContexts_ReplacedWithDate",
                table: "BqContexts");
        }
    }
}
