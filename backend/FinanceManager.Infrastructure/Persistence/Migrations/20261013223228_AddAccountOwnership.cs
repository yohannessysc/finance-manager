
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------
            // 1. Add UserId as a nullable column
            // ---------------------------------------
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "FinancialAccounts",
                type: "uuid",
                nullable: true);

            // ---------------------------------------
            // 2. Assign existing accounts to test user
            // ---------------------------------------
            // User: testuser@example.com
            // User ID:
            // d80b4ff3-6731-4743-9201-064600fdb7a7

            migrationBuilder.Sql("""
                UPDATE "FinancialAccounts"
                SET "UserId" = 'd80b4ff3-6731-4743-9201-064600fdb7a7'::uuid
                WHERE "Id" IN (
                    '89f7d31a-93d5-46b1-af74-4da3fb16ba2a'::uuid,
                    '99894a18-0ff4-4ae9-a3fa-c46c77371c86'::uuid
                );
                """);

            // ---------------------------------------
            // 3. Make UserId required
            // ---------------------------------------
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "FinancialAccounts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // ---------------------------------------
            // 4. Create index for account ownership
            // ---------------------------------------
            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_UserId",
                table: "FinancialAccounts",
                column: "UserId");

            // ---------------------------------------
            // 5. Add foreign key to AspNetUsers
            // ---------------------------------------
            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAccounts_AspNetUsers_UserId",
                table: "FinancialAccounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------
            // 1. Remove foreign key
            // ---------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAccounts_AspNetUsers_UserId",
                table: "FinancialAccounts");

            // ---------------------------------------
            // 2. Remove ownership index
            // ---------------------------------------
            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_UserId",
                table: "FinancialAccounts");

            // ---------------------------------------
            // 3. Remove UserId column
            // ---------------------------------------
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FinancialAccounts");
        }
    }
}
