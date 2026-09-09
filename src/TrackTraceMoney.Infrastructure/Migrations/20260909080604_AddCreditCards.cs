using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    AmountOwed = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreditAccountType = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastFourDigits = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                    AnnualInterestRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    MonthlyInterestRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    StatementCutOffDay = table.Column<int>(type: "INTEGER", nullable: true),
                    PaymentDueDay = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditAccounts_IsActive",
                table: "CreditAccounts",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditAccounts");
        }
    }
}
