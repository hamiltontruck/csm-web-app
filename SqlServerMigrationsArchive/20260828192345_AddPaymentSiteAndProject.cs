using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSiteAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionSiteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE p
                SET
                    p.ConstructionSiteId = i.ConstructionSiteId,
                    p.ProjectId = i.ProjectId
                FROM Payments p
                INNER JOIN Invoices i
                    ON p.InvoiceId = i.Id
                WHERE p.ConstructionSiteId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConstructionSiteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);;

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConstructionSiteId",
                table: "Payments",
                column: "ConstructionSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProjectId",
                table: "Payments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_ApprovedBy",
                table: "MaterialRequests",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_RequestedBy",
                table: "MaterialRequests",
                column: "RequestedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Users_ApprovedBy",
                table: "MaterialRequests",
                column: "ApprovedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Users_RequestedBy",
                table: "MaterialRequests",
                column: "RequestedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments",
                column: "ConstructionSiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Projects_ProjectId",
                table: "Payments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Users_ApprovedBy",
                table: "MaterialRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Users_RequestedBy",
                table: "MaterialRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Projects_ProjectId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ConstructionSiteId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProjectId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_ApprovedBy",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_RequestedBy",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "ConstructionSiteId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Payments");
        }
    }
}
