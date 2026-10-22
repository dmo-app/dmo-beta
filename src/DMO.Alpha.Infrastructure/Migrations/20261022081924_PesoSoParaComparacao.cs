using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PesoSoParaComparacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComparacaoDoPesoId",
                table: "Pesos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ComparacaoDoPesoId",
                table: "Pesos",
                column: "ComparacaoDoPesoId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pesos_ComparacaoSemControlo",
                table: "Pesos",
                sql: "\"ComparacaoDoPesoId\" IS NULL OR \"ControloId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Pesos_Pesos_ComparacaoDoPesoId",
                table: "Pesos",
                column: "ComparacaoDoPesoId",
                principalTable: "Pesos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pesos_Pesos_ComparacaoDoPesoId",
                table: "Pesos");

            migrationBuilder.DropIndex(
                name: "IX_Pesos_ComparacaoDoPesoId",
                table: "Pesos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pesos_ComparacaoSemControlo",
                table: "Pesos");

            migrationBuilder.DropColumn(
                name: "ComparacaoDoPesoId",
                table: "Pesos");
        }
    }
}
