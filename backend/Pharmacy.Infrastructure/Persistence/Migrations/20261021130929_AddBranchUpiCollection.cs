using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchUpiCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UpiEnabled",
                table: "PharmacySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UpiId",
                table: "PharmacySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiMerchantCode",
                table: "PharmacySettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiPayeeName",
                table: "PharmacySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpiEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "UpiId",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "UpiMerchantCode",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "UpiPayeeName",
                table: "PharmacySettings");
        }
    }
}
