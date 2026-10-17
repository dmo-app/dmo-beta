using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// Schema completo da app numa única migração. Uma base nova é criada por
    /// esta migração; uma base existente regista-a no histórico sem a executar.
    /// </summary>
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BqMovementDeletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BqMovementId = table.Column<int>(type: "integer", nullable: false),
                    BqRepairTraceId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MachineCodeFrozen = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DeletedByNameFrozen = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BqMovementDeletions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BqRepairers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BqRepairers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlassDensities",
                columns: table => new
                {
                    Process = table.Column<string>(type: "text", nullable: false),
                    DensityGramsPerCm3 = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlassDensities", x => x.Process);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleSettings",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleSettings", x => x.SettingKey);
                });

            migrationBuilder.CreateTable(
                name: "PegamentosTolerances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToleranceMm = table.Column<decimal>(type: "numeric", nullable: false),
                    SingletonDiscriminator = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PegamentosTolerances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Capabilities = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tools",
                columns: table => new
                {
                    ToolId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Lot = table.Column<string>(type: "text", nullable: false),
                    AssociatedMfReference = table.Column<string>(type: "text", nullable: true),
                    BqAccountedQuantity = table.Column<int>(type: "integer", nullable: true),
                    TechnicalName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tools", x => x.ToolId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ProviderUserId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RequiresPasswordChange = table.Column<bool>(type: "boolean", nullable: false),
                    IsStandBy = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MachineId = table.Column<int>(type: "integer", nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: true),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: true),
                    CaloteMm = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOns_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolMachineLinks",
                columns: table => new
                {
                    ToolId = table.Column<string>(type: "text", nullable: false),
                    MachineId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolMachineLinks", x => new { x.ToolId, x.MachineId });
                    table.ForeignKey(
                        name: "FK_ToolMachineLinks_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ToolMachineLinks_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolTechnicalContexts",
                columns: table => new
                {
                    ToolId = table.Column<string>(type: "text", nullable: false),
                    Process = table.Column<string>(type: "text", nullable: true),
                    NominalWeightGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    VolumePuncaoCm3 = table.Column<decimal>(type: "numeric", nullable: true),
                    VolumeMarisaCm3 = table.Column<decimal>(type: "numeric", nullable: true),
                    DiametroGargaloMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpDiametroMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpRaioMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpSagittaMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpRaioBaseMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpRaioEsferaMm = table.Column<decimal>(type: "numeric", nullable: true),
                    TpVolumeMm3 = table.Column<decimal>(type: "numeric", nullable: true),
                    McaliperUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolTechnicalContexts", x => x.ToolId);
                    table.ForeignKey(
                        name: "FK_ToolTechnicalContexts_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateAssignments_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BqContexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BqContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BqContexts_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BqContexts_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CmContexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmContexts_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmContexts_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Controlos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByOperatorId = table.Column<string>(type: "text", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotApprovedByOperatorId = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    NotApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Controlos", x => x.Id);
                    table.CheckConstraint("CK_Controlos_ApprovedOrNotApproved", "\"ApprovedAtUtc\" IS NULL OR \"NotApprovedAtUtc\" IS NULL");
                    table.ForeignKey(
                        name: "FK_Controlos_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobOnChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OldValueFrozen = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NewValueFrozen = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ChangedByNameFrozen = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOnChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOnChanges_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobOnLotNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    NewToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToolReferenceFrozen = table.Column<string>(type: "text", nullable: false),
                    PreviousLotFrozen = table.Column<string>(type: "text", nullable: true),
                    NewLotFrozen = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOnLotNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOnLotNotes_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobOnLotNotes_Tools_NewToolId",
                        column: x => x.NewToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MfContexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOnId = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfContexts_JobOns_JobOnId",
                        column: x => x.JobOnId,
                        principalTable: "JobOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MfContexts_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BqRepairTraces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolId = table.Column<string>(type: "text", nullable: true),
                    BqContextId = table.Column<int>(type: "integer", nullable: true),
                    ToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    ToolLotFrozen = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BqRepairTraces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BqRepairTraces_BqContexts_BqContextId",
                        column: x => x.BqContextId,
                        principalTable: "BqContexts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BqRepairTraces_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControloAreaStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: false),
                    Area = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControloAreaStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControloAreaStates_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControloViewings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ViewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControloViewings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControloViewings_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pegamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: true),
                    CmToolId = table.Column<string>(type: "text", nullable: true),
                    BqToolId = table.Column<string>(type: "text", nullable: true),
                    MfToolId = table.Column<string>(type: "text", nullable: true),
                    ToleranceMm = table.Column<decimal>(type: "numeric", nullable: false),
                    CmToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    CmToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    BqToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    BqToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    MfToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    MfToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pegamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pegamentos_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pegamentos_Tools_BqToolId",
                        column: x => x.BqToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pegamentos_Tools_CmToolId",
                        column: x => x.CmToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pegamentos_Tools_MfToolId",
                        column: x => x.MfToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pesos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CmToolId = table.Column<string>(type: "text", nullable: false),
                    BqToolId = table.Column<string>(type: "text", nullable: false),
                    Process = table.Column<string>(type: "text", nullable: false),
                    WaterTemperatureC = table.Column<int>(type: "integer", nullable: false),
                    VolumeMarisaCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumePuncaoCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    NominalWeightGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    GlassDensityGramsPerCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    CmToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    CmToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    BqToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    BqToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    ControloId = table.Column<int>(type: "integer", nullable: true),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ProducaoAnteriorControloId = table.Column<int>(type: "integer", nullable: true),
                    ProducaoAnteriorPesoGramas = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pesos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pesos_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pesos_Controlos_ProducaoAnteriorControloId",
                        column: x => x.ProducaoAnteriorControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pesos_Tools_BqToolId",
                        column: x => x.BqToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pesos_Tools_CmToolId",
                        column: x => x.CmToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Puncoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PunchReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Lot = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puncoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Puncoes_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BqMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BqRepairTraceId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    MachineCodeFrozen = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Discrepancy = table.Column<int>(type: "integer", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredByOperatorId = table.Column<string>(type: "text", nullable: true),
                    BqRepairerId = table.Column<int>(type: "integer", nullable: true),
                    BqRepairerNameFrozen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrectedByNameFrozen = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CorrectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BqMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BqMovements_BqRepairTraces_BqRepairTraceId",
                        column: x => x.BqRepairTraceId,
                        principalTable: "BqRepairTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BqMovements_BqRepairers_BqRepairerId",
                        column: x => x.BqRepairerId,
                        principalTable: "BqRepairers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PegamentosMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PegamentosId = table.Column<int>(type: "integer", nullable: false),
                    RowPosition = table.Column<int>(type: "integer", nullable: false),
                    Component = table.Column<string>(type: "text", nullable: false),
                    CosturaMm = table.Column<decimal>(type: "numeric", nullable: false),
                    ContraCosturaMm = table.Column<decimal>(type: "numeric", nullable: true),
                    NominalGargaloMm = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PegamentosMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PegamentosMeasurements_Pegamentos_PegamentosId",
                        column: x => x.PegamentosId,
                        principalTable: "Pegamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PesoComparacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: false),
                    PesoInicialId = table.Column<int>(type: "integer", nullable: false),
                    CmToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WaterTemperatureC = table.Column<int>(type: "integer", nullable: false),
                    WaterDensityGramsPerCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    GlassDensityGramsPerCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumeMarisaCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumePuncaoCm3 = table.Column<decimal>(type: "numeric", nullable: false),
                    CmToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    CmToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesoComparacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesoComparacoes_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PesoComparacoes_Pesos_PesoInicialId",
                        column: x => x.PesoInicialId,
                        principalTable: "Pesos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PesoComparacoes_Tools_CmToolId",
                        column: x => x.CmToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PesoMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PesoId = table.Column<int>(type: "integer", nullable: false),
                    RowPosition = table.Column<int>(type: "integer", nullable: false),
                    CmSubject = table.Column<string>(type: "text", nullable: false),
                    WaterWeightGrams = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesoMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesoMeasurements_Pesos_PesoId",
                        column: x => x.PesoId,
                        principalTable: "Pesos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuncaoInspecoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuncoesId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Lot = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PunchSeen = table.Column<bool>(type: "boolean", nullable: false),
                    CoolerSeen = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredBy = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuncaoInspecoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuncaoInspecoes_Puncoes_PuncoesId",
                        column: x => x.PuncoesId,
                        principalTable: "Puncoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PesoComparacaoMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComparacaoId = table.Column<int>(type: "integer", nullable: false),
                    RowPosition = table.Column<int>(type: "integer", nullable: false),
                    CmSubject = table.Column<string>(type: "text", nullable: false),
                    WaterWeightGrams = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesoComparacaoMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesoComparacaoMeasurements_PesoComparacoes_ComparacaoId",
                        column: x => x.ComparacaoId,
                        principalTable: "PesoComparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BqContexts_JobOnId",
                table: "BqContexts",
                column: "JobOnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BqContexts_ToolId",
                table: "BqContexts",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovements_BqRepairerId",
                table: "BqMovements",
                column: "BqRepairerId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovements_BqRepairTraceId",
                table: "BqMovements",
                column: "BqRepairTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_BqMovements_OccurredAtUtc",
                table: "BqMovements",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BqRepairers_Name",
                table: "BqRepairers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BqRepairTraces_BqContextId",
                table: "BqRepairTraces",
                column: "BqContextId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BqRepairTraces_ToolId",
                table: "BqRepairTraces",
                column: "ToolId",
                unique: true,
                filter: "\"BqContextId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CmContexts_JobOnId",
                table: "CmContexts",
                column: "JobOnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmContexts_ToolId",
                table: "CmContexts",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ControloAreaStates_ControloId_Area",
                table: "ControloAreaStates",
                columns: new[] { "ControloId", "Area" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Controlos_JobOnId",
                table: "Controlos",
                column: "JobOnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControloViewings_ControloId_OperatorId",
                table: "ControloViewings",
                columns: new[] { "ControloId", "OperatorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOnChanges_JobOnId_ChangedAtUtc",
                table: "JobOnChanges",
                columns: new[] { "JobOnId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOnLotNotes_Dia",
                table: "JobOnLotNotes",
                column: "Dia");

            migrationBuilder.CreateIndex(
                name: "IX_JobOnLotNotes_JobOnId_Dia",
                table: "JobOnLotNotes",
                columns: new[] { "JobOnId", "Dia" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOnLotNotes_NewToolId",
                table: "JobOnLotNotes",
                column: "NewToolId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_MachineId_InicioEm_FimEm",
                table: "JobOns",
                columns: new[] { "MachineId", "InicioEm", "FimEm" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_ProductionNumber",
                table: "JobOns",
                column: "ProductionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOns_Reference",
                table: "JobOns",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Code",
                table: "Machines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfContexts_JobOnId",
                table: "MfContexts",
                column: "JobOnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfContexts_ToolId",
                table: "MfContexts",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_BqToolId",
                table: "Pegamentos",
                column: "BqToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_CmToolId",
                table: "Pegamentos",
                column: "CmToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_ControloId",
                table: "Pegamentos",
                column: "ControloId");

            migrationBuilder.CreateIndex(
                name: "IX_Pegamentos_MfToolId",
                table: "Pegamentos",
                column: "MfToolId");

            migrationBuilder.CreateIndex(
                name: "IX_PegamentosMeasurements_PegamentosId_RowPosition",
                table: "PegamentosMeasurements",
                columns: new[] { "PegamentosId", "RowPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PegamentosTolerances_SingletonDiscriminator",
                table: "PegamentosTolerances",
                column: "SingletonDiscriminator",
                unique: true,
                filter: "\"SingletonDiscriminator\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacaoMeasurements_ComparacaoId_RowPosition",
                table: "PesoComparacaoMeasurements",
                columns: new[] { "ComparacaoId", "RowPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacoes_CmToolId",
                table: "PesoComparacoes",
                column: "CmToolId");

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacoes_ControloId",
                table: "PesoComparacoes",
                column: "ControloId");

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacoes_PesoInicialId",
                table: "PesoComparacoes",
                column: "PesoInicialId");

            migrationBuilder.CreateIndex(
                name: "IX_PesoMeasurements_PesoId_RowPosition",
                table: "PesoMeasurements",
                columns: new[] { "PesoId", "RowPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_BqToolId",
                table: "Pesos",
                column: "BqToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_CmToolId",
                table: "Pesos",
                column: "CmToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ControloId",
                table: "Pesos",
                column: "ControloId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pesos_ProducaoAnteriorControloId",
                table: "Pesos",
                column: "ProducaoAnteriorControloId");

            migrationBuilder.CreateIndex(
                name: "IX_PuncaoInspecoes_PuncoesId_Position",
                table: "PuncaoInspecoes",
                columns: new[] { "PuncoesId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puncoes_ControloId",
                table: "Puncoes",
                column: "ControloId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puncoes_Reference",
                table: "Puncoes",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateAssignments_TemplateId",
                table: "TemplateAssignments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateAssignments_UserId",
                table: "TemplateAssignments",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TemplateKey",
                table: "Templates",
                column: "TemplateKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolMachineLinks_MachineId",
                table: "ToolMachineLinks",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_Type_Reference_Lot",
                table: "Tools",
                columns: new[] { "Type", "Reference", "Lot" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OperatorId",
                table: "Users",
                column: "OperatorId",
                unique: true);

            // A linha REMOVED não está no modelo: é para onde passam as
            // produções, Pesos e Pegamentos de uma ferramenta eliminada.
            migrationBuilder.InsertData(
                table: "Tools",
                columns: new[] { "ToolId", "Type", "Reference", "Lot" },
                values: new object[] { "REMOVED", "CM", "REMOVED", "REMOVED" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BqMovementDeletions");

            migrationBuilder.DropTable(
                name: "BqMovements");

            migrationBuilder.DropTable(
                name: "CmContexts");

            migrationBuilder.DropTable(
                name: "ControloAreaStates");

            migrationBuilder.DropTable(
                name: "ControloViewings");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "GlassDensities");

            migrationBuilder.DropTable(
                name: "JobOnChanges");

            migrationBuilder.DropTable(
                name: "JobOnLotNotes");

            migrationBuilder.DropTable(
                name: "MfContexts");

            migrationBuilder.DropTable(
                name: "ModuleSettings");

            migrationBuilder.DropTable(
                name: "PegamentosMeasurements");

            migrationBuilder.DropTable(
                name: "PegamentosTolerances");

            migrationBuilder.DropTable(
                name: "PesoComparacaoMeasurements");

            migrationBuilder.DropTable(
                name: "PesoMeasurements");

            migrationBuilder.DropTable(
                name: "PuncaoInspecoes");

            migrationBuilder.DropTable(
                name: "TemplateAssignments");

            migrationBuilder.DropTable(
                name: "ToolMachineLinks");

            migrationBuilder.DropTable(
                name: "ToolTechnicalContexts");

            migrationBuilder.DropTable(
                name: "BqRepairTraces");

            migrationBuilder.DropTable(
                name: "BqRepairers");

            migrationBuilder.DropTable(
                name: "Pegamentos");

            migrationBuilder.DropTable(
                name: "PesoComparacoes");

            migrationBuilder.DropTable(
                name: "Puncoes");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "BqContexts");

            migrationBuilder.DropTable(
                name: "Pesos");

            migrationBuilder.DropTable(
                name: "Controlos");

            migrationBuilder.DropTable(
                name: "Tools");

            migrationBuilder.DropTable(
                name: "JobOns");

            migrationBuilder.DropTable(
                name: "Machines");
        }
    }
}
