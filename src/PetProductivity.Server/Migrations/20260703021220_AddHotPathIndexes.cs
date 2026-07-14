using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetProductivity.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_PetId_CreatedAt",
                table: "TaskItems",
                columns: new[] { "PetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProofId",
                table: "TaskItems",
                column: "ProofId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskApprovals_GroupId",
                table: "TaskApprovals",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_GroupId_RequesterUserId",
                table: "JoinRequests",
                columns: new[] { "GroupId", "RequesterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupFocusSessions_GroupId",
                table: "GroupFocusSessions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_UserId_PetId",
                table: "FocusSessions",
                columns: new[] { "UserId", "PetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FocusProofs_SessionId_UserId",
                table: "FocusProofs",
                columns: new[] { "SessionId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_PetId_CreatedAt",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProofId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_UserId_CreatedAt",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskApprovals_GroupId",
                table: "TaskApprovals");

            migrationBuilder.DropIndex(
                name: "IX_JoinRequests_GroupId_RequesterUserId",
                table: "JoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupFocusSessions_GroupId",
                table: "GroupFocusSessions");

            migrationBuilder.DropIndex(
                name: "IX_FocusSessions_UserId_PetId",
                table: "FocusSessions");

            migrationBuilder.DropIndex(
                name: "IX_FocusProofs_SessionId_UserId",
                table: "FocusProofs");
        }
    }
}
