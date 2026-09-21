using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackTraceMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BaseType = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultCategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultCreditAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionPresets_IsActive",
                table: "TransactionPresets",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionPresets");
        }
    }
}
