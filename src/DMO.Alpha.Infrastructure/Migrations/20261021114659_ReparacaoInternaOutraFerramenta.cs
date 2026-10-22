using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoInternaOutraFerramenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternasRasto_Acao",
                table: "ReparacoesInternasRasto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternas_Contexto",
                table: "ReparacoesInternas");

            migrationBuilder.AddColumn<string>(
                name: "ToolIdAssociado",
                table: "ReparacoesInternasRasto",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "ReparacoesInternas",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssociadaEmUtc",
                table: "ReparacoesInternas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssociadaPorUserId",
                table: "ReparacoesInternas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssociadaToolId",
                table: "ReparacoesInternas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoteEscrito",
                table: "ReparacoesInternas",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaEscrita",
                table: "ReparacoesInternas",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternasRasto_Acao",
                table: "ReparacoesInternasRasto",
                sql: "(\"Acao\" = 1 AND \"NumeroDepois\" IS NOT NULL AND \"ToolIdAssociado\" IS NULL) OR (\"Acao\" = 2 AND \"NumeroDepois\" IS NULL AND \"ToolIdAssociado\" IS NULL) OR (\"Acao\" = 3 AND \"NumeroDepois\" IS NULL AND \"ToolIdAssociado\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_AssociadaPorUserId",
                table: "ReparacoesInternas",
                column: "AssociadaPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_AssociadaToolId",
                table: "ReparacoesInternas",
                column: "AssociadaToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_PorAssociar",
                table: "ReparacoesInternas",
                column: "Id",
                filter: "\"ReferenciaEscrita\" IS NOT NULL AND \"AssociadaToolId\" IS NULL AND \"AnuladaEmUtc\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternas_Associada",
                table: "ReparacoesInternas",
                sql: "(\"AssociadaToolId\" IS NULL AND \"AssociadaEmUtc\" IS NULL AND \"AssociadaPorUserId\" IS NULL) OR (\"AssociadaToolId\" IS NOT NULL AND \"AssociadaEmUtc\" IS NOT NULL AND \"ReferenciaEscrita\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternas_Contexto",
                table: "ReparacoesInternas",
                sql: "(\"JobOnId\" IS NOT NULL AND \"ReferenciaEscrita\" IS NULL AND \"LoteEscrito\" IS NULL AND ((\"Tipo\" = 1 AND \"CmContextId\" IS NOT NULL AND \"MfContextId\" IS NULL) OR (\"Tipo\" = 2 AND \"MfContextId\" IS NOT NULL AND \"CmContextId\" IS NULL))) OR (\"Tipo\" IN (1, 2) AND \"CmContextId\" IS NULL AND \"MfContextId\" IS NULL AND \"ReferenciaEscrita\" IS NOT NULL AND \"LoteEscrito\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternas_Escrita",
                table: "ReparacoesInternas",
                sql: "(\"ReferenciaEscrita\" IS NULL OR (btrim(\"ReferenciaEscrita\") <> '' AND \"ReferenciaEscrita\" = btrim(\"ReferenciaEscrita\"))) AND (\"LoteEscrito\" IS NULL OR (btrim(\"LoteEscrito\") <> '' AND \"LoteEscrito\" = btrim(\"LoteEscrito\")))");

            migrationBuilder.AddForeignKey(
                name: "FK_ReparacoesInternas_Tools_AssociadaToolId",
                table: "ReparacoesInternas",
                column: "AssociadaToolId",
                principalTable: "Tools",
                principalColumn: "ToolId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReparacoesInternas_Users_AssociadaPorUserId",
                table: "ReparacoesInternas",
                column: "AssociadaPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // O rasto continua só a acrescentar-se: a ferramenta associada
            // também não se muda.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "dmo_ri_rasto_fixo"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW."Id", NEW."ReparacaoId", NEW."Acao", NEW."NumeroAntes", NEW."NumeroDepois", NEW."EmUtc", NEW."Motivo", NEW."ToolIdAssociado")
                            IS DISTINCT FROM (OLD."Id", OLD."ReparacaoId", OLD."Acao", OLD."NumeroAntes", OLD."NumeroDepois", OLD."EmUtc", OLD."Motivo", OLD."ToolIdAssociado")
                        OR (NEW."PorUserId" IS DISTINCT FROM OLD."PorUserId" AND NEW."PorUserId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'O rasto % da reparação % não se muda: só se acrescenta.', OLD."Id", OLD."ReparacaoId"
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "dmo_ri_rasto_fixo"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW."Id", NEW."ReparacaoId", NEW."Acao", NEW."NumeroAntes", NEW."NumeroDepois", NEW."EmUtc", NEW."Motivo")
                            IS DISTINCT FROM (OLD."Id", OLD."ReparacaoId", OLD."Acao", OLD."NumeroAntes", OLD."NumeroDepois", OLD."EmUtc", OLD."Motivo")
                        OR (NEW."PorUserId" IS DISTINCT FROM OLD."PorUserId" AND NEW."PorUserId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'O rasto % da reparação % não se muda: só se acrescenta.', OLD."Id", OLD."ReparacaoId"
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END $$;
                """);

            // Voltar atrás perde as reparações com a ferramenta escrita (e o
            // rasto delas, CASCADE): o esquema antigo não as consegue guardar.
            migrationBuilder.Sql("""DELETE FROM "ReparacoesInternas" WHERE "ReferenciaEscrita" IS NOT NULL OR "JobOnId" IS NULL;""");

            migrationBuilder.DropForeignKey(
                name: "FK_ReparacoesInternas_Tools_AssociadaToolId",
                table: "ReparacoesInternas");

            migrationBuilder.DropForeignKey(
                name: "FK_ReparacoesInternas_Users_AssociadaPorUserId",
                table: "ReparacoesInternas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternasRasto_Acao",
                table: "ReparacoesInternasRasto");

            migrationBuilder.DropIndex(
                name: "IX_ReparacoesInternas_AssociadaPorUserId",
                table: "ReparacoesInternas");

            migrationBuilder.DropIndex(
                name: "IX_ReparacoesInternas_AssociadaToolId",
                table: "ReparacoesInternas");

            migrationBuilder.DropIndex(
                name: "IX_ReparacoesInternas_PorAssociar",
                table: "ReparacoesInternas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternas_Associada",
                table: "ReparacoesInternas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternas_Contexto",
                table: "ReparacoesInternas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternas_Escrita",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "ToolIdAssociado",
                table: "ReparacoesInternasRasto");

            migrationBuilder.DropColumn(
                name: "AssociadaEmUtc",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "AssociadaPorUserId",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "AssociadaToolId",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "LoteEscrito",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "ReferenciaEscrita",
                table: "ReparacoesInternas");

            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "ReparacoesInternas",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternasRasto_Acao",
                table: "ReparacoesInternasRasto",
                sql: "(\"Acao\" = 1 AND \"NumeroDepois\" IS NOT NULL) OR (\"Acao\" = 2 AND \"NumeroDepois\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternas_Contexto",
                table: "ReparacoesInternas",
                sql: "(\"Tipo\" = 1 AND \"CmContextId\" IS NOT NULL AND \"MfContextId\" IS NULL) OR (\"Tipo\" = 2 AND \"MfContextId\" IS NOT NULL AND \"CmContextId\" IS NULL)");
        }
    }
}
