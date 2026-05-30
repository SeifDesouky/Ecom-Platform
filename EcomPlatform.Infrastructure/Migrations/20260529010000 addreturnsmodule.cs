using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    public partial class AddReturnsModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ReturnRequests ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ReturnNumber = table.Column<string>(maxLength: 40, nullable: false),
                    OrderId = table.Column<Guid>(nullable: false),
                    Initiator = table.Column<int>(nullable: false, defaultValue: 1),
                    Reason = table.Column<int>(nullable: false),
                    ReasonNote = table.Column<string>(maxLength: 1000, nullable: false, defaultValue: ""),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0),
                    RefundStatus = table.Column<int>(nullable: false, defaultValue: 1),
                    RefundMethod = table.Column<int>(nullable: false, defaultValue: 1),
                    RefundedAt = table.Column<DateTime>(nullable: true),
                    RefundGatewayTransactionId = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    RefundNote = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    StockRestored = table.Column<bool>(nullable: false, defaultValue: false),
                    ReviewedById = table.Column<Guid>(nullable: true),
                    ReviewedAt = table.Column<DateTime>(nullable: true),
                    TenantId = table.Column<Guid>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_ReturnNumber",
                table: "ReturnRequests",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_OrderId",
                table: "ReturnRequests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_TenantId_Status_CreatedAt",
                table: "ReturnRequests",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            // ── ReturnItems ───────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ReturnRequestId = table.Column<Guid>(nullable: false),
                    OrderItemId = table.Column<Guid>(nullable: false),
                    ProductId = table.Column<Guid>(nullable: false),
                    ProductName = table.Column<string>(maxLength: 300, nullable: false, defaultValue: ""),
                    ProductSKU = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    QuantityRequested = table.Column<int>(nullable: false),
                    QuantityApproved = table.Column<int>(nullable: false, defaultValue: 0),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnItems_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReturnItems");
            migrationBuilder.DropTable(name: "ReturnRequests");
        }
    }
}