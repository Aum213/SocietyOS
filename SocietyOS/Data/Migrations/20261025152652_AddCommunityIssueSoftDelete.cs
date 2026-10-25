using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityIssueSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "CommunityIssues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "CommunityIssues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityIssues",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "CommunityIssues");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "CommunityIssues");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityIssues");
        }
    }
}
