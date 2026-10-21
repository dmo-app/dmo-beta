using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// A folha da fábrica do Job On tem onde ser guardada:
    /// <list type="number">
    /// <item>a <c>JobOnFolhas</c>, uma linha por Job On, com o Job On como chave e
    /// chave estrangeira e uma coluna <c>text</c> por chave do catálogo
    /// (<c>JobOnFolhaCampos</c>), com as QTD Stock e QTD Máq. das famílias e sem o
    /// FO;</item>
    /// <item>a <c>JobOnFolhaCalLinhas</c>, as linhas CAL (designação, valor e QTD
    /// Máq.), com o índice não único <c>(JobOnId, Ordem)</c>;</item>
    /// <item>as duas apagam em cascata com o Job On.</item>
    /// </list>
    /// Só acrescenta duas tabelas novas: os Job Ons que já existem leem-se como
    /// folha vazia, e a primeira gravação com alguma coisa escrita cria a linha.
    /// </summary>
    public partial class AddJobOnFolha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobOnFolhaCalLinhas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Designacao = table.Column<string>(type: "text", nullable: true),
                    Valor = table.Column<string>(type: "text", nullable: true),
                    QtdMaq = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOnFolhaCalLinhas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOnFolhaCalLinhas_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobOnFolhas",
                columns: table => new
                {
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    CabSeccoes = table.Column<string>(type: "text", nullable: true),
                    CabGota = table.Column<string>(type: "text", nullable: true),
                    CabProcesso = table.Column<string>(type: "text", nullable: true),
                    CabPeso = table.Column<string>(type: "text", nullable: true),
                    CmQtdStock = table.Column<string>(type: "text", nullable: true),
                    CmQtdMaq = table.Column<string>(type: "text", nullable: true),
                    CmUtilizacao = table.Column<string>(type: "text", nullable: true),
                    CmReparador = table.Column<string>(type: "text", nullable: true),
                    CmNotas = table.Column<string>(type: "text", nullable: true),
                    CmTipo = table.Column<string>(type: "text", nullable: true),
                    CmAdaptador = table.Column<string>(type: "text", nullable: true),
                    CmParafuso = table.Column<string>(type: "text", nullable: true),
                    CmPata = table.Column<string>(type: "text", nullable: true),
                    CmPataTol = table.Column<string>(type: "text", nullable: true),
                    CmGargalo = table.Column<string>(type: "text", nullable: true),
                    CmGargaloTol = table.Column<string>(type: "text", nullable: true),
                    CmExterior = table.Column<string>(type: "text", nullable: true),
                    CmExteriorTol = table.Column<string>(type: "text", nullable: true),
                    CmFolga1 = table.Column<string>(type: "text", nullable: true),
                    CmFolga2 = table.Column<string>(type: "text", nullable: true),
                    CmFolga3 = table.Column<string>(type: "text", nullable: true),
                    CmInversao = table.Column<string>(type: "text", nullable: true),
                    CmAlmofada3 = table.Column<string>(type: "text", nullable: true),
                    TpQtdStock = table.Column<string>(type: "text", nullable: true),
                    TpQtdMaq = table.Column<string>(type: "text", nullable: true),
                    TpNotas = table.Column<string>(type: "text", nullable: true),
                    TpDiametroPs = table.Column<string>(type: "text", nullable: true),
                    TpBaciaPs = table.Column<string>(type: "text", nullable: true),
                    TpReferencia = table.Column<string>(type: "text", nullable: true),
                    MfQtdStock = table.Column<string>(type: "text", nullable: true),
                    MfQtdMaq = table.Column<string>(type: "text", nullable: true),
                    MfUtilizacao = table.Column<string>(type: "text", nullable: true),
                    MfReparador = table.Column<string>(type: "text", nullable: true),
                    MfNotas = table.Column<string>(type: "text", nullable: true),
                    MfTipo = table.Column<string>(type: "text", nullable: true),
                    MfAdaptador = table.Column<string>(type: "text", nullable: true),
                    MfParafuso = table.Column<string>(type: "text", nullable: true),
                    MfCorpo = table.Column<string>(type: "text", nullable: true),
                    MfCorpoTol = table.Column<string>(type: "text", nullable: true),
                    MfGargalo = table.Column<string>(type: "text", nullable: true),
                    MfGargaloTol = table.Column<string>(type: "text", nullable: true),
                    MfExterior = table.Column<string>(type: "text", nullable: true),
                    MfExteriorTol = table.Column<string>(type: "text", nullable: true),
                    MfFolga1 = table.Column<string>(type: "text", nullable: true),
                    MfFolga2 = table.Column<string>(type: "text", nullable: true),
                    MfFolga3 = table.Column<string>(type: "text", nullable: true),
                    MfInversao = table.Column<string>(type: "text", nullable: true),
                    MfFundoFinal = table.Column<string>(type: "text", nullable: true),
                    CsReferencia = table.Column<string>(type: "text", nullable: true),
                    CsQtdStock = table.Column<string>(type: "text", nullable: true),
                    CsQtdMaq = table.Column<string>(type: "text", nullable: true),
                    CsNotas = table.Column<string>(type: "text", nullable: true),
                    CsFuros = table.Column<string>(type: "text", nullable: true),
                    CsTubo = table.Column<string>(type: "text", nullable: true),
                    BqQtdStock = table.Column<string>(type: "text", nullable: true),
                    BqQtdMaq = table.Column<string>(type: "text", nullable: true),
                    BqUtilizacao = table.Column<string>(type: "text", nullable: true),
                    BqNotas = table.Column<string>(type: "text", nullable: true),
                    AnReferencia = table.Column<string>(type: "text", nullable: true),
                    AnNotas = table.Column<string>(type: "text", nullable: true),
                    PuReferencia = table.Column<string>(type: "text", nullable: true),
                    PuQtdStock = table.Column<string>(type: "text", nullable: true),
                    PuQtdMaq = table.Column<string>(type: "text", nullable: true),
                    PuNotas = table.Column<string>(type: "text", nullable: true),
                    PuVersao = table.Column<string>(type: "text", nullable: true),
                    PuDesenho = table.Column<string>(type: "text", nullable: true),
                    ArrReferencia = table.Column<string>(type: "text", nullable: true),
                    ArrNotas = table.Column<string>(type: "text", nullable: true),
                    ArrVersao = table.Column<string>(type: "text", nullable: true),
                    ArrDesenho = table.Column<string>(type: "text", nullable: true),
                    PiPincas = table.Column<string>(type: "text", nullable: true),
                    PiDiametro = table.Column<string>(type: "text", nullable: true),
                    PiNotas = table.Column<string>(type: "text", nullable: true),
                    NotasGerais = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOnFolhas", x => x.JobOnId);
                    table.ForeignKey(
                        name: "FK_JobOnFolhas_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobOnFolhaCalLinhas_JobOnId_Ordem",
                table: "JobOnFolhaCalLinhas",
                columns: new[] { "JobOnId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobOnFolhaCalLinhas");

            migrationBuilder.DropTable(
                name: "JobOnFolhas");
        }
    }
}
