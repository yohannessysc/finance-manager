using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubcategoriesAndSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCategoryId",
                table: "TransactionCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionSplits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionSplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionSplits_FinancialTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionSplits_TransactionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCategories_ParentCategoryId",
                table: "TransactionCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSplits_CategoryId",
                table: "TransactionSplits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSplits_TransactionId",
                table: "TransactionSplits",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionCategories_TransactionCategories_ParentCategoryId",
                table: "TransactionCategories",
                column: "ParentCategoryId",
                principalTable: "TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionCategories_TransactionCategories_ParentCategoryId",
                table: "TransactionCategories");

            migrationBuilder.DropTable(
                name: "TransactionSplits");

            migrationBuilder.DropIndex(
                name: "IX_TransactionCategories_ParentCategoryId",
                table: "TransactionCategories");

            migrationBuilder.DropColumn(
                name: "ParentCategoryId",
                table: "TransactionCategories");
        }
    }
}
