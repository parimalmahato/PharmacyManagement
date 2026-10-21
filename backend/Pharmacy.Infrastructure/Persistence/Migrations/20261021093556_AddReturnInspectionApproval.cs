using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnInspectionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InspectedAtUtc",
                table: "SaleReturnItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectedByUserId",
                table: "SaleReturnItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InspectionApproved",
                table: "SaleReturnItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectedAtUtc",
                table: "SaleReturnItems");

            migrationBuilder.DropColumn(
                name: "InspectedByUserId",
                table: "SaleReturnItems");

            migrationBuilder.DropColumn(
                name: "InspectionApproved",
                table: "SaleReturnItems");
        }
    }
}
