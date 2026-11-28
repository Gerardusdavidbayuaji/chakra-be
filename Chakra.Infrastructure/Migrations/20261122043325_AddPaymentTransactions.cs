using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chakra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PremiId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SnapToken = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    RedirectUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireRequested = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    MidtransTransactionId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AllocatedInstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundReference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Installments_AllocatedInstallmentId",
                        column: x => x.AllocatedInstallmentId,
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Premis_PremiId",
                        column: x => x.PremiId,
                        principalTable: "Premis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_AllocatedInstallmentId",
                table: "PaymentTransactions",
                column: "AllocatedInstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ExpiresAt",
                table: "PaymentTransactions",
                column: "ExpiresAt",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InstallmentId",
                table: "PaymentTransactions",
                column: "InstallmentId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OrderId",
                table: "PaymentTransactions",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PremiId_Status",
                table: "PaymentTransactions",
                columns: new[] { "PremiId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");
        }
    }
}
