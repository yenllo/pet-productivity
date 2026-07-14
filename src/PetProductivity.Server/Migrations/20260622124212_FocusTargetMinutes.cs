using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class FocusTargetMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetMinutes",
                table: "FocusSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetMinutes",
                table: "FocusSessions");
        }
    }
}
