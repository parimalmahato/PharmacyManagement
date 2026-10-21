using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationFingerprint",
                table: "Sales",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationKey",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Sales",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancellationStockRestored",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "Sales",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationFingerprint",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationKey",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationStockRestored",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "Sales");
        }
    }
}
