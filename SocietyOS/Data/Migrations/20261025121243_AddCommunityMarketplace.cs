using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceListings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ListingType = table.Column<int>(type: "int", nullable: false),
                    PriceInr = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SellerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    SellerContactPhone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    ImageStorageKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModeratedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModeratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModerationNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoldOrWithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceListings_AspNetUsers_ModeratedByAdminId",
                        column: x => x.ModeratedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceListings_AspNetUsers_SellerUserId",
                        column: x => x.SellerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceListings_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceListings_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceListingReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceListingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceListingReports_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceListingReports_MarketplaceListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "MarketplaceListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListingReports_ListingId_ReporterUserId",
                table: "MarketplaceListingReports",
                columns: new[] { "ListingId", "ReporterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListingReports_ReporterUserId",
                table: "MarketplaceListingReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_BuildingId",
                table: "MarketplaceListings",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_FlatId",
                table: "MarketplaceListings",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_ModeratedByAdminId",
                table: "MarketplaceListings",
                column: "ModeratedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_SellerUserId_CreatedAtUtc",
                table: "MarketplaceListings",
                columns: new[] { "SellerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_Status_Category_CreatedAtUtc",
                table: "MarketplaceListings",
                columns: new[] { "Status", "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceListings_Status_ListingType",
                table: "MarketplaceListings",
                columns: new[] { "Status", "ListingType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceListingReports");

            migrationBuilder.DropTable(
                name: "MarketplaceListings");
        }
    }
}
