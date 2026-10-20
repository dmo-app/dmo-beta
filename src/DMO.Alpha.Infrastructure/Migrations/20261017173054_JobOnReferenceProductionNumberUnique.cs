using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JobOnReferenceProductionNumberUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobOns_ProductionNumber",
                table: "JobOns");

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_ProductionNumber",
                table: "JobOns",
                column: "ProductionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_Reference_ProductionNumber",
                table: "JobOns",
                columns: new[] { "Reference", "ProductionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobOns_ProductionNumber",
                table: "JobOns");

            migrationBuilder.DropIndex(
                name: "IX_JobOns_Reference_ProductionNumber",
                table: "JobOns");

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_ProductionNumber",
                table: "JobOns",
                column: "ProductionNumber",
                unique: true);
        }
    }
}
