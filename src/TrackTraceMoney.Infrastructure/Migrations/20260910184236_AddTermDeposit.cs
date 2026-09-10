using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTermDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewal",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedInterest",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialPrincipal",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Institution",
                table: "Accounts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestFrequency",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestReceived",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompounding",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MaturityDate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateType",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoRenewal",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "EstimatedInterest",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InitialPrincipal",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Institution",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InterestFrequency",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InterestReceived",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IsCompounding",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MaturityDate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RateType",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Accounts");
        }
    }
}
