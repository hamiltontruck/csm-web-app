using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    public partial class AddUserAuditCompanySnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UserAudits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAudits_CompanyId_PerformedAtUtc",
                table: "UserAudits",
                columns: new[] { "CompanyId", "PerformedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAudits_CompanyId_PerformedAtUtc",
                table: "UserAudits");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserAudits");
        }
    }
}
