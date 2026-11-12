using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmarterSavingsGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsGoalContributions_SavingsGoalId",
                table: "SavingsGoalContributions");

            migrationBuilder.AddColumn<decimal>(
                name: "AutoContributionAmount",
                table: "SavingsGoals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoContributionFrequency",
                table: "SavingsGoals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoContributionStartUtc",
                table: "SavingsGoals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmergencyFundMonths",
                table: "SavingsGoals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalType",
                table: "SavingsGoals",
                type: "integer",
                nullable: false,
                defaultValue: 1); // Backfill existing goals as SavingsGoalType.Custom.

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "SavingsGoals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastMilestoneNotified",
                table: "SavingsGoals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedAccountId",
                table: "SavingsGoals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAutoContributionUtc",
                table: "SavingsGoals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "SavingsGoals",
                type: "boolean",
                nullable: false,
                defaultValue: true); // Existing goals opt in to milestone emails.

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledOccurrenceUtc",
                table: "SavingsGoalContributions",
                type: "timestamp with time zone",
                nullable: true);

            // Existing goals only announce milestones crossed from now on.
            // Before this migration every goal tracked manual contributions.
            migrationBuilder.Sql("""
                UPDATE "SavingsGoals" AS g
                SET "LastMilestoneNotified" = CASE
                    WHEN c.total >= g."TargetAmount" THEN 100
                    WHEN c.total >= g."TargetAmount" * 0.75 THEN 75
                    WHEN c.total >= g."TargetAmount" * 0.50 THEN 50
                    WHEN c.total >= g."TargetAmount" * 0.25 THEN 25
                    ELSE 0
                END
                FROM (
                    SELECT "SavingsGoalId", SUM("Amount") AS total
                    FROM "SavingsGoalContributions"
                    GROUP BY "SavingsGoalId"
                ) AS c
                WHERE c."SavingsGoalId" = g."Id"
                  AND g."TargetAmount" > 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_HouseholdId",
                table: "SavingsGoals",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_LinkedAccountId",
                table: "SavingsGoals",
                column: "LinkedAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_NextAutoContributionUtc",
                table: "SavingsGoals",
                column: "NextAutoContributionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoalContributions_SavingsGoalId_ScheduledOccurrenceU~",
                table: "SavingsGoalContributions",
                columns: new[] { "SavingsGoalId", "ScheduledOccurrenceUtc" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsGoals_FinancialAccounts_LinkedAccountId",
                table: "SavingsGoals",
                column: "LinkedAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsGoals_Households_HouseholdId",
                table: "SavingsGoals",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavingsGoals_FinancialAccounts_LinkedAccountId",
                table: "SavingsGoals");

            migrationBuilder.DropForeignKey(
                name: "FK_SavingsGoals_Households_HouseholdId",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_HouseholdId",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_LinkedAccountId",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_NextAutoContributionUtc",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_SavingsGoalContributions_SavingsGoalId_ScheduledOccurrenceU~",
                table: "SavingsGoalContributions");

            migrationBuilder.DropColumn(
                name: "AutoContributionAmount",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "AutoContributionFrequency",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "AutoContributionStartUtc",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "EmergencyFundMonths",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "GoalType",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "LastMilestoneNotified",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "LinkedAccountId",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "NextAutoContributionUtc",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "ScheduledOccurrenceUtc",
                table: "SavingsGoalContributions");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoalContributions_SavingsGoalId",
                table: "SavingsGoalContributions",
                column: "SavingsGoalId");
        }
    }
}
