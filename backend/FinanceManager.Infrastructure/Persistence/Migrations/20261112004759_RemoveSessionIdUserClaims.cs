using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSessionIdUserClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Session ids moved from the user record to each device's cookie.
            // Left on the user record they would be re-added to every
            // device's principal and override its own session id.
            migrationBuilder.Sql(
                """DELETE FROM "AspNetUserClaims" WHERE "ClaimType" = 'fm_sid';""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
