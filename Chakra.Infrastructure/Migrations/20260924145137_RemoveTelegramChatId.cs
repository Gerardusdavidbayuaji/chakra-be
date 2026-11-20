using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chakra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTelegramChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Users");

            migrationBuilder.Sql("""
                ALTER TABLE "Users" ALTER COLUMN "ChatId" TYPE uuid USING "ChatId"::uuid;
                UPDATE "Users" SET "ChatId" = '00000000-0000-0000-0000-000000000000' WHERE "ChatId" IS NULL;
                ALTER TABLE "Users" ALTER COLUMN "ChatId" SET NOT NULL;
                ALTER TABLE "Users" ALTER COLUMN "ChatId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChatId",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "Users",
                type: "text",
                nullable: true);
        }
    }
}
