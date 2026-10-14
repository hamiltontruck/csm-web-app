using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CostCodes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionSiteId",
                table: "CostCodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostCodes_ConstructionSiteId",
                table: "CostCodes",
                column: "ConstructionSiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_CostCodes_ConstructionSites_ConstructionSiteId",
                table: "CostCodes",
                column: "ConstructionSiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostCodes_ConstructionSites_ConstructionSiteId",
                table: "CostCodes");

            migrationBuilder.DropIndex(
                name: "IX_CostCodes_ConstructionSiteId",
                table: "CostCodes");

            migrationBuilder.DropColumn(
                name: "ConstructionSiteId",
                table: "CostCodes");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CostCodes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
