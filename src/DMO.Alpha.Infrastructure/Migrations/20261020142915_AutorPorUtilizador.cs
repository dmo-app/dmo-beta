using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// O autor pelo utilizador (fundações do full, E3 do DMO-FULL): ao lado
    /// de cada autor guardado como texto, o <c>Users.Id</c> de quem gravou
    /// (NULL nos registos de antes, no ADMIN e sem sessão), com índice e chave
    /// estrangeira ON DELETE SET NULL. Só acrescenta colunas.
    /// </summary>
    public partial class AutorPorUtilizador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateWarningByUserId",
                table: "Tools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Puncoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredByUserId",
                table: "PuncaoInspecoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistadoPorUserId",
                table: "Pesos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PorUserId",
                table: "PegamentosMeasurementHistorico",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistadoPorUserId",
                table: "Pegamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChangedByUserId",
                table: "JobOnChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnviadoPorUserId",
                table: "EmailEnvios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ViewerUserId",
                table: "ControloViewings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Controlos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotApprovedByUserId",
                table: "Controlos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecididoPorUserId",
                table: "ControloDecisoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "ControloAreaStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PorUserId",
                table: "ControloAreaStateHistorico",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscrepancyResetByUserId",
                table: "BqRepairTraces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectedByUserId",
                table: "BqMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredByUserId",
                table: "BqMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "BqMovementDeletions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_DuplicateWarningByUserId",
                table: "Tools",
                column: "DuplicateWarningByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Puncoes_UpdatedByUserId",
                table: "Puncoes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PuncaoInspecoes_RegisteredByUserId",
                table: "PuncaoInspecoes",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_RegistadoPorUserId",
                table: "Pesos",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacaoCmDecisoes_AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                column: "AprovadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacaoCmDecisoes_EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                column: "EscolhidoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PegamentosMeasurementHistorico_PorUserId",
                table: "PegamentosMeasurementHistorico",
                column: "PorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_RegistadoPorUserId",
                table: "Pegamentos",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOnChanges_ChangedByUserId",
                table: "JobOnChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailEnvios_EnviadoPorUserId",
                table: "EmailEnvios",
                column: "EnviadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControloViewings_ViewerUserId",
                table: "ControloViewings",
                column: "ViewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Controlos_ApprovedByUserId",
                table: "Controlos",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Controlos_NotApprovedByUserId",
                table: "Controlos",
                column: "NotApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControloDecisoes_DecididoPorUserId",
                table: "ControloDecisoes",
                column: "DecididoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControloAreaStates_UpdatedByUserId",
                table: "ControloAreaStates",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControloAreaStateHistorico_PorUserId",
                table: "ControloAreaStateHistorico",
                column: "PorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BqRepairTraces_DiscrepancyResetByUserId",
                table: "BqRepairTraces",
                column: "DiscrepancyResetByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovements_CorrectedByUserId",
                table: "BqMovements",
                column: "CorrectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovements_RegisteredByUserId",
                table: "BqMovements",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovementDeletions_DeletedByUserId",
                table: "BqMovementDeletions",
                column: "DeletedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BqMovementDeletions_Users_DeletedByUserId",
                table: "BqMovementDeletions",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BqMovements_Users_CorrectedByUserId",
                table: "BqMovements",
                column: "CorrectedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BqMovements_Users_RegisteredByUserId",
                table: "BqMovements",
                column: "RegisteredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BqRepairTraces_Users_DiscrepancyResetByUserId",
                table: "BqRepairTraces",
                column: "DiscrepancyResetByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ControloAreaStateHistorico_Users_PorUserId",
                table: "ControloAreaStateHistorico",
                column: "PorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ControloAreaStates_Users_UpdatedByUserId",
                table: "ControloAreaStates",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ControloDecisoes_Users_DecididoPorUserId",
                table: "ControloDecisoes",
                column: "DecididoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Controlos_Users_ApprovedByUserId",
                table: "Controlos",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Controlos_Users_NotApprovedByUserId",
                table: "Controlos",
                column: "NotApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ControloViewings_Users_ViewerUserId",
                table: "ControloViewings",
                column: "ViewerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailEnvios_Users_EnviadoPorUserId",
                table: "EmailEnvios",
                column: "EnviadoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobOnChanges_Users_ChangedByUserId",
                table: "JobOnChanges",
                column: "ChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pegamentos_Users_RegistadoPorUserId",
                table: "Pegamentos",
                column: "RegistadoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PegamentosMeasurementHistorico_Users_PorUserId",
                table: "PegamentosMeasurementHistorico",
                column: "PorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PesoComparacaoCmDecisoes_Users_AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                column: "AprovadoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PesoComparacaoCmDecisoes_Users_EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes",
                column: "EscolhidoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pesos_Users_RegistadoPorUserId",
                table: "Pesos",
                column: "RegistadoPorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PuncaoInspecoes_Users_RegisteredByUserId",
                table: "PuncaoInspecoes",
                column: "RegisteredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Puncoes_Users_UpdatedByUserId",
                table: "Puncoes",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tools_Users_DuplicateWarningByUserId",
                table: "Tools",
                column: "DuplicateWarningByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BqMovementDeletions_Users_DeletedByUserId",
                table: "BqMovementDeletions");

            migrationBuilder.DropForeignKey(
                name: "FK_BqMovements_Users_CorrectedByUserId",
                table: "BqMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_BqMovements_Users_RegisteredByUserId",
                table: "BqMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_BqRepairTraces_Users_DiscrepancyResetByUserId",
                table: "BqRepairTraces");

            migrationBuilder.DropForeignKey(
                name: "FK_ControloAreaStateHistorico_Users_PorUserId",
                table: "ControloAreaStateHistorico");

            migrationBuilder.DropForeignKey(
                name: "FK_ControloAreaStates_Users_UpdatedByUserId",
                table: "ControloAreaStates");

            migrationBuilder.DropForeignKey(
                name: "FK_ControloDecisoes_Users_DecididoPorUserId",
                table: "ControloDecisoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Controlos_Users_ApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropForeignKey(
                name: "FK_Controlos_Users_NotApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropForeignKey(
                name: "FK_ControloViewings_Users_ViewerUserId",
                table: "ControloViewings");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailEnvios_Users_EnviadoPorUserId",
                table: "EmailEnvios");

            migrationBuilder.DropForeignKey(
                name: "FK_JobOnChanges_Users_ChangedByUserId",
                table: "JobOnChanges");

            migrationBuilder.DropForeignKey(
                name: "FK_Pegamentos_Users_RegistadoPorUserId",
                table: "Pegamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_PegamentosMeasurementHistorico_Users_PorUserId",
                table: "PegamentosMeasurementHistorico");

            migrationBuilder.DropForeignKey(
                name: "FK_PesoComparacaoCmDecisoes_Users_AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropForeignKey(
                name: "FK_PesoComparacaoCmDecisoes_Users_EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Pesos_Users_RegistadoPorUserId",
                table: "Pesos");

            migrationBuilder.DropForeignKey(
                name: "FK_PuncaoInspecoes_Users_RegisteredByUserId",
                table: "PuncaoInspecoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Puncoes_Users_UpdatedByUserId",
                table: "Puncoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Tools_Users_DuplicateWarningByUserId",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_DuplicateWarningByUserId",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Puncoes_UpdatedByUserId",
                table: "Puncoes");

            migrationBuilder.DropIndex(
                name: "IX_PuncaoInspecoes_RegisteredByUserId",
                table: "PuncaoInspecoes");

            migrationBuilder.DropIndex(
                name: "IX_Pesos_RegistadoPorUserId",
                table: "Pesos");

            migrationBuilder.DropIndex(
                name: "IX_PesoComparacaoCmDecisoes_AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropIndex(
                name: "IX_PesoComparacaoCmDecisoes_EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropIndex(
                name: "IX_PegamentosMeasurementHistorico_PorUserId",
                table: "PegamentosMeasurementHistorico");

            migrationBuilder.DropIndex(
                name: "IX_Pegamentos_RegistadoPorUserId",
                table: "Pegamentos");

            migrationBuilder.DropIndex(
                name: "IX_JobOnChanges_ChangedByUserId",
                table: "JobOnChanges");

            migrationBuilder.DropIndex(
                name: "IX_EmailEnvios_EnviadoPorUserId",
                table: "EmailEnvios");

            migrationBuilder.DropIndex(
                name: "IX_ControloViewings_ViewerUserId",
                table: "ControloViewings");

            migrationBuilder.DropIndex(
                name: "IX_Controlos_ApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropIndex(
                name: "IX_Controlos_NotApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropIndex(
                name: "IX_ControloDecisoes_DecididoPorUserId",
                table: "ControloDecisoes");

            migrationBuilder.DropIndex(
                name: "IX_ControloAreaStates_UpdatedByUserId",
                table: "ControloAreaStates");

            migrationBuilder.DropIndex(
                name: "IX_ControloAreaStateHistorico_PorUserId",
                table: "ControloAreaStateHistorico");

            migrationBuilder.DropIndex(
                name: "IX_BqRepairTraces_DiscrepancyResetByUserId",
                table: "BqRepairTraces");

            migrationBuilder.DropIndex(
                name: "IX_BqMovements_CorrectedByUserId",
                table: "BqMovements");

            migrationBuilder.DropIndex(
                name: "IX_BqMovements_RegisteredByUserId",
                table: "BqMovements");

            migrationBuilder.DropIndex(
                name: "IX_BqMovementDeletions_DeletedByUserId",
                table: "BqMovementDeletions");

            migrationBuilder.DropColumn(
                name: "DuplicateWarningByUserId",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Puncoes");

            migrationBuilder.DropColumn(
                name: "RegisteredByUserId",
                table: "PuncaoInspecoes");

            migrationBuilder.DropColumn(
                name: "RegistadoPorUserId",
                table: "Pesos");

            migrationBuilder.DropColumn(
                name: "AprovadoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropColumn(
                name: "EscolhidoPorUserId",
                table: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropColumn(
                name: "PorUserId",
                table: "PegamentosMeasurementHistorico");

            migrationBuilder.DropColumn(
                name: "RegistadoPorUserId",
                table: "Pegamentos");

            migrationBuilder.DropColumn(
                name: "ChangedByUserId",
                table: "JobOnChanges");

            migrationBuilder.DropColumn(
                name: "EnviadoPorUserId",
                table: "EmailEnvios");

            migrationBuilder.DropColumn(
                name: "ViewerUserId",
                table: "ControloViewings");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropColumn(
                name: "NotApprovedByUserId",
                table: "Controlos");

            migrationBuilder.DropColumn(
                name: "DecididoPorUserId",
                table: "ControloDecisoes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ControloAreaStates");

            migrationBuilder.DropColumn(
                name: "PorUserId",
                table: "ControloAreaStateHistorico");

            migrationBuilder.DropColumn(
                name: "DiscrepancyResetByUserId",
                table: "BqRepairTraces");

            migrationBuilder.DropColumn(
                name: "CorrectedByUserId",
                table: "BqMovements");

            migrationBuilder.DropColumn(
                name: "RegisteredByUserId",
                table: "BqMovements");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "BqMovementDeletions");
        }
    }
}
