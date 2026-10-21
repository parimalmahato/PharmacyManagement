using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePharmacyConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF (SELECT COUNT(*) FROM [PharmacySettings]) > 1 THROW 51000, 'Multiple pharmacy settings rows exist. Resolve duplicates before applying this migration.', 1;");
            migrationBuilder.AddColumn<int>(
                name: "ExpiryAlertDays",
                table: "PharmacySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ExpiryAlertsEnabled",
                table: "PharmacySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceNumberDigits",
                table: "PharmacySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "PharmacySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "NextInvoiceNumber",
                table: "PharmacySettings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooter",
                table: "PharmacySettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowCustomerPhone",
                table: "PharmacySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowPaymentHistory",
                table: "PharmacySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "PharmacySettings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SingletonKey",
                table: "PharmacySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill configuration defaults without changing the pharmacy profile
            // or any previously issued invoice. Existing GUID invoice numbers remain.
            migrationBuilder.Sql("""
                UPDATE [PharmacySettings] SET
                    [SingletonKey] = 1,
                    [Revision] = NEWID(),
                    [NextInvoiceNumber] = 1,
                    [InvoiceNumberDigits] = 6,
                    [LowStockThreshold] = 10,
                    [ExpiryAlertDays] = 90,
                    [ExpiryAlertsEnabled] = 1,
                    [ReceiptFooter] = N'Thank you for your purchase!',
                    [ReceiptShowCustomerPhone] = 1,
                    [ReceiptShowPaymentHistory] = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings",
                column: "SingletonKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ExpiryAlertDays",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ExpiryAlertsEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "InvoiceNumberDigits",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "NextInvoiceNumber",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ReceiptFooter",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowCustomerPhone",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowPaymentHistory",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "SingletonKey",
                table: "PharmacySettings");
        }
    }
}
