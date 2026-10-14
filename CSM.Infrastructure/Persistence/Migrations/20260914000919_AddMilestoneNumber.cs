using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MilestoneNumber",
                table: "ProjectMilestones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_CompanyId_ProjectId_MilestoneNumber",
                table: "ProjectMilestones",
                columns: new[] { "CompanyId", "ProjectId", "MilestoneNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectMilestones_CompanyId_ProjectId_MilestoneNumber",
                table: "ProjectMilestones");

            migrationBuilder.DropColumn(
                name: "MilestoneNumber",
                table: "ProjectMilestones");
        }
    }
}
