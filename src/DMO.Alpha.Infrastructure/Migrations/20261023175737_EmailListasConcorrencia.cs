using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailListasConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Só metadados EF: Nome e Destinatarios passam a fazer parte do
            // WHERE de UPDATE/DELETE. As colunas e os dados ficam iguais.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não houve alteração física do schema.
        }
    }
}
