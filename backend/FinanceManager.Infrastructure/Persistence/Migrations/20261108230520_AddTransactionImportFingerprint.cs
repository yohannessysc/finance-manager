using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionImportFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportFingerprint",
                table: "FinancialTransactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_UserId_ImportFingerprint",
                table: "FinancialTransactions",
                columns: new[] { "UserId", "ImportFingerprint" },
                unique: true,
                filter: "\"ImportFingerprint\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_UserId_ImportFingerprint",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ImportFingerprint",
                table: "FinancialTransactions");
        }
    }
}
