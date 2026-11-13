using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkExpenseSettlementEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseId",
                table: "PettyCashTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashTransactions_ExpenseId",
                table: "PettyCashTransactions",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashTransactions_PettyCashAccountId_ReferenceNumber",
                table: "PettyCashTransactions",
                columns: new[] { "PettyCashAccountId", "ReferenceNumber" },
                unique: true,
                filter: "\"ExpenseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ExpenseId",
                table: "Payments",
                column: "ExpenseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Expenses_ExpenseId",
                table: "Payments",
                column: "ExpenseId",
                principalTable: "Expenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashTransactions_Expenses_ExpenseId",
                table: "PettyCashTransactions",
                column: "ExpenseId",
                principalTable: "Expenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This additive financial migration cannot be downgraded. Roll back the application while retaining the settlement evidence schema and links.");
        }
    }
}
