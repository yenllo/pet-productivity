using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class R2HistoryAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaskItems",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "GoldEarned",
                table: "TaskItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PetId",
                table: "TaskItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "TaskItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "XpEarned",
                table: "TaskItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: las mascotas de grupo creadas ANTES de AddGroupHatch tienen IsHatched/HatchVotes NULL
            // (se leerían como huevo y bloquearían tareas). Se marcan como ya nacidas.
            migrationBuilder.Sql(@"UPDATE ""Pets"" SET ""IsHatched"" = true WHERE ""Discriminator"" = 'SharedPet' AND ""IsHatched"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Pets"" SET ""HatchVotes"" = '[]' WHERE ""Discriminator"" = 'SharedPet' AND ""HatchVotes"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "GoldEarned",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "PetId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "XpEarned",
                table: "TaskItems");
        }
    }
}
