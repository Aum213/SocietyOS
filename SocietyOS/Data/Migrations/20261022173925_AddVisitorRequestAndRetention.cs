using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorRequestAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PurposeDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccompanyingPersons = table.Column<int>(type: "int", nullable: false),
                    PhotoStorageKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredByGuardId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionByResidentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DecisionNotes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitorRequests_AspNetUsers_DecisionByResidentId",
                        column: x => x.DecisionByResidentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitorRequests_AspNetUsers_RegisteredByGuardId",
                        column: x => x.RegisteredByGuardId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitorRequests_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_DecisionByResidentId",
                table: "VisitorRequests",
                column: "DecisionByResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_ExpiresAtUtc",
                table: "VisitorRequests",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_FlatId",
                table: "VisitorRequests",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_FlatId_Status",
                table: "VisitorRequests",
                columns: new[] { "FlatId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_RegisteredByGuardId",
                table: "VisitorRequests",
                column: "RegisteredByGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorRequests_Status",
                table: "VisitorRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitorRequests");
        }
    }
}
