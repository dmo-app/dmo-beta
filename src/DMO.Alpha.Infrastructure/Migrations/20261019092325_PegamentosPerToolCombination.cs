using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// Uma folha de Pegamentos por combinação de ferramentas em cada
    /// controlo (beta.236; sweep de 19/10/2026, DB-3). Até aqui só o trinco
    /// do registo a garantia; a base passa a recusar a segunda folha. Só
    /// acrescenta um índice, sem mudar dados: as folhas sem controlo ficam
    /// livres, e as de uma ferramenta apagada (REMOVED) ou com um componente
    /// em falta não contam (associar uns Pegamentos sem Job On sem MF
    /// continua a passar, como até aqui).
    /// </summary>
    public partial class PegamentosPerToolCombination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_ControloId_CmToolId_BqToolId_MfToolId",
                table: "Pegamentos",
                columns: new[] { "ControloId", "CmToolId", "BqToolId", "MfToolId" },
                unique: true,
                filter: "\"ControloId\" IS NOT NULL AND \"CmToolId\" IS DISTINCT FROM 'REMOVED' AND \"BqToolId\" IS DISTINCT FROM 'REMOVED' AND \"MfToolId\" IS DISTINCT FROM 'REMOVED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pegamentos_ControloId_CmToolId_BqToolId_MfToolId",
                table: "Pegamentos");
        }
    }
}
