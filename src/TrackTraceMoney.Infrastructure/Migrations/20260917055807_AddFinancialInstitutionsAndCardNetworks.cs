using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialInstitutionsAndCardNetworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NetworkId",
                table: "CreditAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialInstitutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialInstitutions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardNetworks");

            migrationBuilder.DropTable(
                name: "FinancialInstitutions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "NetworkId",
                table: "CreditAccounts");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Accounts");
        }
    }
}
