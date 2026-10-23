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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
