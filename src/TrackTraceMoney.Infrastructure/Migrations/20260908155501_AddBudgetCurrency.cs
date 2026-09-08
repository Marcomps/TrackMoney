using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_CategoryId_Year_Month",
                table: "Budgets");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Budgets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId_Year_Month_Currency",
                table: "Budgets",
                columns: new[] { "CategoryId", "Year", "Month", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_CategoryId_Year_Month_Currency",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Budgets");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId_Year_Month",
                table: "Budgets",
                columns: new[] { "CategoryId", "Year", "Month" },
                unique: true);
        }
    }
}
