using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Fees",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Institution",
                table: "CreditAccounts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyInstallment",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextPaymentDate",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateType",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredPayment",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fees",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "Institution",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "MonthlyInstallment",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "NextPaymentDate",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "RateType",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "RequiredPayment",
                table: "CreditAccounts");
        }
    }
}
