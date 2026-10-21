using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGstPreparationWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstDocumentDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstDocumentDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GstDocumentDetails_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GstReturnReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstReturnReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GstReturnReviews_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GstReturnWorkspaces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentReviewId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstReturnWorkspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GstReturnWorkspaces_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GstDocumentDetails_BranchId_SourceKind_SourceId",
                table: "GstDocumentDetails",
                columns: new[] { "BranchId", "SourceKind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnReviews_BranchId_Year_Month",
                table: "GstReturnReviews",
                columns: new[] { "BranchId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnWorkspaces_BranchId_Year_Month",
                table: "GstReturnWorkspaces",
                columns: new[] { "BranchId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GstDocumentDetails");

            migrationBuilder.DropTable(
                name: "GstReturnReviews");

            migrationBuilder.DropTable(
                name: "GstReturnWorkspaces");
        }
    }
}
