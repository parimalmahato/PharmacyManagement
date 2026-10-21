using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGstInclusiveBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "SaleItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstPercentage",
                table: "SaleItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                table: "SaleItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicineNameSnapshot",
                table: "SaleItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstPercentage",
                table: "Medicines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                table: "Medicines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "GstPercentage",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "MedicineNameSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "GstPercentage",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                table: "Medicines");
        }
    }
}
