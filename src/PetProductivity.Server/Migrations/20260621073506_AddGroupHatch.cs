using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupHatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HatchVotes",
                table: "Pets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHatched",
                table: "Pets",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HatchVotes",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "IsHatched",
                table: "Pets");
        }
    }
}
