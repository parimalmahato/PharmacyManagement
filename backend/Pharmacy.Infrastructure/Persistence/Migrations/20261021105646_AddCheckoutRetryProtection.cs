using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutRetryProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutFingerprint",
                table: "Sales",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutKey",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId_CheckoutKey",
                table: "Sales",
                columns: new[] { "BranchId", "CheckoutKey" },
                unique: true,
                filter: "[CheckoutKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchId_CheckoutKey",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CheckoutFingerprint",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CheckoutKey",
                table: "Sales");
        }
    }
}
