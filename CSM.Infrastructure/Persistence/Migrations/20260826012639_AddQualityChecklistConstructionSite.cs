using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityChecklistConstructionSite : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
       {
           migrationBuilder.AddColumn<Guid>(
               name: "ConstructionSiteId",
               table: "QualityChecklists",
               type: "uniqueidentifier",
               nullable: true);

           migrationBuilder.Sql(@"
               UPDATE QC
               SET ConstructionSiteId = (
                   SELECT TOP (1) Id
                   FROM ConstructionSites
                   ORDER BY Name
               )
               FROM QualityChecklists QC
               WHERE ConstructionSiteId IS NULL;
           ");

           migrationBuilder.AlterColumn<Guid>(
               name: "ConstructionSiteId",
               table: "QualityChecklists",
               type: "uniqueidentifier",
               nullable: false,
               oldClrType: typeof(Guid),
               oldType: "uniqueidentifier",
               oldNullable: true);

           migrationBuilder.CreateIndex(
               name: "IX_QualityChecklists_ConstructionSiteId",
               table: "QualityChecklists",
               column: "ConstructionSiteId");

           migrationBuilder.AddForeignKey(
               name: "FK_QualityChecklists_ConstructionSites_ConstructionSiteId",
               table: "QualityChecklists",
               column: "ConstructionSiteId",
               principalTable: "ConstructionSites",
               principalColumn: "Id",
               onDelete: ReferentialAction.Restrict);
       }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QualityChecklists_ConstructionSites_ConstructionSiteId",
                table: "QualityChecklists");

            migrationBuilder.DropIndex(
                name: "IX_QualityChecklists_ConstructionSiteId",
                table: "QualityChecklists");

            migrationBuilder.DropColumn(
                name: "ConstructionSiteId",
                table: "QualityChecklists");
        }
    }
}
