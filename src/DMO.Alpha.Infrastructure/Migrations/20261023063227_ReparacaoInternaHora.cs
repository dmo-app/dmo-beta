using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoInternaHora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraAntes",
                table: "ReparacoesInternasRasto",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraDepois",
                table: "ReparacoesInternasRasto",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "Hora",
                table: "ReparacoesInternas",
                type: "time without time zone",
                nullable: true);

            // O rasto fica fixo também nas horas (nada se perde).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "dmo_ri_rasto_fixo"() RETURNS trigger LANGUAGE plpgsql SET search_path = '' AS $$
                BEGIN
                    IF (NEW."Id", NEW."ReparacaoId", NEW."Acao", NEW."NumeroAntes", NEW."NumeroDepois", NEW."EmUtc", NEW."Motivo", NEW."ToolIdAssociado", NEW."HoraAntes", NEW."HoraDepois")
                            IS DISTINCT FROM (OLD."Id", OLD."ReparacaoId", OLD."Acao", OLD."NumeroAntes", OLD."NumeroDepois", OLD."EmUtc", OLD."Motivo", OLD."ToolIdAssociado", OLD."HoraAntes", OLD."HoraDepois")
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
                CREATE OR REPLACE FUNCTION "dmo_ri_rasto_fixo"() RETURNS trigger LANGUAGE plpgsql SET search_path = '' AS $$
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

            migrationBuilder.DropColumn(
                name: "HoraAntes",
                table: "ReparacoesInternasRasto");

            migrationBuilder.DropColumn(
                name: "HoraDepois",
                table: "ReparacoesInternasRasto");

            migrationBuilder.DropColumn(
                name: "Hora",
                table: "ReparacoesInternas");
        }
    }
}
