using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MedicineBatches_MedicineId_BatchNumber",
                table: "MedicineBatches");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "SaleReturns",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "SaleReturnItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "SaleRefunds",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "SaleItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "PurchaseItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "PharmacySettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MedicineBatches",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "ExpiryNotifications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[] { 1, "MAIN", true, "Main branch" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BranchId",
                table: "StockMovements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId_InvoiceNumber",
                table: "Sales",
                columns: new[] { "BranchId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_BranchId",
                table: "SaleReturns",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_BranchId",
                table: "SaleReturnItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefunds_BranchId",
                table: "SaleRefunds",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_BranchId",
                table: "SaleItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_BranchId",
                table: "PurchaseItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_BranchId_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "BranchId", "SupplierId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "SupplierId", "InvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_BranchId_SingletonKey",
                table: "PharmacySettings",
                columns: new[] { "BranchId", "SingletonKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings",
                column: "SingletonKey");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BranchId_IdempotencyKey",
                table: "Payments",
                columns: new[] { "BranchId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_BranchId_MedicineId_BatchNumber",
                table: "MedicineBatches",
                columns: new[] { "BranchId", "MedicineId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_MedicineId_BatchNumber",
                table: "MedicineBatches",
                columns: new[] { "MedicineId", "BatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryNotifications_BranchId",
                table: "ExpiryNotifications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BranchId",
                table: "Customers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BranchId",
                table: "AspNetUsers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Branches_BranchId",
                table: "AspNetUsers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Branches_BranchId",
                table: "AuditLogs",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Branches_BranchId",
                table: "Customers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpiryNotifications_Branches_BranchId",
                table: "ExpiryNotifications",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicineBatches_Branches_BranchId",
                table: "MedicineBatches",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Branches_BranchId",
                table: "Payments",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PharmacySettings_Branches_BranchId",
                table: "PharmacySettings",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Branches_BranchId",
                table: "PurchaseInvoices",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Branches_BranchId",
                table: "PurchaseItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Branches_BranchId",
                table: "SaleItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRefunds_Branches_BranchId",
                table: "SaleRefunds",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleReturnItems_Branches_BranchId",
                table: "SaleReturnItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleReturns_Branches_BranchId",
                table: "SaleReturns",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Branches_BranchId",
                table: "Sales",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Branches_BranchId",
                table: "StockMovements",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Branches_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Branches_BranchId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Branches_BranchId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpiryNotifications_Branches_BranchId",
                table: "ExpiryNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicineBatches_Branches_BranchId",
                table: "MedicineBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Branches_BranchId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PharmacySettings_Branches_BranchId",
                table: "PharmacySettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Branches_BranchId",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Branches_BranchId",
                table: "PurchaseItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Branches_BranchId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleRefunds_Branches_BranchId",
                table: "SaleRefunds");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleReturnItems_Branches_BranchId",
                table: "SaleReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleReturns_Branches_BranchId",
                table: "SaleReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Branches_BranchId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Branches_BranchId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BranchId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchId_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleReturns_BranchId",
                table: "SaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_SaleReturnItems_BranchId",
                table: "SaleReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleRefunds_BranchId",
                table: "SaleRefunds");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_BranchId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_BranchId",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_BranchId_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PharmacySettings_BranchId_SingletonKey",
                table: "PharmacySettings");

            migrationBuilder.DropIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BranchId_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MedicineBatches_BranchId_MedicineId_BatchNumber",
                table: "MedicineBatches");

            migrationBuilder.DropIndex(
                name: "IX_MedicineBatches_MedicineId_BatchNumber",
                table: "MedicineBatches");

            migrationBuilder.DropIndex(
                name: "IX_ExpiryNotifications_BranchId",
                table: "ExpiryNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BranchId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SaleReturnItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SaleRefunds");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MedicineBatches");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ExpiryNotifications");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "SupplierId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_SingletonKey",
                table: "PharmacySettings",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_MedicineId_BatchNumber",
                table: "MedicineBatches",
                columns: new[] { "MedicineId", "BatchNumber" },
                unique: true);
        }
    }
}
