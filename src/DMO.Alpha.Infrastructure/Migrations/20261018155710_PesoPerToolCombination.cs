using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PesoPerToolCombination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pesos_ControloId",
                table: "Pesos");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ControloId",
                table: "Pesos",
                column: "ControloId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ControloId_CmToolId_BqToolId",
                table: "Pesos",
                columns: new[] { "ControloId", "CmToolId", "BqToolId" },
                unique: true,
                filter: "\"ControloId\" IS NOT NULL AND \"CmToolId\" <> 'REMOVED' AND \"BqToolId\" <> 'REMOVED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pesos_ControloId",
                table: "Pesos");

            migrationBuilder.DropIndex(
                name: "IX_Pesos_ControloId_CmToolId_BqToolId",
                table: "Pesos");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ControloId",
                table: "Pesos",
                column: "ControloId",
                unique: true);
        }
    }
}
