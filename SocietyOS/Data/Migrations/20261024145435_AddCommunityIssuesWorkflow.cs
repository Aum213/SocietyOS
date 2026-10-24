using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityIssuesWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LocationScope = table.Column<int>(type: "int", nullable: false),
                    LocationDetails = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: true),
                    FlatId = table.Column<int>(type: "int", nullable: true),
                    PhotoStorageKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityIssues_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityIssues_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityIssues_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IssueComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueComments_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssueComments_CommunityIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "CommunityIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InternalManagementNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueStatusHistories_AspNetUsers_ChangedByAdminId",
                        column: x => x.ChangedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssueStatusHistories_CommunityIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "CommunityIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    VoterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VoteValue = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueVotes_AspNetUsers_VoterUserId",
                        column: x => x.VoterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssueVotes_CommunityIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "CommunityIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_BuildingId",
                table: "CommunityIssues",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_Category",
                table: "CommunityIssues",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_CreatedAtUtc",
                table: "CommunityIssues",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_FlatId",
                table: "CommunityIssues",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_ReporterUserId",
                table: "CommunityIssues",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_Status",
                table: "CommunityIssues",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIssues_Status_Category",
                table: "CommunityIssues",
                columns: new[] { "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_AuthorUserId",
                table: "IssueComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_IssueId",
                table: "IssueComments",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueStatusHistories_ChangedByAdminId",
                table: "IssueStatusHistories",
                column: "ChangedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueStatusHistories_IssueId",
                table: "IssueStatusHistories",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueVotes_IssueId_VoterUserId",
                table: "IssueVotes",
                columns: new[] { "IssueId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueVotes_VoterUserId",
                table: "IssueVotes",
                column: "VoterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueComments");

            migrationBuilder.DropTable(
                name: "IssueStatusHistories");

            migrationBuilder.DropTable(
                name: "IssueVotes");

            migrationBuilder.DropTable(
                name: "CommunityIssues");
        }
    }
}
