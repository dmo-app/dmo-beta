using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToolsReferenciaMaiusculas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As procuras das ferramentas comparam a referência sem olhar a
            // maiúsculas (upper("Reference") = …, e o «começa por» com LIKE
            // 'X%'), porque a referência grava-se como foi escrita; o índice
            // (Type, Reference, Lot) não servia. Este serve as duas (o
            // text_pattern_ops deixa o LIKE do início usar o índice). Só
            // acrescenta: não muda dados e a versão anterior da app continua a
            // funcionar com ele (vistoria BD-04, Diogo 27/10/2026). O EF não
            // descreve índices de expressão, por isso vive só aqui.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Tools_ReferenciaMaiusculas" ON "Tools" (upper("Reference") text_pattern_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_Tools_ReferenciaMaiusculas";""");
        }
    }
}
