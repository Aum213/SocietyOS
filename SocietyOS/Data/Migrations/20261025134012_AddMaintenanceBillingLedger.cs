using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceBillingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceBills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    BillingYear = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<int>(type: "int", nullable: false),
                    IssueDateLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDateLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OriginalAmountInr = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsVoid = table.Column<bool>(type: "bit", nullable: false),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceBills_AspNetUsers_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceBills_AspNetUsers_VoidedByAdminId",
                        column: x => x.VoidedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceBills_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    AmountInr = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAdjustments_AspNetUsers_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAdjustments_MaintenanceBills_BillId",
                        column: x => x.BillId,
                        principalTable: "MaintenanceBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenancePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<int>(type: "int", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AmountReceivedInr = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentDateLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    ExternalTransactionRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenancePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenancePayments_AspNetUsers_RecordedByAdminId",
                        column: x => x.RecordedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePayments_AspNetUsers_ReversedByAdminId",
                        column: x => x.ReversedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePayments_MaintenanceBills_BillId",
                        column: x => x.BillId,
                        principalTable: "MaintenanceBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAdjustments_BillId",
                table: "MaintenanceAdjustments",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAdjustments_CreatedByAdminId",
                table: "MaintenanceAdjustments",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_BillingYear_BillingMonth_IsVoid",
                table: "MaintenanceBills",
                columns: new[] { "BillingYear", "BillingMonth", "IsVoid" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_BillNumber",
                table: "MaintenanceBills",
                column: "BillNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_CreatedByAdminId",
                table: "MaintenanceBills",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_DueDateLocal",
                table: "MaintenanceBills",
                column: "DueDateLocal");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_FlatId_BillingYear_BillingMonth",
                table: "MaintenanceBills",
                columns: new[] { "FlatId", "BillingYear", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_VoidedByAdminId",
                table: "MaintenanceBills",
                column: "VoidedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePayments_BillId_IsReversed",
                table: "MaintenancePayments",
                columns: new[] { "BillId", "IsReversed" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePayments_PaymentDateLocal",
                table: "MaintenancePayments",
                column: "PaymentDateLocal");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePayments_PaymentReference",
                table: "MaintenancePayments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePayments_RecordedByAdminId",
                table: "MaintenancePayments",
                column: "RecordedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePayments_ReversedByAdminId",
                table: "MaintenancePayments",
                column: "ReversedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceAdjustments");

            migrationBuilder.DropTable(
                name: "MaintenancePayments");

            migrationBuilder.DropTable(
                name: "MaintenanceBills");
        }
    }
}
