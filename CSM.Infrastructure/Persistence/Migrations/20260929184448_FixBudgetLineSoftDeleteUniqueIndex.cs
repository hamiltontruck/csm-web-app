using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixBudgetLineSoftDeleteUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetLines_BudgetId_CostCodeId",
                table: "BudgetLines");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetId_CostCodeId",
                table: "BudgetLines",
                columns: new[] { "BudgetId", "CostCodeId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetLines_BudgetId_CostCodeId",
                table: "BudgetLines");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetId_CostCodeId",
                table: "BudgetLines",
                columns: new[] { "BudgetId", "CostCodeId" },
                unique: true);
        }
    }
}
