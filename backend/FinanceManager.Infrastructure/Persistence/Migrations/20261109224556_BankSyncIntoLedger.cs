using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BankSyncIntoLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalStreamId",
                table: "RecurringTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankConnectionId",
                table: "FinancialAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                table: "FinancialAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mask",
                table: "FinancialAccounts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionsCursor",
                table: "BankConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_ExternalStreamId",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "ExternalStreamId" },
                unique: true,
                filter: "\"ExternalStreamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_BankConnectionId_ExternalAccountId",
                table: "FinancialAccounts",
                columns: new[] { "BankConnectionId", "ExternalAccountId" },
                unique: true,
                filter: "\"ExternalAccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_ExternalStreamId",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_BankConnectionId_ExternalAccountId",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalStreamId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "BankConnectionId",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "Mask",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "TransactionsCursor",
                table: "BankConnections");
        }
    }
}
