using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyMaterialUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyMaterialUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConstructionSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyProgressLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoggedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantityIssued = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMaterialUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyMaterialUsages_ConstructionSites_ConstructionSiteId",
                        column: x => x.ConstructionSiteId,
                        principalTable: "ConstructionSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyMaterialUsages_DailyProgressLogs_DailyProgressLogId",
                        column: x => x.DailyProgressLogId,
                        principalTable: "DailyProgressLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyMaterialUsages_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyMaterialUsages_Users_LoggedByUserId",
                        column: x => x.LoggedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMaterialUsages_ConstructionSiteId_Date",
                table: "DailyMaterialUsages",
                columns: new[] { "ConstructionSiteId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMaterialUsages_DailyProgressLogId_MaterialId",
                table: "DailyMaterialUsages",
                columns: new[] { "DailyProgressLogId", "MaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMaterialUsages_LoggedByUserId",
                table: "DailyMaterialUsages",
                column: "LoggedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMaterialUsages_MaterialId_Date",
                table: "DailyMaterialUsages",
                columns: new[] { "MaterialId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyMaterialUsages");
        }
    }
}
