using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FerramentaB2Modo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "B2Modo",
                table: "Tools",
                type: "integer",
                nullable: true);

            // As quantidades da folha «Codificação Moldes e acessórios - MG»
            // (Diogo, 28/10/2026) para as linhas sem total gravado; um total
            // já gravado fica. A B2 simples começa como a B2 Dupla.
            migrationBuilder.Sql("""
                INSERT INTO "ModuleSettings" ("SettingKey", "SettingValue") VALUES
                ('Ferramentas:LotQuantity:CM:B1', '96'),
                ('Ferramentas:LotQuantity:MF:B1', '72'),
                ('Ferramentas:LotQuantity:BQ:B1', '120'),
                ('Ferramentas:LotQuantity:AN:B1', '180'),
                ('Ferramentas:LotQuantity:CM:B2:Dupla', '48'),
                ('Ferramentas:LotQuantity:MF:B2:Dupla', '36'),
                ('Ferramentas:LotQuantity:BQ:B2:Dupla', '60'),
                ('Ferramentas:LotQuantity:AN:B2:Dupla', '90'),
                ('Ferramentas:LotQuantity:CM:B2:Tripla', '72'),
                ('Ferramentas:LotQuantity:MF:B2:Tripla', '54'),
                ('Ferramentas:LotQuantity:BQ:B2:Tripla', '90'),
                ('Ferramentas:LotQuantity:AN:B2:Tripla', '135'),
                ('Ferramentas:LotQuantity:CM:B3', '72'),
                ('Ferramentas:LotQuantity:MF:B3', '54'),
                ('Ferramentas:LotQuantity:BQ:B3', '90'),
                ('Ferramentas:LotQuantity:AN:B3', '135'),
                ('Ferramentas:LotQuantity:CM:C1', '48'),
                ('Ferramentas:LotQuantity:MF:C1', '36'),
                ('Ferramentas:LotQuantity:BQ:C1', '60'),
                ('Ferramentas:LotQuantity:AN:C1', '90'),
                ('Ferramentas:LotQuantity:CM:C2', '96'),
                ('Ferramentas:LotQuantity:MF:C2', '72'),
                ('Ferramentas:LotQuantity:BQ:C2', '120'),
                ('Ferramentas:LotQuantity:AN:C2', '180'),
                ('Ferramentas:LotQuantity:CM:C3', '72'),
                ('Ferramentas:LotQuantity:MF:C3', '54'),
                ('Ferramentas:LotQuantity:BQ:C3', '90'),
                ('Ferramentas:LotQuantity:AN:C3', '135'),
                ('Ferramentas:LotQuantity:CM:B2', '48'),
                ('Ferramentas:LotQuantity:MF:B2', '36'),
                ('Ferramentas:LotQuantity:BQ:B2', '60'),
                ('Ferramentas:LotQuantity:AN:B2', '90')
                ON CONFLICT ("SettingKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "B2Modo",
                table: "Tools");
        }
    }
}
