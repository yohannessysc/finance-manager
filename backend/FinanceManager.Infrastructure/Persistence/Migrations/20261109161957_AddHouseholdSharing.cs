using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_UserId_CategoryId_Currency",
                table: "MonthlyBudgets");

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "MonthlyBudgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "FinancialAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Households_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => new { x.HouseholdId, x.UserId });
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_HouseholdId_CategoryId_Currency",
                table: "MonthlyBudgets",
                columns: new[] { "HouseholdId", "CategoryId", "Currency" },
                unique: true,
                filter: "\"HouseholdId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_UserId_CategoryId_Currency",
                table: "MonthlyBudgets",
                columns: new[] { "UserId", "CategoryId", "Currency" },
                unique: true,
                filter: "\"HouseholdId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_HouseholdId",
                table: "FinancialAccounts",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_NormalizedEmail_ExpiresAtU~",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "NormalizedEmail", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_InvitedByUserId",
                table: "HouseholdInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_TokenHash",
                table: "HouseholdInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId_HouseholdId",
                table: "HouseholdMembers",
                columns: new[] { "UserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_Households_OwnerUserId",
                table: "Households",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAccounts_Households_HouseholdId",
                table: "FinancialAccounts",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyBudgets_Households_HouseholdId",
                table: "MonthlyBudgets",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAccounts_Households_HouseholdId",
                table: "FinancialAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyBudgets_Households_HouseholdId",
                table: "MonthlyBudgets");

            migrationBuilder.DropTable(
                name: "HouseholdInvitations");

            migrationBuilder.DropTable(
                name: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_HouseholdId_CategoryId_Currency",
                table: "MonthlyBudgets");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_UserId_CategoryId_Currency",
                table: "MonthlyBudgets");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_HouseholdId",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "MonthlyBudgets");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "FinancialAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_UserId_CategoryId_Currency",
                table: "MonthlyBudgets",
                columns: new[] { "UserId", "CategoryId", "Currency" },
                unique: true);
        }
    }
}
