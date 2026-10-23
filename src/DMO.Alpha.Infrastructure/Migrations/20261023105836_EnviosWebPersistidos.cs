using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnviosWebPersistidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnvioHash",
                table: "OperacoesRegistadas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvioResposta",
                table: "OperacoesRegistadas",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnvioHash",
                table: "OperacoesRegistadas");

            migrationBuilder.DropColumn(
                name: "EnvioResposta",
                table: "OperacoesRegistadas");
        }
    }
}
