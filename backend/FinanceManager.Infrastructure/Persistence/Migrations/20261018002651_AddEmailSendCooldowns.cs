
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Persistence.Migrations;

public partial class AddEmailSendCooldowns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmailSendCooldowns",
            columns: table => new
            {
                UserId = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                Purpose = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false),

                LastSentAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_EmailSendCooldowns",
                    x => new { x.UserId, x.Purpose });

                table.ForeignKey(
                    name: "FK_EmailSendCooldowns_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmailSendCooldowns");
    }
}
