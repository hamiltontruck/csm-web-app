using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeNotificationsRecipientAndSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Notifications",
                newName: "RecipientUserId");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Notifications",
                newName: "Severity");

            migrationBuilder.RenameColumn(
                name: "RelatedEntityType",
                table: "Notifications",
                newName: "SourceEntityType");

            migrationBuilder.RenameColumn(
                name: "RelatedEntityId",
                table: "Notifications",
                newName: "SourceEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                newName: "IX_Notifications_RecipientUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_CompanyId_UserId_IsRead_CreatedAtUtc",
                table: "Notifications",
                newName: "IX_Notifications_CompanyId_RecipientUserId_IsRead_CreatedAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "SourceEntityType",
                table: "Notifications",
                newName: "RelatedEntityType");

            migrationBuilder.RenameColumn(
                name: "SourceEntityId",
                table: "Notifications",
                newName: "RelatedEntityId");

            migrationBuilder.RenameColumn(
                name: "Severity",
                table: "Notifications",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "RecipientUserId",
                table: "Notifications",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                newName: "IX_Notifications_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_CompanyId_RecipientUserId_IsRead_CreatedAtUtc",
                table: "Notifications",
                newName: "IX_Notifications_CompanyId_UserId_IsRead_CreatedAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
