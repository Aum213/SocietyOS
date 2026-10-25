using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencySosWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmergencyIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NotificationStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RespondingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponderUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_AspNetUsers_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_AspNetUsers_ResponderUserId",
                        column: x => x.ResponderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SocietyEmergencyContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocietyEmergencyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocietyEmergencyContacts_AspNetUsers_UpdatedByAdminId",
                        column: x => x.UpdatedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyStatusHistories_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyStatusHistories_EmergencyIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "EmergencyIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_AcknowledgedByUserId",
                table: "EmergencyIncidents",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_BuildingId",
                table: "EmergencyIncidents",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_CancelledByUserId",
                table: "EmergencyIncidents",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_FlatId",
                table: "EmergencyIncidents",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_ReferenceCode",
                table: "EmergencyIncidents",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_ReporterUserId_CreatedAtUtc",
                table: "EmergencyIncidents",
                columns: new[] { "ReporterUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_ResolvedByUserId",
                table: "EmergencyIncidents",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_ResponderUserId",
                table: "EmergencyIncidents",
                column: "ResponderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_Status_CreatedAtUtc",
                table: "EmergencyIncidents",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyStatusHistories_ActorUserId",
                table: "EmergencyStatusHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyStatusHistories_IncidentId",
                table: "EmergencyStatusHistories",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_SocietyEmergencyContacts_IsActive_DisplayOrder",
                table: "SocietyEmergencyContacts",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SocietyEmergencyContacts_UpdatedByAdminId",
                table: "SocietyEmergencyContacts",
                column: "UpdatedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyStatusHistories");

            migrationBuilder.DropTable(
                name: "SocietyEmergencyContacts");

            migrationBuilder.DropTable(
                name: "EmergencyIncidents");
        }
    }
}
