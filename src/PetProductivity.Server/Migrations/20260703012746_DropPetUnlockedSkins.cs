using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropPetUnlockedSkins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockedSkins",
                table: "Pets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlockedSkins",
                table: "Pets",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
