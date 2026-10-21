using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoInterna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReparacoesInternas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    MachineId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    CmContextId = table.Column<int>(type: "integer", nullable: true),
                    MfContextId = table.Column<int>(type: "integer", nullable: true),
                    Numero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RegistadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiaOperacional = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacoesInternas", x => x.Id);
                    table.CheckConstraint("CK_ReparacoesInternas_Contexto", "(\"Tipo\" = 1 AND \"CmContextId\" IS NOT NULL AND \"MfContextId\" IS NULL) OR (\"Tipo\" = 2 AND \"MfContextId\" IS NOT NULL AND \"CmContextId\" IS NULL)");
                    table.CheckConstraint("CK_ReparacoesInternas_Numero", "btrim(\"Numero\") <> '' AND \"Numero\" = btrim(\"Numero\")");
                    table.ForeignKey(
                        name: "FK_ReparacoesInternas_CmContexts_CmContextId",
                        column: x => x.CmContextId,
                        principalTable: "CmContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacoesInternas_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacoesInternas_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacoesInternas_MfContexts_MfContextId",
                        column: x => x.MfContextId,
                        principalTable: "MfContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacoesInternas_Users_RegistadoPorUserId",
                        column: x => x.RegistadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_CmContextId",
                table: "ReparacoesInternas",
                column: "CmContextId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_DiaOperacional",
                table: "ReparacoesInternas",
                column: "DiaOperacional");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_JobOnId",
                table: "ReparacoesInternas",
                column: "JobOnId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_MachineId_DiaOperacional",
                table: "ReparacoesInternas",
                columns: new[] { "MachineId", "DiaOperacional" });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_MfContextId",
                table: "ReparacoesInternas",
                column: "MfContextId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacoesInternas_RegistadoPorUserId_DiaOperacional",
                table: "ReparacoesInternas",
                columns: new[] { "RegistadoPorUserId", "DiaOperacional" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReparacoesInternas");
        }
    }
}
