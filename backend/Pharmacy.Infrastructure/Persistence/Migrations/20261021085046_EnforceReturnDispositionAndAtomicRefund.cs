using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceReturnDispositionAndAtomicRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Disposition",
                table: "SaleReturnItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SaleReturnId",
                table: "SaleRefunds",
                type: "bigint",
                nullable: true);

            // Preserve historical records before enforcing the new domain rule.
            // Previously restocked lines are classified from their recorded flag;
            // all other historical lines remain quarantined, outside sellable stock.
            migrationBuilder.Sql("UPDATE [SaleReturnItems] SET [Disposition] = CASE WHEN [Restock] = 1 THEN 1 ELSE 4 END;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleReturnItems_Disposition",
                table: "SaleReturnItems",
                sql: "[Disposition] IN (1,2,3,4,5) AND ([Restock] = 0 OR [Disposition] = 1)");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefunds_SaleReturnId",
                table: "SaleRefunds",
                column: "SaleReturnId",
                unique: true,
                filter: "[SaleReturnId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRefunds_SaleReturns_SaleReturnId",
                table: "SaleRefunds",
                column: "SaleReturnId",
                principalTable: "SaleReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleRefunds_SaleReturns_SaleReturnId",
                table: "SaleRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleReturnItems_Disposition",
                table: "SaleReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleRefunds_SaleReturnId",
                table: "SaleRefunds");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "SaleReturnItems");

            migrationBuilder.DropColumn(
                name: "SaleReturnId",
                table: "SaleRefunds");
        }
    }
}
