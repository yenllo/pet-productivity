using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPetGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RetiredPets",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Generation",
                table: "Pets",
                type: "integer",
                nullable: false,
                defaultValue: 1); // las mascotas existentes son la Gen 1, no 0
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetiredPets",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Generation",
                table: "Pets");
        }
    }
}
