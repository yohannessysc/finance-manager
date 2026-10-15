using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionCategories_UserId_Type_Name",
                table: "TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_Status_NextOccurrenceUtc",
                table: "RecurringTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TransactionCategories",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "RecurringTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Etc/UTC");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCategories_UserId",
                table: "TransactionCategories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_NextOccurrenceUtc",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "NextOccurrenceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_Status",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionCategories_UserId",
                table: "TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_NextOccurrenceUtc",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_Status",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "RecurringTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TransactionCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCategories_UserId_Type_Name",
                table: "TransactionCategories",
                columns: new[] { "UserId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_Status_NextOccurrenceUtc",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "Status", "NextOccurrenceUtc" });
        }
    }
}
