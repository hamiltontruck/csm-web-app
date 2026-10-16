using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRecordToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayrollRecordId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PayrollRecordId",
                table: "Payments",
                column: "PayrollRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PayrollRecords_PayrollRecordId",
                table: "Payments",
                column: "PayrollRecordId",
                principalTable: "PayrollRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PayrollRecords_PayrollRecordId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PayrollRecordId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PayrollRecordId",
                table: "Payments");
        }
    }
}
