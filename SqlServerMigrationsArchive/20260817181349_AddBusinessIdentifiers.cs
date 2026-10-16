using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhaseNumber",
                table: "ProjectPhases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MilestoneNumber",
                table: "Milestones",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DowntimeNumber",
                table: "EquipmentDowntime",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentNumber",
                table: "EquipmentAssignments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogNumber",
                table: "DailyProgressLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageNumber",
                table: "DailyMaterialUsages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                ;WITH Numbered AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY CompanyId
                            ORDER BY Id
                        ) AS Number
                    FROM DailyMaterialUsages
                )
                UPDATE d
                SET UsageNumber =
                    'DMU-' + RIGHT(
                        '000000' + CAST(n.Number AS varchar(6)),
                        6)
                FROM DailyMaterialUsages d
                INNER JOIN Numbered n
                    ON n.Id = d.Id;
                """);

            migrationBuilder.Sql("""
                ;WITH Numbered AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY CompanyId
                            ORDER BY Id
                        ) AS Number
                    FROM DailyProgressLogs
                )
                UPDATE d
                SET LogNumber =
                    'DPL-' + RIGHT(
                        '000000' + CAST(n.Number AS varchar(6)),
                        6)
                FROM DailyProgressLogs d
                INNER JOIN Numbered n
                    ON n.Id = d.Id;
                """);

            migrationBuilder.Sql("""
                ;WITH Numbered AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY CompanyId
                            ORDER BY Id
                        ) AS Number
                    FROM ProjectPhases
                )
                UPDATE p
                SET PhaseNumber =
                    'PH-' + RIGHT(
                        '000000' + CAST(n.Number AS varchar(6)),
                        6)
                FROM ProjectPhases p
                INNER JOIN Numbered n
                    ON n.Id = p.Id;
                """);

            migrationBuilder.Sql("""
                ;WITH Numbered AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY CompanyId
                            ORDER BY Id
                        ) AS Number
                    FROM Milestones
                )
                UPDATE m
                SET MilestoneNumber =
                    'MS-' + RIGHT(
                        '000000' + CAST(n.Number AS varchar(6)),
                        6)
                FROM Milestones m
                INNER JOIN Numbered n
                    ON n.Id = m.Id;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE DailyMaterialUsages
                ALTER COLUMN UsageNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE DailyProgressLogs
                ALTER COLUMN LogNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE ProjectPhases
                ALTER COLUMN PhaseNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE Milestones
                ALTER COLUMN MilestoneNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE EquipmentAssignments
                ALTER COLUMN AssignmentNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE EquipmentDowntime
                ALTER COLUMN DowntimeNumber nvarchar(30) NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhases_CompanyId_PhaseNumber",
                table: "ProjectPhases",
                columns: new[] { "CompanyId", "PhaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_CompanyId_MilestoneNumber",
                table: "Milestones",
                columns: new[] { "CompanyId", "MilestoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDowntime_CompanyId_DowntimeNumber",
                table: "EquipmentDowntime",
                columns: new[] { "CompanyId", "DowntimeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAssignments_CompanyId_AssignmentNumber",
                table: "EquipmentAssignments",
                columns: new[] { "CompanyId", "AssignmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyProgressLogs_CompanyId_LogNumber",
                table: "DailyProgressLogs",
                columns: new[] { "CompanyId", "LogNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMaterialUsages_CompanyId_UsageNumber",
                table: "DailyMaterialUsages",
                columns: new[] { "CompanyId", "UsageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectPhases_CompanyId_PhaseNumber",
                table: "ProjectPhases");

            migrationBuilder.DropIndex(
                name: "IX_Milestones_CompanyId_MilestoneNumber",
                table: "Milestones");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentDowntime_CompanyId_DowntimeNumber",
                table: "EquipmentDowntime");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentAssignments_CompanyId_AssignmentNumber",
                table: "EquipmentAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DailyProgressLogs_CompanyId_LogNumber",
                table: "DailyProgressLogs");

            migrationBuilder.DropIndex(
                name: "IX_DailyMaterialUsages_CompanyId_UsageNumber",
                table: "DailyMaterialUsages");

            migrationBuilder.DropColumn(
                name: "PhaseNumber",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "MilestoneNumber",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "DowntimeNumber",
                table: "EquipmentDowntime");

            migrationBuilder.DropColumn(
                name: "AssignmentNumber",
                table: "EquipmentAssignments");

            migrationBuilder.DropColumn(
                name: "LogNumber",
                table: "DailyProgressLogs");

            migrationBuilder.DropColumn(
                name: "UsageNumber",
                table: "DailyMaterialUsages");
        }
    }
}
