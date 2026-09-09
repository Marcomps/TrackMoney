using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditAccountId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditCardPurchase_BeneficiaryPersonId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditCardPurchase_PayerPersonId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreditAccountId",
                table: "Transactions",
                column: "CreditAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreditAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreditAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreditCardPurchase_BeneficiaryPersonId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreditCardPurchase_PayerPersonId",
                table: "Transactions");
        }
    }
}
