using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chakra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RemindersPaused",
                table: "Premis",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemindersPausedAt",
                table: "Premis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemindersPausedByUserId",
                table: "Premis",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastRemindedOn",
                table: "Installments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OverdueAlertSentOn",
                table: "Installments",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PremisProcessed = table.Column<int>(type: "integer", nullable: false),
                    RemindersQueued = table.Column<int>(type: "integer", nullable: false),
                    AdminAlertsQueued = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Premis_RemindersPausedByUserId",
                table: "Premis",
                column: "RemindersPausedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_Status_DueDate",
                table: "Installments",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_JobName_BusinessDate",
                table: "JobRuns",
                columns: new[] { "JobName", "BusinessDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Premis_Users_RemindersPausedByUserId",
                table: "Premis",
                column: "RemindersPausedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Premis_Users_RemindersPausedByUserId",
                table: "Premis");

            migrationBuilder.DropTable(
                name: "JobRuns");

            migrationBuilder.DropIndex(
                name: "IX_Premis_RemindersPausedByUserId",
                table: "Premis");

            migrationBuilder.DropIndex(
                name: "IX_Installments_Status_DueDate",
                table: "Installments");

            migrationBuilder.DropColumn(
                name: "RemindersPaused",
                table: "Premis");

            migrationBuilder.DropColumn(
                name: "RemindersPausedAt",
                table: "Premis");

            migrationBuilder.DropColumn(
                name: "RemindersPausedByUserId",
                table: "Premis");

            migrationBuilder.DropColumn(
                name: "LastRemindedOn",
                table: "Installments");

            migrationBuilder.DropColumn(
                name: "OverdueAlertSentOn",
                table: "Installments");
        }
    }
}
