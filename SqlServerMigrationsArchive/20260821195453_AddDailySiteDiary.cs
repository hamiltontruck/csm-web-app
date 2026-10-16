using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySiteDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailySiteDiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConstructionSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Weather = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemperatureCelsius = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ManpowerCount = table.Column<int>(type: "int", nullable: false),
                    WorkCompleted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PlannedTomorrow = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Delays = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SafetyNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VisitorLog = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsSubmitted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySiteDiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailySiteDiaries_ConstructionSites_ConstructionSiteId",
                        column: x => x.ConstructionSiteId,
                        principalTable: "ConstructionSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailySiteDiaries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailySiteDiaries_CompanyId_ConstructionSiteId_DiaryDate",
                table: "DailySiteDiaries",
                columns: new[] { "CompanyId", "ConstructionSiteId", "DiaryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailySiteDiaries_ConstructionSiteId",
                table: "DailySiteDiaries",
                column: "ConstructionSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySiteDiaries_ProjectId",
                table: "DailySiteDiaries",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySiteDiaries");
        }
    }
}
