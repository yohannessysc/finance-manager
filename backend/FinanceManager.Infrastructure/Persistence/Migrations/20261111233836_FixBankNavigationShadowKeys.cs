using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixBankNavigationShadowKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankSyncLogs_BankConnections_BankConnectionId1",
                table: "BankSyncLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_BankConnections_BankConnectionId1",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_FinancialTransactions_FinancialTransactio~1",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_BankConnectionId1",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_FinancialTransactionId1",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankSyncLogs_BankConnectionId1",
                table: "BankSyncLogs");

            migrationBuilder.DropColumn(
                name: "BankConnectionId1",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "FinancialTransactionId1",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "BankConnectionId1",
                table: "BankSyncLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankConnectionId1",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialTransactionId1",
                table: "BankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankConnectionId1",
                table: "BankSyncLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankConnectionId1",
                table: "BankTransactions",
                column: "BankConnectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_FinancialTransactionId1",
                table: "BankTransactions",
                column: "FinancialTransactionId1");

            migrationBuilder.CreateIndex(
                name: "IX_BankSyncLogs_BankConnectionId1",
                table: "BankSyncLogs",
                column: "BankConnectionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BankSyncLogs_BankConnections_BankConnectionId1",
                table: "BankSyncLogs",
                column: "BankConnectionId1",
                principalTable: "BankConnections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankConnections_BankConnectionId1",
                table: "BankTransactions",
                column: "BankConnectionId1",
                principalTable: "BankConnections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_FinancialTransactions_FinancialTransactio~1",
                table: "BankTransactions",
                column: "FinancialTransactionId1",
                principalTable: "FinancialTransactions",
                principalColumn: "Id");
        }
    }
}
