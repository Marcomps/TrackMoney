using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditCardStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreditAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CycleStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CycleEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MinimumPayment = table.Column<decimal>(type: "TEXT", nullable: false),
                    PayInFullAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCardStatements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardStatements_CreditAccountId_CycleEndDate",
                table: "CreditCardStatements",
                columns: new[] { "CreditAccountId", "CycleEndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditCardStatements");
        }
    }
}
