using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialSupabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Hunger = table.Column<double>(type: "double precision", nullable: false),
                    Happiness = table.Column<double>(type: "double precision", nullable: false),
                    CurrentArchetype = table.Column<int>(type: "integer", nullable: false),
                    Stats = table.Column<string>(type: "text", nullable: false),
                    TotalXp = table.Column<double>(type: "double precision", nullable: false),
                    UnlockedSkins = table.Column<string>(type: "text", nullable: false),
                    GoldCoins = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<double>(type: "double precision", nullable: false),
                    MaxHealth = table.Column<double>(type: "double precision", nullable: false),
                    GracePeriodExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    AiDifficultyScore = table.Column<int>(type: "integer", nullable: false),
                    AiStatCategory = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    UserPetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Inventory = table.Column<string>(type: "text", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    TotalTasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    RitualGridState = table.Column<string>(type: "text", nullable: false),
                    LastRitualReset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveXpMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    ThemePreference = table.Column<string>(type: "text", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Pets_UserPetId",
                        column: x => x.UserPetId,
                        principalTable: "Pets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserPetId",
                table: "Users",
                column: "UserPetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskItems");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Pets");
        }
    }
}
