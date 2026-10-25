using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyHelpDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyHelpListings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ServiceArea = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AvailabilityNotes = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExperienceNotes = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    VerificationStatus = table.Column<int>(type: "int", nullable: false),
                    VerifiedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminInternalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedByResidentUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LastModifiedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyHelpListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyHelpListings_AspNetUsers_LastModifiedByAdminId",
                        column: x => x.LastModifiedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyHelpListings_AspNetUsers_SubmittedByResidentUserId",
                        column: x => x.SubmittedByResidentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyHelpListings_AspNetUsers_VerifiedByAdminId",
                        column: x => x.VerifiedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyHelpShortlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyHelpShortlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyHelpShortlists_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyHelpShortlists_DailyHelpListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "DailyHelpListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_Category",
                table: "DailyHelpListings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_LastModifiedByAdminId",
                table: "DailyHelpListings",
                column: "LastModifiedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_Status",
                table: "DailyHelpListings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_Status_Category",
                table: "DailyHelpListings",
                columns: new[] { "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_SubmittedByResidentUserId",
                table: "DailyHelpListings",
                column: "SubmittedByResidentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpListings_VerifiedByAdminId",
                table: "DailyHelpListings",
                column: "VerifiedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpShortlists_ListingId_UserId",
                table: "DailyHelpShortlists",
                columns: new[] { "ListingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyHelpShortlists_UserId",
                table: "DailyHelpShortlists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyHelpShortlists");

            migrationBuilder.DropTable(
                name: "DailyHelpListings");
        }
    }
}
