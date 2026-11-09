using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlaidAccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PlaidItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FinicityInstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FinicityCustomerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankProvider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankConnections_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CategoryRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MerchantPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DescriptionPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TransactionTypeId = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MatchCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryRules_TransactionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionsRetrieved = table.Column<int>(type: "integer", nullable: false),
                    TransactionsImported = table.Column<int>(type: "integer", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SyncFromDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncToDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DetailedLog = table.Column<string>(type: "text", nullable: true),
                    BankConnectionId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankSyncLogs_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankSyncLogs_BankConnections_BankConnectionId1",
                        column: x => x.BankConnectionId1,
                        principalTable: "BankConnections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Merchant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TransactionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutoMatchedCategory = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AutoMatchConfidence = table.Column<int>(type: "integer", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BankConnectionId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancialTransactionId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankConnections_BankConnectionId1",
                        column: x => x.BankConnectionId1,
                        principalTable: "BankConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankTransactions_FinancialTransactions_FinancialTransaction~",
                        column: x => x.FinancialTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BankTransactions_FinancialTransactions_FinancialTransactio~1",
                        column: x => x.FinancialTransactionId1,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_FinancialAccountId",
                table: "BankConnections",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_IsDeleted",
                table: "BankConnections",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_LastSyncAtUtc",
                table: "BankConnections",
                column: "LastSyncAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Status",
                table: "BankConnections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankSyncLogs_BankConnectionId",
                table: "BankSyncLogs",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankSyncLogs_BankConnectionId1",
                table: "BankSyncLogs",
                column: "BankConnectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_BankSyncLogs_StartedAtUtc",
                table: "BankSyncLogs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BankSyncLogs_Status",
                table: "BankSyncLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankConnectionId",
                table: "BankTransactions",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankConnectionId1",
                table: "BankTransactions",
                column: "BankConnectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_FinancialTransactionId",
                table: "BankTransactions",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_FinancialTransactionId1",
                table: "BankTransactions",
                column: "FinancialTransactionId1");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_Status",
                table: "BankTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TransactionDateUtc",
                table: "BankTransactions",
                column: "TransactionDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TransactionHash",
                table: "BankTransactions",
                column: "TransactionHash");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_CategoryId",
                table: "CategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_IsActive",
                table: "CategoryRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_Priority",
                table: "CategoryRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_UserId",
                table: "CategoryRules",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankSyncLogs");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "CategoryRules");

            migrationBuilder.DropTable(
                name: "BankConnections");
        }
    }
}
