using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWbsTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySiteDiaries_ConstructionSites_ConstructionSiteId",
                table: "DailySiteDiaries");

            migrationBuilder.DropForeignKey(
                name: "FK_DailySiteDiaries_Projects_ProjectId",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "Delays",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "PlannedTomorrow",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "SafetyNotes",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "TemperatureCelsius",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "VisitorLog",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "WorkCompleted",
                table: "DailySiteDiaries");

            migrationBuilder.RenameColumn(
                name: "ManpowerCount",
                table: "DailySiteDiaries",
                newName: "WorkforceCount");

            migrationBuilder.RenameColumn(
                name: "IsSubmitted",
                table: "DailySiteDiaries",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "ConstructionSiteId",
                table: "DailySiteDiaries",
                newName: "SiteId");

            migrationBuilder.RenameIndex(
                name: "IX_DailySiteDiaries_ConstructionSiteId",
                table: "DailySiteDiaries",
                newName: "IX_DailySiteDiaries_SiteId");

            migrationBuilder.RenameIndex(
                name: "IX_DailySiteDiaries_CompanyId_ConstructionSiteId_DiaryDate",
                table: "DailySiteDiaries",
                newName: "IX_DailySiteDiaries_CompanyId_SiteId_DiaryDate");

            migrationBuilder.Sql(@"
            UPDATE DailySiteDiaries
            SET Weather = CASE Weather
                WHEN 'Sunny' THEN '0'
                WHEN 'Cloudy' THEN '1'
                WHEN 'Rainy' THEN '2'
                WHEN 'Stormy' THEN '3'
                ELSE '0'
            END
            ");

            migrationBuilder.AlterColumn<int>(
                name: "Weather",
                table: "DailySiteDiaries",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "DailySiteDiaries",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "DailySiteDiaries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "DailySiteDiaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelayReason",
                table: "DailySiteDiaries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "DailySiteDiaries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "DailySiteDiaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentInUse",
                table: "DailySiteDiaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SafetyObservation",
                table: "DailySiteDiaries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                table: "DailySiteDiaries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "DailySiteDiaries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "DailySiteDiaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkPerformed",
                table: "DailySiteDiaries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProgressPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WbsTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectMilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedFinishDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualFinishDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ProgressPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ActualHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_WbsTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WbsTasks_Employees_AssignedEmployeeId",
                        column: x => x.AssignedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WbsTasks_ProjectMilestones_ProjectMilestoneId",
                        column: x => x.ProjectMilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WbsTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_CompanyId_ProjectId_Name",
                table: "ProjectMilestones",
                columns: new[] { "CompanyId", "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId",
                table: "ProjectMilestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsTasks_AssignedEmployeeId",
                table: "WbsTasks",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsTasks_CompanyId_ProjectId_TaskCode",
                table: "WbsTasks",
                columns: new[] { "CompanyId", "ProjectId", "TaskCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WbsTasks_ProjectId",
                table: "WbsTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsTasks_ProjectMilestoneId",
                table: "WbsTasks",
                column: "ProjectMilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySiteDiaries_ConstructionSites_SiteId",
                table: "DailySiteDiaries",
                column: "SiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailySiteDiaries_Projects_ProjectId",
                table: "DailySiteDiaries",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySiteDiaries_ConstructionSites_SiteId",
                table: "DailySiteDiaries");

            migrationBuilder.DropForeignKey(
                name: "FK_DailySiteDiaries_Projects_ProjectId",
                table: "DailySiteDiaries");

            migrationBuilder.DropTable(
                name: "WbsTasks");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "DelayReason",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "EquipmentInUse",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "SafetyObservation",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "DailySiteDiaries");

            migrationBuilder.DropColumn(
                name: "WorkPerformed",
                table: "DailySiteDiaries");

            migrationBuilder.RenameColumn(
                name: "WorkforceCount",
                table: "DailySiteDiaries",
                newName: "ManpowerCount");

            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "DailySiteDiaries",
                newName: "ConstructionSiteId");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "DailySiteDiaries",
                newName: "IsSubmitted");

            migrationBuilder.RenameIndex(
                name: "IX_DailySiteDiaries_SiteId",
                table: "DailySiteDiaries",
                newName: "IX_DailySiteDiaries_ConstructionSiteId");

            migrationBuilder.RenameIndex(
                name: "IX_DailySiteDiaries_CompanyId_SiteId_DiaryDate",
                table: "DailySiteDiaries",
                newName: "IX_DailySiteDiaries_CompanyId_ConstructionSiteId_DiaryDate");

            migrationBuilder.AlterColumn<string>(
                name: "Weather",
                table: "DailySiteDiaries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"
            UPDATE DailySiteDiaries
            SET Weather = CASE Weather
                WHEN '0' THEN 'Sunny'
                WHEN '1' THEN 'Cloudy'
                WHEN '2' THEN 'Rainy'
                WHEN '3' THEN 'Stormy'
                ELSE 'Sunny'
            END
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "DailySiteDiaries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Delays",
                table: "DailySiteDiaries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedTomorrow",
                table: "DailySiteDiaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyNotes",
                table: "DailySiteDiaries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureCelsius",
                table: "DailySiteDiaries",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitorLog",
                table: "DailySiteDiaries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCompleted",
                table: "DailySiteDiaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySiteDiaries_ConstructionSites_ConstructionSiteId",
                table: "DailySiteDiaries",
                column: "ConstructionSiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailySiteDiaries_Projects_ProjectId",
                table: "DailySiteDiaries",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
