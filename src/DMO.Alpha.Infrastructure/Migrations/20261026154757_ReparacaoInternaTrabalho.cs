using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoInternaTrabalho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Trabalho",
                table: "ReparacoesInternas",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReparacoesInternas_Trabalho",
                table: "ReparacoesInternas",
                sql: "\"Trabalho\" IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReparacoesInternas_Trabalho",
                table: "ReparacoesInternas");

            migrationBuilder.DropColumn(
                name: "Trabalho",
                table: "ReparacoesInternas");
        }
    }
}
