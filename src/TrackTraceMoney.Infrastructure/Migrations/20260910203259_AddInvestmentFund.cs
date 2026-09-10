using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Contributions",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fees",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InvestmentDate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Withdrawals",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvestmentValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentFundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentValuations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentValuations_InvestmentFundId_AsOfDate",
                table: "InvestmentValuations",
                columns: new[] { "InvestmentFundId", "AsOfDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentValuations");

            migrationBuilder.DropColumn(
                name: "Contributions",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Fees",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InvestmentDate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Withdrawals",
                table: "Accounts");
        }
    }
}
