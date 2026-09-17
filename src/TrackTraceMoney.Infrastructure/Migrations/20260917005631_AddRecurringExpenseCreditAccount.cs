using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringExpenseCreditAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "RecurringExpenses",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "CreditAccountId",
                table: "RecurringExpenses",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditAccountId",
                table: "RecurringExpenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "RecurringExpenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
