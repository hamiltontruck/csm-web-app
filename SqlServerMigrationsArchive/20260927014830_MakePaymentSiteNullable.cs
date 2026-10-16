using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentSiteNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConstructionSiteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments",
                column: "ConstructionSiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConstructionSiteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ConstructionSites_ConstructionSiteId",
                table: "Payments",
                column: "ConstructionSiteId",
                principalTable: "ConstructionSites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
