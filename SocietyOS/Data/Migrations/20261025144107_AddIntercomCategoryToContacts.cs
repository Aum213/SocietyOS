using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIntercomCategoryToContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "SocietyEmergencyContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnEmergencySos",
                table: "SocietyEmergencyContacts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "SocietyEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "ShowOnEmergencySos",
                table: "SocietyEmergencyContacts");
        }
    }
}
