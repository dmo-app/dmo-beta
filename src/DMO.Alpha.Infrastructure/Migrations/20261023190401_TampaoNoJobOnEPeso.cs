using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TampaoNoJobOnEPeso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TampaoCaloteMm",
                table: "Pesos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TampaoDiametroMm",
                table: "Pesos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TampaoDiametroMm",
                table: "JobOns",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TampaoCaloteMm",
                table: "Pesos");

            migrationBuilder.DropColumn(
                name: "TampaoDiametroMm",
                table: "Pesos");

            migrationBuilder.DropColumn(
                name: "TampaoDiametroMm",
                table: "JobOns");
        }
    }
}
