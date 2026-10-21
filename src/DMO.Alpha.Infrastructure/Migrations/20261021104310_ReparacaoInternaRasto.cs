using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoInternaRasto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnuladaEmUtc",
                table: "ReparacoesInternas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ReparacoesInternas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "ReparacoesInternasRasto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReparacaoId = table.Column<int>(type: "integer", nullable: false),
                    Acao = table.Column<int>(type: "integer", nullable: false),
                    NumeroAntes = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NumeroDepois = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacoesInternasRasto", x => x.Id);
                    table.CheckConstraint("CK_ReparacoesInternasRasto_Acao", "(\"Acao\" = 1 AND \"NumeroDepois\" IS NOT NULL) OR (\"Acao\" = 2 AND \"NumeroDepois\" IS NULL)");
                    table.CheckConstraint("CK_ReparacoesInternasRasto_Motivo", "\"Motivo\" IS NULL OR (btrim(\"Motivo\") <> '' AND \"Motivo\" = btrim(\"Motivo\"))");
                    table.ForeignKey(
                        name: "FK_ReparacoesInternasRasto_ReparacoesInternas_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "ReparacoesInternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReparacoesInternasRasto_Users_PorUserId",
                        column: x => x.PorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_JobOnId_Id_Ativas",
                table: "ReparacoesInternas",
                columns: new[] { "JobOnId", "Id" },
                filter: "\"AnuladaEmUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternasRasto_PorUserId",
                table: "ReparacoesInternasRasto",
                column: "PorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternasRasto_ReparacaoId_Id",
                table: "ReparacoesInternasRasto",
                columns: new[] { "ReparacaoId", "Id" });

            // O rasto só se acrescenta: a base recusa mudar uma linha. Só o
            // SET NULL de quem, ao eliminar o utilizador, passa. Apagar sai
            // com a reparação (CASCADE, ao eliminar o Job On).
            migrationBuilder.Sql("""
                CREATE FUNCTION "dmo_ri_rasto_fixo"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW."Id", NEW."ReparacaoId", NEW."Acao", NEW."NumeroAntes", NEW."NumeroDepois", NEW."EmUtc", NEW."Motivo")
                            IS DISTINCT FROM (OLD."Id", OLD."ReparacaoId", OLD."Acao", OLD."NumeroAntes", OLD."NumeroDepois", OLD."EmUtc", OLD."Motivo")
                        OR (NEW."PorUserId" IS DISTINCT FROM OLD."PorUserId" AND NEW."PorUserId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'O rasto % da reparação % não se muda: só se acrescenta.', OLD."Id", OLD."ReparacaoId"
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_ReparacoesInternasRasto_Fixo" BEFORE UPDATE ON "ReparacoesInternasRasto"
                    FOR EACH ROW EXECUTE FUNCTION "dmo_ri_rasto_fixo"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_ReparacoesInternasRasto_Fixo" ON "ReparacoesInternasRasto";""");
            migrationBuilder.Sql("""DROP FUNCTION "dmo_ri_rasto_fixo"();""");

            migrationBuilder.DropTable(
                name: "ReparacoesInternasRasto");

            migrationBuilder.DropIndex(
                name: "IX_ReparacoesInternas_JobOnId_Id_Ativas",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "AnuladaEmUtc",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ReparacoesInternas");
        }
    }
}
